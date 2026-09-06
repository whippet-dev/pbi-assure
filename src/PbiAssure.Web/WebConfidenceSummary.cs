using PbiAssure.Core.Inventory;

namespace PbiAssure.Web;

/// <summary>
/// How many of this scan's absence conclusions rest on incomplete local evidence.
///
/// Only the two absence states are counted. A limitation can add dependency edges but cannot retract
/// evidence already collected, so a positive result stays established no matter what went unread — the
/// same rule Core applies when it sets the confidence.
///
/// The population matches the semantic-usage metrics this notice sits beside: developer objects only.
/// A count drawn from a different population would explain numbers the reader cannot see.
/// </summary>
public sealed record WebConfidenceSummary(int QualifiedAbsenceCount)
{
    private static readonly string[] AbsenceStates =
    [
        SemanticUsageStates.ApparentlyUnused,
        SemanticUsageStates.UsedOnlyByUnusedBranch,
    ];

    public bool HasQualifiedAbsence => QualifiedAbsenceCount > 0;

    public static string Headline => "Some absence-based results are qualified";

    /// <summary>What is qualified, without restating the count as a second metric.</summary>
    public string Detail => QualifiedAbsenceCount == 1
        ? "1 model object has a classification based on incomplete local evidence."
        : $"{QualifiedAbsenceCount} model objects have classifications based on incomplete local evidence.";

    /// <summary>The route to the detail, which lives in the report's Analysis coverage section.</summary>
    public static string Guidance =>
        "Open the interactive report above and review Analysis coverage before making cleanup decisions.";

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
