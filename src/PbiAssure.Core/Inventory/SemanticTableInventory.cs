namespace PbiAssure.Core.Inventory;

public sealed record SemanticTableInventory(
    string Name,
    string RelativePath,
    bool IsHidden,
    bool IsPrivate,
    IReadOnlyList<SemanticColumnInventory> Columns,
    IReadOnlyList<SemanticMeasureInventory> Measures,
    IReadOnlyList<SemanticHierarchyInventory> Hierarchies,
    IReadOnlyList<SemanticPartitionInventory> Partitions)
{
    public int ColumnCount => Columns.Count;

    public int MeasureCount => Measures.Count;

    public int HierarchyCount => Hierarchies.Count;

    public int PartitionCount => Partitions.Count;
}
