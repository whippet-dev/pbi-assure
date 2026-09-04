using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class PowerQueryParameterTests
{
    [Fact]
    public void DesktopIncrementalRefreshParametersRetainPersistedMetadataAndDependencies()
    {
        var inventory = ScanFixture("desktop-incremental-refresh-evidence");
        var model = Assert.Single(inventory.SemanticModels);

        var rangeStartExpression = Assert.Single(model.NamedExpressions, expression =>
            expression.Name == "RangeStart");
        Assert.True(rangeStartExpression.IsParameter);
        Assert.Equal("DateTime", rangeStartExpression.ParameterType);
        Assert.True(rangeStartExpression.IsParameterRequired);
        Assert.Equal(
            "#datetime(2026, 1, 1, 0, 0, 0) meta [IsParameterQuery=true, Type=\"DateTime\", IsParameterQueryRequired=true]",
            rangeStartExpression.Expression);

        var rangeStart = Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == "RangeStart");
        var rangeEnd = Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == "RangeEnd");
        Assert.All([rangeStart, rangeEnd], parameter =>
        {
            Assert.True(parameter.IsParameter);
            Assert.Equal("DateTime", parameter.ParameterType);
            Assert.True(parameter.IsParameterRequired);
            Assert.Equal(PowerQueryUsageStates.SupportingQuery, parameter.UsageState);
            Assert.Null(parameter.QueryRole);
            Assert.Equal(["FactEvents_Policy"], parameter.RefreshPolicyTables);
            Assert.DoesNotContain(inventory.Findings, finding =>
                finding.RuleId == "PBI-QUERY-002" && finding.ObjectName == parameter.QueryName);
        });

        Assert.Contains(inventory.PowerQueryDependencies, dependency =>
            dependency.FromQueryName == "FactEvents_Policy" && dependency.ToQueryName == "RangeStart");
        Assert.Contains(inventory.PowerQueryDependencies, dependency =>
            dependency.FromQueryName == "FactEvents_FilterOnly" && dependency.ToQueryName == "RangeEnd");
    }

    [Fact]
    public void PowerQuerySurfaceIdentifiesParametersAndTheirRefreshPolicyContext()
    {
        var html = HtmlReportRenderer.Render(ScanFixture("desktop-incremental-refresh-evidence"));

        Assert.Contains("Power Query parameter", html, StringComparison.Ordinal);
        Assert.Contains(">Parameter</span>", html, StringComparison.Ordinal);
        Assert.Contains("Parameter type</dt><dd><code>DateTime", html, StringComparison.Ordinal);
        Assert.Contains("Required</dt><dd><code>Yes", html, StringComparison.Ordinal);
        Assert.Contains("Incremental refresh</dt><dd><code>FactEvents_Policy", html, StringComparison.Ordinal);
    }

    private static ProjectInventory ScanFixture(string fixture) => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", fixture));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
