namespace PbiAssure.Core.Inventory;

public sealed record VisualTooltipBindingInventory(
    string BindingKind,
    bool? IsEnabled,
    string? TargetPage,
    string? TooltipType,
    bool HasDynamicConfiguration,
    string EvidencePath);
