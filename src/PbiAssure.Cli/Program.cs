using System.Text.Json;
using PbiAssure.Core.Scanning;

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

    if (!TryParseScanArguments(arguments[1..], out var projectPath, out var outputPath, out var error))
    {
        Console.Error.WriteLine(error);
        WriteUsage(Console.Error);
        return 2;
    }

    try
    {
        var inventory = ProjectScanner.Scan(projectPath!);
        var json = JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true });

        if (outputPath is null)
        {
            Console.Out.WriteLine(json);
        }
        else
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(fullOutputPath, json + Environment.NewLine);
            Console.Out.WriteLine($"Inventory written to {fullOutputPath}");
        }

        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static bool TryParseScanArguments(
    string[] arguments,
    out string? projectPath,
    out string? outputPath,
    out string? error)
{
    projectPath = null;
    outputPath = null;
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

static void WriteUsage(TextWriter writer)
{
    writer.WriteLine("PBI Assure - read-only Power BI project assurance");
    writer.WriteLine();
    writer.WriteLine("Usage:");
    writer.WriteLine("  pbiassure scan <project-directory> [--output <file>]");
}
