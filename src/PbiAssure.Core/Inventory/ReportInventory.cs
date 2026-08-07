namespace PbiAssure.Core.Inventory;

public sealed record ReportInventory(
    string Name,
    string RelativePath,
    string? PagesSchemaUri,
    string? ActivePageName,
    IReadOnlyList<PageInventory> Pages)
{
    public int PageCount => Pages.Count;

    public int VisualCount => Pages.Sum(page => page.VisualCount);

    public int FieldReferenceCount => Pages.Sum(page => page.FieldReferenceCount);

    public int DistinctFieldCount => Pages
        .SelectMany(page => page.Visuals)
        .SelectMany(visual => visual.FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
