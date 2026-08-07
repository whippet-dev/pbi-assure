namespace PbiAssure.Core.Inventory;

public sealed record VisualInventory(
    string Name,
    string? VisualType,
    string RelativePath,
    string? SchemaUri,
    bool IsHidden,
    VisualPosition Position,
    IReadOnlyList<VisualFieldReference> FieldReferences)
{
    public int FieldReferenceCount => FieldReferences.Count;

    public int DistinctFieldCount => FieldReferences
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
