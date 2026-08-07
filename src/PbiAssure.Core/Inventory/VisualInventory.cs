namespace PbiAssure.Core.Inventory;

public sealed record VisualInventory(
    string Name,
    string? VisualType,
    string RelativePath,
    string? SchemaUri,
    bool IsHidden,
    VisualPosition Position,
    VisualAccessibilityInventory Accessibility,
    IReadOnlyList<VisualFieldReference> FieldReferences,
    IReadOnlyList<VisualActionInventory> Actions)
{
    public bool IsInTabOrder => Position.TabOrder is not null;

    public int FieldReferenceCount => FieldReferences.Count;

    public int ActionCount => Actions.Count;

    public int DistinctFieldCount => FieldReferences
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
