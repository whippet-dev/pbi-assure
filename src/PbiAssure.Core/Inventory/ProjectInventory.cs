namespace PbiAssure.Core.Inventory;

public sealed record ProjectInventory(
    string SchemaVersion,
    string RootPath,
    DateTimeOffset ScannedAtUtc,
    IReadOnlyList<ArtifactInventory> Artifacts,
    IReadOnlyList<ReportInventory> Reports,
    IReadOnlyList<SemanticModelInventory> SemanticModels,
    IReadOnlyList<SemanticObjectUsage> SemanticObjectUsages,
    IReadOnlyList<SemanticTableUsage> SemanticTableUsages,
    IReadOnlyList<SemanticDependencyEdge> SemanticDependencies,
    IReadOnlyList<UnresolvedSemanticReference> UnresolvedSemanticReferences,
    IReadOnlyList<UnresolvedSemanticDependency> UnresolvedSemanticDependencies)
{
    public int ReportCount => Artifacts.Count(artifact => artifact.Kind == ArtifactKinds.Report);

    public int SemanticModelCount => Artifacts.Count(artifact => artifact.Kind == ArtifactKinds.SemanticModel);

    public int PageCount => Reports.Sum(report => report.PageCount);

    public int VisualCount => Reports.Sum(report => report.VisualCount);

    public int FieldReferenceCount => Reports.Sum(report => report.FieldReferenceCount);

    public int DistinctFieldCount => Reports
        .SelectMany(report => report.Pages)
        .SelectMany(page => page.Visuals)
        .SelectMany(visual => visual.FieldReferences)
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int SemanticTableCount => SemanticModels.Sum(model => model.TableCount);

    public int SemanticColumnCount => SemanticModels.Sum(model => model.ColumnCount);

    public int SemanticMeasureCount => SemanticModels.Sum(model => model.MeasureCount);

    public int SemanticRelationshipCount => SemanticModels.Sum(model => model.RelationshipCount);

    public int DirectlyReferencedSemanticObjectCount => SemanticObjectUsages
        .Count(usage => usage.IsDirectlyReferencedByReport);

    public int NotDirectlyReferencedSemanticObjectCount => SemanticObjectUsages
        .Count(usage => !usage.IsDirectlyReferencedByReport);

    public int DirectlyReferencedTableCount => SemanticObjectUsages
        .Where(usage => usage.IsDirectlyReferencedByReport)
        .Select(usage => string.Join('\u001f', usage.SemanticModel, usage.Table))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int NotDirectlyReferencedTableCount => SemanticTableCount - DirectlyReferencedTableCount;

    public int ApparentlyUnusedSemanticObjectCount => SemanticObjectUsages
        .Count(usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);

    public int ApparentlyUnusedTableCount => SemanticTableUsages
        .Count(usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);
}
