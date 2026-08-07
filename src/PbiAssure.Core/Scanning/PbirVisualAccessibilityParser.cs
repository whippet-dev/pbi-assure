using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualAccessibilityParser
{
    public static VisualAccessibilityInventory Parse(JsonElement visual)
    {
        var altTextProperty = FindProperty(visual, "altText");
        var altTextValue = altTextProperty is { } altText
            ? ReadExpression(altText)
            : ExpressionValue.Missing;
        var titleProperties = ReadTitleProperties(visual);
        var titleShow = titleProperties is { } properties &&
                        properties.TryGetProperty("show", out var show)
            ? ReadExpression(show)
            : ExpressionValue.Missing;
        var titleText = titleProperties is { } title &&
                        title.TryGetProperty("text", out var text)
            ? ReadExpression(text)
            : ExpressionValue.Missing;

        return new VisualAccessibilityInventory(
            HasAltText: altTextValue.IsDynamic || !string.IsNullOrWhiteSpace(altTextValue.Literal),
            AltText: altTextValue.Literal,
            AltTextIsDynamic: altTextValue.IsDynamic,
            TitleIsVisible: ParseBoolean(titleShow.Literal),
            HasConfiguredTitleText: titleText.IsDynamic || !string.IsNullOrWhiteSpace(titleText.Literal),
            TitleText: titleText.Literal,
            TitleTextIsDynamic: titleText.IsDynamic);
    }

    private static JsonElement? ReadTitleProperties(JsonElement visual)
    {
        if (!TryGetObject(visual, "visualContainerObjects", out var containerObjects) ||
            !containerObjects.TryGetProperty("title", out var title) ||
            title.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in title.EnumerateArray())
        {
            if (TryGetObject(item, "properties", out var properties))
            {
                return properties;
            }
        }

        return null;
    }

    private static JsonElement? FindProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out var directValue))
            {
                return directValue;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (FindProperty(property.Value, propertyName) is { } nestedValue)
                {
                    return nestedValue;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (FindProperty(item, propertyName) is { } nestedValue)
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static ExpressionValue ReadExpression(JsonElement property)
    {
        var literal = FindLiteralValue(property);
        if (literal is not null)
        {
            return new ExpressionValue(NormalizeLiteral(literal), IsDynamic: false, IsPresent: true);
        }

        return property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
               property.ValueKind == JsonValueKind.Object && !property.EnumerateObject().Any()
            ? new ExpressionValue(null, IsDynamic: false, IsPresent: true)
            : new ExpressionValue(null, IsDynamic: true, IsPresent: true);
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
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }

    private static bool? ParseBoolean(string? value)
    {
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private sealed record ExpressionValue(string? Literal, bool IsDynamic, bool IsPresent)
    {
        public static ExpressionValue Missing { get; } = new(null, IsDynamic: false, IsPresent: false);
    }
}
