using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Marks semantic objects whose usage state could be affected by metadata this scan did not analyse.
///
/// Usage states are never changed. A skipped construct can only add dependency edges, so it cannot
/// retract evidence already collected — which is why the states resting on positive evidence keep their
/// confidence while the two absence-based states do not. That is a conservative product rule grounded in
/// every construct known today being referential, not a proof about constructs nobody has seen; the
/// reserved <see cref="ConstructDependencyImpacts.MayInvalidateExistingEvidence"/> exists for a future
/// construct that changes how existing evidence should be read.
///
/// Qualification is decided by <see cref="AnalysisLimitation.DependencyImpact"/> alone. Construct types
/// and limitation identifiers are deliberately not consulted, so the registry stays the single place
/// where a construct's effect is declared.
/// </summary>
internal static class SemanticUsageConfidenceQualifier
{
    /// <summary>States that assert an absence of usage, and which unread metadata could therefore falsify.</summary>
    private static readonly string[] AbsenceStates =
    [
        SemanticUsageStates.ApparentlyUnused,
        SemanticUsageStates.UsedOnlyByUnusedBranch,
    ];

    /// <summary>States established by evidence already collected.</summary>
    private static readonly string[] PositiveStates =
    [
        SemanticUsageStates.DirectlyUsed,
        SemanticUsageStates.IndirectlyUsed,
        SemanticUsageStates.StructurallyRequired,
    ];

    public static SemanticObjectUsage[] Apply(
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<AnalysisLimitation> limitations)
    {
        var qualifiedStatesByModel = limitations
            .Where(limitation => limitation.SemanticModel is not null)
            .GroupBy(limitation => limitation.SemanticModel!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(limitation => QualifiedStates(limitation.DependencyImpact))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        return usages
            .Select(usage => qualifiedStatesByModel.TryGetValue(usage.SemanticModel, out var states) &&
                             states.Contains(usage.UsageState)
                ? usage with { ClassificationConfidence = ClassificationConfidences.QualifiedByLimitation }
                : usage with { ClassificationConfidence = ClassificationConfidences.Established })
            .ToArray();
    }

    /// <summary>
    /// The usage states a single unanalysed construct could bear on. Unknown impact values qualify
    /// nothing: a value this version does not recognise is not silently treated as dangerous, because
    /// every value the registry can currently produce is handled explicitly.
    /// </summary>
    private static IEnumerable<string> QualifiedStates(string dependencyImpact) => dependencyImpact switch
    {
        ConstructDependencyImpacts.MayCreateDependencies => AbsenceStates,
        ConstructDependencyImpacts.DependencyEffectUnknown => AbsenceStates,
        ConstructDependencyImpacts.MayInvalidateExistingEvidence => AbsenceStates.Concat(PositiveStates),
        _ => [],
    };
}
