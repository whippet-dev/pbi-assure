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

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Contains("<dt>Reference context</dt><dd>Visual filter &#xB7; Tooltips</dd>", html, StringComparison.Ordinal);
        Assert.Contains("Technical details and evidence (4)", html, StringComparison.Ordinal);
        Assert.Contains("$.visual.query.queryState.Tooltips.projections[1].field.Aggregation.Expression.Column", html, StringComparison.Ordinal);
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
