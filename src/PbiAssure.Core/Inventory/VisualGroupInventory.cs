namespace PbiAssure.Core.Inventory;

public sealed record VisualGroupInventory(
    string Name,
    string? DisplayName,
    string? GroupMode,
    string? ParentGroupName,
    string RelativePath,
    string? SchemaUri,
    VisualPosition Position)
{
    public bool HasExplicitTabOrder => Position.TabOrder is >= 0;
}
