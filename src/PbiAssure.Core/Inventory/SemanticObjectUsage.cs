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
}
