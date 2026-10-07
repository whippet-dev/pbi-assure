using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportObjectContextTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
    private static ProjectInventory Inventory() => ProjectScanner.Scan(Path.Combine(Root(), "tests", "fixtures", "pbi-assure-coverage"));
    private static string Article(string html, string id)
    {
        var start = html.IndexOf($"<article id=\"{id}\"", StringComparison.Ordinal);
        return html[start..(html.IndexOf("</article>", start, StringComparison.Ordinal) + 10)];
    }

    [Fact]
    public void EveryObjectSharesOneIdentityAndKeepsAllLocalAndLegacyDestinations()
    {
        var inventory = Inventory();
        var projection = SemanticLineageProjection.Build(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        var ids = Regex.Matches(html, "\\bid=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var card in projection.Cards.Where(card => card.Kind != LineageFocusKind.Visual))
        {
            var article = Article(html, card.Id);
            Assert.Contains(card.Kind == LineageFocusKind.ReportMeasure ? ">Report · Measure</p>" : ">Semantic model · Object</p>", article, StringComparison.Ordinal);
            Assert.Single(Regex.Matches(article, "<h2 "));
            Assert.Contains(HtmlEncoder.Default.Encode(card.Title), article, StringComparison.Ordinal);
            foreach (var view in new[] { "summary", "lineage", "definition" })
                Assert.Single(Regex.Matches(article, $"data-object-view=\"{view}\""));
            Assert.Single(Regex.Matches(article, "<summary>Details</summary>"));
            Assert.DoesNotContain("Object details</a>", article, StringComparison.Ordinal);
            Assert.DoesNotContain("View DAX expression", article, StringComparison.Ordinal);
            if (card.DetailsAnchor is not null) Assert.Contains(card.DetailsAnchor, ids);
        }
        foreach (Match link in Regex.Matches(html, "href=\"#((?:obj|lin|sum|def)-[^\"]+)\""))
            Assert.Contains(WebUtility.HtmlDecode(link.Groups[1].Value), ids);
        Assert.Equal(inventory.SemanticObjectUsages.Count + inventory.ReportMeasureUsages.Count,
            Regex.Count(html, "data-object-summary=\""));
    }

    [Theory]
    [InlineData(SemanticUsageStates.DirectlyUsed)]
    [InlineData(SemanticUsageStates.IndirectlyUsed)]
    [InlineData(SemanticUsageStates.StructurallyRequired)]
    [InlineData(SemanticUsageStates.UsedOnlyByUnusedBranch)]
    [InlineData(SemanticUsageStates.ApparentlyUnused)]
    public void SummaryPresentsThePublishedStateAndCompactEvidence(string state)
    {
        var inventory = Inventory();
        var projection = SemanticLineageProjection.Build(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        foreach (var usage in inventory.SemanticObjectUsages.Where(usage => usage.UsageState == state))
        {
            var card = projection.CardFor(usage)!;
            var article = Article(html, card.Id);
            Assert.Contains("<dt>Used by</dt>", article, StringComparison.Ordinal);
            Assert.Contains("<dt>Used in the report</dt>", article, StringComparison.Ordinal);
            Assert.Contains("<dt>Required by the model</dt>", article, StringComparison.Ordinal);
            Assert.Contains("Evidence", article, StringComparison.Ordinal);
            if (card.Reason is not null) Assert.Contains(HtmlEncoder.Default.Encode(card.Reason), article, StringComparison.Ordinal);
            if (usage.DirectReportLocationCount > 0) Assert.Contains("<h4>Report usage</h4>", article, StringComparison.Ordinal);
            if (state == SemanticUsageStates.ApparentlyUnused) Assert.Contains("Check before removing it", article, StringComparison.Ordinal);
            if (state == SemanticUsageStates.StructurallyRequired) Assert.DoesNotContain("No structural requirement identified", article, StringComparison.Ordinal);
            if (usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation) Assert.Contains("Checks limited", article, StringComparison.Ordinal);
            foreach (var phrase in new[] { "safe to delete", "safe to remove", "complete lineage", "every dependency" })
                Assert.DoesNotContain(phrase, article, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SavedDefinitionsAreRetainedAndOrdinaryColumnsHaveAnHonestEmptyState()
    {
        var inventory = Inventory();
        var projection = SemanticLineageProjection.Build(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        foreach (var model in inventory.SemanticModels)
        foreach (var table in model.Tables)
        {
            foreach (var measure in table.Measures)
                Assert.Contains(HtmlEncoder.Default.Encode(measure.Expression), html, StringComparison.Ordinal);
            foreach (var item in table.CalculationGroup?.Items ?? [])
            {
                Assert.Contains(HtmlEncoder.Default.Encode(item.Expression), html, StringComparison.Ordinal);
                if (!string.IsNullOrWhiteSpace(item.FormatStringExpression)) Assert.Contains(HtmlEncoder.Default.Encode(item.FormatStringExpression), html, StringComparison.Ordinal);
            }
            foreach (var partition in table.Partitions.Where(partition => partition.SourceType == "calculated" && !string.IsNullOrWhiteSpace(partition.Expression)))
                Assert.Contains(HtmlEncoder.Default.Encode(partition.Expression!), html, StringComparison.Ordinal);
        }
        foreach (var card in projection.Cards.Where(card => card.Kind == LineageFocusKind.Function))
        {
            var function = inventory.SemanticModels.Single(model => model.Name == card.SemanticModel).Functions.Single(function => function.Name == card.Reachability!.ObjectName);
            Assert.Contains(HtmlEncoder.Default.Encode(function.Expression), Article(html, card.Id), StringComparison.Ordinal);
            Assert.DoesNotContain("class=\"badge ", Article(html, card.Id), StringComparison.Ordinal);
        }
        var ordinaryColumn = inventory.SemanticObjectUsages.First(usage => usage.Table == "Fact" && usage.ObjectName == "IndirectlyUsedColumn");
        var ordinaryArticle = Article(html, projection.CardFor(ordinaryColumn)!.Id);
        Assert.Contains("This column has no DAX expression of its own.", ordinaryArticle, StringComparison.Ordinal);
        Assert.DoesNotContain("<pre><code></code></pre>", html, StringComparison.Ordinal);
        Assert.Contains("Power Query", ordinaryArticle, StringComparison.Ordinal);
        Assert.Contains("This shows which query loads the table.", ordinaryArticle, StringComparison.Ordinal);
    }
}
