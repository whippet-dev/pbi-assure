using System.Text.Json;
using System.Text.RegularExpressions;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportOverviewTests
{
    private static readonly Lazy<ProjectInventory> Rich = new(() => Scan("pbi-assure-coverage"));
    private static readonly Lazy<ProjectInventory> Simple = new(() => Scan("model-reference-context"));

    [Fact]
    public void OverviewOrdersAttentionBeforeInventoryAndKeepsExistingNavigation()
    {
        var html = HtmlReportRenderer.Render(Rich.Value);
        var overview = Overview(html);
        Assert.Equal(["attention", "reviews", "usage", "confidence", "snapshot"],
            Regex.Matches(overview, "<h3 id=\"summary-([^\"]+)-heading\"").Select(match => match.Groups[1].Value));
        Assert.True(overview.IndexOf("<dt>Errors</dt>", StringComparison.Ordinal) < overview.IndexOf("<dt>Reports</dt>", StringComparison.Ordinal));
        Assert.Single(Regex.Matches(overview, "<details\\b")); // Only the existing scope disclosure.
        Assert.Contains("data-section-target=\"semantic-usage\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"reports\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AttentionUsesExistingCountsAndOnlyTwoConcreteFindingsWithOriginalAnchors()
    {
        var inventory = Rich.Value;
        var primary = inventory.Findings.Where(finding => finding.Category != AssuranceCategories.Accessibility).ToArray();
        var overview = Overview(HtmlReportRenderer.Render(inventory));
        Assert.Contains("<dt>Errors</dt><dd>11</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("<dt>Warnings</dt><dd>10</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("<dt>Review required</dt><dd>7</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("Open 28 primary assurance findings", overview, StringComparison.Ordinal);
        var links = Regex.Matches(overview, "href=\"#finding-(\\d+)\"");
        Assert.Equal(2, links.Count);
        Assert.All(links.Cast<Match>(), link => Assert.Equal(FindingSeverities.Error, primary[int.Parse(link.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - 1].Severity));
    }

    [Fact]
    public void SeverityAndAssessmentCountsCanOverlapWithoutInventingATotal()
    {
        var finding = Rich.Value.Findings.First(finding => finding.Severity == FindingSeverities.Error);
        var overview = Overview(HtmlReportRenderer.Render(Rich.Value with
        {
            Findings = [finding with { Severity = FindingSeverities.Warning, AssessmentType = AssessmentTypes.ReviewRequired }],
        }));
        Assert.Contains("<dt>Warnings</dt><dd>1</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("<dt>Review required</dt><dd>1</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("Open 1 primary assurance finding", overview, StringComparison.Ordinal);
        Assert.Contains("assessments may overlap", overview, StringComparison.Ordinal);
    }

    [Fact]
    public void SimpleOverviewShowsAccessibilityEvenWithoutPrimaryFindings()
    {
        var overview = Overview(HtmlReportRenderer.Render(Simple.Value));
        Assert.Contains("No primary assurance findings identified.", overview, StringComparison.Ordinal);
        Assert.Contains("5 accessibility observations", overview, StringComparison.Ordinal);
        Assert.Contains("href=\"#accessibility-review\"", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("overview-attention-metrics", overview, StringComparison.Ordinal);
        Assert.Contains("Manual review is still recommended.", overview, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pbi-assure-coverage", true)]
    [InlineData("model-reference-context", false)]
    public void UnusedCautionOnlyAppearsWhenCandidatesExist(string fixture, bool hasCandidates)
    {
        var overview = Overview(HtmlReportRenderer.Render(fixture == "pbi-assure-coverage" ? Rich.Value : Simple.Value));
        Assert.Equal(hasCandidates, overview.Contains("class=\"summary-caution\"", StringComparison.Ordinal));
        Assert.Equal(!hasCandidates, overview.Contains("No apparently unused authored objects identified", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DirectlyUsed", "Directly used", 18)]
    [InlineData("IndirectlyUsed", "Indirectly used", 23)]
    [InlineData("StructurallyRequired", "Structurally required", 15)]
    [InlineData("UsedOnlyByUnusedBranch", "Only used by unused items", 5)]
    [InlineData("ApparentlyUnused", "Apparently unused", 26)]
    public void UsageCountsHaveNamedFilteredDestinations(string state, string label, int count)
    {
        var overview = Overview(HtmlReportRenderer.Render(Rich.Value));
        Assert.Matches($"href=\"#semantic-usage\\?state={state}\" data-usage-shortcut=\"{state}\"[^>]*><span>{label}</span><strong>{count}</strong>", overview);
        var simple = HtmlReportRenderer.Render(Simple.Value);
        Assert.Contains($"value=\"{state}\">{label}</option>", simple, StringComparison.Ordinal); // Zero states can be selected too.
    }

    [Fact]
    public void ConfidenceRemainsSeparateAndUsesTheBoundedNoLimitationsState()
    {
        var rich = Overview(HtmlReportRenderer.Render(Rich.Value));
        Assert.Contains("<strong>29 object results</strong>", rich, StringComparison.Ordinal);
        Assert.Contains("Checks limited", rich, StringComparison.Ordinal);
        Assert.Contains("href=\"#analysis-coverage\"", rich, StringComparison.Ordinal);
        var simple = Overview(HtmlReportRenderer.Render(Simple.Value));
        Assert.Contains("No identified limitations", simple, StringComparison.Ordinal);
        Assert.DoesNotContain("Checks limited", simple, StringComparison.Ordinal);
        Assert.Contains("No object results have a qualifying limitation", simple, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeStatesDoNotTreatZeroComparisonsAsNoDifferences()
    {
        var simple = Overview(HtmlReportRenderer.Render(Simple.Value));
        Assert.Contains("No theme settings could be compared automatically.", simple, StringComparison.Ordinal);
        Assert.DoesNotContain("no differences", simple, StringComparison.OrdinalIgnoreCase);
        var rich = Overview(HtmlReportRenderer.Render(Rich.Value));
        Assert.Contains("1 theme deviation · 1 consistency observation", rich, StringComparison.Ordinal);
        Assert.Contains("saved visual settings compared against the theme", rich, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotRetainsHighValueCountsWithoutRepeatingEveryInventoryMetric()
    {
        var overview = Overview(HtmlReportRenderer.Render(Rich.Value));
        foreach (var (label, count) in new[] { ("Semantic models", 5), ("Reports", 5), ("Pages", 19), ("Visuals", 30), ("Authored semantic objects", 87) })
        {
            Assert.Contains($"<dt>{label}</dt><dd>{count}</dd>", overview, StringComparison.Ordinal);
        }
        Assert.Contains("Power Query: 27 queries · 5 recognised data source types", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Report measures</dt>", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>System-generated model objects</dt>", overview, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("model-reference-context")]
    public void OverviewAvoidsCompletenessAndDeletionClaimsAndDoesNotMutateAnalysis(string fixture)
    {
        var inventory = fixture == "pbi-assure-coverage" ? Rich.Value : Simple.Value;
        var before = JsonSerializer.Serialize(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        var overview = Overview(html);
        foreach (var forbidden in new[] { "safe to delete", "dead", "orphaned", "redundant", "checks complete", "fully analysed", "complete coverage", "all checks passed" })
        {
            Assert.DoesNotContain(forbidden, overview, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(before, JsonSerializer.Serialize(inventory));
        Assert.Equal(html, HtmlReportRenderer.Render(inventory));
    }

    private static string Overview(string html) => html[html.IndexOf("    <section id=\"summary\"", StringComparison.Ordinal)..html.IndexOf("    <section id=\"semantic-usage\"", StringComparison.Ordinal)];

    private static ProjectInventory Scan(string fixture)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
                return ProjectScanner.Scan(Path.Combine(directory.FullName, "tests", "fixtures", fixture));
        }
        throw new DirectoryNotFoundException("Could not find the fixture repository.");
    }
}
