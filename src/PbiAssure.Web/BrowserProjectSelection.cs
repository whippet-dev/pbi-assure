using PbiAssure.Core.Scanning;

namespace PbiAssure.Web;

public sealed record BrowserProjectSelectionLimits(
    int MaxVisitedEntries,
    int MaxAcceptedFiles,
    long MaxFileBytes,
    long MaxTotalBytes,
    int MaxDirectoryDepth)
{
    public static BrowserProjectSelectionLimits Default { get; } = new(
        MaxVisitedEntries: 10_000,
        MaxAcceptedFiles: 5_000,
        MaxFileBytes: 25L * 1024 * 1024,
        MaxTotalBytes: 100L * 1024 * 1024,
        MaxDirectoryDepth: 64);
}

public sealed record BrowserProjectFileManifest(string RelativePath, long Length);

public sealed record BrowserProjectSelection(
    string DisplayName,
    List<BrowserProjectFileManifest> Files,
    long TotalBytes,
    int VisitedEntries,
    int MaximumDepth);

public sealed class BrowserProjectSelectionException : Exception
{
    public BrowserProjectSelectionException(string message)
        : base(message)
    {
    }

    public BrowserProjectSelectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class BrowserProjectSelectionValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pbip", ".pbir", ".json", ".tmdl", ".bim", ".pbism",
    };

    public static BrowserProjectSelection Validate(
        BrowserProjectSelection selection,
        BrowserProjectSelectionLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        limits ??= BrowserProjectSelectionLimits.Default;

        if (selection.VisitedEntries > limits.MaxVisitedEntries)
        {
            throw new BrowserProjectSelectionException(
                $"That folder contains more than {limits.MaxVisitedEntries:N0} items. Choose the Power BI project folder itself.");
        }

        if (selection.MaximumDepth > limits.MaxDirectoryDepth)
        {
            throw new BrowserProjectSelectionException(
                $"That project is nested more than {limits.MaxDirectoryDepth:N0} folders deep and cannot be opened safely.");
        }

        if (selection.Files.Count > limits.MaxAcceptedFiles)
        {
            throw new BrowserProjectSelectionException(
                $"That project contains more than {limits.MaxAcceptedFiles:N0} metadata files and is too large for this browser version.");
        }

        var normalizedFiles = new List<BrowserProjectFileManifest>(selection.Files.Count);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var file in selection.Files)
        {
            string path;
            try
            {
                path = ProjectFilePaths.Normalize(file.RelativePath);
            }
            catch (ArgumentException exception)
            {
                throw new BrowserProjectSelectionException("The selected project contains an invalid file path.", exception);
            }

            if (!paths.Add(path))
            {
                throw new BrowserProjectSelectionException(
                    $"The selected project contains duplicate file paths that differ only by letter case: {path}");
            }

            if (file.Length < 0)
            {
                throw new BrowserProjectSelectionException("The selected project contains a file with an invalid size.");
            }

            if (file.Length > limits.MaxFileBytes)
            {
                throw new BrowserProjectSelectionException(
                    $"The metadata file {ProjectFilePaths.GetFileName(path)} is larger than {FormatMiB(limits.MaxFileBytes)} MiB.");
            }

            totalBytes = checked(totalBytes + file.Length);
            if (totalBytes > limits.MaxTotalBytes)
            {
                throw new BrowserProjectSelectionException(
                    $"That project contains more than {FormatMiB(limits.MaxTotalBytes)} MiB of metadata and is too large for this browser version.");
            }

            var depth = path.Count(character => character == '/');
            if (depth > limits.MaxDirectoryDepth)
            {
                throw new BrowserProjectSelectionException(
                    $"That project is nested more than {limits.MaxDirectoryDepth:N0} folders deep and cannot be opened safely.");
            }

            if (!AllowedExtensions.Contains(Path.GetExtension(path)) || !IsProjectPath(path))
            {
                throw new BrowserProjectSelectionException("The selected folder contains unexpected project metadata paths.");
            }

            normalizedFiles.Add(new BrowserProjectFileManifest(path, file.Length));
        }

        var projectFiles = normalizedFiles
            .Where(file => !file.RelativePath.Contains('/') &&
                file.RelativePath.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (projectFiles.Length == 0)
        {
            throw new BrowserProjectSelectionException(
                "No Power BI project was found. Choose the folder that directly contains the .pbip file.");
        }

        if (projectFiles.Length > 1)
        {
            throw new BrowserProjectSelectionException(
                "More than one Power BI project was found. Choose one project folder at a time.");
        }

        if (!normalizedFiles.Any(file => file.RelativePath.Contains('/') &&
                (TopDirectory(file.RelativePath).EndsWith(".Report", StringComparison.OrdinalIgnoreCase) ||
                 TopDirectory(file.RelativePath).EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase))))
        {
            throw new BrowserProjectSelectionException(
                "The Power BI project does not contain readable report or semantic-model metadata.");
        }

        return selection with
        {
            Files = normalizedFiles,
            TotalBytes = totalBytes,
        };
    }

    private static bool IsProjectPath(string path)
    {
        if (!path.Contains('/'))
        {
            return path.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase);
        }

        var topDirectory = TopDirectory(path);
        return topDirectory.EndsWith(".Report", StringComparison.OrdinalIgnoreCase) ||
               topDirectory.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase);
    }

    private static string TopDirectory(string path) => path[..path.IndexOf('/')];

    private static long FormatMiB(long bytes) => bytes / (1024 * 1024);
}
