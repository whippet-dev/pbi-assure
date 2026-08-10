namespace PbiAssure.Core.Inventory;

public sealed record SemanticTablePowerQueryContext(
    string SemanticModel,
    string Table,
    string QueryName,
    string? Partition,
    string QueryRole,
    bool HasDynamicReferences,
    IReadOnlyList<string> UsedByQueries)
{
    public bool IsRequiredUpstream => UsedByQueries.Count > 0;
}
