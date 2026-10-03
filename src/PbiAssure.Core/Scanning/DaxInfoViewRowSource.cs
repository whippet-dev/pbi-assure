namespace PbiAssure.Core.Scanning;

internal static partial class DaxReferenceExtractor
{
    // Microsoft-documented return schemas, not samples inferred from a work model:
    // https://learn.microsoft.com/en-us/dax/info-view-{columns,tables,measures,relationships}-function-dax
    private static readonly Dictionary<string, IReadOnlySet<string>> InfoViewSchemas =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["INFO.VIEW.COLUMNS"] = Schema("ID", "Name", "Table", "DataType", "DataCategory", "Description",
                "IsHidden", "IsUnique", "IsKey", "IsNullable", "Alignment", "SummarizeBy", "ColumnStorage",
                "Type", "SourceColumn", "Expression", "FormatString", "IsAvailableInMDX", "SortByColumn",
                "GroupingBehavior", "SourceProviderType", "DisplayFolder", "AlternateOf", "LineageTag"),
            ["INFO.VIEW.TABLES"] = Schema("ID", "Name", "Model", "DataCategory", "Description", "IsHidden",
                "StorageMode", "TableStorage", "Expression", "ShowAsVariationOnly", "IsPrivate",
                "CalculationGroupPrecedence", "LineageTag"),
            ["INFO.VIEW.MEASURES"] = Schema("ID", "Name", "Table", "Description", "DataType", "Expression",
                "FormatString", "IsHidden", "State", "KPIID", "IsSimpleMeasure", "DisplayFolder",
                "DetailRowsDefinition", "DataCategory", "FormatStringDefinition", "LineageTag"),
            ["INFO.VIEW.RELATIONSHIPS"] = Schema("ID", "Name", "Relationship", "Model", "IsActive",
                "CrossFilteringBehavior", "RelyOnReferentialIntegrity", "FromTable", "FromColumn",
                "FromCardinality", "ToTable", "ToColumn", "ToCardinality", "State", "SecurityFilteringBehavior"),
        };

    // Scalar wrappers evidenced by Microsoft's examples and the supplied work expression. This allowance
    // applies only to virtual-row fields; it does not broaden persisted/owner-row resolution.
    private static readonly HashSet<string> InfoViewRowScalarFunctions = Schema(
        "SWITCH", "TRUE", "NOT", "ISBLANK", "LEFT", "COALESCE", "FALSE", "IF", "LEN", "TRIM");

    private static HashSet<string> Schema(params string[] names) =>
        new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    private static bool IsInfoViewRowIterator(string? function) => IsRowIterator(function) ||
        string.Equals(function, "ADDCOLUMNS", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlySet<string>? ProvenInfoViewRowColumns(Stack<ReferenceContext> contexts)
    {
        foreach (var context in contexts)
        {
            if (context.Function is null || OwnerRowScalarFunctions.Contains(context.Function) ||
                InfoViewRowScalarFunctions.Contains(context.Function))
            {
                continue;
            }

            if (IsInfoViewRowIterator(context.Function))
            {
                if (context.Argument == 0) continue;
                // The innermost row source must be proven. Never borrow an outer schema through an
                // intervening persisted/unknown iterator, or through an unaccounted function call.
                return context.VirtualRowColumns;
            }

            return null;
        }

        return null;
    }

    private static IReadOnlySet<string>? InfoViewRowSource(
        string expression, int argumentsStart, IReadOnlyDictionary<string, DaxVariableTableBinding?>? bindings) =>
        TryReadCallArguments(expression, argumentsStart, out var arguments, out _) && arguments.Count > 0
            ? ReadInfoViewSchema(expression, arguments[0], depth: 0, bindings)
            : null;

    /// <summary>
    /// Proves only INFO.VIEW sources and FILTER/SELECTCOLUMNS/ADDCOLUMNS transformations of them.
    /// Tracks output names, not virtual-column lineage or values. Proven lexical VAR aliases are
    /// accepted; other table expressions remain unproven. Every recognised expression must occupy its entire
    /// argument, and recursion is bounded so unsupported syntax retains ordinary unresolved evidence.
    /// </summary>
    private static IReadOnlySet<string>? ReadInfoViewSchema(
        string expression, DaxArgument argument, int depth, IReadOnlyDictionary<string, DaxVariableTableBinding?>? bindings)
    {
        if (depth >= 32) return null;
        var index = SkipDaxTrivia(expression, argument.Start);
        if (index >= argument.End) return null;

        string? function = null;
        if (expression[index] != '(')
        {
            if (!IsUnquotedIdentifierStart(expression[index])) return null;
            var end = ReadDottedIdentifierEnd(expression, index);
            function = expression[index..end];
            index = SkipDaxTrivia(expression, end);
            if (index == argument.End)
                return bindings?.GetValueOrDefault(function)?.VirtualRowColumns;
        }

        if (index >= argument.End || expression[index] != '(' ||
            !TryReadCallArguments(expression, index + 1, out var arguments, out var callEnd) ||
            SkipDaxTrivia(expression, callEnd) != argument.End)
        {
            return null;
        }

        if (function is null)
            return arguments.Count == 1 ? ReadInfoViewSchema(expression, arguments[0], depth + 1, bindings) : null;

        if (InfoViewSchemas.TryGetValue(function, out var schema))
            return arguments.Count == 0 ? schema : null;

        if (arguments.Count < 2) return null;
        var input = ReadInfoViewSchema(expression, arguments[0], depth + 1, bindings);
        if (input is null) return null;

        if (function.Equals("FILTER", StringComparison.OrdinalIgnoreCase))
            return arguments.Count == 2 ? input : null;

        var addsColumns = function.Equals("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase);
        if (!addsColumns && !function.Equals("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase)) return null;
        var output = new HashSet<string>(addsColumns ? input : [], StringComparer.OrdinalIgnoreCase);
        for (var position = 1; position < arguments.Count; position++)
        {
            var projection = arguments[position];
            index = SkipDaxTrivia(expression, projection.Start);
            if (index < projection.End && expression[index] == '"')
            {
                var start = index;
                TrySkipString(expression, ref index);
                if (index > projection.End || expression[index - 1] != '"' ||
                    SkipDaxTrivia(expression, index) != projection.End || ++position >= arguments.Count)
                    return null;
                var name = expression[(start + 1)..(index - 1)].Replace("\"\"", "\"", StringComparison.Ordinal);
                if (name.Length == 0 || !output.Add(name)) return null;
            }
            else if (!addsColumns && index < projection.End && expression[index] == '[' &&
                     ReadBracketIdentifier(expression, ref index, out var name) &&
                     SkipDaxTrivia(expression, index) == projection.End && input.Contains(name))
            {
                if (!output.Add(name)) return null;
            }
            else
            {
                return null;
            }
        }

        return output;
    }

    private readonly record struct DaxArgument(int Start, int End);

    // Split an evidenced call without interpreting its scalar expressions. Reuse the extractor's
    // string/comment/identifier readers so commas in those tokens cannot become argument boundaries.
    private static bool TryReadCallArguments(
        string expression, int index, out List<DaxArgument> arguments, out int end)
    {
        arguments = [];
        end = index;
        var start = index;
        var delimiters = new Stack<char>();
        while (index < expression.Length)
        {
            if (TrySkipComment(expression, ref index) || TrySkipString(expression, ref index)) continue;
            if (expression[index] == '\'')
            {
                if (!ReadQuotedIdentifier(expression, ref index, out _)) return false;
                continue;
            }
            if (expression[index] == '[')
            {
                if (!ReadBracketIdentifier(expression, ref index, out _)) return false;
                continue;
            }

            var character = expression[index];
            if (character is '(' or '{')
            {
                if (delimiters.Count >= 64) return false;
                delimiters.Push(character);
            }
            else if (character == ')' && delimiters.Count == 0)
            {
                if (arguments.Count > 0 && SkipDaxTrivia(expression, start) == index) return false;
                if (arguments.Count > 0 || SkipDaxTrivia(expression, start) != index)
                    arguments.Add(new DaxArgument(start, index));
                end = index + 1;
                return true;
            }
            else if (character is ')' or '}')
            {
                if (!delimiters.TryPop(out var delimiter) || delimiter != (character == ')' ? '(' : '{')) return false;
            }
            else if (character is ',' or ';' && delimiters.Count == 0)
            {
                if (SkipDaxTrivia(expression, start) == index) return false;
                arguments.Add(new DaxArgument(start, index));
                start = index + 1;
            }

            index++;
        }

        return false;
    }

    private static int SkipDaxTrivia(string expression, int index)
    {
        index = NextNonWhitespace(expression, index);
        while (TrySkipComment(expression, ref index)) index = NextNonWhitespace(expression, index);
        return index;
    }

    private static int ReadDottedIdentifierEnd(string expression, int index)
    {
        while (index < expression.Length && IsUnquotedIdentifierPart(expression[index])) index++;
        while (index + 1 < expression.Length && expression[index] == '.' && IsUnquotedIdentifierStart(expression[index + 1]))
        {
            index += 2;
            while (index < expression.Length && IsUnquotedIdentifierPart(expression[index])) index++;
        }
        return index;
    }
}
