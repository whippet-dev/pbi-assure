namespace PbiAssure.Cli;

public static class DefaultScanOutputPath
{
    public static string Resolve(string? outputPath, string projectPath, DateTime localScanTime, OutputFormat format)
    {
        return outputPath ?? Create(projectPath, localScanTime, format);
    }

    public static string Create(string projectPath, DateTime localScanTime, OutputFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var fullProjectPath = Path.GetFullPath(projectPath);
        var timestamp = localScanTime.ToString("yyyy-MM-dd_HH-mm-ss-fff", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = format == OutputFormat.Html
            ? "assurance.pbiassure.html"
            : "inventory.pbiassure.json";

        return Path.Combine(fullProjectPath, "outputs", timestamp, fileName);
    }
}

public enum OutputFormat
{
    Json,
    Html
}
