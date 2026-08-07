using System.Text.Json;

namespace PbiAssure.Core.Scanning;

internal static class PbirExpressionReader
{
    public static PbirExpressionValue ReadProperty(JsonElement properties, string propertyName)
    {
        return properties.ValueKind == JsonValueKind.Object &&
               properties.TryGetProperty(propertyName, out var property)
            ? Read(property)
            : PbirExpressionValue.Missing;
    }

    public static PbirExpressionValue Read(JsonElement property)
    {
        var literal = FindLiteralValue(property);
        if (literal is not null)
        {
            return new PbirExpressionValue(NormalizeLiteral(literal), IsDynamic: false, IsPresent: true);
        }

        return property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
               property.ValueKind == JsonValueKind.Object && !property.EnumerateObject().Any()
            ? new PbirExpressionValue(null, IsDynamic: false, IsPresent: true)
            : new PbirExpressionValue(null, IsDynamic: true, IsPresent: true);
    }

    public static bool? ParseBoolean(string? value)
    {
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static string? FindLiteralValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("Literal", out var literal) &&
                literal.ValueKind == JsonValueKind.Object &&
                literal.TryGetProperty("Value", out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Number => value.GetRawText(),
                    _ => null,
                };
            }

            foreach (var property in element.EnumerateObject())
            {
                if (FindLiteralValue(property.Value) is { } nestedValue)
                {
                    return nestedValue;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (FindLiteralValue(item) is { } nestedValue)
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static string NormalizeLiteral(string value)
    {
        return value.Length >= 2 && value[0] == '\'' && value[^1] == '\''
            ? value[1..^1].Replace("''", "'", StringComparison.Ordinal)
            : value;
    }
}

internal sealed record PbirExpressionValue(string? Literal, bool IsDynamic, bool IsPresent)
{
    public static PbirExpressionValue Missing { get; } = new(null, IsDynamic: false, IsPresent: false);
}
