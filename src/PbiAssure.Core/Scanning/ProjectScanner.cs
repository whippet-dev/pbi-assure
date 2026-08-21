using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

public static class ProjectScanner
{
    public static ProjectInventory Scan(string rootPath)
    {
        return Scan(new PhysicalProjectFileSource(rootPath));
    }

    public static ProjectInventory Scan(IProjectFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        ProjectInputEligibility.EnsureSupported(source);

        var artifacts = new List<ArtifactInventory>();
        AddProjectFiles(source, artifacts);
        AddArtifactDirectories(source, artifacts);

        var reports = source
            .EnumerateDirectories(string.Empty)
            .Where(directory => directory.EndsWith(".Report", StringComparison.OrdinalIgnoreCase))
            .Select(directory => PbirReportParser.Parse(source, directory))
            .OrderBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var semanticModels = source
            .EnumerateDirectories(string.Empty)
            .Where(directory => directory.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase))
            .Select(directory => TmdlSemanticModelParser.Parse(source, directory))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var (semanticObjectUsages, unresolvedSemanticReferences) =
            SemanticUsageReconciler.Reconcile(semanticModels, reports);
        var dependencyAnalysis = SemanticDependencyAnalyzer.Analyze(semanticModels, semanticObjectUsages, reports);
        var powerQueryAnalysis = PowerQueryLineageAnalyzer.Analyze(semanticModels);
        var powerQueryColumnUsages = PowerQueryColumnLineageAnalyzer.Analyze(
            semanticModels, powerQueryAnalysis.Usages);
        var semanticTablePowerQueryContexts = SemanticTablePowerQueryEnricher.Build(powerQueryAnalysis.Usages);
        var analysisLimitations = AnalysisLimitationDetector.Detect(
            source,
            artifacts,
            AnalysisCoverageRefinements.Build(semanticModels),
            AnalysisCoverageRefinements.BuildFullyAccountedRolePaths(semanticModels));

        // Applied once, after usage states are final and the limitations are known. Usage states are not
        // changed here; only the orthogonal confidence marker is set.
        var semanticObjectUsagesWithConfidence = SemanticUsageConfidenceQualifier.Apply(
            dependencyAnalysis.ObjectUsages, analysisLimitations);

        var inventory = new ProjectInventory(
            SchemaVersion: "0.26",
            RootPath: source.SourceRoot ?? source.DisplayName,
            ScannedAtUtc: DateTimeOffset.UtcNow,
            Artifacts: artifacts
                .OrderBy(artifact => artifact.Kind, StringComparer.Ordinal)
                .ThenBy(artifact => artifact.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Reports: reports,
            SemanticModels: dependencyAnalysis.SemanticModels,
            SemanticObjectUsages: semanticObjectUsagesWithConfidence,
            SemanticTableUsages: dependencyAnalysis.TableUsages,
            SemanticDependencies: dependencyAnalysis.Dependencies,
            PowerQueryUsages: powerQueryAnalysis.Usages,
            PowerQueryDependencies: powerQueryAnalysis.Dependencies,
            PowerQueryColumnUsages: powerQueryColumnUsages,
            SemanticTablePowerQueryContexts: semanticTablePowerQueryContexts,
            DataSources: powerQueryAnalysis.DataSources,
            UnresolvedSemanticReferences: unresolvedSemanticReferences,
            UnresolvedSemanticDependencies: dependencyAnalysis.UnresolvedDependencies,
            Findings: [])
        {
            AnalysisLimitations = analysisLimitations,
            SemanticNodeReachability = dependencyAnalysis.NodeReachability,
        };

        return inventory with { Findings = AssuranceRuleEngine.Evaluate(inventory) };
    }

    private static void AddProjectFiles(IProjectFileSource source, List<ArtifactInventory> artifacts)
    {
        foreach (var projectFile in source.Files.Where(file =>
                     !file.RelativePath.Contains('/') && file.RelativePath.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase)))
        {
            artifacts.Add(new ArtifactInventory(
                Kind: ArtifactKinds.Project,
                Name: ProjectFilePaths.GetFileNameWithoutExtension(projectFile.RelativePath),
                RelativePath: projectFile.RelativePath,
                DefinitionFileCount: 1));
        }
    }

    private static void AddArtifactDirectories(IProjectFileSource source, List<ArtifactInventory> artifacts)
    {
        foreach (var directoryName in source.EnumerateDirectories(string.Empty))
        {
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
                RelativePath: directoryName,
                DefinitionFileCount: CountDefinitionFiles(source, directoryName, kind)));
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

    private static int CountDefinitionFiles(IProjectFileSource source, string directory, string kind)
    {
        // The semantic-model set is shared with SemanticDefinitionFileRegistry so that the artifacts
        // counted here and the artifacts classified there cannot drift apart.
        var supportedExtensions = kind == ArtifactKinds.Report
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".pbir" }
            : SemanticDefinitionFileRegistry.DefinitionExtensions;

        return source
            .EnumerateFiles(directory)
            .Count(file => supportedExtensions.Contains(Path.GetExtension(file.RelativePath)));
    }
}
