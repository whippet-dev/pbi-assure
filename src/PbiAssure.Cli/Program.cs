using System.Text.Json;
using PbiAssure.Cli;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    if (arguments.Length == 0 || arguments[0] is "--help" or "-h")
    {
        WriteUsage(Console.Out);
        return 0;
    }

    if (!string.Equals(arguments[0], "scan", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Unknown command: {arguments[0]}");
        WriteUsage(Console.Error);
        return 2;
    }

    if (!TryParseScanArguments(arguments[1..], out var projectPath, out var outputPath, out var requestedFormat, out var error))
    {
        Console.Error.WriteLine(error);
        WriteUsage(Console.Error);
        return 2;
    }

    try
    {
        var inventory = ProjectScanner.Scan(projectPath!);
        var format = outputPath is null
            ? requestedFormat ?? OutputFormat.Html
            : ResolveFormat(requestedFormat, outputPath);
        var localScanTime = DateTime.Now;
        if (outputPath is null && format == OutputFormat.Html)
        {
            var outputResult = await AssuranceOutputWriter.WriteDefaultOutputsAsync(inventory, projectPath!, localScanTime);
            Console.Out.WriteLine($"HTML report written to {Path.GetFullPath(outputResult.HtmlOutput.HistoricalPath)}");
            Console.Out.WriteLine($"Latest HTML report updated at {Path.GetFullPath(outputResult.HtmlOutput.LatestPath!)}");
            if (outputResult.SemanticUsageCsvOutput is not null)
            {
                Console.Out.WriteLine($"Semantic usage CSV written to {Path.GetFullPath(outputResult.SemanticUsageCsvOutput.HistoricalPath)}");
                Console.Out.WriteLine($"Latest semantic usage CSV updated at {Path.GetFullPath(outputResult.SemanticUsageCsvOutput.LatestPath!)}");
                return 0;
            }

            Console.Error.WriteLine($"HTML report was created, but the semantic usage CSV could not be created: {outputResult.SemanticUsageCsvError}");
            return 1;
        }

        var outputPlan = DefaultScanOutputPath.ResolvePlan(outputPath, projectPath!, localScanTime, format);
        var content = format switch
        {
            OutputFormat.Html => HtmlReportRenderer.Render(inventory),
            OutputFormat.SemanticUsageCsv => SemanticUsageCsvRenderer.Render(inventory),
            _ => JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
        };
        await ScanOutputWriter.WriteAsync(
            outputPlan,
            content,
            format == OutputFormat.SemanticUsageCsv ? new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true) : null);
        Console.Out.WriteLine($"{OutputDescription(format)} written to {Path.GetFullPath(outputPlan.HistoricalPath)}");
        if (outputPlan.LatestPath is not null)
        {
            Console.Out.WriteLine($"Latest {OutputDescription(format).ToLowerInvariant()} updated at {Path.GetFullPath(outputPlan.LatestPath)}");
        }

        return 0;
    }
    catch (Exception exception) when (exception is UnsupportedProjectInputException or ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static bool TryParseScanArguments(
    string[] arguments,
    out string? projectPath,
    out string? outputPath,
    out OutputFormat? requestedFormat,
    out string? error)
{
    projectPath = null;
    outputPath = null;
    requestedFormat = null;
    error = null;

    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (argument is "--output" or "-o")
        {
            if (++index >= arguments.Length)
            {
                error = "The --output option requires a file path.";
                return false;
            }

            outputPath = arguments[index];
            continue;
        }

        if (argument is "--format" or "-f")
        {
            if (++index >= arguments.Length)
            {
                error = "The --format option requires json, html or csv.";
                return false;
            }

            requestedFormat = arguments[index].ToLowerInvariant() switch
            {
                "json" => OutputFormat.Json,
                "html" => OutputFormat.Html,
                "csv" => OutputFormat.SemanticUsageCsv,
                _ => null
            };

            if (requestedFormat is null)
            {
                error = $"Unsupported output format: {arguments[index]}. Use json, html or csv.";
                return false;
            }

            continue;
        }

        if (argument.StartsWith('-'))
        {
            error = $"Unknown option: {argument}";
            return false;
        }

        if (projectPath is not null)
        {
            error = "Only one project directory can be scanned at a time.";
            return false;
        }

        projectPath = argument;
    }

    if (projectPath is null)
    {
        error = "A PBIP project directory is required.";
        return false;
    }

    return true;
}

static OutputFormat ResolveFormat(OutputFormat? requestedFormat, string? outputPath)
{
    if (requestedFormat is not null)
    {
        return requestedFormat.Value;
    }

    ArgumentNullException.ThrowIfNull(outputPath);
    return Path.GetExtension(outputPath).ToLowerInvariant() switch
    {
        ".html" => OutputFormat.Html,
        ".csv" => OutputFormat.SemanticUsageCsv,
        _ => OutputFormat.Json,
    };
}

static string OutputDescription(OutputFormat format) => format switch
{
    OutputFormat.Html => "HTML report",
    OutputFormat.SemanticUsageCsv => "Semantic usage CSV",
    _ => "JSON inventory",
};

static void WriteUsage(TextWriter writer)
{
    writer.WriteLine("PBI Assure - read-only Power BI project assurance");
    writer.WriteLine();
    writer.WriteLine("Usage:");
    writer.WriteLine("  pbiassure scan <project-directory> [--output <file>] [--format json|html|csv]");
    writer.WriteLine();
    writer.WriteLine("Without --output, an HTML report and semantic-usage CSV are saved in outputs/ beside the project.");
    writer.WriteLine("The output format defaults to HTML for .html files, CSV for .csv files, and JSON otherwise.");
}
