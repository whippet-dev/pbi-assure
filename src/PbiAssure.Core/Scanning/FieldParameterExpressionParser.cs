using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class FieldParameterExpressionParser
{
    private static readonly IReadOnlySet<string> NoKnownTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static SemanticFieldParameterInventory? TryParse(string tableName, string expression)
    {
        var trimmed = expression.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            return null;
        }

        var entries = ExtractNameOfArguments(expression)
            .Select(argument => new
            {
                Argument = argument,
                References = DaxReferenceExtractor.Extract(argument, NoKnownTables)
                    .Where(reference => !reference.IsTableReference && reference.Table is not null)
                    .ToArray(),
            })
            .Where(item => item.References.Length == 1)
            .Select(item => new SemanticFieldParameterEntryInventory(
                Table: item.References[0].Table!,
                ObjectName: item.References[0].ObjectName,
                ReferenceText: item.References[0].Text))
            .Distinct()
            .ToArray();

        return entries.Length == 0
            ? null
            : new SemanticFieldParameterInventory(tableName, expression, entries);
    }

    private static IEnumerable<string> ExtractNameOfArguments(string expression)
    {
        var index = 0;
        while (index < expression.Length)
        {
            if (TrySkipComment(expression, ref index) || TrySkipString(expression, ref index))
            {
                continue;
            }

            if (!IsIdentifierStart(expression[index]))
            {
                index++;
                continue;
            }

            var identifierStart = index++;
            while (index < expression.Length && IsIdentifierPart(expression[index]))
            {
                index++;
            }

            if (!expression[identifierStart..index].Equals("NAMEOF", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var openParenthesis = NextNonWhitespace(expression, index);
            if (openParenthesis >= expression.Length || expression[openParenthesis] != '(')
            {
                continue;
            }

            if (TryReadArgument(expression, openParenthesis, out var argument, out var nextIndex))
            {
                yield return argument;
                index = nextIndex;
            }
        }
    }

    private static bool TryReadArgument(
        string expression,
        int openParenthesis,
        out string argument,
        out int nextIndex)
    {
        var depth = 1;
        var index = openParenthesis + 1;
        var argumentStart = index;
        while (index < expression.Length)
        {
            if (TrySkipComment(expression, ref index) ||
                TrySkipString(expression, ref index) ||
                TrySkipQuotedIdentifier(expression, ref index) ||
                TrySkipBracketIdentifier(expression, ref index))
            {
                continue;
            }

            if (expression[index] == '(')
            {
                depth++;
            }
            else if (expression[index] == ')' && --depth == 0)
            {
                argument = expression[argumentStart..index].Trim();
                nextIndex = index + 1;
                return true;
            }

            index++;
        }

        argument = string.Empty;
        nextIndex = expression.Length;
        return false;
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
        return TrySkipDelimited(expression, ref index, '"', '"');
    }

    private static bool TrySkipQuotedIdentifier(string expression, ref int index)
    {
        return TrySkipDelimited(expression, ref index, '\'', '\'');
    }

    private static bool TrySkipBracketIdentifier(string expression, ref int index)
    {
        return TrySkipDelimited(expression, ref index, '[', ']');
    }

    private static bool TrySkipDelimited(string expression, ref int index, char opening, char closing)
    {
        if (expression[index] != opening)
        {
            return false;
        }

        index++;
        while (index < expression.Length)
        {
            if (expression[index] != closing)
            {
                index++;
                continue;
            }

            if (index + 1 < expression.Length && expression[index + 1] == closing)
            {
                index += 2;
                continue;
            }

            index++;
            return true;
        }

        return true;
    }

    private static int NextNonWhitespace(string value, int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsIdentifierStart(char value)
    {
        return char.IsLetter(value) || value == '_';
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }
}
