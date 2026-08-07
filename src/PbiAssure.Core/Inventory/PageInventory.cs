namespace PbiAssure.Core.Inventory;

public sealed record PageInventory(
    string Name,
    string DisplayName,
    string RelativePath,
    string DefinitionPath,
    string? SchemaUri,
    string? PageType,
    PageBindingInventory? PageBinding,
    int? Order,
    bool IsActive,
    string? Visibility,
    string? DisplayOption,
    double? Width,
    double? Height,
    IReadOnlyList<ReportFilterInventory> Filters,
    IReadOnlyList<VisualFieldReference> FieldReferences,
    IReadOnlyList<VisualInventory> Visuals)
{
    public int VisualCount => Visuals.Count;

    public int FilterCount => Filters.Count;

    public int FieldReferenceCount => FieldReferences.Count + Visuals.Sum(visual => visual.FieldReferenceCount);

    public int DistinctFieldCount => Visuals
        .SelectMany(visual => visual.FieldReferences)
        .Concat(FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
