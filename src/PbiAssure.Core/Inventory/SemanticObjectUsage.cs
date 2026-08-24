using System.Text.Json.Serialization;

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
    /// <summary>
    /// Whether metadata this scan did not analyse could bear on <see cref="UsageState"/>. Additive and
    /// orthogonal: the state is computed exactly as before, and consumers that ignore this field behave
    /// exactly as they did before it existed.
    /// </summary>
    public string ClassificationConfidence { get; init; } = ClassificationConfidences.Established;

    /// <summary>
    /// In-process provenance for a structurally required object. This deliberately does not alter the
    /// established five usage states or the public JSON contract.
    /// </summary>
    [JsonIgnore]
    public string? StructuralRequirementProvenance { get; init; }

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
