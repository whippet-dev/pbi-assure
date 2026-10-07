using System.Net;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportEntityContextTests
{
    private static ProjectInventory Inventory()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return ProjectScanner.Scan(Path.Combine(root!.FullName, "tests", "fixtures", "pbi-assure-coverage"));
    }
    private static string Article(string html, string id)
    {
        var start = html.IndexOf($"<article id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        return html[start..(html.IndexOf("</article>", start, StringComparison.Ordinal) + 10)];
    }

    [Fact]
    public void EveryEntityHasOneHeadingPredictableViewsAndResolvingLinksWithoutLegacyVisualBlocks()
    {
        var inventory = Inventory();
        var html = HtmlReportRenderer.Render(inventory);
        var ids = Regex.Matches(html, "\\bid=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var (kind, count, views) in new[] { ("Report", inventory.Reports.Count, new[] { "summary", "pages" }),
                     ("Page", inventory.PageCount, new[] { "summary", "visuals" }), ("Visual", inventory.VisualCount, new[] { "summary", "objects", "reviews" }) })
        {
            var entities = Regex.Matches(html, $"<article id=\"([^\"]+)\"[^>]+data-report-context=\"{kind}\"");
            Assert.Equal(count, entities.Count);
            foreach (Match entity in entities)
            {
                var article = Article(html, entity.Groups[1].Value);
                Assert.Single(Regex.Matches(article, "<h2 "));
                foreach (var view in views) Assert.Single(Regex.Matches(article, $"data-context-view=\"{view}\""));
                Assert.Single(Regex.Matches(article, "<summary>Details</summary>"));
            }
        }
        foreach (Match link in Regex.Matches(html, "href=\"#([^\"]+)\"")) Assert.Contains(WebUtility.HtmlDecode(link.Groups[1].Value).Split('?')[0], ids);
        Assert.DoesNotContain("class=\"visual-card\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"tab\"", html, StringComparison.Ordinal);
        Assert.Contains("Created in this report", html, StringComparison.Ordinal);
        Assert.Contains("Semantic model:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualObjectsUseOnlyThePublishedRelationAndKeepRawBindingsInDetails()
    {
        var inventory = Inventory();
        var projection = SemanticLineageProjection.Build(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        foreach (var card in projection.Cards.Where(card => card.Kind == LineageFocusKind.Visual))
        {
            var article = Article(html, card.Id);
            var objects = article[article.IndexOf("data-context-view=\"objects\"", StringComparison.Ordinal)..article.IndexOf("data-context-view=\"reviews\"", StringComparison.Ordinal)];
            Assert.Equal(card.Uses.Items.Count, Regex.Count(objects, "<li "));
            foreach (var use in card.Uses.Items)
                Assert.Contains($"href=\"#sum-{use.Object.CardId![4..]}\"", objects, StringComparison.Ordinal);
            Assert.DoesNotContain("page-level", objects, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Includes references PBI Assure doesn't count as use", article, StringComparison.Ordinal);
            Assert.Contains($"<dt>Model objects used</dt><dd>{card.Uses.TotalCount}</dd>", article, StringComparison.Ordinal);
        }
        Assert.Contains("Objects used at page level", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SameTitlesAcrossEntitiesCannotChangePbirIdentityAndEmptyVisualsRemainInvestigable()
    {
        var inventory = Inventory();
        var original = HtmlReportRenderer.Render(inventory);
        var changed = inventory with { Reports = inventory.Reports.Select(report => report with { Pages = report.Pages.Select(page => page with {
            Visuals = page.Visuals.Select(visual => visual with { Accessibility = visual.Accessibility with { TitleText = "Same title", HasConfiguredTitleText = true, TitleIsVisible = true } }).ToArray()
        }).ToArray() }).ToArray() };
        var html = HtmlReportRenderer.Render(changed);
        const string pattern = "<article id=\"([^\"]+)\"[^>]+data-report-context=\"Visual\"";
        Assert.Equal(Regex.Matches(original, pattern).Select(match => match.Groups[1].Value), Regex.Matches(html, pattern).Select(match => match.Groups[1].Value));
        Assert.Contains("No model objects found for this visual.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualReviewsSeparateFamiliesAndExcludeOtherOwners()
    {
        var inventory = Inventory();
        var selected = SemanticLineageProjection.Build(inventory).Cards.First(item => item.Kind == LineageFocusKind.Visual);
        var report = selected.Report!; var page = selected.Page!; var visual = selected.Visual!;
        var updatedReport = report with { ThemeReview = report.ThemeReview with {
            Deviations = [new(page.Name, page.DisplayName, visual.Name, visual.VisualType, "specific-setting", "Specific deviation", "12", "10", null)],
            ConsistencyObservations = [new(page.Name, page.DisplayName, visual.Name, visual.VisualType, "specific-setting", "Specific consistency", "12", "10", 3, 2)],
            AccessibilityObservations = [new(page.Name, visual.Name, "Specific contrast", "Saved observation")]
        } };
        inventory = inventory with { Reports = inventory.Reports.Select(item => item == report ? updatedReport : item).ToArray() };
        var template = inventory.Findings[0] with { Report = report.Name, Page = page.Name, PageDisplayName = page.DisplayName, Visual = visual.Name };
        inventory = inventory with { Findings = [template with { RuleId = "VISUAL-F", Message = "Visual assurance cue", Category = "Navigation" },
            template with { RuleId = "VISUAL-A", Message = "Visual accessibility cue", Category = AssuranceCategories.Accessibility },
            template with { RuleId = "PAGE-ONLY", Message = "Page only", Visual = null }, template with { RuleId = "REPORT-ONLY", Message = "Report only", Visual = null, Page = null, PageDisplayName = null }] };
        var card = SemanticLineageProjection.Build(inventory).CardForVisual(report.Name, page.Name, visual.Name)!;
        var article = Article(HtmlReportRenderer.Render(inventory), card.Id);
        Assert.Contains("<h4>Findings</h4>", article, StringComparison.Ordinal);
        Assert.Contains("<h4>Accessibility</h4>", article, StringComparison.Ordinal);
        Assert.Contains("<h4>Theme</h4>", article, StringComparison.Ordinal);
        Assert.Contains("Specific deviation", article, StringComparison.Ordinal);
        Assert.Contains("Specific consistency", article, StringComparison.Ordinal);
        Assert.Contains("Specific contrast", article, StringComparison.Ordinal);
        Assert.Contains("<dt>Theme review items</dt><dd>3</dd>", article, StringComparison.Ordinal);
        Assert.Contains("Visual assurance cue", article, StringComparison.Ordinal);
        Assert.Contains("Visual accessibility cue", article, StringComparison.Ordinal);
        Assert.DoesNotContain("Page only", article, StringComparison.Ordinal);
        Assert.DoesNotContain("Report only", article, StringComparison.Ordinal);
        Assert.Contains("<dt>Findings</dt><dd>1</dd>", article, StringComparison.Ordinal);
        Assert.Contains("<dt>Accessibility observations</dt><dd>1</dd>", article, StringComparison.Ordinal);
        Assert.Contains($"href=\"#visual-{card.Id[4..]}-reviews\"", HtmlReportRenderer.Render(inventory), StringComparison.Ordinal);
    }
}
