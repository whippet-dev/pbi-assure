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
/// construct keeps the conservative default. Fully accounted role files are also returned separately so
/// the file-level limitation detector can omit a limitation that does not apply to that encountered file.
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

        foreach (var perspective in semanticModels.SelectMany(model => model.Perspectives))
        {
            if (perspective.DependencyContentFullyAccountedFor)
            {
                refinements[perspective.RelativePath] = ConstructDependencyImpacts.NoKnownDependencyEffect;
            }
        }

        return refinements;
    }

    public static IReadOnlySet<string> BuildFullyAccountedRolePaths(
        IReadOnlyList<SemanticModelInventory> semanticModels) =>
        new HashSet<string>(
            semanticModels
                .SelectMany(model => model.Roles)
                .Where(role => role.DependencyContentFullyAccountedFor)
                .Select(role => role.RelativePath),
            StringComparer.OrdinalIgnoreCase);
}
