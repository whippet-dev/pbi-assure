using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal enum SemanticDefinitionFileMatch
{
    /// <summary>The pattern is a complete model-relative file path.</summary>
    ExactPath,

    /// <summary>The pattern is a model-relative directory; any file directly inside it matches.</summary>
    DirectoryContents,
}

internal sealed record SemanticDefinitionFileRule(
    string LimitationId,
    string ConstructType,
    string Pattern,
    SemanticDefinitionFileMatch MatchKind,
    string Classification,
    string SupportState,
    string DependencyImpact,
    IReadOnlyList<string> Concerns,
    string Reason);

/// <summary>
/// The single source of truth for what each semantic-model definition artifact is, and therefore for
/// whether not parsing it is a limitation worth reporting.
///
/// Classification is deliberately declarative and centralised rather than inferred at each call site, so
/// that adding support for a construct is a change to one rule rather than a change plus a matching
/// deletion somewhere else.
///
/// Evidence status of the classifications below:
///
/// [verified] Which files the parser actually opens. <see cref="TmdlSemanticModelParser.Parse"/> reads
///            only definition/tables/*.tmdl, definition/relationships.tmdl and
///            definition/expressions.tmdl. roles.tmdl, model.tmdl, database.tmdl, cultures/ and
///            perspectives.tmdl have no references anywhere in PbiAssure.Core.
///
/// [verified] That a table permission in roles.tmdl can reference a column which then receives no
///            dependency evidence. Demonstrated by scanning a project whose only reference to a column
///            was an RLS filter.
///
/// [inferred] The classification of model.tmdl, database.tmdl, cultures/ and perspectives.tmdl, and the
///            dependency impact assigned to each. These rest on reading about TMDL rather than on
///            Power BI Desktop-authored fixtures, and are expected to change. In particular it is not
///            established whether model.tmdl or database.tmdl carry dependency-bearing content, nor
///            whether cultures/ and perspectives.tmdl are purely presentational.
///
/// Changing any [inferred] rule below is a one-line change here, which is the point of the registry.
/// </summary>
internal static class SemanticDefinitionFileRegistry
{
    /// <summary>
    /// The definition-artifact extensions that make up the classified universe. Shared with
    /// <see cref="ProjectScanner"/> so that the set counted and the set classified cannot drift apart.
    /// </summary>
    public static IReadOnlySet<string> DefinitionExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".tmdl", ".bim", ".pbism" };

    public static IReadOnlyList<SemanticDefinitionFileRule> Rules { get; } =
    [
        new(
            LimitationId: "PBI-LIMIT-MODEL-TABLE",
            ConstructType: "table",
            Pattern: "definition/tables",
            MatchKind: SemanticDefinitionFileMatch.DirectoryContents,
            Classification: ConstructClassifications.Analyzed,
            SupportState: ConstructSupportStates.Analyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "Table definitions are parsed into the semantic-model inventory."),
        new(
            LimitationId: "PBI-LIMIT-MODEL-RELATIONSHIP",
            ConstructType: "relationship",
            Pattern: "definition/relationships.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.Analyzed,
            SupportState: ConstructSupportStates.Analyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "Relationships are parsed into the semantic-model inventory."),
        new(
            LimitationId: "PBI-LIMIT-MODEL-EXPRESSION",
            ConstructType: "expression",
            Pattern: "definition/expressions.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.Analyzed,
            SupportState: ConstructSupportStates.Analyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "Named expressions are parsed into the semantic-model inventory."),
        new(
            LimitationId: "PBI-LIMIT-MODEL-ROLE",
            ConstructType: "role",
            Pattern: "definition/roles.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
            Concerns: [AnalysisConcerns.Dependency, AnalysisConcerns.Security],
            Reason: "Row-level security role definitions are not analysed by this version. Security " +
                    "filters can reference model objects that no report or measure uses."),
        new(
            // [inferred] classification and impact. Needs a Desktop-authored fixture.
            LimitationId: "PBI-LIMIT-MODEL-SETTINGS",
            ConstructType: "modelSettings",
            Pattern: "definition/model.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.DependencyEffectUnknown,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "Model-level settings are not analysed by this version. Whether they affect object " +
                    "dependencies has not been determined."),
        new(
            // [inferred] classification and impact. Needs a Desktop-authored fixture.
            LimitationId: "PBI-LIMIT-MODEL-PERSPECTIVE",
            ConstructType: "perspective",
            Pattern: "definition/perspectives.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.DependencyEffectUnknown,
            Concerns: [AnalysisConcerns.Presentation],
            Reason: "Perspectives are not analysed by this version. Whether they affect object " +
                    "dependencies has not been determined."),
        new(
            // [inferred] classification and impact. Needs a Desktop-authored fixture.
            LimitationId: "PBI-LIMIT-MODEL-CULTURE",
            ConstructType: "culture",
            Pattern: "definition/cultures",
            MatchKind: SemanticDefinitionFileMatch.DirectoryContents,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.DependencyEffectUnknown,
            Concerns: [AnalysisConcerns.Presentation],
            Reason: "Cultures and translations are not analysed by this version. Whether they affect " +
                    "object dependencies has not been determined."),
        new(
            // [inferred] classification. Needs a Desktop-authored fixture.
            LimitationId: "PBI-LIMIT-MODEL-DATABASE",
            ConstructType: "database",
            Pattern: "definition/database.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.Packaging,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "Database-level packaging metadata. Not parsed, and not treated as a limitation."),
        new(
            LimitationId: "PBI-LIMIT-MODEL-MANIFEST",
            ConstructType: "semanticModelManifest",
            Pattern: "definition.pbism",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.Packaging,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "Semantic-model manifest. Not parsed, and not treated as a limitation."),
    ];

    /// <summary>
    /// The rule applied when no declared rule matches. Unknown definition artifacts are assumed capable
    /// of creating dependencies: an unnecessary caveat is recoverable, a confident deletion
    /// recommendation for an object something uses is not.
    /// </summary>
    public static SemanticDefinitionFileRule Fallback { get; } = new(
        LimitationId: "PBI-LIMIT-MODEL-UNRECOGNIZED",
        ConstructType: "unrecognizedDefinitionFile",
        Pattern: string.Empty,
        MatchKind: SemanticDefinitionFileMatch.ExactPath,
        Classification: ConstructClassifications.Unrecognized,
        SupportState: ConstructSupportStates.Unrecognized,
        DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
        Concerns: [AnalysisConcerns.Dependency],
        Reason: "This definition file is not recognised by this version of PBI Assure and was not analysed.");

    /// <summary>
    /// Classifies a model-relative definition-artifact path. Total: every path receives exactly one rule.
    /// </summary>
    public static SemanticDefinitionFileRule Classify(string modelRelativePath)
    {
        if (string.IsNullOrWhiteSpace(modelRelativePath))
        {
            return Fallback;
        }

        var normalized = ProjectFilePaths.Normalize(modelRelativePath);
        return Rules.FirstOrDefault(rule => Matches(rule, normalized)) ?? Fallback;
    }

    public static bool IsDefinitionArtifact(string relativePath) =>
        DefinitionExtensions.Contains(Path.GetExtension(relativePath));

    private static bool Matches(SemanticDefinitionFileRule rule, string normalizedPath)
    {
        if (rule.MatchKind == SemanticDefinitionFileMatch.ExactPath)
        {
            return string.Equals(rule.Pattern, normalizedPath, StringComparison.OrdinalIgnoreCase);
        }

        var prefix = rule.Pattern + "/";
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               !normalizedPath[prefix.Length..].Contains('/');
    }
}
