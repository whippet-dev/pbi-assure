using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Records semantic-model and report metadata that was encountered but not analysed.
///
/// The scanner already enumerates every definition artifact in order to count it
/// (<see cref="ProjectScanner"/>), while the model and report parsers open only declared subsets. This
/// compares the two and reports the difference, so metadata cannot be skipped without leaving a trace.
///
/// File-level detection only. Constructs skipped inside a file that is parsed, and properties skipped
/// inside a construct that is parsed, are not detected here.
/// </summary>
internal static class AnalysisLimitationDetector
{
    public static AnalysisLimitation[] Detect(
        IProjectFileSource source,
        IReadOnlyList<ArtifactInventory> artifacts,
        IReadOnlyDictionary<string, string>? refinedDependencyImpacts = null,
        IReadOnlySet<string>? fullyAccountedRolePaths = null,
        IReadOnlyList<ReportInventory>? reports = null)
    {
        var refinements = refinedDependencyImpacts ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var accountedRolePaths = fullyAccountedRolePaths ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var semanticLimitations = artifacts
            .Where(artifact => artifact.Kind == ArtifactKinds.SemanticModel)
            .SelectMany(artifact => DetectForModel(source, artifact, refinements, accountedRolePaths));
        var reportsByPath = (reports ?? [])
            .ToDictionary(report => report.RelativePath, StringComparer.OrdinalIgnoreCase);
        var reportLimitations = artifacts
            .Where(artifact => artifact.Kind == ArtifactKinds.Report)
            .SelectMany(artifact => DetectForReport(source, artifact, reportsByPath));

        return semanticLimitations.Concat(reportLimitations)
            .OrderBy(limitation => limitation.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(limitation => limitation.ArtifactPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<AnalysisLimitation> DetectForModel(
        IProjectFileSource source,
        ArtifactInventory artifact,
        IReadOnlyDictionary<string, string> refinedDependencyImpacts,
        IReadOnlySet<string> fullyAccountedRolePaths)
    {
        var prefix = ProjectFilePaths.Normalize(artifact.RelativePath).TrimEnd('/') + "/";

        foreach (var file in source.EnumerateFiles(artifact.RelativePath))
        {
            if (!SemanticDefinitionFileRegistry.IsDefinitionArtifact(file.RelativePath))
            {
                continue;
            }

            var modelRelativePath = file.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? file.RelativePath[prefix.Length..]
                : file.RelativePath;
            var rule = SemanticDefinitionFileRegistry.Classify(modelRelativePath);
            if (rule.Classification is not (ConstructClassifications.SemanticNotYetAnalyzed
                or ConstructClassifications.Unrecognized))
            {
                continue;
            }

            // Roles are normally a partially analysed construct family. The TMDL role parser can,
            // however, establish that every child actually encountered in this specific role file was
            // accounted for. In that narrow case there is no unsupported role metadata to report.
            if (rule.ConstructType == "role" && fullyAccountedRolePaths.Contains(file.RelativePath))
            {
                continue;
            }

            yield return new AnalysisLimitation(
                LimitationId: rule.LimitationId,
                Cause: AnalysisLimitationCauses.ConstructNotSupported,
                SupportState: rule.SupportState,
                ConstructType: rule.ConstructType,
                Scope: AnalysisLimitationScopes.SemanticModel,
                SemanticModel: artifact.Name,
                Table: null,
                ObjectName: null,
                ArtifactPath: file.RelativePath,
                EvidencePath: AnalysisLimitation.WholeFileEvidence,
                // The registry states what this construct type can contain. Where the scanner proved
                // more about this particular file, the artifact evidence narrows it.
                DependencyImpact: refinedDependencyImpacts.TryGetValue(file.RelativePath, out var refined)
                    ? refined
                    : rule.DependencyImpact,
                Concerns: rule.Concerns,
                Reason: rule.Reason);
        }
    }

    private static IEnumerable<AnalysisLimitation> DetectForReport(
        IProjectFileSource source,
        ArtifactInventory artifact,
        Dictionary<string, ReportInventory> reportsByPath)
    {
        var prefix = ProjectFilePaths.Normalize(artifact.RelativePath).TrimEnd('/') + "/";
        reportsByPath.TryGetValue(ProjectFilePaths.Normalize(artifact.RelativePath), out var report);
        var semanticModel = report?.ModelConnection.IsTargetAvailableLocally == true
            ? report.ModelConnection.TargetSemanticModelName
            : null;

        foreach (var file in source.EnumerateFiles(artifact.RelativePath))
        {
            if (!ReportDefinitionFileRegistry.IsDefinitionArtifact(file.RelativePath))
            {
                continue;
            }

            var reportRelativePath = file.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? file.RelativePath[prefix.Length..]
                : file.RelativePath;
            var rule = ReportDefinitionFileRegistry.Classify(reportRelativePath);
            if (rule.Classification is not (ConstructClassifications.SemanticNotYetAnalyzed
                or ConstructClassifications.Unrecognized))
            {
                continue;
            }

            yield return new AnalysisLimitation(
                LimitationId: rule.LimitationId,
                Cause: AnalysisLimitationCauses.ConstructNotSupported,
                SupportState: rule.SupportState,
                ConstructType: rule.ConstructType,
                Scope: AnalysisLimitationScopes.Report,
                SemanticModel: semanticModel,
                Table: null,
                ObjectName: null,
                ArtifactPath: file.RelativePath,
                EvidencePath: AnalysisLimitation.WholeFileEvidence,
                DependencyImpact: rule.DependencyImpact,
                Concerns: rule.Concerns,
                Reason: rule.Reason);
        }
    }
}
