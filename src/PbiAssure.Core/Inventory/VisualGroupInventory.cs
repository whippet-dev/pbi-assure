using System.Text.Json.Serialization;

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
    [JsonIgnore]
    public bool IsHidden { get; init; }

    public bool HasExplicitTabOrder => Position.TabOrder is >= 0;
}
