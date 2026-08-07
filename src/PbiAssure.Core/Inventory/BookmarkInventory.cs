namespace PbiAssure.Core.Inventory;

public sealed record BookmarkInventory(
    string Name,
    string DisplayName,
    string RelativePath,
    string? SchemaUri,
    string? ActivePageName,
    bool? ApplyOnlyToTargetVisuals,
    IReadOnlyList<string> TargetVisualNames,
    bool? SuppressActivePage,
    bool? SuppressData)
{
    public int TargetVisualCount => TargetVisualNames.Count;
}
