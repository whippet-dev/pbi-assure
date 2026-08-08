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
    IReadOnlyList<PowerQueryUsage> PowerQueryUsages,
    IReadOnlyList<PowerQueryDependencyEdge> PowerQueryDependencies,
    IReadOnlyList<UnresolvedSemanticReference> UnresolvedSemanticReferences,
    IReadOnlyList<UnresolvedSemanticDependency> UnresolvedSemanticDependencies,
    IReadOnlyList<AssuranceFinding> Findings)
{
    public int ReportCount => Artifacts.Count(artifact => artifact.Kind == ArtifactKinds.Report);

    public int SemanticModelCount => Artifacts.Count(artifact => artifact.Kind == ArtifactKinds.SemanticModel);

    public int PageCount => Reports.Sum(report => report.PageCount);

    public int VisualCount => Reports.Sum(report => report.VisualCount);

    public int ActionCount => Reports.Sum(report => report.ActionCount);

    public int BookmarkCount => Reports.Sum(report => report.BookmarkCount);

    public int FilterCount => Reports.Sum(report => report.FilterCount);

    public int VisualInteractionCount => Reports.Sum(report => report.VisualInteractionCount);

    public int TooltipBindingCount => Reports.Sum(report => report.TooltipBindingCount);

    public int FieldReferenceCount => Reports.Sum(report => report.FieldReferenceCount);

    public int DistinctFieldCount => Reports
        .SelectMany(report => report.FieldReferences.Concat(
            report.Pages.SelectMany(page => page.FieldReferences.Concat(
                page.Visuals.SelectMany(visual => visual.FieldReferences)))))
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int SemanticTableCount => SemanticModels.Sum(model => model.TableCount);

    public int SemanticColumnCount => SemanticModels.Sum(model => model.ColumnCount);

    public int SemanticMeasureCount => SemanticModels.Sum(model => model.MeasureCount);

    public int ReportMeasureCount => Reports.Sum(report => report.ReportMeasureCount);

    public int SemanticRelationshipCount => SemanticModels.Sum(model => model.RelationshipCount);

    public int PowerQueryCount => PowerQueryUsages.Count;

    public int ApparentlyUnusedPowerQueryCount => PowerQueryUsages.Count(usage =>
        usage.UsageState == PowerQueryUsageStates.ApparentlyUnused);

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

    public int FindingCount => Findings.Count;

    public int ErrorFindingCount => Findings.Count(finding => finding.Severity == FindingSeverities.Error);

    public int WarningFindingCount => Findings.Count(finding => finding.Severity == FindingSeverities.Warning);

    public int InformationFindingCount => Findings.Count(finding => finding.Severity == FindingSeverities.Information);

    public int ReviewRequiredCount => Findings.Count(finding => finding.AssessmentType == AssessmentTypes.ReviewRequired);
}
