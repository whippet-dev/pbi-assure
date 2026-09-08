namespace PbiAssure.Core.Scanning;

internal static class DaxReferenceExtractor
{
    public static DaxReference[] Extract(string expression, IReadOnlySet<string> knownTables) =>
        Extract(expression, knownTables, NoKnownFunctions);

    private static readonly HashSet<string> NoKnownFunctions = new(StringComparer.OrdinalIgnoreCase);

    // Evidenced scalar wrappers in Desktop auto-date calculated columns. Other calls receive no
    // owner-row privilege until their context behaviour is accounted for explicitly.
    private static readonly HashSet<string> OwnerRowScalarFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "YEAR", "MONTH", "DAY", "FORMAT", "INT",
    };

    private static bool IsRowIterator(string? function) =>
        function is not null && (function.Equals("SUMX", StringComparison.OrdinalIgnoreCase) ||
            function.Equals("FILTER", StringComparison.OrdinalIgnoreCase) ||
            function.Equals("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase));

    private sealed class ReferenceContext(char delimiter, string? function, bool hasPersistedRowSource)
    {
        public char Delimiter { get; } = delimiter;
        public string? Function { get; } = function;
        public int Argument { get; set; }
        public bool HasUnboundIteratorRow => IsRowIterator(Function) && Argument > 0 && !hasPersistedRowSource;

        public bool PreservesOwnerRow => Function is null || OwnerRowScalarFunctions.Contains(Function) ||
            // The table argument is evaluated in the incoming context; the subsequent row expressions
            // of these evidenced iterators are deliberately not bound to an inferred iterator target.
            IsRowIterator(Function) && Argument == 0;
    }

    /// <summary>
    /// Extracts model references, and calls to declared user-defined functions.
    ///
    /// An identifier followed by "(" is normally a built-in DAX call and is ignored. A call is only
    /// recorded when its name matches a declared function: Microsoft documents that a user-defined
    /// function name cannot conflict with a built-in, so a match cannot capture SUM or COUNTROWS.
    /// </summary>
    public static DaxReference[] Extract(
        string expression,
        IReadOnlySet<string> knownTables,
        IReadOnlySet<string> knownFunctions)
    {
        var references = new List<DaxReference>();
        var index = 0;
        var contexts = new Stack<ReferenceContext>();
        string? pendingFunction = null;
        var malformedContext = false;

        while (index < expression.Length)
        {
            if (TrySkipComment(expression, ref index))
            {
                continue;
            }
            if (TrySkipString(expression, ref index)) { pendingFunction = null; continue; }

            if (expression[index] == '\'')
            {
                pendingFunction = null;
                ReadQuotedIdentifierReference(expression, knownTables, references, ref index);
                continue;
            }

            if (expression[index] == '[')
            {
                pendingFunction = null;
                var previousIndex = PreviousNonWhitespace(expression, index - 1);
                if (previousIndex >= 0 && expression[previousIndex] == '.')
                {
                    ReadBracketIdentifier(expression, ref index, out _);
                    continue;
                }

                var startIndex = index;
                if (ReadBracketIdentifier(expression, ref index, out var objectName))
                {
                    references.Add(new DaxReference(
                        Table: null,
                        ObjectName: objectName,
                        IsTableReference: false,
                        Text: expression[startIndex..index])
                    {
                        CanUseOwnerRowContext = !malformedContext && contexts.All(context => context.PreservesOwnerRow),
                        HasUnboundIteratorRowContext = contexts.Any(context => context.HasUnboundIteratorRow),
                    });
                }

                continue;
            }

            if (IsUnquotedIdentifierStart(expression[index]))
            {
                var identifierStart = index;
                var identifierEnd = index + 1;
                while (identifierEnd < expression.Length && IsUnquotedIdentifierPart(expression[identifierEnd])) identifierEnd++;
                ReadUnquotedIdentifierReference(expression, knownTables, knownFunctions, references, ref index);
                var previous = PreviousNonWhitespace(expression, identifierStart - 1);
                pendingFunction = index == identifierEnd
                    ? (previous >= 0 && expression[previous] == '.' ? "." : string.Empty) + expression[identifierStart..identifierEnd]
                    : null;
                continue;
            }

            var character = expression[index];
            if (character is '(' or '{')
            {
                contexts.Push(new ReferenceContext(character, character == '(' ? pendingFunction : null,
                    character == '(' && IsRowIterator(pendingFunction) &&
                    HasPersistedRowSource(expression, index + 1, knownTables)));
            }
            else if (character is ')' or '}')
            {
                if (!contexts.TryPop(out var context) || context.Delimiter != (character == ')' ? '(' : '{')) malformedContext = true;
            }
            else if (character is ',' or ';' && contexts.TryPeek(out var context)) context.Argument++;
            if (!char.IsWhiteSpace(character)) pendingFunction = null;

            index++;
        }

        if (malformedContext || contexts.Count != 0)
            references = references.Select(reference => reference with { CanUseOwnerRowContext = false }).ToList();
        return references.Distinct().ToArray();
    }

    // Only an entire first argument naming a known table is accounted for here. Table expressions,
    // variables and constructors can introduce virtual columns; their row source is not inferred.
    private static bool HasPersistedRowSource(string expression, int index, IReadOnlySet<string> knownTables)
    {
        SkipTrivia();
        string table;
        if (index < expression.Length && expression[index] == '\'')
        {
            if (!ReadQuotedIdentifier(expression, ref index, out table)) return false;
        }
        else
        {
            if (index >= expression.Length || !IsUnquotedIdentifierStart(expression[index])) return false;
            var start = index++;
            while (index < expression.Length && IsUnquotedIdentifierPart(expression[index])) index++;
            table = expression[start..index];
        }
        SkipTrivia();
        return knownTables.Contains(table) && index < expression.Length && expression[index] is ',' or ';';

        void SkipTrivia()
        {
            index = NextNonWhitespace(expression, index);
            while (TrySkipComment(expression, ref index)) index = NextNonWhitespace(expression, index);
        }
    }

    /// <summary>
    /// Extracts only the deliberately narrow <c>USERELATIONSHIP</c> form that can be tied to one
    /// relationship without guessing: exactly two explicit qualified column references. This is kept
    /// separate from the ordinary flat-reference stream because the paired argument identity is the
    /// evidence needed to review inactive-relationship activation.
    /// </summary>
    public static DaxUserRelationshipCall[] ExtractUserRelationshipCalls(string expression)
    {
        var calls = new List<DaxUserRelationshipCall>();
        var index = 0;

        while (index < expression.Length)
        {
            if (TrySkipComment(expression, ref index) || TrySkipString(expression, ref index))
            {
                continue;
            }

            if (expression[index] == '\'')
            {
                ReadQuotedIdentifier(expression, ref index, out _);
                continue;
            }

            if (!IsUnquotedIdentifierStart(expression[index]))
            {
                index++;
                continue;
            }

            var identifierStart = index;
            index++;
            while (index < expression.Length && IsUnquotedIdentifierPart(expression[index]))
            {
                index++;
            }

            if (!expression[identifierStart..index].Equals("USERELATIONSHIP", StringComparison.OrdinalIgnoreCase) ||
                !TryReadUserRelationshipCall(expression, ref index, out var call))
            {
                continue;
            }

            calls.Add(call);
        }

        return calls.Distinct().ToArray();
    }

    private static bool TryReadUserRelationshipCall(
        string expression,
        ref int index,
        out DaxUserRelationshipCall call)
    {
        var openParenthesis = NextNonWhitespace(expression, index);
        if (openParenthesis >= expression.Length || expression[openParenthesis] != '(')
        {
            call = null!;
            return false;
        }

        var arguments = new List<string>();
        var argumentStart = openParenthesis + 1;
        var cursor = argumentStart;
        var depth = 1;
        while (cursor < expression.Length)
        {
            if (TrySkipComment(expression, ref cursor) || TrySkipString(expression, ref cursor))
            {
                continue;
            }

            if (expression[cursor] == '(')
            {
                depth++;
                cursor++;
                continue;
            }

            if (expression[cursor] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    arguments.Add(expression[argumentStart..cursor]);
                    index = cursor + 1;
                    return TryCreateUserRelationshipCall(arguments, out call);
                }

                cursor++;
                continue;
            }

            if (expression[cursor] == ',' && depth == 1)
            {
                arguments.Add(expression[argumentStart..cursor]);
                argumentStart = cursor + 1;
            }

            cursor++;
        }

        call = null!;
        return false;
    }

    private static bool TryCreateUserRelationshipCall(
        List<string> arguments,
        out DaxUserRelationshipCall call)
    {
        if (arguments.Count == 2 &&
            TryParseQualifiedColumnReference(arguments[0], out var first) &&
            TryParseQualifiedColumnReference(arguments[1], out var second))
        {
            call = new DaxUserRelationshipCall(first, second);
            return true;
        }

        call = null!;
        return false;
    }

    private static bool TryParseQualifiedColumnReference(string text, out DaxQualifiedColumnReference reference)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;

        string table;
        if (index < text.Length && text[index] == '\'')
        {
            if (!ReadQuotedIdentifier(text, ref index, out table))
            {
                reference = null!;
                return false;
            }
        }
        else
        {
            var tableStart = index;
            if (index >= text.Length || !IsUnquotedIdentifierStart(text[index]))
            {
                reference = null!;
                return false;
            }

            index++;
            while (index < text.Length && IsUnquotedIdentifierPart(text[index])) index++;
            table = text[tableStart..index];
        }

        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        if (index >= text.Length || text[index] != '[' || !ReadBracketIdentifier(text, ref index, out var column))
        {
            reference = null!;
            return false;
        }

        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        if (index != text.Length || string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
        {
            reference = null!;
            return false;
        }

        reference = new DaxQualifiedColumnReference(table, column);
        return true;
    }

    private static void ReadQuotedIdentifierReference(
        string expression,
        IReadOnlySet<string> knownTables,
        List<DaxReference> references,
        ref int index)
    {
        var startIndex = index;
        if (!ReadQuotedIdentifier(expression, ref index, out var tableName))
        {
            index = startIndex + 1;
            return;
        }

        var nextIndex = NextNonWhitespace(expression, index);
        if (nextIndex < expression.Length && expression[nextIndex] == '[')
        {
            index = nextIndex;
            if (ReadBracketIdentifier(expression, ref index, out var objectName))
            {
                references.Add(new DaxReference(
                    Table: tableName,
                    ObjectName: objectName,
                    IsTableReference: false,
                    Text: expression[startIndex..index]));
                SkipHierarchySuffix(expression, ref index);
            }

            return;
        }

        if (knownTables.Contains(tableName))
        {
            references.Add(new DaxReference(
                Table: tableName,
                ObjectName: tableName,
                IsTableReference: true,
                Text: expression[startIndex..index]));
        }
    }

    private static void ReadUnquotedIdentifierReference(
        string expression,
        IReadOnlySet<string> knownTables,
        IReadOnlySet<string> knownFunctions,
        List<DaxReference> references,
        ref int index)
    {
        var startIndex = index;
        index++;
        while (index < expression.Length && IsUnquotedIdentifierPart(expression[index]))
        {
            index++;
        }

        var identifier = expression[startIndex..index];
        var nextIndex = NextNonWhitespace(expression, index);
        if (nextIndex < expression.Length && expression[nextIndex] == '[')
        {
            index = nextIndex;
            if (ReadBracketIdentifier(expression, ref index, out var objectName))
            {
                references.Add(new DaxReference(
                    Table: identifier,
                    ObjectName: objectName,
                    IsTableReference: false,
                    Text: expression[startIndex..index]));
                SkipHierarchySuffix(expression, ref index);
            }

            return;
        }

        var isCall = nextIndex < expression.Length && expression[nextIndex] == '(';
        if (isCall)
        {
            if (knownFunctions.Contains(identifier))
            {
                references.Add(new DaxReference(
                    Table: null,
                    ObjectName: identifier,
                    IsTableReference: false,
                    Text: expression[startIndex..index])
                {
                    IsFunctionReference = true,
                });
            }

            return;
        }

        if (knownTables.Contains(identifier))
        {
            references.Add(new DaxReference(
                Table: identifier,
                ObjectName: identifier,
                IsTableReference: true,
                Text: expression[startIndex..index]));
        }
    }

    private static void SkipHierarchySuffix(string expression, ref int index)
    {
        while (true)
        {
            var dotIndex = NextNonWhitespace(expression, index);
            if (dotIndex >= expression.Length || expression[dotIndex] != '.')
            {
                return;
            }

            var bracketIndex = NextNonWhitespace(expression, dotIndex + 1);
            if (bracketIndex >= expression.Length || expression[bracketIndex] != '[')
            {
                return;
            }

            index = bracketIndex;
            if (!ReadBracketIdentifier(expression, ref index, out _))
            {
                return;
            }
        }
    }

    private static bool TrySkipComment(string expression, ref int index)
    {
        if (index + 1 >= expression.Length)
        {
            return false;
        }

        if ((expression[index] == '/' && expression[index + 1] == '/') ||
            (expression[index] == '-' && expression[index + 1] == '-'))
        {
            index += 2;
            while (index < expression.Length && expression[index] is not '\r' and not '\n')
            {
                index++;
            }

            return true;
        }

        if (expression[index] != '/' || expression[index + 1] != '*')
        {
            return false;
        }

        index += 2;
        while (index + 1 < expression.Length &&
               (expression[index] != '*' || expression[index + 1] != '/'))
        {
            index++;
        }

        index = Math.Min(expression.Length, index + 2);
        return true;
    }

    private static bool TrySkipString(string expression, ref int index)
    {
        if (expression[index] != '"')
        {
            return false;
        }

        index++;
        while (index < expression.Length)
        {
            if (expression[index] != '"')
            {
                index++;
                continue;
            }

            if (index + 1 < expression.Length && expression[index + 1] == '"')
            {
                index += 2;
                continue;
            }

            index++;
            return true;
        }

        return true;
    }

    private static bool ReadQuotedIdentifier(string expression, ref int index, out string identifier)
    {
        var result = new System.Text.StringBuilder();
        index++;
        while (index < expression.Length)
        {
            if (expression[index] != '\'')
            {
                result.Append(expression[index]);
                index++;
                continue;
            }

            if (index + 1 < expression.Length && expression[index + 1] == '\'')
            {
                result.Append('\'');
                index += 2;
                continue;
            }

            index++;
            identifier = result.ToString();
            return true;
        }

        identifier = string.Empty;
        return false;
    }

    private static bool ReadBracketIdentifier(string expression, ref int index, out string identifier)
    {
        var result = new System.Text.StringBuilder();
        index++;
        while (index < expression.Length)
        {
            if (expression[index] != ']')
            {
                result.Append(expression[index]);
                index++;
                continue;
            }

            if (index + 1 < expression.Length && expression[index + 1] == ']')
            {
                result.Append(']');
                index += 2;
                continue;
            }

            index++;
            identifier = result.ToString();
            return true;
        }

        identifier = string.Empty;
        return false;
    }

    private static int PreviousNonWhitespace(string value, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(value[index]))
        {
            index--;
        }

        return index;
    }

    private static int NextNonWhitespace(string value, int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsUnquotedIdentifierStart(char value)
    {
        return char.IsLetter(value) || value == '_';
    }

    private static bool IsUnquotedIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    internal sealed record DaxReference(
        string? Table,
        string ObjectName,
        bool IsTableReference,
        string Text)
    {
        /// <summary>A call to a declared user-defined function rather than a model object reference.</summary>
        public bool IsFunctionReference { get; init; }

        /// <summary>Occurrence is outside row-changing or unaccounted call scopes; not a table binding.</summary>
        public bool CanUseOwnerRowContext { get; init; }

        /// <summary>A recognised iterator's row expression over an unbound table expression.</summary>
        public bool HasUnboundIteratorRowContext { get; init; }
    }

    internal sealed record DaxQualifiedColumnReference(string Table, string Column);

    internal sealed record DaxUserRelationshipCall(
        DaxQualifiedColumnReference First,
        DaxQualifiedColumnReference Second);
}
