using System.Text;

namespace PbiAssure.Core.Scanning;

internal static class MReferenceExtractor
{
    public static MQueryReferenceResult Analyze(string expression, IReadOnlyCollection<string> knownQueryNames) =>
        MQueryReferenceResolver.Analyze(expression, knownQueryNames);

    public static string[] Extract(string expression, IReadOnlyCollection<string> knownQueryNames) =>
        Analyze(expression, knownQueryNames).References;

    public static bool HasDynamicReferences(string expression) => Analyze(expression, []).Dynamic;

    // Internal compatibility entry point: doubt now means unsupported lexical syntax, not a let shape.
    public static bool HasIncompleteReferences(string expression) => Analyze(expression, []).Incomplete;

    // Retained for existing connector/column-lineage consumers; query discovery uses the tokenizer.
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

}
