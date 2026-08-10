using System.Text;

namespace PbiAssure.Cli;

public static class ScanOutputWriter
{
    public static async Task WriteAsync(ScanOutputPlan outputPlan, string content, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(outputPlan);
        ArgumentNullException.ThrowIfNull(content);

        await WriteFileAsync(outputPlan.HistoricalPath, content, encoding);

        if (outputPlan.LatestPath is not null)
        {
            await WriteFileAsync(outputPlan.LatestPath, content, encoding);
        }
    }

    private static async Task WriteFileAsync(string outputPath, string content, Encoding? encoding)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(fullOutputPath, content, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
