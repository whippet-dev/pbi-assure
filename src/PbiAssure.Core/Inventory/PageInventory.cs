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
    IReadOnlyList<VisualInteractionInventory> VisualInteractions,
    IReadOnlyList<VisualGroupInventory> VisualGroups,
    IReadOnlyList<VisualInventory> Visuals)
{
    public int VisualCount => Visuals.Count;

    public int VisualGroupCount => VisualGroups.Count;

    public int FilterCount => Filters.Count;

    public int VisualInteractionCount => VisualInteractions.Count;

    public int FieldReferenceCount => FieldReferences.Count + Visuals.Sum(visual => visual.FieldReferenceCount);

    public int DistinctFieldCount => Visuals
        .SelectMany(visual => visual.FieldReferences)
        .Concat(FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public bool ContainsContainer(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (Visuals.Any(visual => string.Equals(visual.Name, name, StringComparison.Ordinal)) ||
         VisualGroups.Any(group => string.Equals(group.Name, name, StringComparison.Ordinal)));
}
