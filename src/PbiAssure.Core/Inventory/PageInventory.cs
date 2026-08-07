namespace PbiAssure.Core.Inventory;

public sealed record PageInventory(
    string Name,
    string DisplayName,
    string RelativePath,
    string? SchemaUri,
    int? Order,
    bool IsActive,
    string? Visibility,
    string? DisplayOption,
    double? Width,
    double? Height,
    IReadOnlyList<VisualInventory> Visuals)
{
    public int VisualCount => Visuals.Count;

    public int FieldReferenceCount => Visuals.Sum(visual => visual.FieldReferenceCount);

    public int DistinctFieldCount => Visuals
        .SelectMany(visual => visual.FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
