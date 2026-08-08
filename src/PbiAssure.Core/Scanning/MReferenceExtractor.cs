using System.Text;
using System.Text.RegularExpressions;

namespace PbiAssure.Core.Scanning;

internal static partial class MReferenceExtractor
{
    public static string[] Extract(string expression, IReadOnlyCollection<string> knownQueryNames)
    {
        var searchable = RemoveStringsAndComments(expression);
        var localBindings = LocalBindingRegex().Matches(searchable)
            .Select(match => NormalizeIdentifier(match.Groups[1].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return knownQueryNames
            .Where(name => !localBindings.Contains(name) && ContainsIdentifier(searchable, name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool HasDynamicReferences(string expression) =>
        expression.Contains("Expression.Evaluate", StringComparison.OrdinalIgnoreCase) ||
        expression.Contains("#shared", StringComparison.OrdinalIgnoreCase) ||
        expression.Contains("Record.Field", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIdentifier(string expression, string name)
    {
        var quoted = "#\"" + name.Replace("\"", "\"\"") + "\"";
        if (expression.Contains(quoted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(
            expression,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?![A-Za-z0-9_.])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizeIdentifier(string value)
    {
        return value.StartsWith("#\"", StringComparison.Ordinal) && value.EndsWith('"')
            ? value[2..^1].Replace("\"\"", "\"")
            : value;
    }

    internal static string RemoveStringsAndComments(string expression)
    {
        var result = new StringBuilder(expression.Length);
        for (var index = 0; index < expression.Length; index++)
        {
            if (expression[index] == '#' && index + 1 < expression.Length && expression[index + 1] == '"')
            {
                result.Append("#\"");
                index += 2;
                while (index < expression.Length)
                {
                    result.Append(expression[index]);
                    if (expression[index] == '"')
                    {
                        if (index + 1 < expression.Length && expression[index + 1] == '"')
                        {
                            result.Append('"');
                            index += 2;
                            continue;
                        }
                        break;
                    }
                    index++;
                }
                continue;
            }

            if (expression[index] == '/' && index + 1 < expression.Length && expression[index + 1] == '/')
            {
                while (index < expression.Length && expression[index] is not '\r' and not '\n')
                {
                    result.Append(' ');
                    index++;
                }
                index--;
                continue;
            }

            if (expression[index] == '/' && index + 1 < expression.Length && expression[index + 1] == '*')
            {
                result.Append("  ");
                index += 2;
                while (index + 1 < expression.Length && !(expression[index] == '*' && expression[index + 1] == '/'))
                {
                    result.Append(expression[index] is '\r' or '\n' ? expression[index] : ' ');
                    index++;
                }
                result.Append("  ");
                index++;
                continue;
            }

            if (expression[index] == '"' && (index == 0 || expression[index - 1] != '#'))
            {
                result.Append(' ');
                index++;
                while (index < expression.Length)
                {
                    result.Append(expression[index] is '\r' or '\n' ? expression[index] : ' ');
                    if (expression[index] == '"')
                    {
                        if (index + 1 < expression.Length && expression[index + 1] == '"')
                        {
                            result.Append(' ');
                            index += 2;
                            continue;
                        }
                        break;
                    }
                    index++;
                }
                continue;
            }

            result.Append(expression[index]);
        }
        return result.ToString();
    }

    [GeneratedRegex("(?m)^\\s*(#\"(?:[^\"]|\"\")*\"|[A-Za-z_][A-Za-z0-9_.]*)\\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex LocalBindingRegex();
}
