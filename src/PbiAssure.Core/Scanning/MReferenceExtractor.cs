using System.Text;
using System.Text.RegularExpressions;

namespace PbiAssure.Core.Scanning;

internal static partial class MReferenceExtractor
{
    /// <summary>
    /// M identifiers are case-sensitive, so <c>data</c> and <c>Data</c> are different names. Matching
    /// them case-insensitively was wrong in both directions: a local binding <c>data = 5</c> suppressed
    /// a genuine reference to a global query <c>Data</c>, erasing a real dependency and reporting that
    /// query as having no known use; and a differently-cased identifier could match a query it has
    /// nothing to do with.
    ///
    /// Ordering stays case-insensitive: that is presentation, not identity.
    /// </summary>
    public static string[] Extract(string expression, IReadOnlyCollection<string> knownQueryNames)
    {
        var searchable = RemoveStringsAndComments(expression);
        var localBindings = LocalBindingRegex().Matches(searchable)
            .Select(match => NormalizeIdentifier(match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        return knownQueryNames
            .Where(name => !localBindings.Contains(name) && ContainsIdentifier(searchable, name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool HasDynamicReferences(string expression) =>
        expression.Contains("Expression.Evaluate", StringComparison.OrdinalIgnoreCase) ||
        expression.Contains("#shared", StringComparison.OrdinalIgnoreCase) ||
        expression.Contains("Record.Field", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this expression has a <c>let</c> shape whose bindings the flat binding model cannot be
    /// trusted to see. <see cref="LocalBindingRegex"/> only recognises a binding that starts a line, and
    /// the bindings it does find are applied to the whole expression rather than to their own scope.
    ///
    /// Two shapes break that, and both are recognisable without parsing:
    /// a <c>let</c> with anything after it on the same line, whose first binding is therefore invisible;
    /// and more than one <c>let</c>, which means nested or sequential scopes that a flat set cannot keep
    /// apart. The ordinary Desktop-generated shape — one <c>let</c>, alone on its line, bindings below —
    /// triggers neither, so this does not fire merely because an expression is M.
    ///
    /// This reports doubt; it does not resolve it. A real lexical resolver is still needed.
    /// </summary>
    public static bool HasUnresolvableBindingScope(string expression)
    {
        var searchable = RemoveStringsAndComments(expression);
        var matches = Regex.Matches(searchable, @"(?<![A-Za-z0-9_])let(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
        if (matches.Count > 1)
        {
            return true;
        }

        return matches.Count == 1 && HasContentAfterOnSameLine(searchable, matches[0].Index + 3);
    }

    private static bool HasContentAfterOnSameLine(string expression, int index)
    {
        for (var cursor = index; cursor < expression.Length; cursor++)
        {
            if (expression[cursor] is '\r' or '\n')
            {
                return false;
            }

            if (!char.IsWhiteSpace(expression[cursor]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the name appears at least once in a position where it could be a reference to a global
    /// query, rather than only in positions where M syntax makes it a field name.
    ///
    /// Field-name positions are recognised by immediate adjacency, which is unambiguous without parsing:
    /// <c>[Bar]</c> and <c>Rec[Bar]</c> are field selectors, <c>[Bar = 1]</c> is the first key of a
    /// record, and <c>, Bar =</c> inside brackets is a later key. An occurrence anywhere else still
    /// counts, so a genuine reference in a record value — <c>[A = Bar]</c> — is preserved.
    /// </summary>
    private static bool ContainsIdentifier(string expression, string name)
    {
        var quoted = "#\"" + name.Replace("\"", "\"\"") + "\"";
        foreach (var occurrence in Occurrences(expression, quoted))
        {
            if (!IsFieldNamePosition(expression, occurrence, occurrence + quoted.Length))
            {
                return true;
            }
        }

        foreach (Match match in Regex.Matches(
                     expression,
                     $@"(?<![A-Za-z0-9_]){Regex.Escape(name)}(?![A-Za-z0-9_.])",
                     RegexOptions.CultureInvariant))
        {
            if (!IsFieldNamePosition(expression, match.Index, match.Index + match.Length))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<int> Occurrences(string expression, string value)
    {
        for (var index = expression.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = expression.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            yield return index;
        }
    }

    private static bool IsFieldNamePosition(string expression, int start, int end)
    {
        var previous = PreviousNonWhitespace(expression, start - 1);
        var next = NextNonWhitespace(expression, end);
        if (previous < 0 || next >= expression.Length)
        {
            return false;
        }

        // [Bar] or Rec[Bar] — a field selector. [Bar = ...] — the first key of a record.
        if (expression[previous] == '[' && expression[next] is ']' or '=')
        {
            return true;
        }

        // , Bar = ... inside brackets — a later key of a record. The bracket test keeps ordinary
        // comma-separated argument lists out of it.
        return expression[previous] == ',' && expression[next] == '=' && IsInsideBrackets(expression, start);
    }

    private static bool IsInsideBrackets(string expression, int index)
    {
        var depth = 0;
        for (var cursor = 0; cursor < index; cursor++)
        {
            if (expression[cursor] == '[')
            {
                depth++;
            }
            else if (expression[cursor] == ']' && depth > 0)
            {
                depth--;
            }
        }

        return depth > 0;
    }

    private static int PreviousNonWhitespace(string expression, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(expression[index]))
        {
            index--;
        }

        return index;
    }

    private static int NextNonWhitespace(string expression, int index)
    {
        while (index < expression.Length && char.IsWhiteSpace(expression[index]))
        {
            index++;
        }

        return index;
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
