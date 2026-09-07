using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Reporting;

namespace PbiAssure.Cli;

public static class AssuranceOutputWriter
{
    /// <summary>
    /// Writes the HTML report and the semantic usage CSV a default scan produces.
    ///
    /// They describe one run and are written as one set, sharing a single run identity, so a project's
    /// output history never contains a report whose CSV is missing or belongs to a different scan.
    /// </summary>
    public static async Task<AssuranceOutputResult> WriteDefaultOutputsAsync(
        ProjectInventory inventory,
        string projectPath,
        DateTime localScanTime)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        // Resolved once so both files name the same run even when an earlier run already used this
        // second.
        var runId = DefaultScanOutputPath.CreateRunId(projectPath, localScanTime);
        var htmlOutput = DefaultScanOutputPath.ResolvePlan(null, projectPath, localScanTime, OutputFormat.Html, runId);
        var csvOutput = DefaultScanOutputPath.ResolvePlan(null, projectPath, localScanTime, OutputFormat.SemanticUsageCsv, runId);

        try
        {
            await ScanOutputWriter.WriteSetAsync(
            [
                new ScanOutputFile(htmlOutput, HtmlReportRenderer.Render(inventory)),
                new ScanOutputFile(csvOutput, SemanticUsageCsvRenderer.Render(inventory),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)),
            ]);
            return new AssuranceOutputResult(htmlOutput, csvOutput, null);
        }
        catch (ScanLatestOutputException exception)
        {
            // The timestamped pair is on disk and complete; the mirrors are not there at all. Reporting
            // no latest path keeps every caller pointing at a file that exists.
            return new AssuranceOutputResult(
                htmlOutput with { LatestPath = null },
                csvOutput with { LatestPath = null },
                null)
            {
                LatestUpdateError = exception.Message,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing was written: the set is committed together or not at all.
            return new AssuranceOutputResult(htmlOutput, null, exception.Message);
        }
    }
}

public sealed record AssuranceOutputResult(
    ScanOutputPlan HtmlOutput,
    ScanOutputPlan? SemanticUsageCsvOutput,
    string? SemanticUsageCsvError)
{
    public bool HasSemanticUsageCsv => SemanticUsageCsvOutput is not null;

    /// <summary>
    /// Set when this run's timestamped report and CSV were written, but the latest files were not
    /// updated and were removed. The run's output is complete and available under its own name.
    /// </summary>
    public string? LatestUpdateError { get; init; }
}
