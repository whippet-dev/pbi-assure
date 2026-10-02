namespace PbiAssure.Core.Scanning;

internal static partial class DaxReferenceExtractor
{
    // Environments are snapshots at call boundaries, not scalar values or model-table bindings.
    // Null entries deliberately shadow proven outer tables with scalar/unknown local bindings.
    private static Dictionary<int, IReadOnlyDictionary<string, IReadOnlySet<string>?>> InfoViewVariableEnvironments(string expression)
    {
        var environments = new Dictionary<int, IReadOnlyDictionary<string, IReadOnlySet<string>?>>();
        VisitVariableScope(expression, new DaxArgument(0, expression.Length),
            new Dictionary<string, IReadOnlySet<string>?>(StringComparer.OrdinalIgnoreCase), environments, depth: 0);
        return environments;
    }

    private static void VisitVariableScope(
        string expression,
        DaxArgument span,
        IReadOnlyDictionary<string, IReadOnlySet<string>?> bindings,
        Dictionary<int, IReadOnlyDictionary<string, IReadOnlySet<string>?>> environments,
        int depth)
    {
        if (depth >= 64) return;
        var index = SkipDaxTrivia(expression, span.Start);
        if (IsDaxKeyword(expression, index, "VAR"))
        {
            // Validate the complete block before granting any binding. Inline VAR-valued bindings
            // without parentheses are intentionally unproven: identifying their outer RETURN needs
            // more grammar than this bounded walker owns.
            if (!TryReadVariableBlock(expression, span, out var declarations, out var returned)) return;
            foreach (var declaration in declarations)
            {
                VisitVariableScope(expression, declaration.Value, bindings, environments, depth + 1);
                var schema = ReadInfoViewSchema(expression, declaration.Value, depth: 0, bindings);
                var nextBindings = new Dictionary<string, IReadOnlySet<string>?>(bindings, StringComparer.OrdinalIgnoreCase)
                {
                    [declaration.Name] = schema,
                };
                bindings = nextBindings;
            }
            VisitVariableScope(expression, returned, bindings, environments, depth + 1);
            return;
        }

        while (index < span.End)
        {
            if (TrySkipComment(expression, ref index) || TrySkipString(expression, ref index)) continue;
            if (expression[index] == '\'')
            {
                if (!ReadQuotedIdentifier(expression, ref index, out _)) return;
                continue;
            }
            if (expression[index] == '[')
            {
                if (!ReadBracketIdentifier(expression, ref index, out _)) return;
                continue;
            }
            if (expression[index] == '(')
            {
                if (!TryReadCallArguments(expression, index + 1, out var arguments, out var end) || end > span.End) return;
                environments[index + 1] = bindings;
                foreach (var argument in arguments)
                    VisitVariableScope(expression, argument, bindings, environments, depth + 1);
                index = end;
                continue;
            }
            if (IsUnquotedIdentifierStart(expression[index]))
            {
                // An unexpected VAR/RETURN is not a scope we can safely borrow outer bindings through.
                if (IsDaxKeyword(expression, index, "VAR") || IsDaxKeyword(expression, index, "RETURN")) return;
                index = ReadDottedIdentifierEnd(expression, index);
                continue;
            }
            index++;
        }
    }

    private static bool TryReadVariableBlock(
        string expression, DaxArgument span, out List<DaxVariableDeclaration> declarations, out DaxArgument returned)
    {
        declarations = [];
        returned = default;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = SkipDaxTrivia(expression, span.Start);
        while (IsDaxKeyword(expression, index, "VAR"))
        {
            if (declarations.Count >= 256) return false;
            index = SkipDaxTrivia(expression, index + 3);
            if (index >= span.End || !IsUnquotedIdentifierStart(expression[index])) return false;
            var nameEnd = ReadDottedIdentifierEnd(expression, index);
            var name = expression[index..nameEnd];
            if (name.Contains('.', StringComparison.Ordinal) || !names.Add(name)) return false;
            index = SkipDaxTrivia(expression, nameEnd);
            if (index >= span.End || expression[index] != '=') return false;
            var valueStart = SkipDaxTrivia(expression, index + 1);
            if (IsDaxKeyword(expression, valueStart, "VAR") ||
                !TryFindVariableBoundary(expression, valueStart, span.End, out index) || index == valueStart)
                return false;
            declarations.Add(new DaxVariableDeclaration(name, new DaxArgument(valueStart, index)));
        }

        if (!IsDaxKeyword(expression, index, "RETURN")) return false;
        var returnStart = SkipDaxTrivia(expression, index + 6);
        if (returnStart >= span.End) return false;
        returned = new DaxArgument(returnStart, span.End);
        return true;
    }

    private static bool TryFindVariableBoundary(string expression, int index, int limit, out int boundary)
    {
        boundary = limit;
        while (index < limit)
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
            if (expression[index] == '(')
            {
                if (!TryReadCallArguments(expression, index + 1, out _, out var end) || end > limit) return false;
                index = end;
                continue;
            }
            // Table constructors and unexpected closing delimiters are not inferred in this slice.
            if (expression[index] is '{' or '}' or ')') return false;
            if (IsUnquotedIdentifierStart(expression[index]))
            {
                if (IsDaxKeyword(expression, index, "VAR") || IsDaxKeyword(expression, index, "RETURN"))
                {
                    boundary = index;
                    return true;
                }
                index = ReadDottedIdentifierEnd(expression, index);
                continue;
            }
            index++;
        }
        return false;
    }

    private static bool IsDaxKeyword(string expression, int index, string keyword) =>
        index + keyword.Length <= expression.Length &&
        expression.AsSpan(index, keyword.Length).Equals(keyword, StringComparison.OrdinalIgnoreCase) &&
        (index + keyword.Length == expression.Length ||
         !IsUnquotedIdentifierPart(expression[index + keyword.Length]) && expression[index + keyword.Length] != '.');

    private readonly record struct DaxVariableDeclaration(string Name, DaxArgument Value);
}
