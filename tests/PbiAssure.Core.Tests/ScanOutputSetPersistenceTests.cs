using System.Text;
using PbiAssure.Cli;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A run's historical output is a record of that run. Two scans a second apart resolved to the same
/// second-resolution filename, so the later one replaced the earlier without saying so; and the HTML
/// report and its CSV were written in turn, so a failure between them left a report with no CSV beside
/// it. Both are now one set with one identity.
/// </summary>
public sealed class ScanOutputSetPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "pbiassure-output-set", Guid.NewGuid().ToString("N"));

    private static readonly DateTime ScanTime = new(2026, 8, 9, 9, 55, 32, DateTimeKind.Unspecified);

    [Fact]
    public async Task TwoRunsInTheSameSecondKeepBothHistories()
    {
        await WriteRunAsync("first html", "first csv");
        await WriteRunAsync("second html", "second csv");

        var reports = Directory.GetFiles(Outputs, "assurance_*.pbiassure.html");
        Assert.Equal(
            ["assurance_2026-08-09_09-55-32-2.pbiassure.html", "assurance_2026-08-09_09-55-32.pbiassure.html"],
            reports.Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray());
        // Neither run overwrote the other.
        Assert.Equal("first html", await File.ReadAllTextAsync(
            Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.pbiassure.html")));
        Assert.Equal("second html", await File.ReadAllTextAsync(
            Path.Combine(Outputs, "assurance_2026-08-09_09-55-32-2.pbiassure.html")));
    }

    [Fact]
    public async Task ThePairFromOneRunSharesOneRunIdentity()
    {
        await WriteRunAsync("first html", "first csv");
        await WriteRunAsync("second html", "second csv");

        foreach (var runId in new[] { "2026-08-09_09-55-32", "2026-08-09_09-55-32-2" })
        {
            Assert.True(File.Exists(Path.Combine(Outputs, $"assurance_{runId}.pbiassure.html")));
            Assert.True(File.Exists(Path.Combine(Outputs, $"assurance_{runId}.semantic-usage.csv")));
        }

        // The second run's report and CSV describe the same run, not a mix of the two.
        Assert.Equal("second csv", await File.ReadAllTextAsync(
            Path.Combine(Outputs, "assurance_2026-08-09_09-55-32-2.semantic-usage.csv")));
    }

    [Fact]
    public async Task ASuccessfulRunProducesTheCompleteSet()
    {
        await WriteRunAsync("report", "usage");

        Assert.Equal(
            [
                "assurance_2026-08-09_09-55-32.pbiassure.html",
                "assurance_2026-08-09_09-55-32.semantic-usage.csv",
                "latest.pbiassure.html",
                "latest.semantic-usage.csv",
            ],
            Directory.GetFiles(Outputs).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task AFailureWritingTheSecondFileLeavesNoHistoricalOutputAtAll()
    {
        // A directory where the CSV should go: writing the staged file there cannot succeed.
        Directory.CreateDirectory(Outputs);
        var blocked = Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.semantic-usage.csv");
        Directory.CreateDirectory(blocked);

        await Assert.ThrowsAnyAsync<IOException>(() => WriteRunAsync("report", "usage"));

        // The report is not left behind claiming to be a complete record of the run.
        Assert.False(File.Exists(Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.pbiassure.html")));
        Assert.False(File.Exists(Path.Combine(Outputs, "latest.pbiassure.html")));
    }

    [Fact]
    public async Task StagedFilesAreCleanedUpAfterAFailure()
    {
        Directory.CreateDirectory(Outputs);
        Directory.CreateDirectory(Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.semantic-usage.csv"));

        await Assert.ThrowsAnyAsync<IOException>(() => WriteRunAsync("report", "usage"));

        Assert.Empty(Directory.GetFiles(Outputs, "*.pbiassure-staging-*", SearchOption.AllDirectories));
    }

    /// <summary>
    /// The run's own files are the record and must survive; the latest mirrors are a convenience. If
    /// one mirror cannot be updated, keeping the other would leave a new report beside a stale CSV —
    /// the exact mismatch this work exists to prevent — so both go.
    /// </summary>
    [Fact]
    public async Task AFailedLatestUpdateKeepsTheRunButLeavesNoMismatchedMirrors()
    {
        await WriteRunAsync("old html", "old csv");
        // A directory in place of the latest CSV: its move cannot succeed, but everything before it can.
        File.Delete(Path.Combine(Outputs, "latest.semantic-usage.csv"));
        Directory.CreateDirectory(Path.Combine(Outputs, "latest.semantic-usage.csv"));

        var failure = await Assert.ThrowsAsync<ScanLatestOutputException>(
            () => WriteRunAsync("new html", "new csv"));

        // The second run's record is complete and is this run's content.
        var html = Path.Combine(Outputs, "assurance_2026-08-09_09-55-32-2.pbiassure.html");
        var csv = Path.Combine(Outputs, "assurance_2026-08-09_09-55-32-2.semantic-usage.csv");
        Assert.True(File.Exists(html));
        Assert.True(File.Exists(csv));
        Assert.Equal("new html", await File.ReadAllTextAsync(html));
        Assert.Equal("new csv", (await File.ReadAllTextAsync(csv)).TrimStart('﻿'));

        // No mirror survives, so no pair of them can disagree.
        Assert.False(File.Exists(Path.Combine(Outputs, "latest.pbiassure.html")));
        Assert.False(File.Exists(Path.Combine(Outputs, "latest.semantic-usage.csv")));
        Assert.Empty(Directory.GetFiles(Outputs, "*.pbiassure-staging-*", SearchOption.AllDirectories));

        // The failure says what happened rather than reporting a complete update.
        Assert.Contains("were saved", failure.Message, StringComparison.Ordinal);
        Assert.Contains("latest files could not be updated", failure.Message, StringComparison.Ordinal);
        Assert.NotNull(failure.InnerException);
    }

    [Fact]
    public async Task TheFirstRunSurvivesASecondRunsFailedLatestUpdate()
    {
        await WriteRunAsync("first html", "first csv");
        File.Delete(Path.Combine(Outputs, "latest.semantic-usage.csv"));
        Directory.CreateDirectory(Path.Combine(Outputs, "latest.semantic-usage.csv"));

        await Assert.ThrowsAsync<ScanLatestOutputException>(() => WriteRunAsync("second html", "second csv"));

        // Both runs keep their own record; only the shared mirrors were given up.
        Assert.Equal("first html", await File.ReadAllTextAsync(
            Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.pbiassure.html")));
        Assert.Equal("second html", await File.ReadAllTextAsync(
            Path.Combine(Outputs, "assurance_2026-08-09_09-55-32-2.pbiassure.html")));
    }

    [Fact]
    public async Task ContentsAndCsvByteOrderMarkAreUnchanged()
    {
        await WriteRunAsync("<html>report</html>", "Header\r\nvalue\r\n");

        var html = await File.ReadAllBytesAsync(Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.pbiassure.html"));
        var csv = await File.ReadAllBytesAsync(Path.Combine(Outputs, "assurance_2026-08-09_09-55-32.semantic-usage.csv"));

        // HTML has no BOM; the CSV keeps the UTF-8 BOM spreadsheets rely on.
        Assert.Equal(Encoding.UTF8.GetBytes("<html>report</html>"), html);
        Assert.Equal([0xEF, 0xBB, 0xBF], csv[..3]);
        Assert.Equal("Header\r\nvalue\r\n", Encoding.UTF8.GetString(csv[3..]));
        // The latest mirrors are byte-identical to the historical files.
        Assert.Equal(html, await File.ReadAllBytesAsync(Path.Combine(Outputs, "latest.pbiassure.html")));
        Assert.Equal(csv, await File.ReadAllBytesAsync(Path.Combine(Outputs, "latest.semantic-usage.csv")));
    }

    [Fact]
    public async Task OrdinaryNamesStayReadableAndOnlyGainASuffixWhenNeeded()
    {
        await WriteRunAsync("report", "usage");
        var first = DefaultScanOutputPath.CreateRunId(root, ScanTime);

        await WriteRunAsync("report", "usage");
        var second = DefaultScanOutputPath.CreateRunId(root, ScanTime);

        // The first run of a second reads as a plain timestamp; only a repeat is disambiguated.
        Assert.Equal("2026-08-09_09-55-32-2", first);
        Assert.Equal("2026-08-09_09-55-32-3", second);
        Assert.Equal("2026-08-09_09-55-32", DefaultScanOutputPath.CreateRunId(
            Path.Combine(root, "never-scanned"), ScanTime));
        Assert.DoesNotContain(':', Path.GetFileName(
            DefaultScanOutputPath.Create(root, ScanTime, OutputFormat.Html)));
    }

    [Fact]
    public async Task AnExplicitOutputPathIsStillTheCallersToReplace()
    {
        var explicitPath = Path.Combine(root, "review", "custom.html");
        var plan = DefaultScanOutputPath.ResolvePlan(explicitPath, root, ScanTime, OutputFormat.Html);

        await ScanOutputWriter.WriteAsync(plan, "first");
        await ScanOutputWriter.WriteAsync(plan, "second");

        Assert.False(plan.IsGeneratedName);
        Assert.Equal("second", await File.ReadAllTextAsync(explicitPath));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private string Outputs => Path.Combine(root, "outputs");

    /// <summary>Mirrors how the CLI writes a default scan: one run identity, both files as one set.</summary>
    private async Task WriteRunAsync(string html, string csv)
    {
        var runId = DefaultScanOutputPath.CreateRunId(root, ScanTime);
        await ScanOutputWriter.WriteSetAsync(
        [
            new ScanOutputFile(
                DefaultScanOutputPath.ResolvePlan(null, root, ScanTime, OutputFormat.Html, runId), html),
            new ScanOutputFile(
                DefaultScanOutputPath.ResolvePlan(null, root, ScanTime, OutputFormat.SemanticUsageCsv, runId),
                csv,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)),
        ]);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
