using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ModelReferenceContextFixtureTests
{
    [Fact]
    public void CleanDesktopFixtureHasNoUnresolvedModelReferenceFindings()
    {
        var inventory = ScanFixture("model-reference-context");

        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-MODEL-001");
    }

    [Fact]
    public void BrokenDesktopFixtureRetainsOneFindingPerLocationWithMeaningfulContext()
    {
        var inventory = ScanFixture("model-reference-context-broken");
        var findings = ModelReferenceFindings(inventory);

        Assert.Equal(6, findings.Length);
        Assert.All(findings, finding => Assert.Equal("1.1.0", finding.RuleVersion));

        AssertContext(
            Assert.Single(findings, finding => finding.ObjectName == "ReportFilter"),
            UsageContexts.Filter,
            "filter");
        AssertContext(
            Assert.Single(findings, finding => finding.ObjectName == "PageFilter"),
            UsageContexts.Filter,
            "filter");
        AssertContext(
            Assert.Single(findings, finding => finding.ObjectName == "TooltipField"),
            UsageContexts.Projection,
            "tooltips");
        AssertContext(
            Assert.Single(findings, finding => finding.ObjectName == "VisualFilter"),
            UsageContexts.Filter,
            "filter");

        var drillthroughFindings = findings.Where(finding => finding.ObjectName == "DrillthroughField").ToArray();
        Assert.Equal(2, drillthroughFindings.Length);
        AssertContext(
            Assert.Single(drillthroughFindings, finding => finding.Visual is not null),
            UsageContexts.Projection,
            "Values");
        AssertContext(
            Assert.Single(drillthroughFindings, finding => finding.Visual is null),
            UsageContexts.Drillthrough,
            "drillthrough");

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("<dt>Reference context</dt><dd>Report filter</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Reference context</dt><dd>Page filter</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Reference context</dt><dd>Tooltips</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Reference context</dt><dd>Visual filter</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Reference context</dt><dd>Values</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Reference context</dt><dd>Drillthrough field</dd>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MultiContextDesktopFixtureKeepsOneFindingAndAllContextsAndEvidence()
    {
        var inventory = ScanFixture("model-reference-context-broken-2visualfilter");
        var finding = Assert.Single(ModelReferenceFindings(inventory), finding => finding.ObjectName == "VisualFilter");
        var visual = Assert.Single(inventory.Reports
            .SelectMany(report => report.Pages)
            .SelectMany(page => page.Visuals),
            candidate => candidate.FieldReferences.Any(reference => reference.ObjectName == "VisualFilter"));

        Assert.Equal(2, finding.ReferenceContexts.Count);
        Assert.Contains(finding.ReferenceContexts, context =>
            context.UsageContext == UsageContexts.Filter && context.Role == "filter");
        Assert.Contains(finding.ReferenceContexts, context =>
            context.UsageContext == UsageContexts.Projection && context.Role == "tooltips");
        Assert.Equal(4, finding.EvidencePaths.Count);
        Assert.Contains("$.filterConfig.filters[3].field.Column", finding.EvidencePaths, StringComparer.Ordinal);
        Assert.Contains("$.filterConfig.filters[3].filter.Where[0].Condition.In.Expressions[0].Column", finding.EvidencePaths, StringComparer.Ordinal);
        Assert.Contains("$.filterConfig.filters[4].field.Aggregation.Expression.Column", finding.EvidencePaths, StringComparer.Ordinal);
        Assert.Contains("$.visual.query.queryState.Tooltips.projections[1].field.Aggregation.Expression.Column", finding.EvidencePaths, StringComparer.Ordinal);
        Assert.Equal(4, visual.DistinctFieldCount);

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("<dt>Reference context</dt><dd>Visual filter &#xB7; Tooltips</dd>", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(
            html,
            "<li><code>ReferenceTest[VisualFilter]</code><span>Column — <span class=\"usage-label\">Used as:</span> Visual filter &#xB7; Tooltips</span></li>"));
        Assert.DoesNotContain("Visual filter &#xB7; Visual filter", html, StringComparison.Ordinal);
        Assert.Contains("Technical details and evidence (4)", html, StringComparison.Ordinal);
        Assert.Contains("$.visual.query.queryState.Tooltips.projections[1].field.Aggregation.Expression.Column", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvedDesktopFixturePresentsAllUsesOncePerObjectAndReportLocation()
    {
        var inventory = ScanFixture("model-reference-context");
        var visual = Assert.Single(inventory.Reports
            .SelectMany(report => report.Pages)
            .SelectMany(page => page.Visuals),
            candidate => candidate.FieldReferences.Any(reference => reference.ObjectName == "VisualFilter"));
        var references = visual.FieldReferences
            .Where(reference => reference.ObjectName == "VisualFilter")
            .ToArray();
        var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.Table == "ReferenceTest" && candidate.ObjectName == "VisualFilter");

        Assert.Equal(4, visual.DistinctFieldCount);
        Assert.Equal(4, references.Length);
        Assert.Equal(3, references.Count(reference => reference.UsageContext == UsageContexts.Filter));
        Assert.Single(references, reference =>
            reference.UsageContext == UsageContexts.Projection && reference.Role == "tooltips");
        Assert.Equal(4, usage.DirectReportReferenceCount);
        Assert.Equal(1, usage.DirectReportLocationCount);

        var html = HtmlReportRenderer.Render(inventory);
        const string visualObjectRow =
            "<li><code>ReferenceTest[VisualFilter]</code><span>Column — <span class=\"usage-label\">Used as:</span> Visual filter &#xB7; Tooltips</span></li>";
        Assert.Equal(1, CountOccurrences(html, visualObjectRow));
        Assert.Equal(2, CountOccurrences(html, "Visual filter &#xB7; Tooltips"));
        Assert.Contains(
            "<span class=\"usage-role\"><span class=\"usage-label\">Used as:</span> Visual filter &#xB7; Tooltips</span>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<li><code>ReferenceTest[Category]</code><span>Column — <span class=\"usage-label\">Used as:</span> Category</span></li>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<li><code>ReferenceTest[TooltipField]</code><span>Column — <span class=\"usage-label\">Used as:</span> Tooltips</span></li>",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Visual filter &#xB7; Visual filter", html, StringComparison.Ordinal);
    }

    private static void AssertContext(AssuranceFinding finding, string usageContext, string role)
    {
        var context = Assert.Single(finding.ReferenceContexts);
        Assert.Equal(usageContext, context.UsageContext);
        Assert.Equal(role, context.Role);
    }

    private static ProjectInventory ScanFixture(string fixtureName) => ProjectScanner.Scan(Path.Combine(
        FindRepositoryRoot(),
        "tests",
        "fixtures",
        fixtureName));

    private static AssuranceFinding[] ModelReferenceFindings(ProjectInventory inventory) => inventory.Findings
        .Where(finding => finding.RuleId == "PBI-MODEL-001")
        .ToArray();

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        for (var index = 0; (index = value.IndexOf(expected, index, StringComparison.Ordinal)) >= 0; index += expected.Length)
        {
            count++;
        }

        return count;
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
