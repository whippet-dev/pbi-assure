namespace PbiAssure.Core.Inventory;

public sealed record VisualActionInventory(
    bool? IsEnabled,
    string? ActionType,
    string? BookmarkTarget,
    string? PageTarget,
    string? WebUrl,
    bool HasDynamicConfiguration,
    string EvidencePath);
