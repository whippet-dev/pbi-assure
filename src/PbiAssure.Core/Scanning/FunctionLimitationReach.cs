using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Bounds what the DAX user-defined function limitation can bear on.
///
/// PBI-LIMIT-MODEL-FUNCTION records that a function may be called from somewhere the scan does not
/// read — a visual calculation, or anything outside the project. Such a call makes the function live,
/// and with it whatever the function's body references, transitively, through other functions and
/// measures. It can make nothing else live: an object the function's body does not reach cannot become
/// used through a call nobody saw. The limitation therefore keeps its impact and is bounded to that
/// closure, computed over the same dependency edges the classifier traverses. For a body that
/// references nothing, the closure is empty and no semantic object is qualified while the limitation
/// itself stays in Analysis coverage; for a body that references Sales[Amount], Amount and everything
/// reachable from it stay qualified.
///
/// The reach is left unbounded when a function body left a reference unresolved, because the closure
/// of a reference that could not be bound is not known. No function is treated as dependency-free
/// merely because its edges are absent when its references are.
/// </summary>
internal static class FunctionLimitationReach
{
    private const string LimitationId = "PBI-LIMIT-MODEL-FUNCTION";

    public static AnalysisLimitation[] Apply(
        IReadOnlyList<AnalysisLimitation> limitations,
        SemanticDependencyAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(limitations);
        ArgumentNullException.ThrowIfNull(analysis);

        return limitations
            .Select(limitation => limitation.LimitationId == LimitationId && limitation.SemanticModel is not null
                ? Bound(limitation, analysis)
                : limitation)
            .ToArray();
    }

    private static AnalysisLimitation Bound(AnalysisLimitation limitation, SemanticDependencyAnalysis analysis)
    {
        var modelName = limitation.SemanticModel!;
        var model = analysis.SemanticModels.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, modelName, StringComparison.OrdinalIgnoreCase));
        if (model is null)
        {
            return limitation;
        }

        // A reference the scan could not bind has an unknown closure; the conservative reading stands.
        if (analysis.UnresolvedDependencies.Any(dependency =>
                string.Equals(dependency.SemanticModel, modelName, StringComparison.OrdinalIgnoreCase) &&
                dependency.FromObjectType == SemanticObjectTypes.Function))
        {
            return limitation;
        }

        var reach = SemanticDependencyReach.Closure(
            analysis.Dependencies,
            modelName,
            model.Functions.Select(function =>
                FieldIdentity.Create(string.Empty, function.Name, SemanticObjectTypes.Function)));

        return limitation with { Reach = reach };
    }
}
