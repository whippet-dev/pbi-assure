namespace PbiAssure.Core.Inventory;

/// <summary>
/// A perspective: a curated subset of the model that an author deliberately exposes to consumers.
///
/// Only the parts that name model objects are modelled. A perspective also carries presentation meaning
/// that PBI Assure does not interpret, so the presence of this type is not complete perspective support.
/// </summary>
public sealed record SemanticPerspectiveInventory(
    string Name,
    IReadOnlyList<SemanticPerspectiveTableInventory> Tables,
    string RelativePath)
{
    /// <summary>
    /// Constructs found inside this perspective that were neither analysed nor shown to be free of
    /// model-object references. Empty means the scanner accounted for everything the file contains.
    /// </summary>
    public IReadOnlyList<string> UnanalyzedConstructs { get; init; } = [];

    public bool DependencyContentFullyAccountedFor => UnanalyzedConstructs.Count == 0;

    public int TableCount => Tables.Count;
}

/// <summary>
/// A table included in a perspective, and the members of it that are exposed.
///
/// Membership is explicit per object unless <see cref="IncludeAll"/> is set: Microsoft documents that
/// otherwise each column, hierarchy and measure must be added individually. Treating a listed table as
/// exposing all of its fields would therefore be wrong, and would be a large false-positive risk.
/// </summary>
public sealed record SemanticPerspectiveTableInventory(
    string Table,
    bool IncludeAll,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Measures,
    IReadOnlyList<string> Hierarchies);
