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
    string ToColumn);
