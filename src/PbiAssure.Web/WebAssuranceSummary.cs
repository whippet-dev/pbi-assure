using PbiAssure.Core.Inventory;

namespace PbiAssure.Web;

public sealed record WebAssuranceSummary(int Errors, int Warnings, int ReviewRequired, int TotalFindings)
{
    public static WebAssuranceSummary FromFindings(IEnumerable<AssuranceFinding> findings)
    {
        // Match the HTML report's primary Assurance boundary without changing inventory totals.
        var primary = findings.Where(finding => !string.Equals(
            finding.Category, AssuranceCategories.Accessibility, StringComparison.OrdinalIgnoreCase)).ToArray();
        return new(
            primary.Count(finding => finding.Severity == FindingSeverities.Error),
            primary.Count(finding => finding.Severity == FindingSeverities.Warning),
            primary.Count(finding => finding.AssessmentType == AssessmentTypes.ReviewRequired),
            primary.Length);
    }
}
