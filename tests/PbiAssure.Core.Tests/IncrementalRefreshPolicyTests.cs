using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Bounded incremental-refresh coverage. The paired Desktop fixtures prove that RangeStart/RangeEnd
/// filtering and an authored refresh policy are separate persisted states.
/// </summary>
public sealed class IncrementalRefreshPolicyTests
{
    [Fact]
    public void DesktopBaselineRetainsParameterFilteringWithoutARefreshPolicy()
    {
        var model = Assert.Single(ScanFixture("desktop-incremental-refresh-evidence-baseline").SemanticModels);

        Assert.All(
            model.Tables.Where(table => table.Name.StartsWith("FactEvents_", StringComparison.Ordinal)),
            table =>
            {
                Assert.Null(table.RefreshPolicy);
                var expression = Assert.Single(table.Partitions).Expression;
                Assert.Contains("[EventDate] >= RangeStart", expression, StringComparison.Ordinal);
                Assert.Contains("[EventDate] < RangeEnd", expression, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void DesktopPolicyFixtureRetainsTheExplicitTableOwnedPolicy()
    {
        var inventory = ScanFixture("desktop-incremental-refresh-evidence");
        var model = Assert.Single(inventory.SemanticModels);
        var policyTable = Assert.Single(model.Tables, table => table.Name == "FactEvents_Policy");
        var filterOnlyTable = Assert.Single(model.Tables, table => table.Name == "FactEvents_FilterOnly");
        var policy = Assert.IsType<SemanticRefreshPolicyInventory>(policyTable.RefreshPolicy);

        Assert.Equal("basic", policy.PolicyType);
        Assert.Equal("year", policy.RollingWindowGranularity);
        Assert.Equal(2, policy.RollingWindowPeriods);
        Assert.Equal("day", policy.IncrementalGranularity);
        Assert.Equal(30, policy.IncrementalPeriods);
        Assert.Equal(-1, policy.IncrementalPeriodsOffset);
        Assert.Null(policy.Mode);
        Assert.Equal("LastModified", policy.ChangeDetectionColumn);
        Assert.Contains("List.Max(FactEvents_Policy[LastModified])", policy.PollingExpression, StringComparison.Ordinal);
        Assert.Contains("[EventDate] >= RangeStart", policy.SourceExpression, StringComparison.Ordinal);
        Assert.Contains("[EventDate] < RangeEnd", policy.SourceExpression, StringComparison.Ordinal);
        Assert.Null(filterOnlyTable.RefreshPolicy);
    }

    [Fact]
    public void ExplicitChangeDetectionColumnCreatesAStructuralDependencyWithoutMInference()
    {
        var withPolicy = ScanSynthetic(includePolicy: true);
        var withoutPolicy = ScanSynthetic(includePolicy: false);

        var policyUsage = Usage(withPolicy, "LastModified");
        Assert.Equal(SemanticUsageStates.StructurallyRequired, policyUsage.UsageState);
        Assert.Equal(
            "Needed by the Events incremental refresh change-detection setting",
            SemanticUsagePresentation.DescribeReason(withPolicy, policyUsage));
        Assert.Contains(withPolicy.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.IncrementalRefreshPolicy &&
            edge.FromObjectType == SemanticObjectTypes.RefreshPolicy &&
            edge.ToTable == "Events" &&
            edge.ToObjectName == "LastModified");

        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(withoutPolicy, "LastModified").UsageState);
        Assert.DoesNotContain(withoutPolicy.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.IncrementalRefreshPolicy);
    }

    [Fact]
    public void CustomPollingExpressionIsRetainedWithoutInventingAColumnDependency()
    {
        var inventory = ScanSynthetic(includePolicy: true, pollingExpression: "CustomPollingQuery");
        var policy = Assert.IsType<SemanticRefreshPolicyInventory>(
            Assert.Single(inventory.SemanticModels).Tables.Single().RefreshPolicy);

        Assert.Equal("CustomPollingQuery", policy.PollingExpression);
        Assert.Null(policy.ChangeDetectionColumn);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "LastModified").UsageState);
    }

    [Fact]
    public void HtmlShowsOnlyTheExplicitPolicyAndStatesItsEvidenceBoundary()
    {
        var html = HtmlReportRenderer.Render(ScanFixture("desktop-incremental-refresh-evidence"));

        const string heading = "<h4>Incremental refresh</h4>";
        Assert.Contains(heading, html, StringComparison.Ordinal);
        Assert.Equal(
            html.IndexOf(heading, StringComparison.Ordinal),
            html.LastIndexOf(heading, StringComparison.Ordinal));
        Assert.Contains("A refresh policy is configured for this table.", html, StringComparison.Ordinal);
        Assert.Contains("These saved settings do not confirm query folding or a successful refresh in Power BI Service.", html, StringComparison.Ordinal);
        Assert.Contains("Archive window</dt><dd>2 years", html, StringComparison.Ordinal);
        Assert.Contains("Refresh window</dt><dd>30 days", html, StringComparison.Ordinal);
        Assert.Contains("Complete periods only</dt><dd>Yes", html, StringComparison.Ordinal);
        Assert.Contains("Change detection</dt><dd>LastModified", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Real-time data</dt>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("query folding works", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefreshPolicyIsAdditiveJsonAndDoesNotChangeCsvOrFindings()
    {
        var inventory = ScanFixture("desktop-incremental-refresh-evidence");
        var json = JsonSerializer.Serialize(inventory);
        var csv = SemanticUsageCsvRenderer.Render(inventory);

        Assert.Equal("0.26", inventory.SchemaVersion);
        Assert.Contains("\"RefreshPolicy\"", json, StringComparison.Ordinal);
        Assert.Contains("\"RollingWindowPeriods\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"ChangeDetectionColumn\":\"LastModified\"", json, StringComparison.Ordinal);
        Assert.StartsWith("Report,Table,Object,ObjectType,SemanticUsage", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshPolicy", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(inventory.Findings, finding =>
            finding.RuleId.Contains("REFRESH", StringComparison.OrdinalIgnoreCase));
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, item => item.Table == "Events" && item.ObjectName == name);

    private static ProjectInventory ScanSynthetic(bool includePolicy, string? pollingExpression = null)
    {
        var policy = includePolicy
            ? $$"""

                refreshPolicy
                    policyType: basic
                    rollingWindowGranularity: year
                    rollingWindowPeriods: 2
                    incrementalGranularity: day
                    incrementalPeriods: 30
                    incrementalPeriodsOffset: -1
                    pollingExpression = {{pollingExpression ?? "List.Max(Events[LastModified])"}}
                    sourceExpression = let Source = Events in Source
            """
            : string.Empty;
        return ProjectScanner.Scan(new InMemoryProjectFileSource(
            "Incremental refresh synthetic",
            [
                File("Refresh.pbip", "{}"),
                File("Refresh.SemanticModel/definition.pbism", "{}"),
                File("Refresh.SemanticModel/definition/expressions.tmdl", """
                    expression RangeStart = #datetime(2026, 1, 1, 0, 0, 0)
                    expression RangeEnd = #datetime(2026, 2, 1, 0, 0, 0)
                    """),
                File("Refresh.SemanticModel/definition/tables/Events.tmdl", $$"""
                    table Events{{policy}}

                        column EventDate
                            dataType: dateTime

                        column LastModified
                            dataType: dateTime

                        partition Events = m
                            mode: import
                            source = Table.SelectRows(EventsSource, each [EventDate] >= RangeStart and [EventDate] < RangeEnd)
                    """),
            ]));
    }

    private static ProjectInventory ScanFixture(string fixture) => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", fixture));

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
