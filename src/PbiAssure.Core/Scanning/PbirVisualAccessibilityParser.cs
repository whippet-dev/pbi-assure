using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualAccessibilityParser
{
    public static VisualAccessibilityInventory Parse(JsonElement visual)
    {
        var altTextProperty = FindProperty(visual, "altText");
        var altTextValue = altTextProperty is { } altText
            ? PbirExpressionReader.Read(altText)
            : PbirExpressionValue.Missing;
        var titleProperties = ReadTitleProperties(visual);
        var titleShow = titleProperties is { } properties &&
                        properties.TryGetProperty("show", out var show)
            ? PbirExpressionReader.Read(show)
            : PbirExpressionValue.Missing;
        var titleText = titleProperties is { } title &&
                        title.TryGetProperty("text", out var text)
            ? PbirExpressionReader.Read(text)
            : PbirExpressionValue.Missing;

        return new VisualAccessibilityInventory(
            HasAltText: altTextValue.IsDynamic || !string.IsNullOrWhiteSpace(altTextValue.Literal),
            AltText: altTextValue.Literal,
            AltTextIsDynamic: altTextValue.IsDynamic,
            TitleIsVisible: PbirExpressionReader.ParseBoolean(titleShow.Literal),
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
}
