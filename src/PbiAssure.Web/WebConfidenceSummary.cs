using PbiAssure.Core.Inventory;

namespace PbiAssure.Web;

/// <summary>
/// How many of this scan's absence conclusions have limited checks.
///
/// Only the two absence states are counted. A limitation can add dependency edges but cannot retract
/// evidence already collected, so a positive result stays established no matter what went unread — the
/// same rule Core applies when it sets the confidence.
///
/// The population matches the semantic-usage metrics this notice sits beside: developer objects only,
/// in either absence state. That is a wider population than the apparently unused review, which lists
/// only Apparently unused objects, so the notice names both states rather than one.
/// </summary>
public sealed record WebConfidenceSummary(int QualifiedAbsenceCount)
{
    private static readonly string[] AbsenceStates =
    [
        SemanticUsageStates.ApparentlyUnused,
        SemanticUsageStates.UsedOnlyByUnusedBranch,
    ];

    public bool HasQualifiedAbsence => QualifiedAbsenceCount > 0;

    public static string Headline => "Checks limited for some results";

    /// <summary>Which results, without restating the count as a second metric.</summary>
    public string Detail => QualifiedAbsenceCount == 1
        ? "1 model object classed as Apparently unused or Only used by unused items has limited checks."
        : $"{QualifiedAbsenceCount} model objects classed as Apparently unused or Only used by unused items have limited checks.";

    /// <summary>What the marker means, in the words every surface uses for it.</summary>
    public static string Explanation =>
        "Checks limited means missing or partly understood metadata could affect this result. The usage classification has not changed.";

    /// <summary>The route to the detail, which lives in the report's Analysis coverage section.</summary>
    public static string Guidance =>
        "Open the interactive report and review Analysis coverage before making cleanup decisions.";

    public static WebConfidenceSummary FromInventory(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return new(inventory.SemanticObjectUsages.Count(usage =>
            string.Equals(
                usage.ClassificationConfidence,
                ClassificationConfidences.QualifiedByLimitation,
                StringComparison.Ordinal) &&
            AbsenceStates.Contains(usage.UsageState, StringComparer.Ordinal) &&
            !inventory.IsSystemGeneratedSemanticObject(usage)));
    }
}
