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
                var show = PbirExpressionReader.ReadProperty(properties, "show");
                var type = PbirExpressionReader.ReadProperty(properties, "type");
                var bookmark = PbirExpressionReader.ReadProperty(properties, "bookmark");
                var navigationSection = PbirExpressionReader.ReadProperty(properties, "navigationSection");
                var destination = PbirExpressionReader.ReadProperty(properties, "destination");
                var webUrl = PbirExpressionReader.ReadProperty(properties, "webUrl");

                actions.Add(new VisualActionInventory(
                    IsEnabled: show.IsPresent ? PbirExpressionReader.ParseBoolean(show.Literal) : true,
                    ActionType: type.Literal,
                    BookmarkTarget: bookmark.Literal,
                    PageTarget: navigationSection.Literal ?? destination.Literal,
                    WebUrl: webUrl.Literal,
                    HasDynamicConfiguration: show.IsDynamic ||
                                             type.IsDynamic ||
                                             bookmark.IsDynamic ||
                                             navigationSection.IsDynamic ||
                                             destination.IsDynamic ||
                                             webUrl.IsDynamic,
                    EvidencePath: $"$.visual.visualContainerObjects.visualLink[{index}]"));
            }

            index++;
        }

        return actions.ToArray();
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
