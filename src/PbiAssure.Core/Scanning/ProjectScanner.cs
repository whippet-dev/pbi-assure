using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

public static class ProjectScanner
{
    public static ProjectInventory Scan(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException($"The project directory was not found: {fullRootPath}");
        }

        var artifacts = new List<ArtifactInventory>();
        AddProjectFiles(fullRootPath, artifacts);
        AddArtifactDirectories(fullRootPath, artifacts);

        var reports = Directory
            .EnumerateDirectories(fullRootPath, "*.Report", SearchOption.TopDirectoryOnly)
            .Select(directory => PbirReportParser.Parse(fullRootPath, directory))
            .OrderBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var semanticModels = Directory
            .EnumerateDirectories(fullRootPath, "*.SemanticModel", SearchOption.TopDirectoryOnly)
            .Select(directory => TmdlSemanticModelParser.Parse(fullRootPath, directory))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var (semanticObjectUsages, unresolvedSemanticReferences) =
            SemanticUsageReconciler.Reconcile(semanticModels, reports);
        var dependencyAnalysis = SemanticDependencyAnalyzer.Analyze(semanticModels, semanticObjectUsages, reports);
        var powerQueryAnalysis = PowerQueryLineageAnalyzer.Analyze(semanticModels);

        var inventory = new ProjectInventory(
            SchemaVersion: "0.13",
            RootPath: fullRootPath,
            ScannedAtUtc: DateTimeOffset.UtcNow,
            Artifacts: artifacts
                .OrderBy(artifact => artifact.Kind, StringComparer.Ordinal)
                .ThenBy(artifact => artifact.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Reports: reports,
            SemanticModels: semanticModels,
            SemanticObjectUsages: dependencyAnalysis.ObjectUsages,
            SemanticTableUsages: dependencyAnalysis.TableUsages,
            SemanticDependencies: dependencyAnalysis.Dependencies,
            PowerQueryUsages: powerQueryAnalysis.Usages,
            PowerQueryDependencies: powerQueryAnalysis.Dependencies,
            UnresolvedSemanticReferences: unresolvedSemanticReferences,
            UnresolvedSemanticDependencies: dependencyAnalysis.UnresolvedDependencies,
            Findings: []);

        return inventory with { Findings = AssuranceRuleEngine.Evaluate(inventory) };
    }

    private static void AddProjectFiles(string rootPath, List<ArtifactInventory> artifacts)
    {
        foreach (var projectFile in Directory.EnumerateFiles(rootPath, "*.pbip", SearchOption.TopDirectoryOnly))
        {
            artifacts.Add(new ArtifactInventory(
                Kind: ArtifactKinds.Project,
                Name: Path.GetFileNameWithoutExtension(projectFile),
                RelativePath: Path.GetRelativePath(rootPath, projectFile),
                DefinitionFileCount: 1));
        }
    }

    private static void AddArtifactDirectories(string rootPath, List<ArtifactInventory> artifacts)
    {
        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var directoryName = Path.GetFileName(directory);
            var kind = GetArtifactKind(directoryName);
            if (kind is null)
            {
                continue;
            }

            var suffix = kind == ArtifactKinds.Report ? ".Report" : ".SemanticModel";
            var name = directoryName[..^suffix.Length];

            artifacts.Add(new ArtifactInventory(
                Kind: kind,
                Name: name,
                RelativePath: Path.GetRelativePath(rootPath, directory),
                DefinitionFileCount: CountDefinitionFiles(directory, kind)));
        }
    }

    private static string? GetArtifactKind(string directoryName)
    {
        if (directoryName.EndsWith(".Report", StringComparison.OrdinalIgnoreCase))
        {
            return ArtifactKinds.Report;
        }

        return directoryName.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase)
            ? ArtifactKinds.SemanticModel
            : null;
    }

    private static int CountDefinitionFiles(string directory, string kind)
    {
        var supportedExtensions = kind == ArtifactKinds.Report
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".pbir" }
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".tmdl", ".bim", ".pbism" };

        return Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Count(path => supportedExtensions.Contains(Path.GetExtension(path)));
    }
}
