namespace PbiAssure.Core.Inventory;

public sealed record SemanticObjectUsage(
    string SemanticModel,
    string Table,
    string ObjectName,
    string ObjectType,
    string? HierarchyName,
    IReadOnlyList<SemanticUsageEvidence> DirectReportReferences,
    string UsageState)
{
    public bool IsDirectlyReferencedByReport => DirectReportReferences.Count > 0;

    public int DirectReportReferenceCount => DirectReportReferences.Count;

    public IReadOnlyList<SemanticUsageLocation> DirectReportLocations
    {
        get
        {
            var locations = DirectReportReferences.Select(SemanticUsageLocation.FromEvidence).Distinct().ToArray();
            var drillthroughPages = locations
                .Where(location => location.Visual is null && location.UsageContext == UsageContexts.Drillthrough)
                .Select(location => $"{location.Report}\u001f{location.Page}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return locations.Where(location =>
                location.Visual is not null ||
                location.UsageContext != UsageContexts.Filter ||
                !drillthroughPages.Contains($"{location.Report}\u001f{location.Page}"))
                .ToArray();
        }
    }

    public int DirectReportLocationCount => DirectReportLocations.Count;
}
