namespace PbiAssure.Core.Inventory;

public sealed record SemanticCalculationGroupInventory(
    int? Precedence,
    string? SelectionExpression,
    string? MultipleOrEmptySelectionExpression,
    IReadOnlyList<SemanticCalculationItemInventory> Items)
{
    public int ItemCount => Items.Count;
}
