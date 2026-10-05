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

    private SemanticLineageProjection(
        IReadOnlyList<LineageCard> cards,
        Dictionary<string, LineageCard> cardsByNode,
        Dictionary<string, LineageCard> cardsByVisual,
        Dictionary<string, LineageCard> cardsByReportMeasure,
        Dictionary<string, string> objectRowIds,
        Dictionary<string, SemanticUsageReason?> reasons,
        Dictionary<string, string?> reportModels)
    {
        Cards = cards;
        this.cardsByNode = cardsByNode;
        this.cardsByVisual = cardsByVisual;
        this.cardsByReportMeasure = cardsByReportMeasure;
        this.objectRowIds = objectRowIds;
        this.reasons = reasons;
        this.reportModels = reportModels;
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
            CardForReportMeasure(report.Name, reference.Table, reference.ObjectName) is { } reportMeasure)
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
    public LineageCard? CardForReportMeasure(string report, string entity, string name) =>
        cardsByReportMeasure.GetValueOrDefault(ReportMeasureKey(report, entity, name));

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

    private static string ReportMeasureKey(string report, string entity, string name) =>
        string.Join('\u001f', report, entity, name);

    /// <summary>
    /// A report measure's identity includes its report. The graph node does not, so it cannot be used
    /// to tell two reports' same-named report measures apart.
    /// </summary>
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

    /// <summary>Anything that owns direct report-usage evidence: a model object or one report's report measure.</summary>
    private sealed record UsageOwner(
        string NodeKey,
        string CardId,
        string SemanticModel,
        string Table,
        string ObjectName,
        string ObjectType,
        string? HierarchyName,
        string? Report,
        IReadOnlyList<SemanticUsageEvidence> References);

    /// <summary>
    /// A place in lineage: a graph node, and for a report measure the report that owns it there. Two
    /// reports' same-named report measures share a node but never a position.
    /// </summary>
    private readonly record struct Position(string NodeKey, UsageOwner? Owner)
    {
        public string Id => Owner is null ? NodeKey : string.Join('\u001e', NodeKey, Owner.Report);
    }

    private sealed class Builder(ProjectInventory inventory)
    {
        /// <summary>The most nodes the search for the start of an unused branch visits.</summary>
        private const int BranchSearchLimit = 500;

        private readonly Dictionary<string, LineageGroup<LineageReportLocation>> terminalLocations = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UsageOwner> reportMeasureOwnersByEvidence = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reachedReportMeasures = new(StringComparer.OrdinalIgnoreCase);
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
        private readonly Dictionary<string, List<UsageOwner>> reportMeasureOwnersByNode = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UsageOwner> reportMeasureOwnersByKey = new(StringComparer.OrdinalIgnoreCase);
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
                    usage.ObjectType, usage.HierarchyName, Report: null, usage.DirectReportReferences));
            }

            // Report measures are owned by their report. Each gets its own card even where the graph keys
            // two reports' same-named report measures as one node; the card then says so.
            foreach (var reportMeasure in inventory.ReportMeasureUsages)
            {
                var nodeKey = SemanticGraphIndex.NodeKey(reportMeasure.SemanticModel, reportMeasure.Entity, reportMeasure.Name,
                    SemanticObjectTypes.ReportMeasure, null);
                var owner = new UsageOwner(
                    nodeKey,
                    LineageIds.Create("lin", $"report measure {reportMeasure.Report} {reportMeasure.Entity} {reportMeasure.Name}",
                        ReportMeasureIdentity(reportMeasure.SemanticModel, reportMeasure.Report, reportMeasure.Entity, reportMeasure.Name)),
                    reportMeasure.SemanticModel,
                    reportMeasure.Entity,
                    reportMeasure.Name,
                    SemanticObjectTypes.ReportMeasure,
                    HierarchyName: null,
                    reportMeasure.Report,
                    reportMeasure.DirectReportReferences);
                if (!reportMeasureOwnersByKey.TryAdd(ReportMeasureKey(reportMeasure.Report, reportMeasure.Entity, reportMeasure.Name), owner))
                {
                    continue;
                }

                Add(reportMeasureOwnersByNode, nodeKey, owner);
                AddVisualUses(owner);
            }

            // A neighbour that is a report measure links to its card only when exactly one report defines
            // it; otherwise there is no single object to send the reader to.
            foreach (var (nodeKey, owners) in reportMeasureOwnersByNode)
            {
                if (owners.Count == 1)
                {
                    nodeCardIds.TryAdd(nodeKey, owners[0].CardId);
                }
            }

            AttributeReportMeasureEdges();

            var functionNodes = inventory.SemanticNodeReachability
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
                    unresolved.FromObjectType, unresolved.FromHierarchyName), unresolved);
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

            var cards = new List<LineageCard>();
            var cardsByNode = new Dictionary<string, LineageCard>(StringComparer.OrdinalIgnoreCase);
            var cardsByVisual = new Dictionary<string, LineageCard>(StringComparer.OrdinalIgnoreCase);
            var cardsByReportMeasure = new Dictionary<string, LineageCard>(StringComparer.OrdinalIgnoreCase);
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

            foreach (var (reportMeasureKey, owner) in reportMeasureOwnersByKey)
            {
                var card = ReportMeasureCard(owner);
                cards.Add(card);
                cardsByReportMeasure.Add(reportMeasureKey, card);
                if (reportMeasureOwnersByNode[owner.NodeKey].Count == 1)
                {
                    cardsByNode.TryAdd(owner.NodeKey, card);
                }
            }

            foreach (var (visualKey, uses) in visualUses)
            {
                var card = VisualCard(visualKey, uses);
                cards.Add(card);
                cardsByVisual.Add(visualKey, card);
            }

            return new SemanticLineageProjection(
                cards, cardsByNode, cardsByVisual, cardsByReportMeasure, objectRowIds, reasons, reportModels);
        }

        /// <summary>
        /// The graph keys a report measure by model, entity and name, so two reports' same-named report
        /// measures share one node. Every edge a report measure contributes carries its own report's
        /// extension file as evidence, which identifies the report that owns the edge. Lineage reads
        /// report-measure relationships through that ownership so nothing crosses a report boundary.
        /// An edge whose evidence matches no report's measure is left unattributed and is never followed.
        ///
        /// A report measure is reached from a report when its own report places it, or when another
        /// report measure of the same report that is reached uses it.
        /// </summary>
        private void AttributeReportMeasureEdges()
        {
            foreach (var owner in reportMeasureOwnersByKey.Values)
            {
                var definition = reports.GetValueOrDefault(owner.Report!)?.ReportMeasures.FirstOrDefault(measure =>
                    string.Equals(measure.Entity, owner.Table, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(measure.Name, owner.ObjectName, StringComparison.OrdinalIgnoreCase));
                if (definition is not null)
                {
                    reportMeasureOwnersByEvidence.TryAdd(string.Join('\u001e', owner.NodeKey, definition.RelativePath), owner);
                }
            }

            var queue = new Queue<UsageOwner>(reportMeasureOwnersByKey.Values.Where(owner => owner.References.Count > 0));
            foreach (var owner in queue)
            {
                reachedReportMeasures.Add(owner.CardId);
            }

            while (queue.TryDequeue(out var owner))
            {
                foreach (var edge in index.OutgoingFromNode(owner.NodeKey))
                {
                    if (edge.ToObjectType != SemanticObjectTypes.ReportMeasure ||
                        !ReferenceEquals(EdgeOwner(edge), owner) ||
                        !reportMeasureOwnersByKey.TryGetValue(ReportMeasureKey(owner.Report!, edge.ToTable, edge.ToObjectName), out var used) ||
                        !reachedReportMeasures.Add(used.CardId))
                    {
                        continue;
                    }

                    queue.Enqueue(used);
                }
            }
        }

        /// <summary>The report measure, in its own report, that contributed this edge; null for other sources.</summary>
        private UsageOwner? EdgeOwner(SemanticDependencyEdge edge) =>
            edge.FromObjectType == SemanticObjectTypes.ReportMeasure
                ? reportMeasureOwnersByEvidence.GetValueOrDefault(string.Join('\u001e', SemanticGraphIndex.SourceKey(edge), edge.EvidencePath))
                : null;

        private bool IsReached(UsageOwner owner) => reachedReportMeasures.Contains(owner.CardId);

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
            var (usedBy, requiredByModel) = Incoming(new Position(key, null), reason?.Dependency);
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
                DependsOn = Outgoing(new Position(key, null), reason?.Dependency),
                NotResolved = NotResolved(key, usage.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                UsedInReport = usedInReport,
                PossibleUse = PossibleUse(usage),
                Path = PathFor(new Position(key, null), usage.IsDirectlyReferencedByReport ? usedInReport : null, usage, reason?.Text, requiredByModel),
            };
        }

        private LineageCard FunctionCard(string key, SemanticNodeReachability function)
        {
            var (usedBy, requiredByModel) = Incoming(new Position(key, null), reasonDependency: null);
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
                DependsOn = Outgoing(new Position(key, null), reasonDependency: null),
                NotResolved = NotResolved(key, function.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                Path = PathFor(new Position(key, null), directLocations: null, usage: null, reason: null, requiredByModel),
            };
        }

        /// <summary>
        /// One report's report measure: its own relationships (the edges its report contributed), its own
        /// report locations and its own reachability. Other reports' same-named report measures share the
        /// graph node but none of what is shown here.
        /// </summary>
        private LineageCard ReportMeasureCard(UsageOwner owner)
        {
            var position = new Position(owner.NodeKey, owner);
            var (usedBy, requiredByModel) = Incoming(position, reasonDependency: null);
            var usedInReport = ReportLocations(owner.References);
            return new LineageCard(
                owner.CardId,
                LineageFocusKind.ReportMeasure,
                DisplayName(owner.SemanticModel, owner.Table, owner.ObjectName, owner.ObjectType, null),
                SemanticObjectTypes.ReportMeasure)
            {
                NodeKey = owner.NodeKey,
                SemanticModel = owner.SemanticModel,
                Table = owner.Table,
                Report = reports.GetValueOrDefault(owner.Report!),
                ReportName = owner.Report,
                ReachedFromReport = IsReached(owner),
                SharedWithReports = reportMeasureOwnersByNode[owner.NodeKey]
                    .Where(other => !ReferenceEquals(other, owner))
                    .Select(other => other.Report!)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                DetailsAnchor = ReportMeasureRowId(owner.SemanticModel, owner.Report!, owner.Table, owner.ObjectName),
                DependsOn = Outgoing(position, reasonDependency: null),
                NotResolved = NotResolved(owner.NodeKey, owner.SemanticModel, OwnEvidencePath(owner)),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                UsedInReport = usedInReport,
                Path = PathFor(position, owner.References.Count > 0 ? usedInReport : null, usage: null, reason: null, requiredByModel),
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
                    use.Owner.Report is null
                        ? Neighbour(use.Owner.SemanticModel, use.Owner.Table, use.Owner.ObjectName, use.Owner.ObjectType, use.Owner.HierarchyName, [], isReasonSource: false)
                        : OwnerNeighbour(use.Owner, [], isReasonSource: false),
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
        /// the stable qualified order used elsewhere.
        ///
        /// A report measure is walked as a position in its own report: entered only through an edge its
        /// report contributed, left only through its report's edges, and ending only at its report's own
        /// locations. Where the classifier's merged report-measure node is the only thing that makes the
        /// focus reached, no report path exists, and the path says that the result is shared.
        /// </summary>
        private LineagePath PathFor(
            Position focusPosition,
            LineageGroup<LineageReportLocation>? directLocations,
            SemanticObjectUsage? usage,
            string? reason,
            LineageGroup<LineageNeighbour> requiredByModel)
        {
            var focus = FocusStep(focusPosition);
            if (directLocations is { TotalCount: > 0 })
            {
                return new LineagePath(LineagePathStatus.DirectlyUsed, [focus], directLocations.Items[0], directLocations.TotalCount, 0);
            }

            var reachability = index.ReachabilityOfNode(focusPosition.NodeKey);
            var shared = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var reached = focusPosition.Owner is { } focusOwner ? IsReached(focusOwner) : reachability?.ReachableFromReport == true;
            if (reached && SearchTowardReport(focusPosition, focus, shared) is { } found)
            {
                return found;
            }

            var path = new LineagePath(LineagePathStatus.NotFound, [focus], null, 0, 0)
            {
                ChecksLimited = usage?.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation,
            };
            if (reached)
            {
                // Reached only by way of a report-measure node that several reports share.
                return path with { SharedReportMeasures = shared.ToArray() };
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

            return Predecessors(focusPosition, shared: null).Length == 0
                ? path
                : path with { OnlyReachedFrom = BranchHeads(focusPosition) };
        }

        private LineagePath? SearchTowardReport(Position focusPosition, LineagePathStep focus, ISet<string> shared)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { focusPosition.Id };
            var next = new Dictionary<string, (Position Toward, IReadOnlyList<SemanticDependencyEdge> Edges)>(StringComparer.OrdinalIgnoreCase);
            var positions = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase) { [focusPosition.Id] = focusPosition };
            var queue = new Queue<Position>();
            queue.Enqueue(focusPosition);
            var otherFirstHops = 0;
            while (queue.TryDequeue(out var current))
            {
                var predecessors = Predecessors(current, shared)
                    .Where(item => item.Source.Owner is { } owner
                        ? IsReached(owner)
                        : index.ReachabilityOfNode(item.Source.NodeKey)?.ReachableFromReport == true)
                    .ToArray();
                if (current.Id == focusPosition.Id)
                {
                    otherFirstHops = Math.Max(0, predecessors.Length - 1);
                }

                foreach (var (source, edges) in predecessors)
                {
                    if (!visited.Add(source.Id))
                    {
                        continue;
                    }

                    next[source.Id] = (current, edges);
                    positions[source.Id] = source;
                    var endpoint = TerminalLocations(source);
                    if (endpoint.TotalCount > 0)
                    {
                        var chain = new List<LineagePathStep>();
                        for (var position = source; position.Id != focusPosition.Id; position = next[position.Id].Toward)
                        {
                            chain.Add(Step(position, next[position.Id].Edges));
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
        /// The positions whose edges the classifier traverses into this one, grouped per position, in the
        /// stable qualified order. Structural sources (relationships, roles, perspectives, refresh policies)
        /// are never traversed by the classifier, so they are never path steps. A report measure is a
        /// position in the report that contributed the edge; into a report measure, only that report's
        /// own report measures lead. An edge no report can be identified for is not followed. Shared
        /// report-measure names met on the way are recorded for the explanation.
        /// </summary>
        private (Position Source, IReadOnlyList<SemanticDependencyEdge> Edges)[] Predecessors(Position position, ISet<string>? shared) =>
            index.IncomingToNode(position.NodeKey)
                .Where(IsTraversedSource)
                .Select(edge =>
                {
                    var owner = EdgeOwner(edge);
                    if (edge.FromObjectType == SemanticObjectTypes.ReportMeasure &&
                        reportMeasureOwnersByNode.TryGetValue(SemanticGraphIndex.SourceKey(edge), out var owners) && owners.Count > 1)
                    {
                        shared?.Add($"{edge.FromTable}[{edge.FromObjectName}] in {JoinReports(owners)}");
                    }

                    return (Edge: edge, Owner: owner);
                })
                .Where(item => item.Edge.FromObjectType != SemanticObjectTypes.ReportMeasure
                    ? position.Owner is null
                    : item.Owner is not null && (position.Owner is null ||
                        string.Equals(item.Owner.Report, position.Owner.Report, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(item => new Position(SemanticGraphIndex.SourceKey(item.Edge), item.Owner).Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => (
                    Source: new Position(SemanticGraphIndex.SourceKey(group.First().Edge), group.First().Owner),
                    Edges: (IReadOnlyList<SemanticDependencyEdge>)group.Select(item => item.Edge).ToArray()))
                .OrderBy(item => item.Edges[0].FromTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Edges[0].FromObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Edges[0].FromObjectType, StringComparer.Ordinal)
                .ThenBy(item => item.Edges[0].FromHierarchyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Source.Id, StringComparer.Ordinal)
                .ToArray();

        private static string JoinReports(IEnumerable<UsageOwner> owners)
        {
            var names = owners.Select(owner => owner.Report!).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            return names.Length switch
            {
                1 => $"report {names[0]}",
                _ => $"reports {string.Join(", ", names.Take(names.Length - 1))} and {names[^1]}",
            };
        }

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
        /// A position's own direct report locations in the cards' order: a model object's, or one report
        /// measure's in its own report. Never another report's.
        /// </summary>
        private LineageGroup<LineageReportLocation> TerminalLocations(Position position)
        {
            if (terminalLocations.TryGetValue(position.Id, out var cached))
            {
                return cached;
            }

            IEnumerable<LineageReportLocation> locations = [];
            if (position.Owner is { } owner)
            {
                locations = SemanticUsageLocation.Distinct(owner.References)
                    .Select(location => Location(owner.References, location));
            }
            else if (usagesByNode.TryGetValue(position.NodeKey, out var usage))
            {
                locations = SemanticUsageLocation.Distinct(usage.DirectReportReferences)
                    .Select(location => Location(usage.DirectReportReferences, location));
            }

            var result = Cap(OrderLocations(locations));
            terminalLocations.Add(position.Id, result);
            return result;
        }

        private LineagePathStep FocusStep(Position position)
        {
            var key = position.NodeKey;
            var neighbour = position.Owner is { } owner
                ? OwnerNeighbour(owner, [], false)
                : usagesByNode.TryGetValue(key, out var usage)
                    ? Neighbour(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName, [], false)
                    : index.ReachabilityOfNode(key) is { } node
                        ? Neighbour(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName, [], false)
                        : throw new InvalidOperationException($"No lineage node for '{key}'.");
            return PathStep(neighbour with { CardId = null });
        }

        /// <summary>One hop toward the report: the item, and how it uses the previous one.</summary>
        private LineagePathStep Step(Position position, IReadOnlyList<SemanticDependencyEdge> edges)
        {
            var edge = edges[0];
            return PathStep(position.Owner is { } owner
                ? OwnerNeighbour(owner, edges, false)
                : Neighbour(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName, edges, false));
        }

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
        private LineageGroup<LineageNeighbour> BranchHeads(Position focusPosition)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { focusPosition.Id };
            var edgesInto = new Dictionary<string, (Position Source, IReadOnlyList<SemanticDependencyEdge> Edges)>(StringComparer.OrdinalIgnoreCase);
            var heads = new List<string>();
            var queue = new Queue<Position>();
            queue.Enqueue(focusPosition);
            while (queue.TryDequeue(out var current) && visited.Count <= BranchSearchLimit)
            {
                var predecessors = Predecessors(current, shared: null);
                if (current.Id != focusPosition.Id && predecessors.Length == 0)
                {
                    heads.Add(current.Id);
                }

                foreach (var (source, edges) in predecessors)
                {
                    if (visited.Add(source.Id))
                    {
                        edgesInto[source.Id] = (source, edges);
                        queue.Enqueue(source);
                    }
                }
            }

            var ids = heads.Count > 0 ? heads : Predecessors(focusPosition, shared: null).Select(item => item.Source.Id).ToList();
            return Cap(ids
                .Select(id =>
                {
                    var (source, edges) = edgesInto[id];
                    var edge = edges[0];
                    return source.Owner is { } owner
                        ? OwnerNeighbour(owner, [], false)
                        : Neighbour(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName, [], false);
                })
                .OrderBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.Report ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal));
        }

        /// <summary>
        /// What the focus depends on. For a report measure, only the edges its own report contributed;
        /// a report measure it uses is that same report's.
        /// </summary>
        private LineageGroup<LineageNeighbour> Outgoing(Position position, SemanticDependencyEdge? reasonDependency)
        {
            var neighbours = index.OutgoingFromNode(position.NodeKey)
                .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable)
                .Where(edge => position.Owner is null || ReferenceEquals(EdgeOwner(edge), position.Owner))
                .GroupBy(SemanticGraphIndex.TargetKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    return position.Owner is { } owner &&
                           first.ToObjectType == SemanticObjectTypes.ReportMeasure &&
                           reportMeasureOwnersByKey.TryGetValue(ReportMeasureKey(owner.Report!, first.ToTable, first.ToObjectName), out var used)
                        ? OwnerNeighbour(used, group.ToArray(), group.Contains(reasonDependency))
                        : Neighbour(first.SemanticModel, first.ToTable, first.ToObjectName, first.ToObjectType, first.ToHierarchyName,
                            group.ToArray(), group.Contains(reasonDependency));
                })
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.ObjectType, StringComparer.Ordinal)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            return Cap(neighbours);
        }

        /// <summary>
        /// What uses the focus. A report measure that uses it is shown as that report's own report
        /// measure; one no report can be identified for is shown without a link or a reachability claim.
        /// For a report-measure focus, only its own report's report measures are its consumers.
        /// </summary>
        private (LineageGroup<LineageNeighbour> UsedBy, LineageGroup<LineageNeighbour> RequiredByModel) Incoming(
            Position position,
            SemanticDependencyEdge? reasonDependency)
        {
            var neighbours = index.IncomingToNode(position.NodeKey)
                .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable)
                .Select(edge => (Edge: edge, Owner: EdgeOwner(edge)))
                .Where(item => position.Owner is null ||
                    (item.Owner is not null && string.Equals(item.Owner.Report, position.Owner.Report, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(item => new Position(SemanticGraphIndex.SourceKey(item.Edge), item.Owner).Id, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var (first, owner) = group.First();
                    var edges = group.Select(item => item.Edge).ToArray();
                    if (owner is not null)
                    {
                        return OwnerNeighbour(owner, edges, edges.Contains(reasonDependency));
                    }

                    var neighbour = Neighbour(first.SemanticModel, first.FromTable, first.FromObjectName, first.FromObjectType, first.FromHierarchyName,
                        edges, edges.Contains(reasonDependency));
                    return first.FromObjectType == SemanticObjectTypes.ReportMeasure
                        ? neighbour with { CardId = null, ReachableFromReport = null, ReachableFromModelStructure = null }
                        : neighbour;
                })
                .ToArray();

            var usedBy = neighbours
                .Where(neighbour => !StructuralSourceTypes.Contains(neighbour.ObjectType))
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenByDescending(neighbour => neighbour.ReachableFromReport == true)
                .ThenByDescending(neighbour => neighbour.ReachableFromModelStructure == true)
                .ThenBy(neighbour => UsageOrder(neighbour.UsageState))
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            var requiredByModel = neighbours
                .Where(neighbour => StructuralSourceTypes.Contains(neighbour.ObjectType))
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenBy(neighbour => ObjectTypeLabel(neighbour.ObjectType), StringComparer.Ordinal)
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            return (Cap(usedBy), Cap(requiredByModel));
        }

        /// <summary>One report's report measure as a neighbour: its own card, report and reachability.</summary>
        private LineageNeighbour OwnerNeighbour(UsageOwner owner, IReadOnlyList<SemanticDependencyEdge> dependencies, bool isReasonSource) =>
            Neighbour(owner.SemanticModel, owner.Table, owner.ObjectName, owner.ObjectType, owner.HierarchyName, dependencies, isReasonSource)
                with
                {
                    CardId = owner.CardId,
                    Report = owner.Report,
                    ReachableFromReport = IsReached(owner),
                    ReachableFromModelStructure = null,
                };

        private string? OwnEvidencePath(UsageOwner owner) =>
            reports.GetValueOrDefault(owner.Report!)?.ReportMeasures.FirstOrDefault(measure =>
                string.Equals(measure.Entity, owner.Table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(measure.Name, owner.ObjectName, StringComparison.OrdinalIgnoreCase))?.RelativePath;

        private LineageNeighbour Neighbour(
            string model,
            string table,
            string objectName,
            string objectType,
            string? hierarchyName,
            IReadOnlyList<SemanticDependencyEdge> dependencies,
            bool isReasonSource)
        {
            var key = SemanticGraphIndex.NodeKey(model, table, objectName, objectType, hierarchyName);
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
            return new LineageNeighbour(
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
                    ? reportMeasureOwnersByNode.GetValueOrDefault(key)?.Count ?? 0
                    : 0,
            };
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

        private LineageGroup<LineageUnresolvedReference> NotResolved(string key, string model, string? evidencePath = null) =>
            Cap((unresolvedBySource.GetValueOrDefault(key) ?? [])
                .Where(unresolved => evidencePath is null ||
                    string.Equals(unresolved.EvidencePath, evidencePath, StringComparison.OrdinalIgnoreCase))
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
                        unresolved.FromObjectType, unresolved.FromHierarchyName, [], isReasonSource: false),
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
    /// means the graph analyses them as one node.
    /// </summary>
    public int ReportCount { get; init; }

    public bool IsSharedReportMeasure => ReportCount > 1;

    /// <summary>For a report measure attributed to its report, that report.</summary>
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
    /// <summary>A report measure node that several reports' same-named report measures share.</summary>
    public bool IsSharedReportMeasure => ReportCount > 1;

    /// <summary>For a report measure, the report it belongs to on this path.</summary>
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

    /// <summary>
    /// Set when the focus's usage result counts it as reached only because the classifier analyses
    /// same-named report measures in several reports as one item: each such report measure, with its
    /// reports. No report path exists within any one report.
    /// </summary>
    public IReadOnlyList<string> SharedReportMeasures { get; init; } = [];
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

    /// <summary>
    /// Other reports bound to the same model that define a report measure of the same name. The
    /// dependency graph analyses all of them as one node.
    /// </summary>
    public IReadOnlyList<string> SharedWithReports { get; init; } = [];

    public LineagePath? Path { get; init; }

    /// <summary>
    /// For a function or report measure, which have no usage state: whether it is reached from a report.
    /// A report measure's is its own report's, never a same-named report measure's elsewhere.
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
