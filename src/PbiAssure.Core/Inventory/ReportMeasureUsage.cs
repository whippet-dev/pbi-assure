namespace PbiAssure.Core.Inventory;

/// <summary>
/// Where one report measure is used directly in its own report.
///
/// Report measures belong to a report. <see cref="ReportPath"/>, with the model, entity and name, is the
/// report measure's node in the dependency graph, so same-named report measures in two reports bound to
/// one model are separate nodes. Its evidence is the report's own, never another report's. A report
/// measure with no evidence is still listed, because it exists whether or not anything places it on the
/// report.
/// </summary>
public sealed record ReportMeasureUsage(
    string Report,
    string ReportPath,
    string SemanticModel,
    string Entity,
    string Name,
    IReadOnlyList<SemanticUsageEvidence> DirectReportReferences)
{
    public bool IsDirectlyReferencedByReport => DirectReportReferences.Count > 0;

    public IReadOnlyList<SemanticUsageLocation> DirectReportLocations =>
        SemanticUsageLocation.Distinct(DirectReportReferences);
}
