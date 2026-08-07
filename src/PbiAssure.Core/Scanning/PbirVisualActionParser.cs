using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualActionParser
{
    public static VisualActionInventory[] Parse(JsonElement visual)
    {
        if (!TryGetObject(visual, "visualContainerObjects", out var containerObjects) ||
            !containerObjects.TryGetProperty("visualLink", out var visualLinks) ||
            visualLinks.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var actions = new List<VisualActionInventory>();
        var index = 0;
        foreach (var visualLink in visualLinks.EnumerateArray())
        {
            if (TryGetObject(visualLink, "properties", out var properties))
            {
                var show = ReadProperty(properties, "show");
                var type = ReadProperty(properties, "type");
                var bookmark = ReadProperty(properties, "bookmark");
                var destination = ReadProperty(properties, "destination");
                var webUrl = ReadProperty(properties, "webUrl");

                actions.Add(new VisualActionInventory(
                    IsEnabled: show.IsPresent ? ParseBoolean(show.Literal) : true,
                    ActionType: type.Literal,
                    BookmarkTarget: bookmark.Literal,
                    PageTarget: destination.Literal,
                    WebUrl: webUrl.Literal,
                    HasDynamicConfiguration: show.IsDynamic ||
                                             type.IsDynamic ||
                                             bookmark.IsDynamic ||
                                             destination.IsDynamic ||
                                             webUrl.IsDynamic,
                    EvidencePath: $"$.visual.visualContainerObjects.visualLink[{index}]"));
            }

            index++;
        }

        return actions.ToArray();
    }

    private static ExpressionValue ReadProperty(JsonElement properties, string propertyName)
    {
        return properties.TryGetProperty(propertyName, out var property)
            ? ReadExpression(property)
            : ExpressionValue.Missing;
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

            foreach (var child in element.EnumerateObject())
            {
                if (FindLiteralValue(child.Value) is { } nestedValue)
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
