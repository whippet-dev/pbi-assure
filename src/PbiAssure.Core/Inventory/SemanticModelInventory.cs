namespace PbiAssure.Core.Inventory;

public sealed record SemanticModelInventory(
    string Name,
    string RelativePath,
    IReadOnlyList<SemanticTableInventory> Tables,
    IReadOnlyList<SemanticRelationshipInventory> Relationships)
{
    public int TableCount => Tables.Count;

    public int ColumnCount => Tables.Sum(table => table.ColumnCount);

    public int MeasureCount => Tables.Sum(table => table.MeasureCount);

    public int HierarchyCount => Tables.Sum(table => table.HierarchyCount);

    public int HierarchyLevelCount => Tables
        .SelectMany(table => table.Hierarchies)
        .Sum(hierarchy => hierarchy.Levels.Count);

    public int PartitionCount => Tables.Sum(table => table.PartitionCount);

    public int RelationshipCount => Relationships.Count;
}
