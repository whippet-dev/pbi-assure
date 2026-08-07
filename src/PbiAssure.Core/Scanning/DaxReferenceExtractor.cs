namespace PbiAssure.Core.Scanning;

internal static class DaxReferenceExtractor
{
    public static DaxReference[] Extract(string expression, IReadOnlySet<string> knownTables)
    {
        var references = new List<DaxReference>();
        var index = 0;

        while (index < expression.Length)
        {
            if (TrySkipComment(expression, ref index) || TrySkipString(expression, ref index))
            {
                continue;
            }

            if (expression[index] == '\'')
            {
                ReadQuotedIdentifierReference(expression, knownTables, references, ref index);
                continue;
            }

            if (expression[index] == '[')
            {
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
                        Text: expression[startIndex..index]));
                }

                continue;
            }

            if (IsUnquotedIdentifierStart(expression[index]))
            {
                ReadUnquotedIdentifierReference(expression, knownTables, references, ref index);
                continue;
            }

            index++;
        }

        return references.Distinct().ToArray();
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

        if (knownTables.Contains(identifier) &&
            (nextIndex >= expression.Length || expression[nextIndex] != '('))
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
        string Text);
}
