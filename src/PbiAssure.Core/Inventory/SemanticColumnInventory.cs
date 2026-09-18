using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record SemanticColumnInventory(
    string Name,
    string? DataType,
    bool IsHidden,
    string? SourceColumn,
    string? SortByColumn,
    string? Expression)
{
    /// <summary>Desktop-authored description, retained in process only; logical lines use LF.</summary>
    [JsonIgnore]
    public string? Description { get; init; }

    /// <summary>
    /// Explicit aggregation mapping metadata owned by this column. A null value means no
    /// <c>alternateOf</c> block was present.
    /// </summary>
    public SemanticAggregationMappingInventory? AlternateOf { get; init; }

    /// <summary>
    /// The table named by this column's Desktop <c>variation</c> (its <c>defaultHierarchy</c> owner) —
    /// the mechanism that swaps a date column for a generated Auto Date/Time hierarchy in the field
    /// list. Retained in process only, to recognise a generated date table whose annotation was lost.
    /// </summary>
    [JsonIgnore]
    public string? VariationTargetTable { get; init; }

    public bool IsCalculated => Expression is not null;
}
