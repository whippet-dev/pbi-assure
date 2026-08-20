namespace PbiAssure.Core.Inventory;

public sealed record SemanticModelInventory(
    string Name,
    string RelativePath,
    IReadOnlyList<SemanticTableInventory> Tables,
    IReadOnlyList<SemanticRelationshipInventory> Relationships,
    IReadOnlyList<SemanticNamedExpressionInventory> NamedExpressions)
{
    /// <summary>
    /// Stored security roles. Additive: consumers that ignore this behave as they did before it
    /// existed. Only dependency-bearing parts of a role are modelled; see SemanticRoleInventory.
    /// </summary>
    public IReadOnlyList<SemanticRoleInventory> Roles { get; init; } = [];

    /// <summary>
    /// Perspectives. Additive; consumers that ignore this behave as they did before it existed.
    /// </summary>
    public IReadOnlyList<SemanticPerspectiveInventory> Perspectives { get; init; } = [];

    public int RoleCount => Roles.Count;

    public int PerspectiveCount => Perspectives.Count;

    /// <summary>DAX user-defined functions. Additive; ignoring it preserves previous behaviour.</summary>
    public IReadOnlyList<SemanticFunctionInventory> Functions { get; init; } = [];

    public int FunctionCount => Functions.Count;

    public int TablePermissionCount => Roles.Sum(role => role.TablePermissionCount);

    public int TableCount => Tables.Count;

    public int ColumnCount => Tables.Sum(table => table.ColumnCount);

    public int MeasureCount => Tables.Sum(table => table.MeasureCount);

    public int HierarchyCount => Tables.Sum(table => table.HierarchyCount);

    public int HierarchyLevelCount => Tables
        .SelectMany(table => table.Hierarchies)
        .Sum(hierarchy => hierarchy.Levels.Count);

    public int PartitionCount => Tables.Sum(table => table.PartitionCount);

    public int RelationshipCount => Relationships.Count;

    public int NamedExpressionCount => NamedExpressions.Count;

    public int CalculationGroupCount => Tables.Count(table => table.IsCalculationGroup);

    public int CalculationItemCount => Tables.Sum(table => table.CalculationGroup?.ItemCount ?? 0);

    public int FieldParameterCount => Tables.Count(table => table.IsFieldParameter);

    public int FieldParameterEntryCount => Tables.Sum(table => table.FieldParameter?.EntryCount ?? 0);
}
