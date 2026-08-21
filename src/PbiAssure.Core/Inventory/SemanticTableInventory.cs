namespace PbiAssure.Core.Inventory;

public sealed record SemanticTableInventory(
    string Name,
    string RelativePath,
    bool IsHidden,
    bool IsPrivate,
    bool IsSystemGenerated,
    string? SystemGeneratedKind,
    IReadOnlyList<SemanticColumnInventory> Columns,
    IReadOnlyList<SemanticMeasureInventory> Measures,
    IReadOnlyList<SemanticHierarchyInventory> Hierarchies,
    IReadOnlyList<SemanticPartitionInventory> Partitions,
    SemanticCalculationGroupInventory? CalculationGroup,
    SemanticFieldParameterInventory? FieldParameter)
{
    /// <summary>
    /// Explicit table-owned incremental-refresh policy metadata. The absence of this property means no
    /// policy block was found; RangeStart/RangeEnd query references alone never populate it.
    /// </summary>
    public SemanticRefreshPolicyInventory? RefreshPolicy { get; init; }

    public int ColumnCount => Columns.Count;

    public int MeasureCount => Measures.Count;

    public int HierarchyCount => Hierarchies.Count;

    public int PartitionCount => Partitions.Count;

    public bool IsCalculationGroup => CalculationGroup is not null;

    public bool IsFieldParameter => FieldParameter is not null;
}

public static class SystemGeneratedSemanticTableKinds
{
    public const string AutoDateTimeLocalTable = "AutoDateTimeLocalTable";

    public const string AutoDateTimeTemplateTable = "AutoDateTimeTemplateTable";
}
