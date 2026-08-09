using PbiAssure.Cli;

namespace PbiAssure.Core.Tests;

public sealed class DefaultScanOutputPathTests
{
    [Fact]
    public void CreateUsesProjectOutputsFolderAndSortableLocalTimestampInHtmlFilename()
    {
        var projectPath = Path.Combine("C:", "Projects", "pbi-assure", "samples-local", "Columns Usage");
        var scanTime = new DateTime(2026, 8, 9, 9, 55, 32, DateTimeKind.Unspecified);

        var outputPath = DefaultScanOutputPath.Create(projectPath, scanTime, OutputFormat.Html);

        Assert.Equal(
            Path.Combine(projectPath, "outputs", "assurance_2026-08-09_09-55-32.pbiassure.html"),
            outputPath);
        Assert.Equal("outputs", Path.GetFileName(Path.GetDirectoryName(outputPath)));
        Assert.DoesNotContain(':', Path.GetFileName(outputPath));
    }

    [Fact]
    public void CreateUsesJsonFilenameWhenJsonIsRequested()
    {
        var outputPath = DefaultScanOutputPath.Create("Test Project", new DateTime(2026, 8, 9, 9, 55, 32), OutputFormat.Json);

        Assert.EndsWith(Path.Combine("outputs", "2026-08-09_09-55-32-000", "inventory.pbiassure.json"), outputPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePreservesAnExplicitOutputPath()
    {
        var outputPath = DefaultScanOutputPath.Resolve(
            "review\\custom.html",
            "Test Project",
            new DateTime(2026, 8, 9, 9, 55, 32),
            OutputFormat.Html);

        Assert.Equal("review\\custom.html", outputPath);
    }

    [Fact]
    public void ResolvePlanAddsStableLatestPathOnlyForAutomaticHtmlOutput()
    {
        var outputPlan = DefaultScanOutputPath.ResolvePlan(
            null,
            "Test Project",
            new DateTime(2026, 8, 9, 20, 14, 32),
            OutputFormat.Html);

        Assert.EndsWith(
            Path.Combine("Test Project", "outputs", "assurance_2026-08-09_20-14-32.pbiassure.html"),
            outputPlan.HistoricalPath,
            StringComparison.Ordinal);
        Assert.EndsWith(
            Path.Combine("Test Project", "outputs", "latest.pbiassure.html"),
            outputPlan.LatestPath,
            StringComparison.Ordinal);
        Assert.Equal(
            Path.GetDirectoryName(outputPlan.HistoricalPath),
            Path.GetDirectoryName(outputPlan.LatestPath));
    }

    [Fact]
    public void ResolvePlanDoesNotAddLatestPathForExplicitOutput()
    {
        var outputPlan = DefaultScanOutputPath.ResolvePlan(
            "review\\custom.html",
            "Test Project",
            new DateTime(2026, 8, 9, 20, 14, 32),
            OutputFormat.Html);

        Assert.Equal("review\\custom.html", outputPlan.HistoricalPath);
        Assert.Null(outputPlan.LatestPath);
    }
}
