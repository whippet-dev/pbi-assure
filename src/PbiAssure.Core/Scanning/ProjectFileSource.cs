namespace PbiAssure.Core.Scanning;

/// <summary>
/// A read-only, project-relative view of Power BI project files.
/// Paths use forward slashes regardless of the host operating system.
/// </summary>
public interface IProjectFileSource
{
    string DisplayName { get; }

    /// <summary>
    /// The physical project root when one exists. Browser and in-memory sources return null.
    /// </summary>
    string? SourceRoot { get; }

    IReadOnlyCollection<ProjectFileEntry> Files { get; }

    Stream OpenRead(string relativePath);
}

public sealed record ProjectFileEntry(string RelativePath, long Length);

public sealed record ProjectFileContent(string RelativePath, byte[] Contents);

public static class ProjectFilePaths
{
    public static string Normalize(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (relativePath.Replace('\\', '/')[0] == '/' ||
            Path.IsPathRooted(relativePath) ||
            (relativePath.Length >= 2 && char.IsAsciiLetter(relativePath[0]) && relativePath[1] == ':'))
        {
            throw new ArgumentException("The project-relative path must not be rooted.", nameof(relativePath));
        }

        var segments = new List<string>();
        foreach (var segment in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new ArgumentException("The project-relative path cannot escape the project root.", nameof(relativePath));
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (Path.IsPathRooted(segment) || segment.Contains(':'))
            {
                throw new ArgumentException("The project-relative path must not be rooted.", nameof(relativePath));
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException("The project-relative path must identify a file or directory.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    public static string Combine(string first, params string[] remaining) =>
        Normalize(string.Join('/', new[] { first }.Concat(remaining)));

    public static string GetFileName(string relativePath) =>
        Normalize(relativePath).Split('/')[^1];

    public static string GetFileNameWithoutExtension(string relativePath) =>
        Path.GetFileNameWithoutExtension(GetFileName(relativePath));

    public static string GetDirectoryName(string relativePath)
    {
        var path = Normalize(relativePath);
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }

    public static string ResolveRelative(string baseDirectory, string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        if (configuredPath.Replace('\\', '/')[0] == '/' ||
            configuredPath.Contains(':'))
        {
            throw new ArgumentException("The project reference must be project-relative.", nameof(configuredPath));
        }

        return Normalize(string.Join('/', new[] { baseDirectory, configuredPath }));
    }
}

public static class ProjectFileSourceExtensions
{
    public static bool FileExists(this IProjectFileSource source, string relativePath) =>
        source.Files.Any(file => string.Equals(file.RelativePath, ProjectFilePaths.Normalize(relativePath), StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ProjectFileEntry> EnumerateFiles(this IProjectFileSource source, string directory, bool recursive = true)
    {
        var normalizedDirectory = ProjectFilePaths.Normalize(directory).TrimEnd('/') + "/";
        return source.Files.Where(file =>
        {
            if (!file.RelativePath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return recursive || !file.RelativePath[normalizedDirectory.Length..].Contains('/');
        });
    }

    public static IEnumerable<string> EnumerateDirectories(this IProjectFileSource source, string directory)
    {
        var normalizedDirectory = string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : ProjectFilePaths.Normalize(directory).TrimEnd('/') + "/";

        return source.Files
            .Where(file => file.RelativePath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            .Select(file => file.RelativePath[normalizedDirectory.Length..].Split('/')[0])
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class PhysicalProjectFileSource : IProjectFileSource
{
    private static readonly HashSet<string> MetadataExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pbir", ".json", ".tmdl", ".bim", ".pbism",
    };

    private readonly Dictionary<string, string> physicalPaths;

    public PhysicalProjectFileSource(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        SourceRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(SourceRoot))
        {
            throw new DirectoryNotFoundException($"The project directory was not found: {SourceRoot}");
        }

        DisplayName = Path.GetFileName(Path.TrimEndingDirectorySeparator(SourceRoot));
        physicalPaths = EnumerateProjectFiles(SourceRoot)
            .ToDictionary(
                path => ProjectFilePaths.Normalize(Path.GetRelativePath(SourceRoot, path)),
                path => path,
                StringComparer.OrdinalIgnoreCase);
        Files = physicalPaths
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectFileEntry(item.Key, new FileInfo(item.Value).Length))
            .ToArray();
    }

    public string DisplayName { get; }
    public string? SourceRoot { get; }
    public IReadOnlyCollection<ProjectFileEntry> Files { get; }

    private static IEnumerable<string> EnumerateProjectFiles(string rootPath)
    {
        foreach (var projectFile in Directory.EnumerateFiles(rootPath, "*.pbip", SearchOption.TopDirectoryOnly))
        {
            if (!File.GetAttributes(projectFile).HasFlag(FileAttributes.ReparsePoint))
            {
                yield return projectFile;
            }
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(directory);
            if ((!name.EndsWith(".Report", StringComparison.OrdinalIgnoreCase) &&
                 !name.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase)) ||
                File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", options)
                         .Where(file => MetadataExtensions.Contains(Path.GetExtension(file))))
            {
                yield return file;
            }
        }
    }

    public Stream OpenRead(string relativePath)
    {
        var normalizedPath = ProjectFilePaths.Normalize(relativePath);
        if (!physicalPaths.TryGetValue(normalizedPath, out var physicalPath))
        {
            throw new FileNotFoundException($"The project file was not found: {normalizedPath}", normalizedPath);
        }

        return File.OpenRead(physicalPath);
    }
}

public sealed class InMemoryProjectFileSource : IProjectFileSource
{
    private readonly Dictionary<string, byte[]> contents;

    public InMemoryProjectFileSource(string displayName, IEnumerable<ProjectFileContent> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(files);
        DisplayName = displayName;
        contents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var path = ProjectFilePaths.Normalize(file.RelativePath);
            if (!contents.TryAdd(path, file.Contents))
            {
                throw new ArgumentException($"The project source contains duplicate paths: {path}", nameof(files));
            }
        }

        Files = contents
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProjectFileEntry(item.Key, item.Value.LongLength))
            .ToArray();
    }

    public string DisplayName { get; }
    public string? SourceRoot => null;
    public IReadOnlyCollection<ProjectFileEntry> Files { get; }

    public Stream OpenRead(string relativePath)
    {
        var normalizedPath = ProjectFilePaths.Normalize(relativePath);
        if (!contents.TryGetValue(normalizedPath, out var content))
        {
            throw new FileNotFoundException($"The project file was not found: {normalizedPath}", normalizedPath);
        }

        return new MemoryStream(content, writable: false);
    }
}
