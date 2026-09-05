using System.Text.Json.Serialization;

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
    /// <summary>Desktop-authored description, retained in process only; logical lines use LF.</summary>
    [JsonIgnore]
    public string? Description { get; init; }

    /// <summary>
    /// Table-level constructs found in this file that are known to carry model-object references and
    /// that this version does not analyse. Empty means nothing of that kind was seen — which is a
    /// statement about the constructs this version can recognise, not a proof that the file holds
    /// nothing else.
    ///
    /// Deliberately narrower than the role and perspective equivalents. Those deny by default, listing
    /// the children known to be reference-free and treating anything else as unaccounted. A table's
    /// property surface is large and mostly reference-free, so denying by default there would qualify
    /// almost every model and make the signal worthless. This lists only constructs positively
    /// identified as dependency-bearing and unparsed.
    ///
    /// In process only: the role and perspective equivalents are serialized, but adding a field to the
    /// table contract would change JSON schema 0.26.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> UnanalyzedDependencyConstructs { get; init; } = [];

    /// <summary>
    /// True when no table-level construct known to carry references was left unanalysed. Absence of
    /// evidence is not evidence of absence: this only reports on constructs this version recognises.
    /// </summary>
    [JsonIgnore]
    public bool DependencyContentFullyAccountedFor => UnanalyzedDependencyConstructs.Count == 0;

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
