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

    public IReadOnlyList<SemanticUsageLocation> DirectReportLocations =>
        SemanticUsageLocation.Distinct(DirectReportReferences);

    public int DirectReportLocationCount => DirectReportLocations.Count;
}
