namespace PbiAssure.Core.Inventory;

public sealed record VisualTooltipBindingInventory(
    string BindingKind,
    bool? IsEnabled,
    string? TargetPage,
    bool HasExplicitTarget,
    string? TooltipType,
    bool HasDynamicConfiguration,
    string EvidencePath);
