using System.Globalization;
using System.Text;

namespace PbiAssure.Core.Scanning;

internal enum MReferenceTokenKind { Identifier, QuotedIdentifier, Keyword, Number, Text, Symbol, End }

internal readonly record struct MReferenceToken(MReferenceTokenKind Kind, string Text, int Start, int End);

/// <summary>Tokens for lexical reference discovery only; never evaluates M.</summary>
internal static class MReferenceTokenizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "and", "as", "each", "else", "error", "false", "if", "in", "is", "let", "meta", "not",
        "null", "or", "otherwise", "section", "shared", "then", "true", "try", "type",
    };

    public static (MReferenceToken[] Tokens, bool Incomplete) Tokenize(string source)
    {
        if (source.Length > 2_000_000) return ([new(MReferenceTokenKind.End, string.Empty, 0, 0)], true);
        var tokens = new List<MReferenceToken>();
        var incomplete = false;
        var index = 0;
        while (index < source.Length && tokens.Count < 100_000)
        {
            var start = index;
            var current = source[index];
            if (char.IsWhiteSpace(current)) { index++; continue; }
            if (source.AsSpan(index).StartsWith("//", StringComparison.Ordinal))
            {
                while (index < source.Length && source[index] is not '\r' and not '\n') index++;
                continue;
            }
            if (source.AsSpan(index).StartsWith("/*", StringComparison.Ordinal))
            {
                index += 2;
                var depth = 1;
                while (index < source.Length && depth > 0)
                {
                    if (source.AsSpan(index).StartsWith("/*", StringComparison.Ordinal)) { depth++; index += 2; }
                    else if (source.AsSpan(index).StartsWith("*/", StringComparison.Ordinal)) { depth--; index += 2; }
                    else index++;
                }
                incomplete |= depth != 0;
                continue;
            }

            var quotedIdentifier = current == '#' && index + 1 < source.Length && source[index + 1] == '"';
            if (current == '"' || quotedIdentifier)
            {
                index += quotedIdentifier ? 2 : 1;
                var value = new StringBuilder();
                var closed = false;
                while (index < source.Length)
                {
                    current = source[index++];
                    if (current == '"')
                    {
                        if (index < source.Length && source[index] == '"') { value.Append('"'); index++; }
                        else { closed = true; break; }
                    }
                    else value.Append(current);
                }
                incomplete |= !closed;
                var decoded = quotedIdentifier ? DecodeIdentifier(value.ToString(), ref incomplete) : string.Empty;
                tokens.Add(new(quotedIdentifier ? MReferenceTokenKind.QuotedIdentifier : MReferenceTokenKind.Text,
                    decoded, start, index));
                continue;
            }

            if (char.IsDigit(current))
            {
                index++;
                while (index < source.Length)
                {
                    var next = source[index];
                    if (char.IsLetterOrDigit(next) || next == '.' &&
                        !(index + 1 < source.Length && source[index + 1] == '.') ||
                        (next is '+' or '-') && (source[index - 1] is 'e' or 'E')) index++;
                    else break;
                }
                tokens.Add(new(MReferenceTokenKind.Number, source[start..index], start, index));
                continue;
            }
            if (IsIdentifierStart(current) || current == '#')
            {
                index++;
                while (index < source.Length && (IsIdentifierPart(source[index]) ||
                    source[index] == '.' && index + 1 < source.Length && IsIdentifierStart(source[index + 1]))) index++;
                var text = source[start..index];
                tokens.Add(new(Keywords.Contains(text) ? MReferenceTokenKind.Keyword : MReferenceTokenKind.Identifier,
                    text, start, index));
                continue;
            }
            var symbol = current.ToString();
            if (index + 1 < source.Length && source.Substring(index, 2) is "=>" or "<>" or "<=" or ">=" or "??" or "..")
                symbol = source.Substring(index, 2);
            index += symbol.Length;
            tokens.Add(new(MReferenceTokenKind.Symbol, symbol, start, index));
        }
        incomplete |= index < source.Length;
        tokens.Add(new(MReferenceTokenKind.End, string.Empty, index, index));
        return (tokens.ToArray(), incomplete);
    }

    private static bool IsIdentifierStart(char character) => char.IsLetter(character) || character == '_';
    private static bool IsIdentifierPart(char character) => IsIdentifierStart(character) || char.IsDigit(character) ||
        char.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.Format;

    private static string DecodeIdentifier(string value, ref bool incomplete)
    {
        var result = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '#' || index + 1 >= value.Length || value[index + 1] != '(')
            { result.Append(value[index]); continue; }
            var end = value.IndexOf(')', index + 2);
            if (end < 0) { incomplete = true; return value; }
            foreach (var escape in value[(index + 2)..end].Split(','))
            {
                var text = escape.Trim();
                if (text == "cr") result.Append('\r');
                else if (text == "lf") result.Append('\n');
                else if (text == "tab") result.Append('\t');
                else if (text == "#") result.Append('#');
                else if ((text.Length is 4 or 8) && int.TryParse(text, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var code) && code is >= 0 and <= 0x10ffff && code is not (>= 0xd800 and <= 0xdfff))
                    result.Append(char.ConvertFromUtf32(code));
                else incomplete = true;
            }
            index = end;
        }
        return result.ToString();
    }
}
