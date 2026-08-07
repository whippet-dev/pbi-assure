namespace PbiAssure.Core.Inventory;

public sealed record PageBindingInventory(
    string? Name,
    string? Type,
    string? AcceptsFilterContext,
    IReadOnlyList<PageBindingParameterInventory> Parameters)
{
    public int ParameterCount => Parameters.Count;
}
