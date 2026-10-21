using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting.Viewer;

/// <summary>A presentation contract, independent of the public inventory JSON schema.</summary>
public sealed record ViewerSnapshot(
    string ContractVersion,
    string SourceSchemaVersion,
    string ProjectName,
    DateTimeOffset ScannedAtUtc,
    ViewerSummary Summary,
    IReadOnlyList<ArtifactInventory> Artifacts,
    IReadOnlyList<ViewerTarget> Targets,
    IReadOnlyList<ViewerModel> Models,
    IReadOnlyList<ViewerObject> Objects,
    IReadOnlyList<ViewerNode> Nodes,
    IReadOnlyList<ViewerDependency> Dependencies,
    IReadOnlyList<ViewerUnresolvedDependency> UnresolvedDependencies,
    IReadOnlyList<ViewerReport> Reports,
    IReadOnlyList<ViewerReportMeasure> ReportMeasures,
    IReadOnlyList<ViewerUsage> DirectUsages,
    IReadOnlyList<ViewerRelationship> Relationships,
    IReadOnlyList<ViewerRole> Roles,
    IReadOnlyList<ViewerQuery> Queries,
    IReadOnlyList<ViewerQueryDependency> QueryDependencies,
    IReadOnlyList<ViewerDataSource> DataSources,
    IReadOnlyList<ViewerQueryColumnUsage> QueryColumnUsages,
    IReadOnlyList<ViewerTableQueryContext> TableQueryContexts,
    IReadOnlyList<ViewerFinding> Findings,
    IReadOnlyList<ViewerLimitation> Limitations,
    IReadOnlyList<ViewerQualification> Qualifications,
    IReadOnlyList<ViewerUnresolvedReportReference> UnresolvedReportReferences);

public sealed record ViewerSummary(
    int ReportArtifacts, int ModelArtifacts, int ParsedReports, int ParsedModels,
    int Pages, int Visuals, int SemanticObjects, int DeveloperObjects,
    int ApparentlyUnusedReviewObjects, int QualifiedObjectResults, int LimitationRecords,
    int Queries, int Findings, IReadOnlyDictionary<string, int> AllObjectUsageCounts,
    IReadOnlyDictionary<string, int> DeveloperObjectUsageCounts);

/// <summary>Opaque IDs are stable across scans of the same logical identity; labels are never keys.</summary>
public sealed record ViewerTarget(string Id, string Kind, string Label,
    string? ModelId = null, string? TableId = null, string? ReportId = null, string? PageId = null);

/// <summary>Candidate IDs are navigation candidates, not newly resolved analysis evidence.</summary>
public sealed record ViewerReference(string Status, IReadOnlyList<string> TargetIds);

public sealed record ViewerModel(string Id, string Name, string RelativePath, ViewerModelSummary Summary,
    IReadOnlyList<ViewerTable> Tables, IReadOnlyList<ViewerFunction> Functions,
    IReadOnlyList<ViewerPerspective> Perspectives);
public sealed record ViewerModelSummary(int Tables, int Columns, int Measures, int Hierarchies,
    int HierarchyLevels, int CalculationItems, int Relationships, int Roles, int TableFilters,
    int ObjectPermissionDefinitions, int SemanticObjects, int QualifiedObjectResults, int Queries);
public sealed record ViewerTable(string Id, string Name, string RelativePath,
    IReadOnlyList<string> DefinitionPaths, string? Description, bool IsHidden, bool IsPrivate,
    bool IsSystemGenerated, string? SystemGeneratedKind, string? UsageState,
    IReadOnlyList<string> ObjectIds, IReadOnlyList<ViewerPartition> Partitions,
    IReadOnlyList<SemanticHierarchyInventory> Hierarchies,
    SemanticFieldParameterInventory? FieldParameter, ViewerCalculationGroup? CalculationGroup,
    SemanticRefreshPolicyInventory? RefreshPolicy, IReadOnlyList<string> UnanalyzedDependencyConstructs);
public sealed record ViewerPartition(string Name, string SourceType, string? Mode, string? Expression,
    string? EntityName, string? ExpressionSource, ViewerReference Query);
public sealed record ViewerCalculationGroup(int? Precedence, string? SelectionExpression,
    string? MultipleOrEmptySelectionExpression, string? NoSelectionExpression,
    string? NoSelectionFormatStringExpression, string? MultipleOrEmptySelectionFormatStringExpression);
public sealed record ViewerFunction(string Id, SemanticFunctionInventory Definition);
public sealed record ViewerPerspective(string Id, SemanticPerspectiveInventory Definition);

public sealed record ViewerObject(string Id, string ModelId, string TableId, string Name,
    string ObjectType, string? HierarchyName, string UsageState, string ClassificationConfidence,
    string? UserFacing, bool IsSystemGenerated, bool InApparentlyUnusedReview,
    string? StructuralRequirementProvenance, string? UsageReason, string? ReasonDependencyId,
    ViewerObjectDefinition Definition, int DirectEvidenceCount, int DirectLocationCount);
public sealed record ViewerObjectDefinition(string? Description,
    SemanticColumnInventory? Column, SemanticMeasureInventory? Measure,
    SemanticHierarchyLevelInventory? HierarchyLevel, SemanticCalculationItemInventory? CalculationItem,
    string? DynamicFormatStringExpression, string? DetailRowsDefinitionExpression);

public sealed record ViewerNode(string Id, string ModelId, string? TableId, string Name,
    string ObjectType, string? HierarchyName, string? ReportId,
    bool? ReachableFromReport, bool? ReachableFromModelStructure);
public sealed record ViewerDependency(string Id, ViewerReference Source, ViewerReference Target,
    string DependencyKind, string EvidencePath, string EvidenceText, string? StructuralProvenance);
public sealed record ViewerUnresolvedDependency(string Id, ViewerReference Source,
    string DependencyKind, string ReferenceText, string Reason, string EvidencePath,
    string ResolutionOutcome, bool CandidateSetKnown, IReadOnlyList<string> CandidateTargetIds,
    IReadOnlyList<string> UnmappedCandidateKeys);

public sealed record ViewerReport(string Id, string Name, string RelativePath, ViewerReportSummary Summary,
    ReportModelConnectionInventory ModelConnection, string? LocalModelId, string BindingBasis,
    string? DefinitionPath, string? SchemaUri, string? PagesSchemaUri, string? PbirDefinitionVersion,
    string? ActivePageId, string? LandingPageId, IReadOnlyList<ViewerPage> Pages,
    IReadOnlyList<ReportFilterInventory> Filters, IReadOnlyList<ViewerSavedReference> FieldReferences,
    IReadOnlyList<ViewerBookmark> Bookmarks, IReadOnlyList<string> BookmarkOrder,
    ThemeInventory Theme, ViewerThemeReview ThemeReview,
    IReadOnlyList<ReportSchemaObservation> SchemaObservations,
    string? ReportExtensionsPath, string? ReportExtensionsSchemaUri);
public sealed record ViewerReportSummary(int Pages, int Visuals, int ReportMeasures, int SavedFieldReferences,
    int DistinctSavedFields, int Filters, int Actions, int Bookmarks, int VisualInteractions, int TooltipBindings);
public sealed record ViewerPage(string Id, string Name, string DisplayName, string RelativePath,
    string DefinitionPath, string? SchemaUri, string? PageType, PageBindingInventory? PageBinding,
    int? Order, bool IsActive, string? Visibility, string? DisplayOption, double? Width, double? Height,
    IReadOnlyList<ReportFilterInventory> Filters, IReadOnlyList<ViewerSavedReference> FieldReferences,
    IReadOnlyList<ViewerInteraction> Interactions, IReadOnlyList<ViewerVisualGroup> Groups,
    IReadOnlyList<ViewerVisual> Visuals);
public sealed record ViewerVisual(string Id, string Name, string DisplayName, string? VisualType,
    string RelativePath, bool IsHidden, ViewerReference ParentGroup, VisualPosition Position,
    VisualAccessibilityInventory Accessibility, string? OnCanvasText, bool OnCanvasTextIsDynamic,
    IReadOnlyList<ViewerSavedReference> FieldReferences, IReadOnlyList<ViewerAction> Actions,
    IReadOnlyList<ViewerTooltip> TooltipBindings, IReadOnlyList<PersistedFormattingObservation> PersistedFormatting);
public sealed record ViewerVisualGroup(string Id, VisualGroupInventory Definition, bool IsHidden,
    ViewerReference ParentGroup);
public sealed record ViewerInteraction(VisualInteractionInventory Evidence, ViewerReference Source,
    ViewerReference Target);
public sealed record ViewerAction(VisualActionInventory Evidence, ViewerReference PageTarget, ViewerReference BookmarkTarget);
public sealed record ViewerTooltip(VisualTooltipBindingInventory Evidence, ViewerReference PageTarget);
public sealed record ViewerBookmark(string Id, BookmarkInventory Definition, ViewerReference ActivePage,
    IReadOnlyList<ViewerReference> TargetVisuals, IReadOnlyList<ViewerReference> CapturedVisuals);
public sealed record ViewerThemeReview(ThemeStatusInventory Status, IReadOnlyList<ViewerThemeDeviation> Deviations,
    IReadOnlyList<ViewerConsistencyObservation> ConsistencyObservations,
    IReadOnlyList<ViewerThemeAccessibilityObservation> AccessibilityObservations);
public sealed record ViewerThemeDeviation(ThemeDeviationInventory Evidence, ViewerReference Visual);
public sealed record ViewerConsistencyObservation(ConsistencyObservation Evidence, ViewerReference Visual);
public sealed record ViewerThemeAccessibilityObservation(ThemeAccessibilityObservation Evidence, ViewerReference Visual);
public sealed record ViewerSavedReference(VisualFieldReference Evidence, string ReferenceOrigin,
    string ReferenceRelevance, bool IsHiddenProjection, string? FormattingObject,
    string? FormattingProperty, string? SelectorKind, string? MatchedProjectionQueryRef,
    IReadOnlyList<string> ConfirmedUsageTargetIds, ViewerReference RecordedObject);
public sealed record ViewerReportMeasure(string Id, string ReportId, string? ModelId,
    ReportMeasureInventory Definition, bool DirectUsageEvidenceAvailable, int DirectEvidenceCount);
public sealed record ViewerUsage(string ObjectId, ViewerReference Location, SemanticUsageEvidence Evidence,
    bool IsHiddenProjection, string? UserFacing);
public sealed record ViewerUnresolvedReportReference(UnresolvedSemanticReference Evidence,
    ViewerReference Location, string ReferenceOrigin, string ReferenceRelevance,
    string? FormattingObject, string? FormattingProperty, string? SelectorKind,
    string? MatchedProjectionQueryRef);

public sealed record ViewerRelationship(string Id, string ModelId, SemanticRelationshipInventory Definition,
    ViewerReference FromColumn, ViewerReference ToColumn, IReadOnlyList<ViewerActivationSource> ActivationSources);
public sealed record ViewerActivationSource(SemanticRelationshipActivationSourceInventory Evidence,
    ViewerReference Source);
public sealed record ViewerRole(string Id, string ModelId, string Name, string? ModelPermission,
    string RelativePath, IReadOnlyList<string> UnanalyzedConstructs, IReadOnlyList<ViewerPermissionFact> Permissions);
public sealed record ViewerPermissionFact(SemanticTablePermissionInventory Definition, ViewerReference Table,
    IReadOnlyList<ViewerColumnPermissionFact> Columns);
public sealed record ViewerColumnPermissionFact(SemanticColumnPermissionInventory Definition, ViewerReference Column);

public sealed record ViewerQuery(string Id, string ModelId, ViewerReference Table, PowerQueryUsage Definition,
    bool IsParameter, string? ParameterType, bool? IsParameterRequired, IReadOnlyList<ViewerReference> RefreshPolicyTables);
public sealed record ViewerQueryDependency(PowerQueryDependencyEdge Evidence, ViewerReference Source,
    ViewerReference Target, string TargetIdentityPrecision);
public sealed record ViewerDataSource(DataSourceInventory Evidence, ViewerReference Query);
public sealed record ViewerQueryColumnUsage(PowerQueryColumnUsage Evidence, ViewerReference SourceQuery,
    ViewerReference ConsumerQuery, ViewerReference SourceColumn);
public sealed record ViewerTableQueryContext(SemanticTablePowerQueryContext Evidence,
    ViewerReference Table, ViewerReference Query);

public sealed record ViewerFinding(string Id, AssuranceFinding Evidence,
    IReadOnlyList<FindingReferenceContext> ReferenceContexts, ViewerReference AffectedEntity, ViewerReference DefinitionContext);
public sealed record ViewerLimitation(string Id, AnalysisLimitation Evidence, ViewerReference Context,
    string ReachKind, IReadOnlyList<string>? KnownReachObjectIds, IReadOnlyList<string> UnmappedReachKeys);
/// <summary>A Core qualification decision, not a claim of exact physical impact.</summary>
public sealed record ViewerQualification(string ObjectId, string LimitationId, string Basis);
