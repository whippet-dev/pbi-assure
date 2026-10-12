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
        Assert.Equal(["attention", "usage", "confidence", "snapshot", "reviews"],
            Regex.Matches(overview, "<h3 id=\"summary-([^\"]+)-heading\"").Select(match => match.Groups[1].Value));
        Assert.True(overview.IndexOf("<dt>Errors</dt>", StringComparison.Ordinal) < overview.IndexOf("<dt>Reports</dt>", StringComparison.Ordinal));
        Assert.Single(Regex.Matches(overview, "<details\\b")); // Only the existing scope disclosure.
        Assert.Contains("data-section-target=\"semantic-usage\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-target=\"reports\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AttentionUsesExistingCountsAndRoutesToFindingsWithoutSamplingMessagesOrLocations()
    {
        var inventory = Rich.Value;
        inventory = inventory with { Findings = inventory.Findings.Select((finding, index) => finding with { Message = $"Individual finding message {index}: only in the canonical review." }).ToArray() };
        var html = HtmlReportRenderer.Render(inventory);
        var overview = Overview(html);
        Assert.Contains("<dt>Errors</dt><dd>11</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("<dt>Warnings</dt><dd>10</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("<dt>Review required</dt><dd>7</dd>", overview, StringComparison.Ordinal);
        Assert.Contains("Review 28 findings", overview, StringComparison.Ordinal);
        Assert.Contains("href=\"#findings\">Review 28 findings", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Individual finding message", overview, StringComparison.Ordinal);
        Assert.Contains("Individual finding message", html[html.IndexOf("<section id=\"findings\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Empty(Regex.Matches(overview, "href=\"#finding-"));
        Assert.DoesNotContain("overview-findings", html, StringComparison.Ordinal);
        Assert.DoesNotContain("overview-finding-location", html, StringComparison.Ordinal);
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
        Assert.Contains("Review 1 finding", overview, StringComparison.Ordinal);
        Assert.Contains("these counts can overlap", overview, StringComparison.Ordinal);
    }

    [Fact]
    public void SimpleOverviewShowsAccessibilityEvenWithoutPrimaryFindings()
    {
        var overview = Overview(HtmlReportRenderer.Render(Simple.Value));
        Assert.Contains("No findings.", overview, StringComparison.Ordinal);
        Assert.Contains("5 accessibility observations", overview, StringComparison.Ordinal);
        Assert.Contains("href=\"#accessibility-review\"", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("overview-attention-metrics", overview, StringComparison.Ordinal);
        Assert.Contains("Manual review is still recommended.", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Review 0 findings", overview, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pbi-assure-coverage", true)]
    [InlineData("model-reference-context", false)]
    public void UnusedCautionOnlyAppearsWhenCandidatesExist(string fixture, bool hasCandidates)
    {
        var overview = Overview(HtmlReportRenderer.Render(fixture == "pbi-assure-coverage" ? Rich.Value : Simple.Value));
        Assert.Equal(hasCandidates, overview.Contains("class=\"summary-caution\"", StringComparison.Ordinal));
        Assert.Equal(!hasCandidates, overview.Contains("No apparently unused model objects found", StringComparison.Ordinal));
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
        Assert.Contains("No results have Checks limited", simple, StringComparison.Ordinal);
        Assert.Contains("Use outside this project still can't be seen", simple, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondaryReviewsAreOrderedAndThemeIsOmittedOnlyFromOverviewWhenItHasNoItems()
    {
        var simpleHtml = HtmlReportRenderer.Render(Simple.Value);
        var simple = Overview(simpleHtml);
        Assert.DoesNotContain("href=\"#theme-review\"", simple, StringComparison.Ordinal);
        Assert.Contains("href=\"#theme-review\"", simpleHtml, StringComparison.Ordinal);
        Assert.Contains("id=\"theme-review\"", simpleHtml, StringComparison.Ordinal);
        Assert.Contains("No theme settings could be compared automatically in this report.", simpleHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("no differences", simple, StringComparison.OrdinalIgnoreCase);
        var rich = Overview(HtmlReportRenderer.Render(Rich.Value));
        Assert.Contains("1 theme deviation · 1 consistency observation", rich, StringComparison.Ordinal);
        Assert.Contains("saved visual settings compared against the theme", rich, StringComparison.Ordinal);
        Assert.True(rich.IndexOf("summary-reviews-heading", StringComparison.Ordinal) < rich.IndexOf("href=\"#accessibility-review\"", StringComparison.Ordinal));
        Assert.True(rich.IndexOf("href=\"#accessibility-review\"", StringComparison.Ordinal) < rich.IndexOf("href=\"#theme-review\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void EitherThemeReviewItemKindKeepsItsOverviewEntry(bool deviations, bool consistency)
    {
        var inventory = Rich.Value with { Reports = Rich.Value.Reports.Select(report => report with {
            ThemeReview = report.ThemeReview with { Deviations = deviations ? report.ThemeReview.Deviations : [],
                ConsistencyObservations = consistency ? report.ThemeReview.ConsistencyObservations : [] }
        }).ToArray() };
        Assert.Equal(deviations || consistency, Overview(HtmlReportRenderer.Render(inventory)).Contains("href=\"#theme-review\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AccessibilityZeroStateStaysUnderOtherReviewsWithoutBecomingAMainFinding()
    {
        var html = HtmlReportRenderer.Render(Simple.Value with { Findings = [] });
        var overview = Overview(html);
        var reviews = overview[overview.IndexOf("<h3 id=\"summary-reviews-heading\"", StringComparison.Ordinal)..];
        Assert.Contains("No findings.", overview, StringComparison.Ordinal);
        Assert.Contains("No accessibility observations found.", reviews, StringComparison.Ordinal);
        Assert.Contains("href=\"#accessibility-review\"", reviews, StringComparison.Ordinal);
        Assert.DoesNotContain("Review 0 findings", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("compliance", overview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotRetainsHighValueCountsWithoutRepeatingEveryInventoryMetric()
    {
        var overview = Overview(HtmlReportRenderer.Render(Rich.Value));
        foreach (var (label, count) in new[] { ("Semantic models", 5), ("Reports", 5), ("Pages", 19), ("Visuals", 30), ("Your model objects", 87) })
        {
            Assert.Contains($"<dt>{label}</dt><dd>{count}</dd>", overview, StringComparison.Ordinal);
        }
        Assert.Contains("Power Query: 27 queries · 5 recognised data source types", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Report measures</dt>", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Power BI-generated model objects</dt>", overview, StringComparison.Ordinal);
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
