using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

/// <summary>
/// Turns the analysis limitations a scan recorded into something a Power BI developer can read.
///
/// This layer translates and groups. It does not decide anything: whether a particular object's
/// classification is qualified is already answered by <see cref="SemanticObjectUsage.ClassificationConfidence"/>,
/// and the counts here are taken from that field rather than recomputed. The rule that decides it lives
/// in the scanner and must stay there, so the registry remains the single place a construct's effect is
/// declared.
///
/// Grouping matters as much as wording. One unanalysed construct can qualify most of a model, and a
/// model can emit one file per role, so limitations are grouped by construct rather than listed per
/// file. The explanation is then written once at model scope instead of beside every affected object.
/// </summary>
internal static class AnalysisCoveragePresentation
{
    public static AnalysisCoverage Build(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        if (inventory.AnalysisLimitations.Count == 0)
        {
            return new AnalysisCoverage([]);
        }

        var qualifiedByModel = inventory.SemanticObjectUsages
            .Where(usage => usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation)
            .GroupBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var objectsByModel = inventory.SemanticObjectUsages
            .GroupBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var models = inventory.AnalysisLimitations
            .GroupBy(limitation => limitation.SemanticModel ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select((group, index) => BuildModel(group.Key, index + 1, group, qualifiedByModel, objectsByModel))
            .ToArray();

        return new AnalysisCoverage(models);
    }

    private static AnalysisCoverageModel BuildModel(
        string modelName,
        int anchorOrdinal,
        IEnumerable<AnalysisLimitation> limitations,
        IReadOnlyDictionary<string, int> qualifiedByModel,
        IReadOnlyDictionary<string, int> objectsByModel)
    {
        var groups = limitations
            // Construct, support state, impact and reason together, so every artifact in a group is
            // genuinely described by the one explanation the group displays.
            .GroupBy(limitation => (
                limitation.ConstructType,
                limitation.SupportState,
                limitation.DependencyImpact,
                limitation.Reason))
            .Select(BuildGroup)
            .OrderBy(group => group.MayAffectClassification ? 0 : 1)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AnalysisCoverageModel(
            ModelName: modelName,
            AnchorId: string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"analysis-coverage-model-{anchorOrdinal}"),
            ObjectCount: objectsByModel.GetValueOrDefault(modelName),
            QualifiedObjectCount: qualifiedByModel.GetValueOrDefault(modelName),
            QualifyingGroups: groups.Where(group => group.MayAffectClassification).ToArray(),
            OtherGroups: groups.Where(group => !group.MayAffectClassification).ToArray());
    }

    private static AnalysisCoverageGroup BuildGroup(
        IGrouping<(string ConstructType, string SupportState, string DependencyImpact, string Reason), AnalysisLimitation> group)
    {
        var impact = DescribeImpact(group.Key.DependencyImpact);

        return new AnalysisCoverageGroup(
            ConstructType: group.Key.ConstructType,
            Label: ConstructLabel(group.Key.ConstructType),
            SupportStateLabel: SupportStateLabel(group.Key.SupportState),
            ImpactLabel: impact.Label,
            MayAffectClassification: impact.MayAffectClassification,
            Reason: group.Key.Reason,
            ArtifactPaths: group
                .Select(limitation => limitation.ArtifactPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    /// <summary>
    /// What unchecked metadata means for the results in this report, said as a consequence rather than
    /// as a taxonomy. The internal names describe the construct ("may create dependencies"); a reader
    /// needs to know what it does to their answers ("could hide extra usage").
    ///
    /// Note that "does not change any used/unused result" is not the same as "fully checked". The
    /// construct is still only partly read; what is established is that the unread part cannot add
    /// usage. The support state beside it carries the other half of that distinction.
    ///
    /// <see cref="MayAffectClassification"/> drives ordering and the model headline only. It never
    /// decides whether an object is marked — that comes from the scanner.
    /// </summary>
    private static (string Label, bool MayAffectClassification) DescribeImpact(string dependencyImpact) =>
        dependencyImpact switch
        {
            ConstructDependencyImpacts.MayCreateDependencies =>
                ("Could hide extra usage", true),
            ConstructDependencyImpacts.DependencyEffectUnknown =>
                ("Not known whether it hides extra usage", true),
            ConstructDependencyImpacts.MayInvalidateExistingEvidence =>
                ("Could change how other results should be read", true),
            ConstructDependencyImpacts.NoKnownDependencyEffect =>
                ("Does not change any used or unused result", false),
            // An impact this version of the report does not recognise is described plainly rather than
            // assumed harmless or alarming.
            _ => ("Not known whether it hides extra usage", true),
        };

    private static string SupportStateLabel(string supportState) => supportState switch
    {
        ConstructSupportStates.Analyzed => "Checked",
        ConstructSupportStates.PartiallyAnalyzed => "Partially checked",
        ConstructSupportStates.NotYetAnalyzed => "Not checked yet",
        ConstructSupportStates.Unrecognized => "Not recognised",
        _ => HumanReadable(supportState),
    };

    /// <summary>
    /// Names the construct the way Power BI documentation does, for the few whose generic name would be
    /// ambiguous — "function" alone could mean a Power Query function or a built-in DAX function.
    /// Anything else falls back to the generic formatter, so a construct added later still reads
    /// sensibly without an entry here.
    /// </summary>
    private static string ConstructLabel(string constructType) => constructType switch
    {
        "function" => "DAX user-defined functions",
        "role" => "Row-level security roles",
        "perspective" => "Perspectives",
        "culture" => "Cultures and translations",
        "modelDefinition" => "Model-level settings",
        "database" => "Database settings",
        "dataSource" => "Data source definitions",
        _ => HumanReadable(constructType),
    };

    private static string HumanReadable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var words = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(value[index - 1]))
            {
                words.Append(' ');
                words.Append(char.ToLowerInvariant(character));
                continue;
            }

            words.Append(words.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return words.ToString();
    }
}

/// <summary>Analysis coverage for a whole scan. Empty when nothing was left unanalysed.</summary>
internal sealed record AnalysisCoverage(IReadOnlyList<AnalysisCoverageModel> Models)
{
    public bool HasLimitations => Models.Count > 0;

    public int QualifiedObjectCount => Models.Sum(model => model.QualifiedObjectCount);
}

/// <summary>
/// Analysis coverage for one semantic model. Limitations are model scoped, so an explanation must never
/// be presented as covering a model it did not come from.
/// </summary>
internal sealed record AnalysisCoverageModel(
    string ModelName,
    string AnchorId,
    int ObjectCount,
    int QualifiedObjectCount,
    IReadOnlyList<AnalysisCoverageGroup> QualifyingGroups,
    IReadOnlyList<AnalysisCoverageGroup> OtherGroups)
{
    public int ArtifactCount =>
        QualifyingGroups.Sum(group => group.ArtifactPaths.Count) +
        OtherGroups.Sum(group => group.ArtifactPaths.Count);
}

/// <summary>One construct that was not fully analysed, and every artifact of that construct.</summary>
internal sealed record AnalysisCoverageGroup(
    string ConstructType,
    string Label,
    string SupportStateLabel,
    string ImpactLabel,
    bool MayAffectClassification,
    string Reason,
    IReadOnlyList<string> ArtifactPaths);
