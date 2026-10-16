using PbiAssure.Core.Inventory;
using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

public sealed class WebSummarySurfaceTests
{
    [Fact]
    public void CompletionKeepsScaleWithoutDuplicatingTheReportDashboard()
    {
        var markup = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));
        foreach (var (count, noun) in new[] { ("ReportCount", "report"), ("PageCount", "page"), ("VisualCount", "visual") })
            Assert.Contains($"@CountLabel(inventory.{count}, \"{noun}\")", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<Metric", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Analysis summary", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("WebAssuranceSummary.FromFindings", markup, StringComparison.Ordinal);
        Assert.Contains("WebConfidenceSummary.FromInventory", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AssuranceExcludesAccessibilityWithoutChangingSeverityAssessmentOrSourceFindings()
    {
        var findings = new[]
        {
            Finding("Model", FindingSeverities.Error, AssessmentTypes.Finding),
            Finding("Report", FindingSeverities.Warning, AssessmentTypes.ReviewRequired),
            Finding("Power Query", FindingSeverities.Information, AssessmentTypes.ReviewRequired),
            Finding("Model", FindingSeverities.Information, AssessmentTypes.Finding),
            Finding("Accessibility", FindingSeverities.Error, AssessmentTypes.Finding),
            Finding("accessibility", FindingSeverities.Warning, AssessmentTypes.ReviewRequired),
            Finding("ACCESSIBILITY", FindingSeverities.Information, AssessmentTypes.ReviewRequired),
        };
        var before = findings.ToArray();
        Assert.Equal(new WebAssuranceSummary(1, 1, 2, 4), WebAssuranceSummary.FromFindings(findings));
        Assert.Equal(before, findings);
        Assert.Equal(2, findings.Count(finding => finding.Severity == FindingSeverities.Error));
        Assert.Equal(4, findings.Count(finding => finding.AssessmentType == AssessmentTypes.ReviewRequired));
    }

    [Fact]
    public void EmptyAndAccessibilityOnlyFindingsHaveZeroPrimaryCounts()
    {
        var empty = new WebAssuranceSummary(0, 0, 0, 0);
        Assert.Equal(empty, WebAssuranceSummary.FromFindings([]));
        Assert.Equal(empty, WebAssuranceSummary.FromFindings([
            Finding(AssuranceCategories.Accessibility, FindingSeverities.Warning, AssessmentTypes.ReviewRequired)]));
    }

    private static AssuranceFinding Finding(string category, string severity, string assessment) => new(
        "TEST001", "1", category, severity, "Test finding", "Review", null, null, null, null, null, null, null,
        "report.json", [], assessment, null);

    private static string ExtractBetween(string value, string startMarker, string endMarker)
    {
        var start = value.IndexOf(startMarker, StringComparison.Ordinal);
        var end = value.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not find summary group between {startMarker} and {endMarker}.");
        return value[start..end];
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
