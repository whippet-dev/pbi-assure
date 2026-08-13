namespace PbiAssure.Core.Tests;

public sealed class WebSummarySurfaceTests
{
    [Fact]
    public void WebSummaryUsesAccurateLabelsExistingCountsAndBalancedProjectGrid()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "Pages", "Home.razor"));
        var styles = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "css", "app.css"));
        var project = ExtractBetween(markup, "<h3>Project</h3>", "<h3>Power Query</h3>");
        var powerQuery = ExtractBetween(markup, "<h3>Power Query</h3>", "<h3>Semantic usage</h3>");

        Assert.Contains("<h3>Assurance</h3>", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<h3>Findings</h3>", markup, StringComparison.Ordinal);

        Assert.Contains("Label=\"Reports\" Value=\"@inventory.ReportCount\"", project, StringComparison.Ordinal);
        Assert.Contains("Label=\"Pages\" Value=\"@inventory.PageCount\"", project, StringComparison.Ordinal);
        Assert.Contains("Label=\"Visuals\" Value=\"@inventory.VisualCount\"", project, StringComparison.Ordinal);
        Assert.Contains("Label=\"Your model objects\" Value=\"@inventory.DeveloperSemanticObjectCount\"", project, StringComparison.Ordinal);
        Assert.Contains("Label=\"System-generated model objects\" Value=\"@inventory.SystemGeneratedSemanticObjectCount\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Power Query", project, StringComparison.Ordinal);

        Assert.Contains("class=\"metrics power-query-metrics\"", powerQuery, StringComparison.Ordinal);
        Assert.Contains("Label=\"Power Query queries\" Value=\"@inventory.PowerQueryCount\"", powerQuery, StringComparison.Ordinal);
        Assert.Contains("Label=\"Data source types\" Value=\"@inventory.DistinctConnectorFamilyCount\"", powerQuery, StringComparison.Ordinal);
        Assert.Contains("Label=\"Data source references\" Value=\"@inventory.DataSourceCount\"", powerQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=\"Developer objects\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=\"System-generated objects\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Label=\"Power Query sources\"", markup, StringComparison.Ordinal);

        Assert.Contains("Power Query queries found in this project.", markup, StringComparison.Ordinal);
        Assert.Contains("Different types of recognised data sources used by those queries.", markup, StringComparison.Ordinal);
        Assert.Contains("This is not the number of individual connections.", markup, StringComparison.Ordinal);
        Assert.Contains("counted once per query and function", markup, StringComparison.Ordinal);

        Assert.Contains("Label=\"Errors\" Value=\"@inventory.ErrorFindingCount\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Warnings\" Value=\"@inventory.WarningFindingCount\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Review required\" Value=\"@inventory.ReviewRequiredCount\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Total findings\" Value=\"@inventory.FindingCount\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Directly used\" Value=\"@UsageCount(SemanticUsageStates.DirectlyUsed)\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Indirectly used\" Value=\"@UsageCount(SemanticUsageStates.IndirectlyUsed)\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Structurally required\" Value=\"@UsageCount(SemanticUsageStates.StructurallyRequired)\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Only used by unused items\" Value=\"@UsageCount(SemanticUsageStates.UsedOnlyByUnusedBranch)\"", markup, StringComparison.Ordinal);
        Assert.Contains("Label=\"Apparently unused\" Value=\"@inventory.DeveloperApparentlyUnusedSemanticObjectCount\"", markup, StringComparison.Ordinal);
        Assert.Contains("Only used by other model items that themselves have no detected report usage.", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Developer-authored model objects", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Unused branch", markup, StringComparison.Ordinal);

        Assert.Contains("class=\"metrics project-metrics\"", markup, StringComparison.Ordinal);
        Assert.Contains(".project-metrics { grid-template-columns: repeat(5, minmax(0, 1fr)); }", styles, StringComparison.Ordinal);
        Assert.Contains(".power-query-metrics { grid-template-columns: repeat(3, minmax(0, 1fr)); }", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 46rem)", styles, StringComparison.Ordinal);
        Assert.Contains(".project-metrics .metric:last-child { grid-column: 1 / -1; }", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 34rem)", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", styles, StringComparison.Ordinal);
    }

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
