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

    /// <summary>
    /// Tables whose file holds a construct known to reference model objects that this version does not
    /// parse. The registry classifies the tables directory as fully analysed, which is true of every
    /// construct the parser reads — so where the parser positively recognised one it does not read, that
    /// specific file needs a limitation the registry cannot supply.
    /// </summary>
    public static IReadOnlyList<UnanalyzedTableConstructs> BuildUnanalyzedTableConstructs(
        IReadOnlyList<SemanticModelInventory> semanticModels) =>
        semanticModels
            .SelectMany(model => model.Tables.Select(table => (model, table)))
            .Where(pair => !pair.table.DependencyContentFullyAccountedFor)
            .Select(pair => new UnanalyzedTableConstructs(
                pair.model.Name,
                pair.table.Name,
                pair.table.RelativePath,
                pair.table.UnanalyzedDependencyConstructs))
            .ToArray();

    public static IReadOnlySet<string> BuildFullyAccountedRolePaths(
        IReadOnlyList<SemanticModelInventory> semanticModels) =>
        new HashSet<string>(
            semanticModels
                .SelectMany(model => model.Roles)
                .Where(role => role.DependencyContentFullyAccountedFor)
                .Select(role => role.RelativePath),
            StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One table file holding constructs that reference model objects and are not parsed by this version.
/// </summary>
internal sealed record UnanalyzedTableConstructs(
    string SemanticModel,
    string Table,
    string RelativePath,
    IReadOnlyList<string> Constructs);
