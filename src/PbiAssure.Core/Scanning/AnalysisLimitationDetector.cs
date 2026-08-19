using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Records semantic-model metadata that was encountered but not analysed.
///
/// The scanner already enumerates every definition artifact in order to count it
/// (<see cref="ProjectScanner"/>), while the TMDL parser opens only a subset. This compares the two and
/// reports the difference, so that metadata cannot be skipped without leaving a trace.
///
/// File-level detection only. Constructs skipped inside a file that is parsed, and properties skipped
/// inside a construct that is parsed, are not detected here.
/// </summary>
internal static class AnalysisLimitationDetector
{
    public static AnalysisLimitation[] Detect(
        IProjectFileSource source,
        IReadOnlyList<ArtifactInventory> artifacts)
    {
        return artifacts
            .Where(artifact => artifact.Kind == ArtifactKinds.SemanticModel)
            .SelectMany(artifact => DetectForModel(source, artifact))
            .OrderBy(limitation => limitation.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(limitation => limitation.ArtifactPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<AnalysisLimitation> DetectForModel(
        IProjectFileSource source,
        ArtifactInventory artifact)
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
                DependencyImpact: rule.DependencyImpact,
                Concerns: rule.Concerns,
                Reason: rule.Reason);
        }
    }
}
