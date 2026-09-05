namespace PbiAssure.Core.Inventory;

public sealed record ReportInventory(
    string Name,
    string RelativePath,
    ReportModelConnectionInventory ModelConnection,
    string? DefinitionPath,
    string? SchemaUri,
    string? PagesSchemaUri,
    string? ActivePageName,
    string? LandingPageName,
    IReadOnlyList<PageInventory> Pages,
    IReadOnlyList<ReportFilterInventory> Filters,
    IReadOnlyList<VisualFieldReference> FieldReferences,
    string? ReportExtensionsPath,
    string? ReportExtensionsSchemaUri,
    IReadOnlyList<ReportMeasureInventory> ReportMeasures,
    string? BookmarksSchemaUri,
    IReadOnlyList<string> BookmarkOrder,
    IReadOnlyList<BookmarkInventory> Bookmarks)
{
    // Parser-only evidence is converted to the existing AnalysisLimitations contract.
    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<UnresolvedReportAlias> UnresolvedAliases { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<UnreadReportPage> UnreadPages { get; init; } = [];

    /// <summary>
    /// Report-side PBIR schema declarations observed while parsing this report. Exact fixture-backed
    /// baselines are retained for technical inventory but remain silent in normal coverage presentation.
    /// </summary>
    public IReadOnlyList<ReportSchemaObservation> SchemaObservations { get; init; } = [];

    public string? VersionMetadataPath { get; init; }

    /// <summary>The PBIR definition version from definition/version.json, distinct from its schema URI.</summary>
    public string? PbirDefinitionVersion { get; init; }

    public ThemeInventory Theme { get; init; } = ThemeInventory.Unavailable;

    public ThemeReviewInventory ThemeReview { get; init; } = ThemeReviewInventory.Unavailable;

    public int PageCount => Pages.Count;

    public int VisualCount => Pages.Sum(page => page.VisualCount);

    public int ActionCount => Pages.Sum(page => page.Visuals.Sum(visual => visual.ActionCount));

    public int BookmarkCount => Bookmarks.Count;

    public int ReportMeasureCount => ReportMeasures.Count;

    public int FilterCount => Filters.Count + Pages.Sum(page => page.FilterCount);

    public int VisualInteractionCount => Pages.Sum(page => page.VisualInteractionCount);

    public int TooltipBindingCount => Pages.Sum(page => page.Visuals.Sum(visual => visual.TooltipBindingCount));

    public int FieldReferenceCount => FieldReferences.Count + Pages.Sum(page => page.FieldReferenceCount);

    public int DistinctFieldCount => Pages
        .SelectMany(page => page.FieldReferences.Concat(page.Visuals.SelectMany(visual => visual.FieldReferences)))
        .Concat(FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}

internal sealed record UnresolvedReportAlias(string ArtifactPath, string EvidencePath, string Alias);

internal sealed record UnreadReportPage(string DefinitionPath, string Reason);
