namespace PbiAssure.Cli;

public static class DefaultScanOutputPath
{
    public static ScanOutputPlan ResolvePlan(string? outputPath, string projectPath, DateTime localScanTime, OutputFormat format)
    {
        if (outputPath is not null)
        {
            return new ScanOutputPlan(outputPath, null);
        }

        var historicalPath = Create(projectPath, localScanTime, format);
        var latestPath = format switch
        {
            OutputFormat.Html => Path.Combine(Path.GetDirectoryName(historicalPath)!, "latest.pbiassure.html"),
            OutputFormat.SemanticUsageCsv => Path.Combine(Path.GetDirectoryName(historicalPath)!, "latest.semantic-usage.csv"),
            _ => null,
        };

        return new ScanOutputPlan(historicalPath, latestPath);
    }

    public static string Resolve(string? outputPath, string projectPath, DateTime localScanTime, OutputFormat format)
    {
        return ResolvePlan(outputPath, projectPath, localScanTime, format).HistoricalPath;
    }

    public static string Create(string projectPath, DateTime localScanTime, OutputFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var fullProjectPath = Path.GetFullPath(projectPath);
        if (format == OutputFormat.Html)
        {
            var timestamp = localScanTime.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
            return Path.Combine(fullProjectPath, "outputs", $"assurance_{timestamp}.pbiassure.html");
        }

        if (format == OutputFormat.SemanticUsageCsv)
        {
            var timestamp = localScanTime.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
            return Path.Combine(fullProjectPath, "outputs", $"assurance_{timestamp}.semantic-usage.csv");
        }

        var jsonTimestamp = localScanTime.ToString("yyyy-MM-dd_HH-mm-ss-fff", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(fullProjectPath, "outputs", jsonTimestamp, "inventory.pbiassure.json");
    }
}

public sealed record ScanOutputPlan(string HistoricalPath, string? LatestPath);

public enum OutputFormat
{
    Json,
    Html,
    SemanticUsageCsv,
}
