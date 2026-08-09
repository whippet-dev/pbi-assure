namespace PbiAssure.Cli;

public static class ScanOutputWriter
{
    public static async Task WriteAsync(ScanOutputPlan outputPlan, string content)
    {
        ArgumentNullException.ThrowIfNull(outputPlan);
        ArgumentNullException.ThrowIfNull(content);

        await WriteFileAsync(outputPlan.HistoricalPath, content);

        if (outputPlan.LatestPath is not null)
        {
            await WriteFileAsync(outputPlan.LatestPath, content);
        }
    }

    private static async Task WriteFileAsync(string outputPath, string content)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(fullOutputPath, content);
    }
}
