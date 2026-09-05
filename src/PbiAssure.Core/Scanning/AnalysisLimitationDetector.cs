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
        IReadOnlyList<ReportInventory>? reports = null,
        IReadOnlyList<UnresolvedSemanticDependency>? unresolvedDependencies = null,
        IReadOnlyList<UnanalyzedTableConstructs>? unanalyzedTableConstructs = null,
        IReadOnlyList<SemanticModelInventory>? semanticModels = null)
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
            .SelectMany(artifact => DetectForReport(source, artifact, reportsByPath, semanticModels ?? []));

        return semanticLimitations
            .Concat(reportLimitations)
            .Concat(DetectForUnresolvedDependencies(unresolvedDependencies ?? []))
            .Concat(DetectForUnanalyzedTableConstructs(unanalyzedTableConstructs ?? []))
            .OrderBy(limitation => limitation.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(limitation => limitation.ArtifactPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// A reference PBI Assure read but could not bind is doubt about the dependency graph, so it has to
    /// reach confidence the same way every other doubt does — as an <see cref="AnalysisLimitation"/>
    /// carrying <see cref="ConstructDependencyImpacts.MayCreateDependencies"/>. Routing it here keeps
    /// <see cref="SemanticUsageConfidenceQualifier"/> with a single input rather than giving confidence
    /// a second, parallel source of truth.
    ///
    /// Only <c>NotFound</c> and <c>Ambiguous</c> qualify. Both mean the edge that reference would have
    /// created is missing from the graph, which is exactly what an absence conclusion depends on.
    ///
    /// Scope is the semantic model, because an unresolved reference does not say which object it meant:
    /// a name that resolved to nothing could have been any object in that model. Positive states are
    /// untouched — the qualifier only ever marks the two absence states.
    /// </summary>
    /// <summary>
    /// The tables directory is registered as fully analysed, which holds for every construct the table
    /// parser reads. Where the parser positively recognised a table-level construct it does not read and
    /// that can reference model objects, that specific file is partially analysed and the registry
    /// cannot say so — the classification is per directory, not per file.
    ///
    /// Only files the parser flagged reach here, so ordinary tables emit nothing and qualification stays
    /// meaningful.
    /// </summary>
    private static IEnumerable<AnalysisLimitation> DetectForUnanalyzedTableConstructs(
        IReadOnlyList<UnanalyzedTableConstructs> unanalyzedTableConstructs)
    {
        return unanalyzedTableConstructs
            .Where(table => table.Constructs.Count > 0)
            .Select(table => new AnalysisLimitation(
                LimitationId: "PBI-LIMIT-MODEL-TABLE-REFERENCES",
                Cause: AnalysisLimitationCauses.ConstructNotSupported,
                SupportState: ConstructSupportStates.PartiallyAnalyzed,
                ConstructType: "table",
                Scope: AnalysisLimitationScopes.SemanticModel,
                SemanticModel: table.SemanticModel,
                Table: table.Table,
                ObjectName: null,
                ArtifactPath: table.RelativePath,
                EvidencePath: AnalysisLimitation.WholeFileEvidence,
                DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
                Concerns: [AnalysisConcerns.Dependency],
                Reason: $"Table '{table.Table}' declares " +
                        string.Join(", ", table.Constructs.Select(construct => $"'{construct}'")) +
                        ". The DAX in that construct can reference model objects and is not analysed by " +
                        "this version, so dependencies it creates are absent from the graph."));
    }

    private static IEnumerable<AnalysisLimitation> DetectForUnresolvedDependencies(
        IReadOnlyList<UnresolvedSemanticDependency> unresolvedDependencies)
    {
        return unresolvedDependencies
            .Where(dependency =>
                dependency.ResolutionOutcome is UnresolvedSemanticDependencyResolutionOutcomes.NotFound
                    or UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous)
            .DistinctBy(dependency => (
                dependency.SemanticModel,
                dependency.FromTable,
                dependency.FromObjectName,
                dependency.DependencyKind,
                dependency.ReferenceText,
                dependency.ResolutionOutcome))
            .Select(dependency => new AnalysisLimitation(
                LimitationId: "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE",
                Cause: AnalysisLimitationCauses.ReferenceUnresolved,
                SupportState: ConstructSupportStates.PartiallyAnalyzed,
                ConstructType: "semanticReference",
                Scope: AnalysisLimitationScopes.SemanticModel,
                SemanticModel: dependency.SemanticModel,
                Table: dependency.FromTable,
                ObjectName: dependency.FromObjectName,
                ArtifactPath: dependency.EvidencePath,
                EvidencePath: dependency.EvidencePath,
                DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
                Concerns: [AnalysisConcerns.Dependency],
                Reason: $"'{dependency.FromTable}[{dependency.FromObjectName}]' references " +
                        $"'{dependency.ReferenceText}', which could not be resolved to a model object " +
                        $"({dependency.ResolutionOutcome}). The dependency it would have created is " +
                        "absent from the graph, so absence conclusions in this model may be incomplete."));
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
        Dictionary<string, ReportInventory> reportsByPath,
        IReadOnlyList<SemanticModelInventory> semanticModels)
    {
        var prefix = ProjectFilePaths.Normalize(artifact.RelativePath).TrimEnd('/') + "/";
        reportsByPath.TryGetValue(ProjectFilePaths.Normalize(artifact.RelativePath), out var report);
        var semanticModel = report?.ModelConnection.IsTargetAvailableLocally == true
            ? report.ModelConnection.TargetSemanticModelName
            : null;

        // Every failed page matters, even when other pages supplied valid positive evidence.
        foreach (var page in report?.UnreadPages ?? [])
        {
            yield return new AnalysisLimitation(
                LimitationId: "PBI-LIMIT-REPORT-PAGES-UNREAD",
                Cause: AnalysisLimitationCauses.ParseFailed,
                SupportState: ConstructSupportStates.PartiallyAnalyzed,
                ConstructType: "pageDefinition",
                Scope: AnalysisLimitationScopes.Report,
                SemanticModel: ReportModelBinder.FindLocalModel(report!, semanticModels)?.Name,
                Table: null,
                ObjectName: null,
                ArtifactPath: page.DefinitionPath,
                EvidencePath: AnalysisLimitation.WholeFileEvidence,
                DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
                Concerns: [AnalysisConcerns.Dependency],
                Reason: $"Report '{artifact.Name}' could not parse page '{page.DefinitionPath}'. " +
                        page.Reason + " Semantic references from this page may be missing from the " +
                        "analysis, so absence conclusions may be incomplete.");
        }

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

        if (report is null)
        {
            yield break;
        }

        foreach (var alias in report.UnresolvedAliases)
        {
            yield return new AnalysisLimitation(
                LimitationId: "PBI-LIMIT-REPORT-UNRESOLVED-ALIAS",
                Cause: AnalysisLimitationCauses.ReferenceUnresolved,
                SupportState: ConstructSupportStates.PartiallyAnalyzed,
                ConstructType: "reportSourceAlias",
                Scope: AnalysisLimitationScopes.Report,
                SemanticModel: ReportModelBinder.FindLocalModel(report, semanticModels)?.Name,
                Table: null,
                ObjectName: null,
                ArtifactPath: alias.ArtifactPath,
                EvidencePath: alias.EvidencePath,
                DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
                Concerns: [AnalysisConcerns.Dependency],
                Reason: $"Source alias '{alias.Alias}' could not be resolved within its owning PBIR " +
                        "query/filter scope. Its semantic references may be absent from the graph.");
        }

        foreach (var measure in report.ReportMeasures.Where(measure => measure.HasUnrecognizedReferences))
        {
            yield return new AnalysisLimitation(
                LimitationId: "PBI-LIMIT-REPORT-MEASURE-REFERENCES",
                Cause: AnalysisLimitationCauses.DependencyMetadataIncomplete,
                SupportState: ConstructSupportStates.PartiallyAnalyzed,
                ConstructType: "reportMeasure",
                Scope: AnalysisLimitationScopes.Report,
                SemanticModel: semanticModel,
                Table: measure.Entity,
                ObjectName: measure.Name,
                ArtifactPath: measure.RelativePath,
                EvidencePath: "$.entities[].measures[].references.unrecognizedReferences",
                DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
                Concerns: [AnalysisConcerns.Dependency],
                Reason: $"Report measure '{measure.Entity}[{measure.Name}]' declares that its persisted " +
                        "reference list is incomplete. Its DAX expression is analysed, but additional " +
                        "dependencies may still be absent from the report metadata.");
        }
    }
}
