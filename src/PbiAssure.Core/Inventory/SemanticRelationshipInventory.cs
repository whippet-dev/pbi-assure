namespace PbiAssure.Core.Inventory;

public sealed record SemanticRelationshipInventory(
    string Name,
    bool IsActive,
    string CrossFilteringBehavior,
    string FromCardinality,
    string FromTable,
    string FromColumn,
    string ToCardinality,
    string ToTable,
    string ToColumn)
{
    /// <summary>
    /// Additive static activation evidence for inactive relationships. Active relationships deliberately
    /// have no review annotation.
    /// </summary>
    public SemanticRelationshipActivationInventory? Activation { get; init; }
}
