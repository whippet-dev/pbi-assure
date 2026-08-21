namespace PbiAssure.Core.Inventory;

/// <summary>
/// Explicit aggregation mapping metadata persisted on an aggregation-side TMDL column.
/// <see cref="BaseColumnReference"/> retains the authored qualified reference exactly as it was read;
/// it is resolved separately against model columns before it can affect semantic usage.
/// </summary>
public sealed record SemanticAggregationMappingInventory(
    string? BaseColumnReference,
    string? Summarization);
