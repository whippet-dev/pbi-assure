using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Narrows a construct's declared dependency impact for a specific artifact.
///
/// The registry states what a construct type can contain, which must stay conservative because any role
/// might carry unsupported dependency-bearing content. An emitted limitation, though, describes metadata
/// actually encountered in this scan, so where the scanner has affirmative evidence that a particular
/// file holds nothing unanalysed that could reference a model object, the limitation may say so.
///
/// Refinements only ever narrow to <see cref="ConstructDependencyImpacts.NoKnownDependencyEffect"/>, and
/// only where coverage was positively established. Silence produces no refinement, so an unrecognised
/// construct keeps the conservative default.
/// </summary>
internal static class AnalysisCoverageRefinements
{
    public static IReadOnlyDictionary<string, string> Build(
        IReadOnlyList<SemanticModelInventory> semanticModels)
    {
        var refinements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in semanticModels.SelectMany(model => model.Roles))
        {
            if (role.DependencyContentFullyAccountedFor)
            {
                refinements[role.RelativePath] = ConstructDependencyImpacts.NoKnownDependencyEffect;
            }
        }

        return refinements;
    }
}
