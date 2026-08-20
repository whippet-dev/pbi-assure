namespace PbiAssure.Core.Inventory;

/// <summary>
/// The resolver's outcome when it retains an unresolved semantic dependency.
/// This is structured evidence; <see cref="UnresolvedSemanticDependency.Reason"/> remains explanatory text.
/// </summary>
public static class UnresolvedSemanticDependencyResolutionOutcomes
{
    public const string NotFound = "NotFound";

    public const string Ambiguous = "Ambiguous";
}
