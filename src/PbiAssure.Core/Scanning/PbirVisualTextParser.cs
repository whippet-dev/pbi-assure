using System.Text.Json;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualTextParser
{
    public static (string? Text, bool IsDynamic) Parse(JsonElement visual)
    {
        if (!TryGetObject(visual, "objects", out var objects) ||
            !objects.TryGetProperty("text", out var textObjects) ||
            textObjects.ValueKind != JsonValueKind.Array)
        {
            return (null, false);
        }

        var hasDynamicText = false;
        foreach (var textObject in textObjects.EnumerateArray())
        {
            if (!TryGetObject(textObject, "properties", out var properties) ||
                !properties.TryGetProperty("text", out var textProperty))
            {
                continue;
            }

            var value = PbirExpressionReader.Read(textProperty);
            hasDynamicText |= value.IsDynamic;
            if (!string.IsNullOrWhiteSpace(value.Literal))
            {
                return (value.Literal.Trim(), value.IsDynamic);
            }
        }

        return (null, hasDynamicText);
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
