using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualTooltipParser
{
    private static readonly (string PropertyName, string BindingKind)[] BindingCollections =
    [
        ("visualTooltip", VisualTooltipBindingKinds.Visual),
        ("visualHeaderTooltip", VisualTooltipBindingKinds.VisualHeader),
    ];

    public static VisualTooltipBindingInventory[] Parse(JsonElement visual)
    {
        if (!TryGetObject(visual, "visualContainerObjects", out var containerObjects))
        {
            return [];
        }

        var bindings = new List<VisualTooltipBindingInventory>();
        foreach (var (propertyName, bindingKind) in BindingCollections)
        {
            if (!containerObjects.TryGetProperty(propertyName, out var collection) ||
                collection.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var index = 0;
            foreach (var item in collection.EnumerateArray())
            {
                if (TryGetObject(item, "properties", out var properties))
                {
                    var show = PbirExpressionReader.ReadProperty(properties, "show");
                    var section = PbirExpressionReader.ReadProperty(properties, "section");
                    var type = PbirExpressionReader.ReadProperty(properties, "type");
                    var isReportPageBinding = section.IsPresent ||
                                              type.IsDynamic ||
                                              string.Equals(type.Literal, "Canvas", StringComparison.OrdinalIgnoreCase);
                    if (!isReportPageBinding)
                    {
                        index++;
                        continue;
                    }

                    bindings.Add(new VisualTooltipBindingInventory(
                        BindingKind: bindingKind,
                        IsEnabled: show.IsPresent ? PbirExpressionReader.ParseBoolean(show.Literal) : true,
                        TargetPage: section.Literal,
                        HasExplicitTarget: section.IsPresent,
                        TooltipType: type.Literal,
                        HasDynamicConfiguration: show.IsDynamic || section.IsDynamic || type.IsDynamic,
                        EvidencePath: $"$.visual.visualContainerObjects.{propertyName}[{index}]"));
                }

                index++;
            }
        }

        return bindings.ToArray();
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
