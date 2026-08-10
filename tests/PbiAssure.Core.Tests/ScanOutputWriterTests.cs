using PbiAssure.Cli;

namespace PbiAssure.Core.Tests;

public sealed class ScanOutputWriterTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"pbi-assure-output-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task AutomaticHtmlWritesPreserveHistoryAndUpdateLatestInSharedDirectory()
    {
        var firstPlan = DefaultScanOutputPath.ResolvePlan(
            null,
            tempDirectory,
            new DateTime(2026, 8, 9, 19, 28, 0),
            OutputFormat.Html);
        var secondPlan = DefaultScanOutputPath.ResolvePlan(
            null,
            tempDirectory,
            new DateTime(2026, 8, 9, 20, 14, 32),
            OutputFormat.Html);

        await ScanOutputWriter.WriteAsync(firstPlan, "first report");
        await ScanOutputWriter.WriteAsync(secondPlan, "second report");

        Assert.Equal("first report", await File.ReadAllTextAsync(firstPlan.HistoricalPath));
        Assert.Equal("second report", await File.ReadAllTextAsync(secondPlan.HistoricalPath));
        Assert.Equal("second report", await File.ReadAllTextAsync(secondPlan.LatestPath!));
        Assert.Equal(
            Path.GetDirectoryName(firstPlan.HistoricalPath),
            Path.GetDirectoryName(secondPlan.HistoricalPath));
    }

    [Fact]
    public async Task ExplicitOutputWritesOnlyTheRequestedFile()
    {
        var explicitPath = Path.Combine(tempDirectory, "review", "custom.html");
        var outputPlan = DefaultScanOutputPath.ResolvePlan(
            explicitPath,
            tempDirectory,
            new DateTime(2026, 8, 9, 20, 14, 32),
            OutputFormat.Html);

        await ScanOutputWriter.WriteAsync(outputPlan, "custom report");

        Assert.Equal("custom report", await File.ReadAllTextAsync(explicitPath));
        Assert.Null(outputPlan.LatestPath);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(explicitPath)!));
    }

    [Fact]
    public async Task AutomaticSemanticUsageCsvWritesPreserveHistoryAndUpdateLatest()
    {
        var firstPlan = DefaultScanOutputPath.ResolvePlan(
            null,
            tempDirectory,
            new DateTime(2026, 8, 10, 15, 30, 0),
            OutputFormat.SemanticUsageCsv);
        var secondPlan = DefaultScanOutputPath.ResolvePlan(
            null,
            tempDirectory,
            new DateTime(2026, 8, 10, 15, 31, 0),
            OutputFormat.SemanticUsageCsv);

        await ScanOutputWriter.WriteAsync(firstPlan, "first csv");
        await ScanOutputWriter.WriteAsync(secondPlan, "second csv");

        Assert.Equal("first csv", await File.ReadAllTextAsync(firstPlan.HistoricalPath));
        Assert.Equal("second csv", await File.ReadAllTextAsync(secondPlan.HistoricalPath));
        Assert.Equal("second csv", await File.ReadAllTextAsync(secondPlan.LatestPath!));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
