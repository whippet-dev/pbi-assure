namespace PbiAssure.Core.Inventory;

/// <summary>
/// Why PBI Assure could not fully analyse a construct it encountered.
/// </summary>
public static class AnalysisLimitationCauses
{
    /// <summary>The construct type is not supported by this version.</summary>
    public const string ConstructNotSupported = "ConstructNotSupported";

    /// <summary>
    /// The construct was present but could not be parsed. Emitted when a report has page directories
    /// from which no page was read, so the report's field usage is missing from the analysis.
    /// </summary>
    public const string ParseFailed = "ParseFailed";

    /// <summary>The persisted artifact explicitly states that its dependency metadata is incomplete.</summary>
    public const string DependencyMetadataIncomplete = "DependencyMetadataIncomplete";

    /// <summary>
    /// An expression was parsed, but a reference inside it could not be bound to a model object — it
    /// matched nothing, or matched more than one candidate. The dependency that reference would have
    /// created is therefore missing from the graph, so absence conclusions in that model rest on an
    /// incomplete picture.
    /// </summary>
    public const string ReferenceUnresolved = "ReferenceUnresolved";
}

/// <summary>
/// How well this version of PBI Assure supports a construct type. This describes PBI Assure, not the
/// analysed project, and is read from the construct registry rather than observed during a scan.
/// </summary>
public static class ConstructSupportStates
{
    public const string Analyzed = "Analyzed";

    public const string NotYetAnalyzed = "NotYetAnalyzed";

    /// <summary>
    /// Some aspects understood, others not. Emitted for roles (table permission filters are parsed,
    /// column permissions are not), perspectives (membership is parsed, presentation meaning is not) and
    /// DAX user-defined functions (definitions are parsed, some call sites are not).
    /// </summary>
    public const string PartiallyAnalyzed = "PartiallyAnalyzed";

    public const string Unrecognized = "Unrecognized";
}

/// <summary>
/// What a definition artifact is, for the purpose of deciding whether not parsing it is a limitation.
/// Every definition artifact receives exactly one classification, including packaging files that are
/// correctly not parsed.
/// </summary>
public static class ConstructClassifications
{
    /// <summary>Parsed into the inventory.</summary>
    public const string Analyzed = "Analyzed";

    /// <summary>Semantic content that this version does not parse.</summary>
    public const string SemanticNotYetAnalyzed = "SemanticNotYetAnalyzed";

    /// <summary>Manifest or control content that is correctly not parsed and is not a limitation.</summary>
    public const string Packaging = "Packaging";

    /// <summary>Not known to this version of PBI Assure.</summary>
    public const string Unrecognized = "Unrecognized";
}

/// <summary>
/// Whether an unanalysed construct could affect the dependency graph.
/// </summary>
public static class ConstructDependencyImpacts
{
    /// <summary>The construct can contain references to model objects.</summary>
    public const string MayCreateDependencies = "MayCreateDependencies";

    /// <summary>Whether the construct references model objects has not been determined.</summary>
    public const string DependencyEffectUnknown = "DependencyEffectUnknown";

    /// <summary>Established to carry no object references.</summary>
    public const string NoKnownDependencyEffect = "NoKnownDependencyEffect";

    /// <summary>
    /// The construct could change how already-collected evidence should be read, rather than only adding
    /// references. No construct is currently classified this way; the value exists so the case can be
    /// expressed without a later schema change.
    /// </summary>
    public const string MayInvalidateExistingEvidence = "MayInvalidateExistingEvidence";
}

/// <summary>
/// Which kinds of conclusion an unanalysed construct could affect.
/// </summary>
public static class AnalysisConcerns
{
    public const string Dependency = "Dependency";

    public const string Security = "Security";

    public const string Refresh = "Refresh";

    public const string Presentation = "Presentation";
}

/// <summary>
/// How much of the project an unanalysed construct bears on.
/// </summary>
public static class AnalysisLimitationScopes
{
    public const string SemanticModel = "SemanticModel";

    /// <summary>A locally persisted report artifact, associated with its resolved local semantic model.</summary>
    public const string Report = "Report";
}

/// <summary>
/// A record that PBI Assure encountered metadata in the analysed project but did not fully analyse it.
///
/// This is deliberately distinct from <see cref="UnresolvedSemanticDependency"/>. An unresolved
/// dependency is a bounded uncertainty: the source, kind and reference text are known and exactly one
/// edge is missing. An analysis limitation is unbounded: it is not known whether the construct creates
/// dependencies at all, how many, or where they point.
///
/// It is also distinct from a permanent boundary of the input format, such as Power BI Service role
/// membership, which is never present in a PBIP project and therefore is not something a scan can
/// encounter.
/// </summary>
public sealed record AnalysisLimitation(
    string LimitationId,
    string Cause,
    string SupportState,
    string ConstructType,
    string Scope,
    string? SemanticModel,
    string? Table,
    string? ObjectName,
    string ArtifactPath,
    string EvidencePath,
    string DependencyImpact,
    IReadOnlyList<string> Concerns,
    string Reason)
{
    /// <summary>Evidence marker used when an entire file was not analysed, so no inner location applies.</summary>
    public const string WholeFileEvidence = "(entire file)";
}
