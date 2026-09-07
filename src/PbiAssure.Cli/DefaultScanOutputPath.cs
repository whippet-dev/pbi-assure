namespace PbiAssure.Cli;

public static class DefaultScanOutputPath
{
    /// <summary>
    /// Historical names are timestamped to the second, so two scans of one project inside the same
    /// second would otherwise resolve to the same file and the later run would replace the earlier
    /// one. A short counter is appended only when that second is already taken, so an ordinary run
    /// keeps the plain readable name.
    /// </summary>
    private const int MaxRunsPerSecond = 1000;

    /// <summary>
    /// The identity every file in one run's historical output set shares. Resolve it once per run and
    /// pass it to each format, so the HTML report and the CSV beside it always name the same run.
    /// </summary>
    public static string CreateRunId(string projectPath, DateTime localScanTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var timestamp = Timestamp(localScanTime);
        var outputsDirectory = Path.Combine(Path.GetFullPath(projectPath), "outputs");
        if (!Directory.Exists(outputsDirectory))
        {
            return timestamp;
        }

        for (var attempt = 1; attempt <= MaxRunsPerSecond; attempt++)
        {
            var candidate = attempt == 1 ? timestamp : $"{timestamp}-{attempt}";
            if (!Directory.EnumerateFiles(outputsDirectory, $"assurance_{candidate}.*").Any())
            {
                return candidate;
            }
        }

        throw new IOException(
            $"This project already has {MaxRunsPerSecond:N0} outputs recorded for {timestamp}.");
    }

    public static ScanOutputPlan ResolvePlan(string? outputPath, string projectPath, DateTime localScanTime, OutputFormat format) =>
        ResolvePlan(outputPath, projectPath, localScanTime, format, runId: null);

    public static ScanOutputPlan ResolvePlan(
        string? outputPath,
        string projectPath,
        DateTime localScanTime,
        OutputFormat format,
        string? runId)
    {
        if (outputPath is not null)
        {
            // An explicitly requested path belongs to the caller, including whether to replace it.
            return new ScanOutputPlan(outputPath, null);
        }

        var historicalPath = Create(projectPath, localScanTime, format, runId);
        var latestPath = format switch
        {
            OutputFormat.Html => Path.Combine(Path.GetDirectoryName(historicalPath)!, "latest.pbiassure.html"),
            OutputFormat.SemanticUsageCsv => Path.Combine(Path.GetDirectoryName(historicalPath)!, "latest.semantic-usage.csv"),
            _ => null,
        };

        return new ScanOutputPlan(historicalPath, latestPath) { IsGeneratedName = true };
    }

    public static string Resolve(string? outputPath, string projectPath, DateTime localScanTime, OutputFormat format)
    {
        return ResolvePlan(outputPath, projectPath, localScanTime, format).HistoricalPath;
    }

    public static string Create(string projectPath, DateTime localScanTime, OutputFormat format) =>
        Create(projectPath, localScanTime, format, runId: null);

    public static string Create(string projectPath, DateTime localScanTime, OutputFormat format, string? runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var fullProjectPath = Path.GetFullPath(projectPath);
        if (format == OutputFormat.Html)
        {
            var id = runId ?? CreateRunId(projectPath, localScanTime);
            return Path.Combine(fullProjectPath, "outputs", $"assurance_{id}.pbiassure.html");
        }

        if (format == OutputFormat.SemanticUsageCsv)
        {
            var id = runId ?? CreateRunId(projectPath, localScanTime);
            return Path.Combine(fullProjectPath, "outputs", $"assurance_{id}.semantic-usage.csv");
        }

        var jsonTimestamp = localScanTime.ToString("yyyy-MM-dd_HH-mm-ss-fff", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(fullProjectPath, "outputs", jsonTimestamp, "inventory.pbiassure.json");
    }

    private static string Timestamp(DateTime localScanTime) =>
        localScanTime.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record ScanOutputPlan(string HistoricalPath, string? LatestPath)
{
    /// <summary>
    /// True when PBI Assure chose the historical name. Such a file records one run and is never
    /// replaced; an explicitly requested path is the caller's to overwrite as they see fit.
    /// </summary>
    public bool IsGeneratedName { get; init; }
}

public enum OutputFormat
{
    Json,
    Html,
    SemanticUsageCsv,
}
