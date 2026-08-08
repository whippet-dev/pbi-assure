namespace PbiAssure.Core.Inventory;

public sealed record SemanticFieldParameterInventory(
    string Name,
    string Expression,
    IReadOnlyList<SemanticFieldParameterEntryInventory> Entries)
{
    public int EntryCount => Entries.Count;
}
