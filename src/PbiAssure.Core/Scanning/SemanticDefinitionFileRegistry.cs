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
/// Evidence status of the classifications below. Three levels are distinguished deliberately, because
/// primary documentation and observed Power BI Desktop output are not the same kind of evidence:
///
/// [verified in this repository]
///     Which files the parser actually opens. <see cref="TmdlSemanticModelParser.Parse"/> reads only
///     definition/tables/*.tmdl, definition/relationships.tmdl and definition/expressions.tmdl.
///     No other definition path is referenced anywhere in PbiAssure.Core.
///
/// [verified by Microsoft primary documentation]
///     The TMDL folder shape: cultures/, perspectives/, roles/ and tables/ are sub-folders holding one
///     file per object, and database, model, relationships, expressions, dataSources and functions are
///     root files. Also that definition.pbism holds semantic-model settings and format version, that
///     model.bim is the TMSL alternative to the definition/ folder, and that TMDLScripts/ holds TMDL
///     view editor scripts rather than model definition.
///     Sources: learn.microsoft.com/analysis-services/tmdl/tmdl-overview and
///     learn.microsoft.com/power-bi/developer/projects/projects-dataset.
///
/// [verified by Power BI Desktop-authored fixture]
///     tests/fixtures/desktop-semantic-constructs is a Desktop-authored project containing roles, a
///     perspective, a culture, functions, model.tmdl, database.tmdl, relationships and TMDL view editor
///     scripts. Every path rule below that it exercises is confirmed against real Desktop output by
///     DesktopSemanticConstructsFixtureTests. Also confirmed there: model.tmdl names objects only
///     through collection-ordering ref declarations, database.tmdl contains only a compatibility level,
///     and the default culture file is empty.
///
/// [not verified by Desktop serialization]
///     dataSources.tmdl has never been observed and its dependency impact remains undetermined.
///     model.bim has not been observed either. No culture containing actual translations has been
///     observed, so the culture rule's impact rests on a design decision rather than on evidence:
///     a translation describes an object rather than consuming it. The open sub-case is Q&A linguistic
///     metadata, which also lives in culture files and is closer to a consumer than a caption is;
///     settling it needs a Desktop-authored fixture containing translations and synonyms.
///
/// Changing any rule below is a one-line change here, which is the point of the registry.
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
            // Partially analysed. Table permission filter expressions and explicitly named column-level
            // object permissions are parsed and become model-structure roots, so a referenced column is
            // no longer reported as unused. Other role content can still carry dependency-bearing data,
            // so the construct remains partial and unknown content remains qualifying.
            // Role membership lives in the Power BI service, never in the project, so it is a permanent
            // scope boundary rather than something this construct could ever supply.
            LimitationId: "PBI-LIMIT-MODEL-ROLE",
            ConstructType: "role",
            Pattern: "definition/roles",
            MatchKind: SemanticDefinitionFileMatch.DirectoryContents,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.PartiallyAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
            Concerns: [AnalysisConcerns.Dependency, AnalysisConcerns.Security],
            Reason: "Row-level security filters and explicitly named object-level column permissions are " +
                    "analysed, so objects they reference are treated as required. Other role content can " +
                    "also reference model objects."),
        new(
            // Partially analysed. The documented members that name model objects — perspectiveTable,
            // perspectiveColumn, perspectiveMeasure, perspectiveHierarchy and includeAll — are parsed and
            // become model-structure roots. Not analysed: perspective sets, and the presentation meaning
            // of a perspective. The construct-type default stays conservative because a perspective may
            // carry content this version does not recognise; an individual file whose content is fully
            // accounted for is narrowed by artifact evidence instead.
            LimitationId: "PBI-LIMIT-MODEL-PERSPECTIVE",
            ConstructType: "perspective",
            Pattern: "definition/perspectives",
            MatchKind: SemanticDefinitionFileMatch.DirectoryContents,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.PartiallyAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
            Concerns: [AnalysisConcerns.Dependency, AnalysisConcerns.Presentation],
            Reason: "Perspective members that name model objects are analysed, so objects a perspective " +
                    "exposes are treated as required. The presentation meaning of a perspective is not " +
                    "analysed."),
        new(
            // Documented as one file per culture under cultures/. Dependency effect not established.
            // Desktop emits an empty "cultureInfo en-US" for every model, so this rule fires on every
            // real project. A culture may name model objects once translations exist, but naming an
            // object and consuming it are different propositions: a translation supplies a caption for
            // an object and is deleted with it, so it cannot keep an otherwise-unused object alive.
            // Treated as presentation metadata rather than a usage dependency. Still recorded as
            // unanalysed. See the note on Q&A linguistic metadata in the class comment.
            LimitationId: "PBI-LIMIT-MODEL-CULTURE",
            ConstructType: "culture",
            Pattern: "definition/cultures",
            MatchKind: SemanticDefinitionFileMatch.DirectoryContents,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [AnalysisConcerns.Presentation],
            Reason: "Cultures and translations are not analysed by this version. Translations describe " +
                    "model objects rather than consuming them, so they are not treated as usage."),
        new(
            // Documented root file holding DAX user-defined functions, whose expressions are DAX.
            LimitationId: "PBI-LIMIT-MODEL-FUNCTION",
            ConstructType: "function",
            Pattern: "definition/functions.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.PartiallyAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "DAX user-defined function definitions are analysed, including what their bodies " +
                    "reference and which functions call one another. What is not analysed is where a " +
                    "function is called from outside the model definition: visual calculations and " +
                    "report-level measures can call one, and neither is read, so a function may be " +
                    "used more than the analysed evidence shows."),
        new(
            // Documented root file holding all data sources. Dependency effect not established.
            LimitationId: "PBI-LIMIT-MODEL-DATASOURCE",
            ConstructType: "dataSource",
            Pattern: "definition/dataSources.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.DependencyEffectUnknown,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "Data-source definitions are not analysed by this version. Whether they affect " +
                    "object dependencies has not been determined."),
        new(
            // Documented root file holding model-level definition, including ref declarations that
            // order tables, roles, cultures and perspectives. Dependency effect not established.
            // Emitted for every model. It names objects through ref declarations — ref table, ref role,
            // ref perspective, ref cultureInfo — but those list every member of a collection regardless
            // of use, and exist to preserve ordering on round-trip. Treating them as usage would mark
            // every object in every model as used, so reading this file could not correct a usage
            // conclusion. Still recorded as unanalysed.
            LimitationId: "PBI-LIMIT-MODEL-SETTINGS",
            ConstructType: "modelDefinition",
            Pattern: "definition/model.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "The model-level definition is not analysed by this version. Its object references " +
                    "are collection-ordering declarations rather than usage evidence."),
        new(
            // Documented as part of the TMDL database definition, not as PBIP packaging. Classified as
            // semantic content rather than packaging so that not reading it is recorded rather than
            // silent. No claim is made that it creates dependencies.
            // Emitted for every model. Every Desktop-authored fixture in this repository contains only a
            // compatibilityLevel, with no object reference of any kind. Still recorded as unanalysed.
            LimitationId: "PBI-LIMIT-MODEL-DATABASE",
            ConstructType: "database",
            Pattern: "definition/database.tmdl",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "The database-level definition is not analysed by this version. Every observed " +
                    "instance contains only a compatibility level, with no object references."),
        new(
            // The TMSL alternative to the definition/ folder: the whole model in one JSON document.
            // Matched at its documented exact path only, so that an unrelated .bim file elsewhere is
            // not assumed to be a semantic model.
            LimitationId: "PBI-LIMIT-MODEL-TMSL",
            ConstructType: "tmslModelDefinition",
            Pattern: "model.bim",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.SemanticNotYetAnalyzed,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "This semantic model is stored in TMSL format, which is not analysed by this " +
                    "version. The entire model definition was not read."),
        new(
            // TMDL view editor scripts, not part of the model definition. Correctly not parsed, so not
            // a limitation, but classified explicitly so it cannot reach the unrecognised fallback.
            LimitationId: "PBI-LIMIT-MODEL-EDITOR-SCRIPT",
            ConstructType: "tmdlEditorScript",
            Pattern: "TMDLScripts",
            MatchKind: SemanticDefinitionFileMatch.DirectoryContents,
            Classification: ConstructClassifications.Packaging,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "TMDL view editor script. Not part of the semantic-model definition, and not " +
                    "treated as a limitation."),
        new(
            LimitationId: "PBI-LIMIT-MODEL-MANIFEST",
            ConstructType: "semanticModelSettings",
            Pattern: "definition.pbism",
            MatchKind: SemanticDefinitionFileMatch.ExactPath,
            Classification: ConstructClassifications.Packaging,
            SupportState: ConstructSupportStates.NotYetAnalyzed,
            DependencyImpact: ConstructDependencyImpacts.NoKnownDependencyEffect,
            Concerns: [],
            Reason: "Semantic-model settings and definition-format version. Not parsed, and not " +
                    "treated as a limitation."),
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
        var matches = MatchingRules(modelRelativePath);
        return matches.Count > 0 ? matches[0] : Fallback;
    }

    /// <summary>
    /// Every rule that matches a path. A well-formed registry returns at most one; more than one means
    /// two rules overlap and classification would depend on declaration order.
    /// </summary>
    public static IReadOnlyList<SemanticDefinitionFileRule> MatchingRules(string modelRelativePath)
    {
        if (string.IsNullOrWhiteSpace(modelRelativePath))
        {
            return [];
        }

        var normalized = ProjectFilePaths.Normalize(modelRelativePath);
        return Rules.Where(rule => Matches(rule, normalized)).ToArray();
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
