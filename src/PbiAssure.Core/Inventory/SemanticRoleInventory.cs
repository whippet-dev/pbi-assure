namespace PbiAssure.Core.Inventory;

/// <summary>
/// A row-level security role declared in the semantic model.
///
/// Only the parts that bear on semantic-object dependencies are modelled. A Power BI role carries more
/// than this — column permissions, and membership that lives in the Power BI service rather than in the
/// project — so the presence of this type must not be read as complete role support.
/// </summary>
public sealed record SemanticRoleInventory(
    string Name,
    string? ModelPermission,
    IReadOnlyList<SemanticTablePermissionInventory> TablePermissions,
    string RelativePath)
{
    /// <summary>
    /// Constructs found inside this role that this version neither analysed nor can show to be free of
    /// model-object references. Empty means the scanner accounted for everything the file actually
    /// contains — which is a statement about this artifact, not about roles in general.
    /// </summary>
    public IReadOnlyList<string> UnanalyzedConstructs { get; init; } = [];

    /// <summary>
    /// True when every construct present in this role file was either analysed or is known to carry no
    /// model-object reference. Absence of evidence is never treated as evidence of absence: an
    /// unrecognised construct leaves this false.
    /// </summary>
    public bool DependencyContentFullyAccountedFor => UnanalyzedConstructs.Count == 0;

    public int TablePermissionCount => TablePermissions.Count;
}

/// <summary>
/// A table permission within a role: the table it filters, and the DAX filter expression.
///
/// The table is load-bearing for reference resolution. Power BI Desktop serialises column references
/// inside the filter unqualified — <c>[Region]</c> rather than <c>Sales[Region]</c> — so the owning table
/// declared here is what makes those references resolvable.
/// </summary>
public sealed record SemanticTablePermissionInventory(
    string Table,
    string FilterExpression);
