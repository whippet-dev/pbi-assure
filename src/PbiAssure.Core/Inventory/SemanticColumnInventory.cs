namespace PbiAssure.Core.Inventory;

public sealed record SemanticColumnInventory(
    string Name,
    string? DataType,
    bool IsHidden,
    string? SourceColumn,
    string? SortByColumn,
    string? Expression)
{
    public bool IsCalculated => Expression is not null;
}
