using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportTerminologyTests
{
    private static ProjectInventory Scan(string fixture = "pbi-assure-coverage")
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return ProjectScanner.Scan(Path.Combine(root!.FullName, "tests", "fixtures", fixture));
    }

    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(
        Regex.Replace(html, "<(style|script)\\b[^>]*>.*?</\\1>", "", RegexOptions.Singleline), "<[^>]+>", " "));

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("model-reference-context")]
    public void VisibleCopyUsesProductTermsWithoutEngineJargonOrCompletenessClaims(string fixture)
    {
        var inventory = Scan(fixture);
        var before = JsonSerializer.Serialize(inventory);
        var html = HtmlReportRenderer.Render(inventory);
        var text = Text(html);
        foreach (var phrase in new[] { "Selected object", "Selected visual", "consumers out", "consumer", "provenance",
                     "qualifying limitation", "primary assurance", "owner-level", "analysis boundaries", "direct-use evidence policy",
                     "retained non-headline", "absent from the graph", "absence conclusions", "(NotFound)",
                     "fully checked everything", "complete lineage", "everything checked" })
            Assert.False(Regex.IsMatch(text, @"\b" + Regex.Escape(phrase) + @"\b", RegexOptions.IgnoreCase), phrase);
        foreach (var phrase in new[] { "Report pages", "What was checked", "Findings, theme and accessibility are checked separately",
                     "Use alongside manual testing", "Review support, not a compliance verdict", "output format 0.26" })
            Assert.Contains(phrase, text, StringComparison.Ordinal);
        Assert.Equal(before, JsonSerializer.Serialize(inventory));
    }

    [Fact]
    public void LineageCentresNameEveryObjectAndVisualWithoutChangingDirections()
    {
        var inventory = Scan();
        var html = HtmlReportRenderer.Render(inventory);
        foreach (var card in SemanticLineageProjection.Build(inventory).Cards)
        {
            var start = html.IndexOf($"<article id=\"{card.Id}\"", StringComparison.Ordinal);
            var article = html[start..html.IndexOf("</article>", start, StringComparison.Ordinal)];
            var focus = Regex.Match(article, "<header class=\"lineage-focus\">(?<centre>.*?)</header>", RegexOptions.Singleline);
            Assert.True(focus.Success);
            Assert.Contains(card.Visual is null ? card.Title : Text(Regex.Match(article, "<h2[^>]*>(.*?)</h2>").Groups[1].Value), Text(focus.Value), StringComparison.Ordinal);
            Assert.Contains("data-lineage-side=\"upstream\"", article, StringComparison.Ordinal);
            Assert.Contains("<h3>Uses", article, StringComparison.Ordinal);
            if (card.Visual is null)
            {
                Assert.Contains("data-lineage-side=\"downstream\"", article, StringComparison.Ordinal);
                Assert.Contains("<h3>Used by", article, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData("NotFound", "wasn't found in the model")]
    [InlineData("Ambiguous", "could match more than one object")]
    public void DisplayReasonsDistinguishMissingFromAmbiguousAndPreserveTheStoredReason(string outcome, string meaning)
    {
        var reason = $"'Fact[X]' references 'MissingY', which could not be resolved to a model object ({outcome}). The dependency it would have created is absent from the graph, so absence conclusions in this model may be incomplete.";
        var limitation = new AnalysisLimitation("test", AnalysisLimitationCauses.ReferenceUnresolved,
            ConstructSupportStates.PartiallyAnalyzed, "semanticReference", AnalysisLimitationScopes.SemanticModel,
            "Model", "Fact", "X", "definition/tables/Fact.tmdl", "DAX", ConstructDependencyImpacts.MayCreateDependencies,
            [AnalysisConcerns.Dependency], reason);
        var display = AnalysisCoveragePresentation.DisplayReason(limitation);
        Assert.Contains("Fact[X] refers to MissingY", display, StringComparison.Ordinal);
        Assert.Contains(meaning, display, StringComparison.Ordinal);
        Assert.Contains("could look less used", display, StringComparison.Ordinal);
        Assert.DoesNotContain(outcome, display, StringComparison.Ordinal);
        Assert.Equal(reason, limitation.Reason);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
    }

    [Fact]
    public void ObjectDetailsRetainQueryLimitsAndUseCorrectCountGrammar()
    {
        var text = Text(HtmlReportRenderer.Render(Scan()));
        foreach (var phrase in new[] { "Some checks were limited for this model", "other reports and dynamic behaviour can't be seen here",
                     "doesn't follow the column through every query step", "a query could depend on it without appearing here",
                     "Defined in:", "Report measure details", "Created in this report", "This column has no DAX expression of its own",
                     "1 object in the model", "1 place", "Yes – 1 relationship", "Saved tab order", "All saved field references",
                     "can't see who is assigned to roles", "No use by another query found", "can't fully check from the saved report files",
                     "can't see every query this expression uses" })
            Assert.Contains(phrase, text, StringComparison.Ordinal);
        foreach (var phrase in new[] { "1 objects in the model", "1 places", "1 relationships", "1 findings", "1 accessibility observations" })
            Assert.False(Regex.IsMatch(text, @"\b" + Regex.Escape(phrase) + @"\b"), phrase);
    }
}
