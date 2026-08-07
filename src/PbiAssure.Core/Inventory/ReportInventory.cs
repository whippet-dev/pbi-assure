namespace PbiAssure.Core.Inventory;

public sealed record ReportInventory(
    string Name,
    string RelativePath,
    string? PagesSchemaUri,
    string? ActivePageName,
    IReadOnlyList<PageInventory> Pages,
    string? BookmarksSchemaUri,
    IReadOnlyList<string> BookmarkOrder,
    IReadOnlyList<BookmarkInventory> Bookmarks)
{
    public int PageCount => Pages.Count;

    public int VisualCount => Pages.Sum(page => page.VisualCount);

    public int ActionCount => Pages.Sum(page => page.Visuals.Sum(visual => visual.ActionCount));

    public int BookmarkCount => Bookmarks.Count;

    public int FieldReferenceCount => Pages.Sum(page => page.FieldReferenceCount);

    public int DistinctFieldCount => Pages
        .SelectMany(page => page.Visuals)
        .SelectMany(visual => visual.FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
