using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting;

/// <summary>
/// Object-focused lineage, projected once from what the scan has already published.
///
/// Nothing here classifies, traverses or resolves. Every neighbour is one end of a published edge,
/// every state is the neighbour's own usage row, every report location is the direct-usage evidence
/// the scanner kept, and every unresolved reference stays a note. The projection only indexes those
/// facts around one focus at a time, so the report can show "what does this depend on, and what uses
/// it" without the reader reconstructing it from separate lists.
///
/// A few presentation rules are applied, each of which removes a misleading reading rather than
/// adding a claim:
/// <list type="bullet">
/// <item>Containing-table edges are not listed. Every object has one, and listing them would present
/// "this column is in table Sales" as if it were a dependency.</item>
/// <item>Several edge kinds between the same two nodes are one neighbour with combined labels.</item>
/// <item>Edges whose source is a relationship, role, perspective or refresh policy are "required by
/// the model", never "used by": the scanner treats those sources as structure, not as consumers that a
/// report path can run through.</item>
/// <item>Each group is capped, with its total kept, so a hub object cannot make the report unbounded.</item>
/// </list>
/// </summary>
internal sealed class SemanticLineageProjection
{
    /// <summary>The most neighbours of one kind a single card lists. The total is always kept.</summary>
    public const int GroupLimit = 50;

    /// <summary>
    /// How many neighbours a compact presentation shows before offering the rest. The text cards show
    /// every listed neighbour; items beyond this are marked so a later layout can collapse them.
    /// </summary>
    public const int PreviewLimit = 8;

    /// <summary>The relationship label of an ordinary DAX reference, the default edge.</summary>
    public const string DaxLabel = "DAX";

    private static readonly HashSet<string> StructuralSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        SemanticObjectTypes.Relationship,
        SemanticObjectTypes.Role,
        SemanticObjectTypes.Perspective,
        SemanticObjectTypes.RefreshPolicy,
    };

    /// <summary>How many branch heads or structural sources a no-path explanation names before summarising.</summary>
    public const int PathNoteLimit = 5;

    private readonly Dictionary<string, LineageCard> cardsByNode;
    private readonly Dictionary<string, LineageCard> cardsByVisual;
    private readonly Dictionary<string, LineageCard> cardsByReportMeasure;
    private readonly Dictionary<string, string> objectRowIds;
    private readonly Dictionary<string, SemanticUsageReason?> reasons;
    private readonly Dictionary<string, string?> reportModels;
    private readonly Dictionary<string, string> reportPaths;

    private SemanticLineageProjection(
        IReadOnlyList<LineageCard> cards,
        Dictionary<string, LineageCard> cardsByNode,
        Dictionary<string, LineageCard> cardsByVisual,
        Dictionary<string, LineageCard> cardsByReportMeasure,
        Dictionary<string, string> objectRowIds,
        Dictionary<string, SemanticUsageReason?> reasons,
        Dictionary<string, string?> reportModels,
        Dictionary<string, string> reportPaths)
    {
        Cards = cards;
        this.cardsByNode = cardsByNode;
        this.cardsByVisual = cardsByVisual;
        this.cardsByReportMeasure = cardsByReportMeasure;
        this.objectRowIds = objectRowIds;
        this.reasons = reasons;
        this.reportModels = reportModels;
        this.reportPaths = reportPaths;
    }

    public IReadOnlyList<LineageCard> Cards { get; }

    public static SemanticLineageProjection Build(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return new Builder(inventory).Build();
    }

    public static string NodeKey(SemanticObjectUsage usage) =>
        SemanticGraphIndex.NodeKey(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName);

    public LineageCard? CardFor(SemanticObjectUsage usage) => cardsByNode.GetValueOrDefault(NodeKey(usage));

    public LineageCard? CardForNode(string nodeKey) => cardsByNode.GetValueOrDefault(nodeKey);

    public LineageCard? CardForVisual(string report, string page, string visual) =>
        cardsByVisual.GetValueOrDefault(VisualKey(report, page, visual));

    /// <summary>The id of an object's row in the Semantic model section.</summary>
    public string ObjectRowId(SemanticObjectUsage usage) => objectRowIds[NodeKey(usage)];

    /// <summary>The existing usage reason, computed once and shared with the Semantic model rows.</summary>
    public string? ReasonFor(SemanticObjectUsage usage) =>
        reasons.GetValueOrDefault(NodeKey(usage))?.Text;

    /// <summary>
    /// The lineage card for a field a report refers to: the report's own report measure when it defines
    /// one of that name, otherwise the object in the report's own model.
    /// </summary>
    public LineageCard? CardForReference(ReportInventory report, VisualFieldReference reference)
    {
        if (reference.ObjectType == SemanticObjectTypes.Measure &&
            CardForReportMeasure(report, reference.Table, reference.ObjectName) is { } reportMeasure)
        {
            return reportMeasure;
        }

        var model = reportModels.GetValueOrDefault(report.Name);
        return model is null
            ? null
            : cardsByNode.GetValueOrDefault(SemanticGraphIndex.NodeKey(
                model, reference.Table, reference.ObjectName, reference.ObjectType, reference.HierarchyName));
    }

    /// <summary>The card for one report's own report measure.</summary>
    public LineageCard? CardForReportMeasure(ReportInventory report, string entity, string name) =>
        cardsByReportMeasure.GetValueOrDefault(ReportMeasureKey(SemanticNodeIdentity.ReportOwner(report.RelativePath), entity, name));

    /// <summary>The card for one report's own report measure, the report named as it is displayed.</summary>
    public LineageCard? CardForReportMeasure(string report, string entity, string name) =>
        reportPaths.TryGetValue(report, out var path)
            ? cardsByReportMeasure.GetValueOrDefault(ReportMeasureKey(path, entity, name))
            : null;

    /// <summary>The id of a report measure's entry in Report pages.</summary>
    public static string ReportMeasureRowId(string semanticModel, string report, string entity, string name) =>
        LineageIds.Create("obj", $"{report} {entity} {name}", ReportMeasureIdentity(semanticModel, report, entity, name));

    /// <summary>The user-facing label for one dependency kind, read from the edge's direction-free meaning.</summary>
    public static string RelationshipLabel(string dependencyKind, string sourceObjectType) => dependencyKind switch
    {
        SemanticDependencyKinds.Dax or SemanticDependencyKinds.ReportMeasure => DaxLabel,
        SemanticDependencyKinds.FunctionCall => string.Equals(sourceObjectType, SemanticObjectTypes.Role, StringComparison.OrdinalIgnoreCase)
            ? "security filter"
            : "function call",
        SemanticDependencyKinds.SortBy => "sort by",
        SemanticDependencyKinds.HierarchyLevel => "hierarchy level",
        SemanticDependencyKinds.FieldParameter => "field parameter",
        SemanticDependencyKinds.CalculationGroupItem => "calculation item",
        SemanticDependencyKinds.AggregationMapping => "aggregation detail",
        SemanticDependencyKinds.RelationshipEndpoint => "relationship key",
        SemanticDependencyKinds.TablePermission => "security filter",
        SemanticDependencyKinds.ObjectLevelPermission => "object-level security",
        SemanticDependencyKinds.PerspectiveMember => "perspective",
        SemanticDependencyKinds.IncrementalRefreshPolicy => "incremental refresh",
        SemanticDependencyKinds.ContainingTable => "in table",
        _ => dependencyKind,
    };

    public static string ObjectTypeLabel(string objectType) => objectType switch
    {
        SemanticObjectTypes.ReportMeasure => "Report measure",
        SemanticObjectTypes.HierarchyLevel => "Hierarchy level",
        SemanticObjectTypes.CalculationItem => "Calculation item",
        SemanticObjectTypes.Role => "Security role",
        SemanticObjectTypes.RefreshPolicy => "Incremental refresh policy",
        _ => objectType,
    };

    private static string VisualKey(string report, string page, string visual) =>
        string.Join('\u001f', report, page, visual);

    /// <summary>One report's report measure: the report's project-relative path, the entity and the name.</summary>
    private static string ReportMeasureKey(string reportPath, string entity, string name) =>
        string.Join('\u001f', reportPath, entity, name);

    /// <summary>The identity of a report measure's row in Report pages, which is drawn per report.</summary>
    private static string ReportMeasureIdentity(string semanticModel, string report, string entity, string name) =>
        string.Join('\u001e', SemanticObjectTypes.ReportMeasure, semanticModel, report, entity, name);

    private static int KindOrder(string label) => label switch
    {
        "sort by" => 0,
        "hierarchy level" => 1,
        "field parameter" => 2,
        "calculation item" => 3,
        "aggregation detail" => 4,
        "function call" => 5,
        "relationship key" => 6,
        "security filter" => 7,
        "object-level security" => 8,
        "perspective" => 9,
        "incremental refresh" => 10,
        DaxLabel => 20,
        _ => 15,
    };

    private static int UsageOrder(string? usageState) => usageState switch
    {
        SemanticUsageStates.DirectlyUsed => 0,
        SemanticUsageStates.IndirectlyUsed => 1,
        SemanticUsageStates.StructurallyRequired => 2,
        SemanticUsageStates.UsedOnlyByUnusedBranch => 3,
        SemanticUsageStates.ApparentlyUnused => 4,
        _ => 5,
    };

    private static LineageGroup<T> Cap<T>(IEnumerable<T> ordered)
    {
        var all = ordered.ToArray();
        return new LineageGroup<T>(all.Take(GroupLimit).ToArray(), all.Length);
    }

    /// <summary>
    /// Anything that owns direct report-usage evidence: a model object, or one report's report measure
    /// with that report's display name and project-relative path.
    /// </summary>
    private sealed record UsageOwner(
        string NodeKey,
        string CardId,
        string SemanticModel,
        string Table,
        string ObjectName,
        string ObjectType,
        string? HierarchyName,
        string? Report,
        string? ReportPath,
        IReadOnlyList<SemanticUsageEvidence> References);

    private sealed class Builder(ProjectInventory inventory)
    {
        /// <summary>The most nodes the search for the start of an unused branch visits.</summary>
        private const int BranchSearchLimit = 500;

        private readonly Dictionary<string, LineageGroup<LineageReportLocation>> terminalLocations = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemanticGraphIndex index = SemanticGraphIndex.For(inventory);
        private readonly Dictionary<string, SemanticObjectUsage> usagesByNode = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> tableStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SemanticModelInventory> models = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> nodeCardIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> visualCardIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> objectRowIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SemanticUsageReason?> reasons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<UnresolvedSemanticDependency>> unresolvedBySource = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<UnresolvedSemanticDependency>> ambiguityByCandidate = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<(UsageOwner Owner, SemanticUsageLocation Location)>> visualUses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UsageOwner> reportMeasures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> reportMeasureNameCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> tablesWithObjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (ReportInventory Report, PageInventory Page, VisualInventory Visual)> visuals = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ReportInventory> reports = new(StringComparer.OrdinalIgnoreCase);

        public SemanticLineageProjection Build()
        {
            foreach (var model in inventory.SemanticModels)
            {
                models.TryAdd(model.Name, model);
            }

            foreach (var table in inventory.SemanticTableUsages)
            {
                tableStates.TryAdd(TableKey(table.SemanticModel, table.Table), table.UsageState);
            }

            foreach (var report in inventory.Reports)
            {
                reports.TryAdd(report.Name, report);
                foreach (var page in report.Pages)
                {
                    foreach (var visual in page.Visuals)
                    {
                        visuals.TryAdd(VisualKey(report.Name, page.Name, visual.Name), (report, page, visual));
                    }
                }
            }

            foreach (var usage in inventory.SemanticObjectUsages)
            {
                var key = NodeKey(usage);
                if (!usagesByNode.TryAdd(key, usage))
                {
                    continue;
                }

                var readable = string.Join(' ', usage.Table, usage.HierarchyName, usage.ObjectName);
                nodeCardIds.Add(key, LineageIds.Create("lin", readable, key));
                objectRowIds.Add(key, LineageIds.Create("obj", readable, key));
                reasons.Add(key, SemanticUsagePresentation.ExplainReason(inventory, usage));

                tablesWithObjects.Add(TableKey(usage.SemanticModel, usage.Table));
                AddVisualUses(new UsageOwner(key, nodeCardIds[key], usage.SemanticModel, usage.Table, usage.ObjectName,
                    usage.ObjectType, usage.HierarchyName, Report: null, ReportPath: null, usage.DirectReportReferences));
            }

            // A report measure is its own report's graph node, so it is a card, a neighbour and a path step
            // like any other node. Same-named report measures in other reports are other nodes.
            var cardsByReportMeasure = new Dictionary<string, LineageCard>(StringComparer.OrdinalIgnoreCase);
            var reportMeasureKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reportMeasure in inventory.ReportMeasureUsages)
            {
                var nodeKey = SemanticGraphIndex.NodeKey(reportMeasure.SemanticModel, reportMeasure.Entity, reportMeasure.Name,
                    SemanticObjectTypes.ReportMeasure, null, reportMeasure.ReportPath);
                var owner = new UsageOwner(
                    nodeKey,
                    LineageIds.Create("lin", $"report measure {reportMeasure.Report} {reportMeasure.Entity} {reportMeasure.Name}", nodeKey),
                    reportMeasure.SemanticModel,
                    reportMeasure.Entity,
                    reportMeasure.Name,
                    SemanticObjectTypes.ReportMeasure,
                    HierarchyName: null,
                    reportMeasure.Report,
                    reportMeasure.ReportPath,
                    reportMeasure.DirectReportReferences);
                if (!reportMeasures.TryAdd(nodeKey, owner))
                {
                    continue;
                }

                reportMeasureKeys.Add(nodeKey, ReportMeasureKey(reportMeasure.ReportPath, reportMeasure.Entity, reportMeasure.Name));
                nodeCardIds.TryAdd(nodeKey, owner.CardId);
                var nameKey = ReportMeasureNameKey(reportMeasure.SemanticModel, reportMeasure.Entity, reportMeasure.Name);
                reportMeasureNameCounts[nameKey] = reportMeasureNameCounts.GetValueOrDefault(nameKey) + 1;
                AddVisualUses(owner);
            }

            var functionNodes = index.Nodes
                .Where(node => node.ObjectType == SemanticObjectTypes.Function)
                .ToArray();
            foreach (var function in functionNodes)
            {
                var key = SemanticGraphIndex.NodeKey(function.SemanticModel, function.Table, function.ObjectName, function.ObjectType, function.HierarchyName);
                nodeCardIds.TryAdd(key, LineageIds.Create("lin", "function " + function.ObjectName, key));
            }

            foreach (var unresolved in inventory.UnresolvedSemanticDependencies)
            {
                Add(unresolvedBySource, SemanticGraphIndex.NodeKey(
                    unresolved.SemanticModel, unresolved.FromTable, unresolved.FromObjectName,
                    unresolved.FromObjectType, unresolved.FromHierarchyName, unresolved.FromReport), unresolved);
                if (unresolved.ResolutionOutcome != UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous ||
                    unresolved.CandidateTargets is null)
                {
                    continue;
                }

                foreach (var candidate in unresolved.CandidateTargets)
                {
                    Add(ambiguityByCandidate, string.Join('\u001e', unresolved.SemanticModel, candidate), unresolved);
                }
            }

            var reportModels = inventory.Reports
                .GroupBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => ReportModelBinder.FindLocalModel(group.First(), inventory.SemanticModels)?.Name,
                    StringComparer.OrdinalIgnoreCase);
            var reportPaths = inventory.Reports
                .GroupBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => SemanticNodeIdentity.ReportOwner(group.First().RelativePath),
                    StringComparer.OrdinalIgnoreCase);

            var cards = new List<LineageCard>();
            var cardsByNode = new Dictionary<string, LineageCard>(StringComparer.OrdinalIgnoreCase);
            var cardsByVisual = new Dictionary<string, LineageCard>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, usage) in usagesByNode)
            {
                var card = SemanticCard(key, usage);
                cards.Add(card);
                cardsByNode.Add(key, card);
            }

            foreach (var function in functionNodes)
            {
                var key = SemanticGraphIndex.NodeKey(function.SemanticModel, function.Table, function.ObjectName, function.ObjectType, function.HierarchyName);
                if (cardsByNode.ContainsKey(key))
                {
                    continue;
                }

                var card = FunctionCard(key, function);
                cards.Add(card);
                cardsByNode.Add(key, card);
            }

            foreach (var (nodeKey, owner) in reportMeasures)
            {
                var card = ReportMeasureCard(owner);
                cards.Add(card);
                cardsByNode.TryAdd(nodeKey, card);
                cardsByReportMeasure.TryAdd(reportMeasureKeys[nodeKey], card);
            }

            foreach (var (visualKey, uses) in visualUses)
            {
                var card = VisualCard(visualKey, uses);
                cards.Add(card);
                cardsByVisual.Add(visualKey, card);
            }

            return new SemanticLineageProjection(
                cards, cardsByNode, cardsByVisual, cardsByReportMeasure, objectRowIds, reasons, reportModels, reportPaths);
        }

        private static string ReportMeasureNameKey(string model, string entity, string name) =>
            SemanticGraphIndex.NodeKey(model, entity, name, SemanticObjectTypes.ReportMeasure, null);

        /// <summary>
        /// Whether every report-measure end of this edge is a report measure this projection knows, owned
        /// by the report whose extension file the edge was read from. The scanner always records both, so
        /// this only fails where an inventory's ownership is missing or contradicts its evidence. Such an
        /// edge is never followed, and its report-measure end is shown without a link or a reachability
        /// claim: no report's measure is credited with it.
        /// </summary>
        private bool IsAttributed(SemanticDependencyEdge edge) =>
            SemanticGraphIndex.HasConsistentOwnership(edge) &&
            (edge.FromObjectType != SemanticObjectTypes.ReportMeasure || reportMeasures.ContainsKey(SemanticGraphIndex.SourceKey(edge))) &&
            (edge.ToObjectType != SemanticObjectTypes.ReportMeasure || reportMeasures.ContainsKey(SemanticGraphIndex.TargetKey(edge)));

        private void AddVisualUses(UsageOwner owner)
        {
            foreach (var location in SemanticUsageLocation.Distinct(owner.References))
            {
                if (location.Visual is null || location.Page is null)
                {
                    continue;
                }

                var visualKey = VisualKey(location.Report, location.Page, location.Visual);
                if (!visuals.TryGetValue(visualKey, out var visual))
                {
                    continue;
                }

                if (!visualUses.TryGetValue(visualKey, out var uses))
                {
                    uses = [];
                    visualUses.Add(visualKey, uses);
                    visualCardIds.Add(visualKey, LineageIds.Create(
                        "lin",
                        HtmlReportRenderer.VisualAnchor(visual.Report, visual.Page, visual.Visual),
                        visualKey,
                        maximumTokenLength: 96));
                }

                uses.Add((owner, location));
            }
        }

        private LineageCard SemanticCard(string key, SemanticObjectUsage usage)
        {
            var reason = reasons[key];
            var (usedBy, requiredByModel) = Incoming(key, reason?.Dependency);
            var usedInReport = ReportLocations(usage.DirectReportReferences);
            return new LineageCard(
                nodeCardIds[key],
                LineageFocusKind.SemanticObject,
                DisplayName(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName),
                usage.ObjectType)
            {
                NodeKey = key,
                SemanticModel = usage.SemanticModel,
                Table = usage.Table,
                Usage = usage,
                Reachability = index.ReachabilityOfNode(key),
                Reason = reason?.Text,
                DetailsAnchor = objectRowIds[key],
                PowerQuery = PowerQueryContext(usage),
                DependsOn = Outgoing(key, reason?.Dependency),
                NotResolved = NotResolved(key, usage.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                UsedInReport = usedInReport,
                PossibleUse = PossibleUse(usage),
                Path = PathFor(key, usage.IsDirectlyReferencedByReport ? usedInReport : null, usage, reason?.Text, requiredByModel),
            };
        }

        private LineageCard FunctionCard(string key, SemanticNodeReachability function)
        {
            var (usedBy, requiredByModel) = Incoming(key, reasonDependency: null);
            return new LineageCard(
                nodeCardIds[key],
                LineageFocusKind.Function,
                function.ObjectName,
                SemanticObjectTypes.Function)
            {
                NodeKey = key,
                SemanticModel = function.SemanticModel,
                Reachability = function,
                ReachedFromReport = function.ReachableFromReport,
                DependsOn = Outgoing(key, reasonDependency: null),
                NotResolved = NotResolved(key, function.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                Path = PathFor(key, directLocations: null, usage: null, reason: null, requiredByModel),
            };
        }

        /// <summary>
        /// One report's report measure: its own node, so its own relationships, report locations and
        /// reachability, all as the scanner published them.
        /// </summary>
        private LineageCard ReportMeasureCard(UsageOwner owner)
        {
            var key = owner.NodeKey;
            var reachability = index.ReachabilityOfNode(key);
            var (usedBy, requiredByModel) = Incoming(key, reasonDependency: null);
            var usedInReport = ReportLocations(owner.References);
            return new LineageCard(
                owner.CardId,
                LineageFocusKind.ReportMeasure,
                DisplayName(owner.SemanticModel, owner.Table, owner.ObjectName, owner.ObjectType, null),
                SemanticObjectTypes.ReportMeasure)
            {
                NodeKey = key,
                SemanticModel = owner.SemanticModel,
                Table = owner.Table,
                Report = reports.GetValueOrDefault(owner.Report!),
                ReportName = owner.Report,
                Reachability = reachability,
                ReachedFromReport = reachability?.ReachableFromReport,
                DetailsAnchor = ReportMeasureRowId(owner.SemanticModel, owner.Report!, owner.Table, owner.ObjectName),
                DependsOn = Outgoing(key, reasonDependency: null),
                NotResolved = NotResolved(key, owner.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                UsedInReport = usedInReport,
                Path = PathFor(key, owner.References.Count > 0 ? usedInReport : null, usage: null, reason: null, requiredByModel),
            };
        }

        /// <summary>
        /// A visual's objects are exactly the objects whose direct-usage evidence names this visual, so
        /// the visual card and each object's "Used in the report" are two views of one relation. Raw
        /// visual field references are deliberately not used: the scanner's direct-usage policy excludes
        /// some of them, and showing those here would contradict the object's own card.
        /// </summary>
        private LineageCard VisualCard(string visualKey, List<(UsageOwner Owner, SemanticUsageLocation Location)> uses)
        {
            var (report, page, visual) = visuals[visualKey];
            var items = uses
                .Select(use => new LineageVisualUse(
                    Neighbour(use.Owner.SemanticModel, use.Owner.Table, use.Owner.ObjectName, use.Owner.ObjectType,
                        use.Owner.HierarchyName, use.Owner.ReportPath, [], isReasonSource: false),
                    Location(use.Owner.References, use.Location)))
                .OrderBy(use => use.Object.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(use => use.Object.NodeKey, StringComparer.Ordinal);
            var unresolved = inventory.UnresolvedSemanticReferences
                .Where(reference =>
                    string.Equals(reference.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.Page, page.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.Visual, visual.Name, StringComparison.OrdinalIgnoreCase) &&
                    SemanticReportReferencePolicy.EstablishesDirectUsage(new VisualFieldReference(
                        reference.Table, reference.ObjectName, reference.ObjectType, reference.HierarchyName,
                        reference.UsageContext, reference.Role, reference.EvidencePath)
                    {
                        ReferenceOrigin = reference.ReferenceOrigin,
                        ReferenceRelevance = reference.ReferenceRelevance,
                    }))
                .DistinctBy(reference => FieldIdentity.Create(reference.Table, reference.ObjectName, reference.ObjectType, reference.HierarchyName), StringComparer.OrdinalIgnoreCase)
                .OrderBy(reference => reference.Table, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reference => reference.ObjectName, StringComparer.OrdinalIgnoreCase);

            return new LineageCard(
                visualCardIds[visualKey],
                LineageFocusKind.Visual,
                visual.Name,
                "Visual")
            {
                Report = report,
                Page = page,
                Visual = visual,
                DetailsAnchor = HtmlReportRenderer.VisualAnchor(report, page, visual),
                Uses = Cap(items),
                UnresolvedReportReferences = Cap(unresolved),
            };
        }

        /// <summary>
        /// Path to report, read entirely from facts the scanner published; nothing is reclassified.
        ///
        /// A focus with its own direct report evidence needs no walk: its first report location, in the
        /// cards' location order, is the endpoint. Otherwise, when published reachability says the focus is
        /// reached from a report, a breadth-first walk follows incoming edges back through predecessors
        /// that are themselves reached from a report, over the same edges the classifier traverses —
        /// containing-table and calculation-group hops included, structural sources excluded — and stops
        /// at the first item with direct report evidence. Breadth-first gives the fewest hops; ties go to
        /// the stable qualified order used elsewhere. A report measure is its own report's node, so a walk
        /// through one stays in its report and ends only at that report's locations.
        /// </summary>
        private LineagePath PathFor(
            string focusKey,
            LineageGroup<LineageReportLocation>? directLocations,
            SemanticObjectUsage? usage,
            string? reason,
            LineageGroup<LineageNeighbour> requiredByModel)
        {
            var focus = FocusStep(focusKey);
            if (directLocations is { TotalCount: > 0 })
            {
                return new LineagePath(LineagePathStatus.DirectlyUsed, [focus], directLocations.Items[0], directLocations.TotalCount, 0);
            }

            var reachability = index.ReachabilityOfNode(focusKey);
            var path = new LineagePath(LineagePathStatus.NotFound, [focus], null, 0, 0)
            {
                ChecksLimited = usage?.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation,
            };
            if (reachability?.ReachableFromReport == true)
            {
                // A walk finds a path whenever the published facts are consistent; where they are not,
                // the card claims no path rather than explaining one it cannot show.
                return SearchTowardReport(focusKey, focus) ?? path;
            }

            if (usage?.UsageState == SemanticUsageStates.StructurallyRequired || reachability?.ReachableFromModelStructure == true)
            {
                return path with
                {
                    RequiredByModelStructure = true,
                    StructuralSources = requiredByModel,
                    StructuralReason = usage?.UsageState == SemanticUsageStates.StructurallyRequired ? reason : null,
                };
            }

            return Predecessors(focusKey).Length == 0
                ? path
                : path with { OnlyReachedFrom = BranchHeads(focusKey) };
        }

        private LineagePath? SearchTowardReport(string focusKey, LineagePathStep focus)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { focusKey };
            var next = new Dictionary<string, (string Toward, IReadOnlyList<SemanticDependencyEdge> Edges)>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(focusKey);
            var otherFirstHops = 0;
            while (queue.TryDequeue(out var current))
            {
                var predecessors = Predecessors(current)
                    .Where(item => index.ReachabilityOfNode(item.Source)?.ReachableFromReport == true)
                    .ToArray();
                if (string.Equals(current, focusKey, StringComparison.OrdinalIgnoreCase))
                {
                    otherFirstHops = Math.Max(0, predecessors.Length - 1);
                }

                foreach (var (source, edges) in predecessors)
                {
                    if (!visited.Add(source))
                    {
                        continue;
                    }

                    next[source] = (current, edges);
                    var endpoint = TerminalLocations(source);
                    if (endpoint.TotalCount > 0)
                    {
                        var chain = new List<LineagePathStep>();
                        for (var key = source; !string.Equals(key, focusKey, StringComparison.OrdinalIgnoreCase); key = next[key].Toward)
                        {
                            chain.Add(Step(next[key].Edges));
                        }

                        chain.Reverse();
                        return new LineagePath(
                            LineagePathStatus.ReachedThroughModel,
                            [focus, .. chain],
                            endpoint.Items[0],
                            endpoint.TotalCount,
                            otherFirstHops);
                    }

                    queue.Enqueue(source);
                }
            }

            return null;
        }

        /// <summary>
        /// The nodes whose edges the classifier traverses into this one, grouped per node, in the stable
        /// qualified order. Structural sources (relationships, roles, perspectives, refresh policies) are
        /// never traversed by the classifier, so they are never path steps; nor is an edge whose report
        /// measure no report can be identified for.
        /// </summary>
        private (string Source, IReadOnlyList<SemanticDependencyEdge> Edges)[] Predecessors(string nodeKey) =>
            index.IncomingToNode(nodeKey)
                .Where(edge => IsTraversedSource(edge) && IsAttributed(edge))
                .GroupBy(SemanticGraphIndex.SourceKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => (Source: group.Key, Edges: (IReadOnlyList<SemanticDependencyEdge>)group.ToArray()))
                .OrderBy(item => item.Edges[0].FromTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Edges[0].FromObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Edges[0].FromObjectType, StringComparer.Ordinal)
                .ThenBy(item => item.Edges[0].FromHierarchyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Edges[0].FromReport ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Source, StringComparer.Ordinal)
                .ToArray();

        /// <summary>Whether the classifier's traversal follows edges from this edge's source.</summary>
        private bool IsTraversedSource(SemanticDependencyEdge edge)
        {
            if (StructuralSourceTypes.Contains(edge.FromObjectType))
            {
                return false;
            }

            return edge.FromObjectType switch
            {
                SemanticObjectTypes.Table => tablesWithObjects.Contains(TableKey(edge.SemanticModel, edge.FromTable)),
                SemanticObjectTypes.ReportMeasure or SemanticObjectTypes.Function => true,
                _ => usagesByNode.ContainsKey(SemanticGraphIndex.SourceKey(edge)),
            };
        }

        /// <summary>
        /// A node's own direct report locations in the cards' order: a model object's, or a report
        /// measure's in its own report.
        /// </summary>
        private LineageGroup<LineageReportLocation> TerminalLocations(string nodeKey)
        {
            if (terminalLocations.TryGetValue(nodeKey, out var cached))
            {
                return cached;
            }

            IEnumerable<LineageReportLocation> locations = [];
            if (reportMeasures.TryGetValue(nodeKey, out var owner))
            {
                locations = SemanticUsageLocation.Distinct(owner.References)
                    .Select(location => Location(owner.References, location));
            }
            else if (usagesByNode.TryGetValue(nodeKey, out var usage))
            {
                locations = SemanticUsageLocation.Distinct(usage.DirectReportReferences)
                    .Select(location => Location(usage.DirectReportReferences, location));
            }

            var result = Cap(OrderLocations(locations));
            terminalLocations.Add(nodeKey, result);
            return result;
        }

        private LineagePathStep FocusStep(string key)
        {
            var neighbour = reportMeasures.TryGetValue(key, out var owner)
                ? Neighbour(owner.SemanticModel, owner.Table, owner.ObjectName, owner.ObjectType, owner.HierarchyName, owner.ReportPath, [], false)
                : usagesByNode.TryGetValue(key, out var usage)
                    ? Neighbour(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName, null, [], false)
                    : index.ReachabilityOfNode(key) is { } node
                        ? Neighbour(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName, node.Report, [], false)
                        : throw new InvalidOperationException($"No lineage node for '{key}'.");
            return PathStep(neighbour with { CardId = null });
        }

        /// <summary>One hop toward the report: the item, and how it uses the previous one.</summary>
        private LineagePathStep Step(IReadOnlyList<SemanticDependencyEdge> edges) =>
            PathStep(SourceNeighbour(edges[0], edges, false));

        private static LineagePathStep PathStep(LineageNeighbour neighbour) =>
            new(neighbour.NodeKey, neighbour.CardId, neighbour.Name, neighbour.ObjectType, neighbour.UsageState,
                neighbour.ReachableFromReport, neighbour.RelationshipLabels, neighbour.ReportCount)
            {
                Report = neighbour.Report,
            };

        /// <summary>
        /// Where an unused branch starts: the items above the focus that nothing traversed references. They
        /// are what the focus is reached from, and nothing reaches them from a report. A cycle with no
        /// start names the focus's immediate referrers instead.
        /// </summary>
        private LineageGroup<LineageNeighbour> BranchHeads(string focusKey)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { focusKey };
            var edgesInto = new Dictionary<string, IReadOnlyList<SemanticDependencyEdge>>(StringComparer.OrdinalIgnoreCase);
            var heads = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(focusKey);
            while (queue.TryDequeue(out var current) && visited.Count <= BranchSearchLimit)
            {
                var predecessors = Predecessors(current);
                if (!string.Equals(current, focusKey, StringComparison.OrdinalIgnoreCase) && predecessors.Length == 0)
                {
                    heads.Add(current);
                }

                foreach (var (source, edges) in predecessors)
                {
                    if (visited.Add(source))
                    {
                        edgesInto[source] = edges;
                        queue.Enqueue(source);
                    }
                }
            }

            var keys = heads.Count > 0 ? heads : Predecessors(focusKey).Select(item => item.Source).ToList();
            return Cap(keys
                .Select(key => SourceNeighbour(edgesInto[key][0], [], false))
                .OrderBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.Report ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal));
        }

        /// <summary>What the focus depends on. A report measure's are its own report's edges.</summary>
        private LineageGroup<LineageNeighbour> Outgoing(string nodeKey, SemanticDependencyEdge? reasonDependency)
        {
            var neighbours = index.OutgoingFromNode(nodeKey)
                .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable)
                .GroupBy(SemanticGraphIndex.TargetKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var edges = group.ToArray();
                    var first = edges[0];
                    return Neighbour(first.SemanticModel, first.ToTable, first.ToObjectName, first.ToObjectType, first.ToHierarchyName,
                        first.ToReport, edges, edges.Contains(reasonDependency), edges.All(IsAttributed));
                })
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.ObjectType, StringComparer.Ordinal)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            return Cap(neighbours);
        }

        /// <summary>
        /// What uses the focus. Each report's report measure that uses it is its own neighbour, linked to
        /// its own card with its own reachability.
        /// </summary>
        private (LineageGroup<LineageNeighbour> UsedBy, LineageGroup<LineageNeighbour> RequiredByModel) Incoming(
            string nodeKey,
            SemanticDependencyEdge? reasonDependency)
        {
            var neighbours = index.IncomingToNode(nodeKey)
                .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable)
                .GroupBy(SemanticGraphIndex.SourceKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var edges = group.ToArray();
                    return SourceNeighbour(edges[0], edges, edges.Contains(reasonDependency));
                })
                .ToArray();

            var usedBy = neighbours
                .Where(neighbour => !StructuralSourceTypes.Contains(neighbour.ObjectType))
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenByDescending(neighbour => neighbour.ReachableFromReport == true)
                .ThenByDescending(neighbour => neighbour.ReachableFromModelStructure == true)
                .ThenBy(neighbour => UsageOrder(neighbour.UsageState))
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.Report ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            var requiredByModel = neighbours
                .Where(neighbour => StructuralSourceTypes.Contains(neighbour.ObjectType))
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenBy(neighbour => ObjectTypeLabel(neighbour.ObjectType), StringComparer.Ordinal)
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            return (Cap(usedBy), Cap(requiredByModel));
        }

        /// <summary>The source of one edge as a neighbour, with <paramref name="dependencies"/> as its relationships.</summary>
        private LineageNeighbour SourceNeighbour(
            SemanticDependencyEdge edge,
            IReadOnlyList<SemanticDependencyEdge> dependencies,
            bool isReasonSource) =>
            Neighbour(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName,
                edge.FromReport, dependencies, isReasonSource, IsAttributed(edge));

        /// <summary>
        /// One node as a neighbour: its own card, state and reachability, and for a report measure its
        /// report. An unattributed report measure keeps its name and nothing else.
        /// </summary>
        private LineageNeighbour Neighbour(
            string model,
            string table,
            string objectName,
            string objectType,
            string? hierarchyName,
            string? report,
            IReadOnlyList<SemanticDependencyEdge> dependencies,
            bool isReasonSource,
            bool isAttributed = true)
        {
            var key = SemanticGraphIndex.NodeKey(model, table, objectName, objectType, hierarchyName, report);
            var usage = usagesByNode.GetValueOrDefault(key);
            var reachability = index.ReachabilityOfNode(key);
            var usageState = usage?.UsageState ??
                (objectType == SemanticObjectTypes.Table ? tableStates.GetValueOrDefault(TableKey(model, table)) : null);
            var labels = dependencies
                .Select(edge => RelationshipLabel(edge.DependencyKind, edge.FromObjectType))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(KindOrder)
                .ThenBy(label => label, StringComparer.Ordinal)
                .ToArray();
            var neighbour = new LineageNeighbour(
                key,
                nodeCardIds.GetValueOrDefault(key),
                DisplayName(model, table, objectName, objectType, hierarchyName),
                objectType,
                labels,
                usageState,
                usage?.ClassificationConfidence,
                reachability?.ReachableFromReport,
                reachability?.ReachableFromModelStructure,
                isReasonSource,
                dependencies)
            {
                ReportCount = objectType == SemanticObjectTypes.ReportMeasure
                    ? reportMeasureNameCounts.GetValueOrDefault(ReportMeasureNameKey(model, table, objectName))
                    : 0,
                Report = reportMeasures.GetValueOrDefault(key)?.Report,
            };
            return isAttributed
                ? neighbour
                : neighbour with { CardId = null, Report = null, ReachableFromReport = null, ReachableFromModelStructure = null };
        }

        private LineageGroup<LineageReportLocation> ReportLocations(IReadOnlyList<SemanticUsageEvidence> references) =>
            Cap(OrderLocations(SemanticUsageLocation.Distinct(references).Select(location => Location(references, location))));

        private static IOrderedEnumerable<LineageReportLocation> OrderLocations(IEnumerable<LineageReportLocation> locations) =>
            locations
                .OrderBy(location => location.Location.Report, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Page?.Order ?? int.MaxValue)
                .ThenBy(location => location.Page?.DisplayName ?? location.Location.Page ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Visual is null ? 0 : 1)
                .ThenBy(location => location.Visual?.Position.Y ?? double.MaxValue)
                .ThenBy(location => location.Visual?.Position.X ?? double.MaxValue)
                .ThenBy(location => location.Location.Visual ?? location.Location.UsageContext ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private LineageReportLocation Location(IReadOnlyList<SemanticUsageEvidence> ownerReferences, SemanticUsageLocation location)
        {
            var report = reports.GetValueOrDefault(location.Report);
            var page = location.Page is null
                ? null
                : report?.Pages.FirstOrDefault(candidate => string.Equals(candidate.Name, location.Page, StringComparison.OrdinalIgnoreCase));
            var visualKey = location.Visual is null || location.Page is null
                ? null
                : VisualKey(location.Report, location.Page, location.Visual);
            var visual = visualKey is not null && visuals.TryGetValue(visualKey, out var match) ? match.Visual : null;
            var evidence = ownerReferences
                .Where(reference => SemanticUsageLocation.FromEvidence(reference) == location)
                .ToArray();
            return new LineageReportLocation(
                ownerReferences,
                location,
                report,
                page,
                visual,
                visualKey is null ? null : visualCardIds.GetValueOrDefault(visualKey),
                evidence);
        }

        private LineageGroup<LineageUnresolvedReference> NotResolved(string key, string model) =>
            Cap((unresolvedBySource.GetValueOrDefault(key) ?? [])
                .Select(unresolved => new LineageUnresolvedReference(
                    unresolved,
                    RelationshipLabel(unresolved.DependencyKind, unresolved.FromObjectType),
                    unresolved.CandidateTargets is null
                        ? []
                        : unresolved.CandidateTargets
                            .Select(candidate => CandidateName(model, candidate))
                            .Order(StringComparer.OrdinalIgnoreCase)
                            .ToArray()))
                .OrderBy(item => item.Dependency.ReferenceText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Dependency.DependencyKind, StringComparer.Ordinal)
                .ThenBy(item => item.Dependency.EvidencePath, StringComparer.Ordinal));

        /// <summary>
        /// References elsewhere that may mean this object. An ambiguous reference names a set of
        /// candidates without choosing one, so each candidate is told about the possibility and none is
        /// given an edge.
        /// </summary>
        private LineageGroup<LineagePossibleUse> PossibleUse(SemanticObjectUsage usage)
        {
            var candidateKey = string.Join('\u001e', usage.SemanticModel, FieldIdentity.Create(
                usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName));
            return Cap((ambiguityByCandidate.GetValueOrDefault(candidateKey) ?? [])
                .Select(unresolved => new LineagePossibleUse(
                    unresolved,
                    Neighbour(unresolved.SemanticModel, unresolved.FromTable, unresolved.FromObjectName,
                        unresolved.FromObjectType, unresolved.FromHierarchyName, unresolved.FromReport, [], isReasonSource: false),
                    unresolved.CandidateTargets!
                        .Where(candidate => !string.Equals(candidate, FieldIdentity.Create(
                            usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName), StringComparison.OrdinalIgnoreCase))
                        .Select(candidate => CandidateName(usage.SemanticModel, candidate))
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray()))
                .DistinctBy(item => string.Join('\u001e', item.Source.NodeKey, item.Dependency.ReferenceText), StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item.Source.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Dependency.ReferenceText, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// What Power Query evidence says about a column, and no more. The query that loads the column's
        /// table is known; the column's own route through that query is not, so the table is what the
        /// query is said to load. The source column name and the bounded column-usage evidence are shown
        /// as they are elsewhere in the report.
        ///
        /// A source column on its own is not Power Query evidence: calculation groups, calculated tables
        /// (field parameters, CALENDARAUTO) and entity partitions all carry one without any query. So a
        /// context exists only when a query loads the table or bounded column evidence names the column;
        /// the source column is then supporting detail within it.
        /// </summary>
        private LineagePowerQueryContext? PowerQueryContext(SemanticObjectUsage usage)
        {
            if (usage.ObjectType != SemanticObjectTypes.Column)
            {
                return null;
            }

            var queries = inventory.SemanticTablePowerQueryContexts
                .Where(context =>
                    string.Equals(context.SemanticModel, usage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(context.Table, usage.Table, StringComparison.OrdinalIgnoreCase))
                .OrderBy(context => context.QueryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(context => context.Partition, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var column = models.GetValueOrDefault(usage.SemanticModel)?.Tables
                .FirstOrDefault(table => string.Equals(table.Name, usage.Table, StringComparison.OrdinalIgnoreCase))?.Columns
                .FirstOrDefault(candidate => string.Equals(candidate.Name, usage.ObjectName, StringComparison.OrdinalIgnoreCase));
            var sourceColumn = column is { Expression: null } ? column.SourceColumn : null;
            var evidence = inventory.PowerQueryColumnUsages
                .Where(item =>
                    string.Equals(item.SemanticModel, usage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.SourceTable, usage.Table, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.SourceColumn, usage.ObjectName, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(item => string.Join('\u001f', item.ConsumerQuery, item.UsageKind), StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item.ConsumerQuery, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.UsageKind, StringComparer.Ordinal)
                .ToArray();

            return queries.Length == 0 && evidence.Length == 0
                ? null
                : new LineagePowerQueryContext(queries, sourceColumn, evidence);
        }

        private string CandidateName(string model, string fieldIdentity)
        {
            var parts = fieldIdentity.Split('\u001f');
            return parts.Length == 4
                ? DisplayName(model, parts[1], parts[3], parts[0], string.IsNullOrEmpty(parts[2]) ? null : parts[2])
                : fieldIdentity;
        }

        private string DisplayName(string model, string table, string objectName, string objectType, string? hierarchyName)
        {
            switch (objectType)
            {
                case SemanticObjectTypes.Table:
                    return table;
                case SemanticObjectTypes.Function:
                case SemanticObjectTypes.Role:
                case SemanticObjectTypes.Perspective:
                    return objectName;
                case SemanticObjectTypes.RefreshPolicy:
                    return table;
                case SemanticObjectTypes.Relationship:
                    var relationship = models.GetValueOrDefault(model)?.Relationships
                        .FirstOrDefault(candidate => string.Equals(candidate.Name, objectName, StringComparison.OrdinalIgnoreCase));
                    return relationship is null
                        ? objectName
                        : $"{relationship.FromTable}[{relationship.FromColumn}] → {relationship.ToTable}[{relationship.ToColumn}]" +
                          (relationship.IsActive ? string.Empty : " (inactive)");
                case SemanticObjectTypes.HierarchyLevel when !string.IsNullOrWhiteSpace(hierarchyName):
                    return $"{table}[{hierarchyName} › {objectName}]";
                default:
                    return string.IsNullOrEmpty(table) ? objectName : $"{table}[{objectName}]";
            }
        }

        private static string TableKey(string model, string table) => string.Join('\u001e', model, table);

        private static void Add<T>(Dictionary<string, List<T>> index, string key, T item)
        {
            if (!index.TryGetValue(key, out var items))
            {
                items = [];
                index.Add(key, items);
            }

            items.Add(item);
        }
    }
}

internal enum LineageFocusKind
{
    SemanticObject,
    Function,
    ReportMeasure,
    Visual,
}

/// <summary>One relationship group on a card: the listed items, in order, and how many there were.</summary>
internal sealed record LineageGroup<T>(IReadOnlyList<T> Items, int TotalCount)
{
    public static LineageGroup<T> Empty { get; } = new([], 0);

    public int ShownCount => Items.Count;

    public int HiddenCount => TotalCount - Items.Count;
}

/// <summary>
/// The other end of one or more published edges. State and confidence are that object's own; a node
/// without a usage row (a function, a report measure, a relationship) carries reachability or nothing.
/// </summary>
internal sealed record LineageNeighbour(
    string NodeKey,
    string? CardId,
    string Name,
    string ObjectType,
    IReadOnlyList<string> RelationshipLabels,
    string? UsageState,
    string? ClassificationConfidence,
    bool? ReachableFromReport,
    bool? ReachableFromModelStructure,
    bool IsReasonSource,
    IReadOnlyList<SemanticDependencyEdge> Dependencies)
{
    /// <summary>
    /// For a report measure, how many reports define one of this name for this model. More than one
    /// means the name alone does not say which report's measure this is.
    /// </summary>
    public int ReportCount { get; init; }

    public bool IsSharedReportMeasure => ReportCount > 1;

    /// <summary>For a report measure, the report that owns it; null where none could be identified.</summary>
    public string? Report { get; init; }

    /// <summary>Whether every edge to this neighbour is an ordinary DAX reference, the unlabelled default.</summary>
    public bool HasOnlyDefaultRelationship =>
        RelationshipLabels.Count > 0 &&
        RelationshipLabels.All(label => label == SemanticLineageProjection.DaxLabel);
}

/// <summary>
/// One direct-usage location of an object or report measure, with the evidence the scanner kept for it.
/// <see cref="OwnerReferences"/> is all of the owner's direct-usage evidence, which role labels read.
/// </summary>
internal sealed record LineageReportLocation(
    IReadOnlyList<SemanticUsageEvidence> OwnerReferences,
    SemanticUsageLocation Location,
    ReportInventory? Report,
    PageInventory? Page,
    VisualInventory? Visual,
    string? VisualCardId,
    IReadOnlyList<SemanticUsageEvidence> Evidence);

internal enum LineagePathStatus
{
    /// <summary>The focus has its own direct report evidence.</summary>
    DirectlyUsed,

    /// <summary>A walk through report-reachable predecessors reached an item with direct report evidence.</summary>
    ReachedThroughModel,

    /// <summary>No path to a report location was found in the analysed project.</summary>
    NotFound,
}

/// <summary>
/// One step of a path to report. Every step after the focus uses the step before it, by the
/// relationships named in <see cref="RelationshipLabels"/>.
/// </summary>
internal sealed record LineagePathStep(
    string NodeKey,
    string? CardId,
    string Name,
    string ObjectType,
    string? UsageState,
    bool? ReachableFromReport,
    IReadOnlyList<string> RelationshipLabels,
    int ReportCount)
{
    /// <summary>A report measure whose name other reports bound to this model also define.</summary>
    public bool IsSharedReportMeasure => ReportCount > 1;

    /// <summary>For a report measure, the report it belongs to.</summary>
    public string? Report { get; init; }

    public bool HasOnlyDefaultRelationship =>
        RelationshipLabels.Count > 0 &&
        RelationshipLabels.All(label => label == SemanticLineageProjection.DaxLabel);
}

/// <summary>
/// The path to report shown on a card: one shortest path, never the only one.
/// <see cref="OtherReportReachingConsumers"/> counts the focus's other immediate consumers that are
/// themselves reached from a report — not every route. <see cref="EndpointLocationCount"/> is how many
/// direct report locations the path's last item has; the endpoint is the first of them.
/// </summary>
internal sealed record LineagePath(
    LineagePathStatus Status,
    IReadOnlyList<LineagePathStep> Steps,
    LineageReportLocation? Endpoint,
    int EndpointLocationCount,
    int OtherReportReachingConsumers)
{
    /// <summary>Where an unused branch above the focus starts, when no report path exists.</summary>
    public LineageGroup<LineageNeighbour> OnlyReachedFrom { get; init; } = LineageGroup<LineageNeighbour>.Empty;

    /// <summary>
    /// Whether the focus is required by model structure: its state is Structurally required, or for a
    /// function or report measure, published reachability says the model structure reaches it.
    /// </summary>
    public bool RequiredByModelStructure { get; init; }

    /// <summary>The existing structural usage reason, when the focus is structurally required.</summary>
    public string? StructuralReason { get; init; }

    public LineageGroup<LineageNeighbour> StructuralSources { get; init; } = LineageGroup<LineageNeighbour>.Empty;

    public bool ChecksLimited { get; init; }
}

/// <summary>An object a visual uses, seen from the visual.</summary>
internal sealed record LineageVisualUse(LineageNeighbour Object, LineageReportLocation Location);

/// <summary>A reference from the focus that the scanner could not resolve. Candidates are names only.</summary>
internal sealed record LineageUnresolvedReference(
    UnresolvedSemanticDependency Dependency,
    string RelationshipLabel,
    IReadOnlyList<string> CandidateNames);

/// <summary>An ambiguous reference elsewhere whose candidates include the focus.</summary>
internal sealed record LineagePossibleUse(
    UnresolvedSemanticDependency Dependency,
    LineageNeighbour Source,
    IReadOnlyList<string> OtherCandidateNames);

/// <summary>Power Query evidence for a column: the queries that load its table, and nothing inferred beyond.</summary>
internal sealed record LineagePowerQueryContext(
    IReadOnlyList<SemanticTablePowerQueryContext> TableQueries,
    string? SourceColumn,
    IReadOnlyList<PowerQueryColumnUsage> ColumnEvidence);

internal sealed record LineageCard(
    string Id,
    LineageFocusKind Kind,
    string Title,
    string ObjectType)
{
    /// <summary>The graph node the card focuses on; null for a visual.</summary>
    public string? NodeKey { get; init; }

    public string? SemanticModel { get; init; }

    public string? Table { get; init; }

    public SemanticObjectUsage? Usage { get; init; }

    /// <summary>The report that owns a report measure.</summary>
    public string? ReportName { get; init; }

    public LineagePath? Path { get; init; }

    /// <summary>
    /// For a function or report measure, which have no usage state: whether it is reached from a report,
    /// as the scanner published it for that node. Null where no reachability was published.
    /// </summary>
    public bool? ReachedFromReport { get; init; }

    public SemanticNodeReachability? Reachability { get; init; }

    public string? Reason { get; init; }

    /// <summary>The existing detail view for the focus: its Semantic model row, or its visual card.</summary>
    public string? DetailsAnchor { get; init; }

    public LineagePowerQueryContext? PowerQuery { get; init; }

    public LineageGroup<LineageNeighbour> DependsOn { get; init; } = LineageGroup<LineageNeighbour>.Empty;

    public LineageGroup<LineageUnresolvedReference> NotResolved { get; init; } = LineageGroup<LineageUnresolvedReference>.Empty;

    public LineageGroup<LineageNeighbour> UsedBy { get; init; } = LineageGroup<LineageNeighbour>.Empty;

    public LineageGroup<LineageNeighbour> RequiredByModel { get; init; } = LineageGroup<LineageNeighbour>.Empty;

    public LineageGroup<LineageReportLocation> UsedInReport { get; init; } = LineageGroup<LineageReportLocation>.Empty;

    public LineageGroup<LineagePossibleUse> PossibleUse { get; init; } = LineageGroup<LineagePossibleUse>.Empty;

    public ReportInventory? Report { get; init; }

    public PageInventory? Page { get; init; }

    public VisualInventory? Visual { get; init; }

    public LineageGroup<LineageVisualUse> Uses { get; init; } = LineageGroup<LineageVisualUse>.Empty;

    public LineageGroup<UnresolvedSemanticReference> UnresolvedReportReferences { get; init; } =
        LineageGroup<UnresolvedSemanticReference>.Empty;
}

/// <summary>
/// Element ids for lineage. The readable part helps a person reading a URL; the hash is the identity.
/// <see cref="HtmlReportRenderer.DomToken"/> lower-cases and folds every character outside ASCII
/// letters and digits, so "Sales (EUR)" and "Sales EUR" — or two names in a non-Latin script — share a
/// token. The hash is taken over the complete identity, upper-cased because the scanner compares
/// identities case-insensitively, so names that differ only in case are one object and one id.
/// </summary>
internal static class LineageIds
{
    private const int MaximumTokenLength = 48;

    public static string Create(string prefix, string readable, string identity, int maximumTokenLength = MaximumTokenLength)
    {
        var token = HtmlReportRenderer.DomToken(readable);
        if (token.Length > maximumTokenLength)
        {
            token = token[..maximumTokenLength].TrimEnd('-');
        }

        var hash = Hash(identity.ToUpperInvariant());
        return token.Length == 0 ? $"{prefix}-{hash}" : $"{prefix}-{token}-{hash}";
    }

    /// <summary>
    /// 64-bit FNV-1a over UTF-16 code units, printed as twelve hex digits. Deterministic across runs,
    /// processes and runtimes, unlike <see cref="string.GetHashCode()"/>.
    /// </summary>
    private static string Hash(string value)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offsetBasis;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return (hash & 0xFFFF_FFFF_FFFF).ToString("x12", System.Globalization.CultureInfo.InvariantCulture);
    }
}
