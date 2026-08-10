using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Reporting;

namespace PbiAssure.Cli;

public static class AssuranceOutputWriter
{
    public static async Task<AssuranceOutputResult> WriteDefaultOutputsAsync(
        ProjectInventory inventory,
        string projectPath,
        DateTime localScanTime)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var htmlOutput = DefaultScanOutputPath.ResolvePlan(null, projectPath, localScanTime, OutputFormat.Html);
        await ScanOutputWriter.WriteAsync(htmlOutput, HtmlReportRenderer.Render(inventory));

        var csvOutput = DefaultScanOutputPath.ResolvePlan(null, projectPath, localScanTime, OutputFormat.SemanticUsageCsv);
        try
        {
            await ScanOutputWriter.WriteAsync(
                csvOutput,
                SemanticUsageCsvRenderer.Render(inventory),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return new AssuranceOutputResult(htmlOutput, csvOutput, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
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
}
