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

    private readonly Dictionary<string, LineageCard> cardsByNode;
    private readonly Dictionary<string, LineageCard> cardsByVisual;
    private readonly Dictionary<string, string> objectRowIds;
    private readonly Dictionary<string, SemanticUsageReason?> reasons;
    private readonly Dictionary<string, string?> reportModels;

    private SemanticLineageProjection(
        IReadOnlyList<LineageCard> cards,
        Dictionary<string, LineageCard> cardsByNode,
        Dictionary<string, LineageCard> cardsByVisual,
        Dictionary<string, string> objectRowIds,
        Dictionary<string, SemanticUsageReason?> reasons,
        Dictionary<string, string?> reportModels)
    {
        Cards = cards;
        this.cardsByNode = cardsByNode;
        this.cardsByVisual = cardsByVisual;
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

    /// <summary>The lineage card for a field a report refers to, resolved within the report's own model.</summary>
    public LineageCard? CardForReference(ReportInventory report, VisualFieldReference reference)
    {
        var model = reportModels.GetValueOrDefault(report.Name);
        return model is null
            ? null
            : cardsByNode.GetValueOrDefault(SemanticGraphIndex.NodeKey(
                model, reference.Table, reference.ObjectName, reference.ObjectType, reference.HierarchyName));
    }

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

    private sealed class Builder(ProjectInventory inventory)
    {
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
        private readonly Dictionary<string, List<(SemanticObjectUsage Usage, SemanticUsageLocation Location)>> visualUses = new(StringComparer.OrdinalIgnoreCase);
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

                foreach (var location in usage.DirectReportLocations)
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

                    uses.Add((usage, location));
                }
            }

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

            foreach (var (visualKey, uses) in visualUses)
            {
                var card = VisualCard(visualKey, uses);
                cards.Add(card);
                cardsByVisual.Add(visualKey, card);
            }

            return new SemanticLineageProjection(
                cards, cardsByNode, cardsByVisual, objectRowIds, reasons, reportModels);
        }

        private LineageCard SemanticCard(string key, SemanticObjectUsage usage)
        {
            var reason = reasons[key];
            var (usedBy, requiredByModel) = Incoming(key, reason?.Dependency);
            return new LineageCard(
                nodeCardIds[key],
                LineageFocusKind.SemanticObject,
                DisplayName(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName),
                usage.ObjectType)
            {
                SemanticModel = usage.SemanticModel,
                Table = usage.Table,
                Usage = usage,
                Reason = reason?.Text,
                DetailsAnchor = objectRowIds[key],
                PowerQuery = PowerQueryContext(usage),
                DependsOn = Outgoing(key, reason?.Dependency),
                NotResolved = NotResolved(key, usage.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
                UsedInReport = ReportLocations(usage),
                PossibleUse = PossibleUse(usage),
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
                SemanticModel = function.SemanticModel,
                Reachability = function,
                DependsOn = Outgoing(key, reasonDependency: null),
                NotResolved = NotResolved(key, function.SemanticModel),
                UsedBy = usedBy,
                RequiredByModel = requiredByModel,
            };
        }

        /// <summary>
        /// A visual's objects are exactly the objects whose direct-usage evidence names this visual, so
        /// the visual card and each object's "Used in the report" are two views of one relation. Raw
        /// visual field references are deliberately not used: the scanner's direct-usage policy excludes
        /// some of them, and showing those here would contradict the object's own card.
        /// </summary>
        private LineageCard VisualCard(string visualKey, List<(SemanticObjectUsage Usage, SemanticUsageLocation Location)> uses)
        {
            var (report, page, visual) = visuals[visualKey];
            var items = uses
                .Select(use => new LineageVisualUse(
                    Neighbour(use.Usage.SemanticModel, use.Usage.Table, use.Usage.ObjectName, use.Usage.ObjectType, use.Usage.HierarchyName, [], isReasonSource: false),
                    Location(use.Usage, use.Location)))
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

        private LineageGroup<LineageNeighbour> Outgoing(string key, SemanticDependencyEdge? reasonDependency)
        {
            var neighbours = index.OutgoingFromNode(key)
                .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable)
                .GroupBy(SemanticGraphIndex.TargetKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    return Neighbour(first.SemanticModel, first.ToTable, first.ToObjectName, first.ToObjectType, first.ToHierarchyName,
                        group.ToArray(), group.Contains(reasonDependency));
                })
                .OrderByDescending(neighbour => neighbour.IsReasonSource)
                .ThenBy(neighbour => neighbour.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(neighbour => neighbour.ObjectType, StringComparer.Ordinal)
                .ThenBy(neighbour => neighbour.NodeKey, StringComparer.Ordinal);
            return Cap(neighbours);
        }

        private (LineageGroup<LineageNeighbour> UsedBy, LineageGroup<LineageNeighbour> RequiredByModel) Incoming(
            string key,
            SemanticDependencyEdge? reasonDependency)
        {
            var neighbours = index.IncomingToNode(key)
                .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable)
                .GroupBy(SemanticGraphIndex.SourceKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    return Neighbour(first.SemanticModel, first.FromTable, first.FromObjectName, first.FromObjectType, first.FromHierarchyName,
                        group.ToArray(), group.Contains(reasonDependency));
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
                dependencies);
        }

        private LineageGroup<LineageReportLocation> ReportLocations(SemanticObjectUsage usage) =>
            Cap(usage.DirectReportLocations
                .Select(location => Location(usage, location))
                .OrderBy(location => location.Location.Report, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Page?.Order ?? int.MaxValue)
                .ThenBy(location => location.Page?.DisplayName ?? location.Location.Page ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Visual is null ? 0 : 1)
                .ThenBy(location => location.Visual?.Position.Y ?? double.MaxValue)
                .ThenBy(location => location.Visual?.Position.X ?? double.MaxValue)
                .ThenBy(location => location.Location.Visual ?? location.Location.UsageContext ?? string.Empty, StringComparer.OrdinalIgnoreCase));

        private LineageReportLocation Location(SemanticObjectUsage usage, SemanticUsageLocation location)
        {
            var report = reports.GetValueOrDefault(location.Report);
            var page = location.Page is null
                ? null
                : report?.Pages.FirstOrDefault(candidate => string.Equals(candidate.Name, location.Page, StringComparison.OrdinalIgnoreCase));
            var visualKey = location.Visual is null || location.Page is null
                ? null
                : VisualKey(location.Report, location.Page, location.Visual);
            var visual = visualKey is not null && visuals.TryGetValue(visualKey, out var match) ? match.Visual : null;
            var evidence = usage.DirectReportReferences
                .Where(reference => SemanticUsageLocation.FromEvidence(reference) == location)
                .ToArray();
            return new LineageReportLocation(
                usage,
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
    /// <summary>Whether every edge to this neighbour is an ordinary DAX reference, the unlabelled default.</summary>
    public bool HasOnlyDefaultRelationship =>
        RelationshipLabels.Count > 0 &&
        RelationshipLabels.All(label => label == SemanticLineageProjection.DaxLabel);
}

/// <summary>One direct-usage location of an object, with the evidence the scanner kept for it.</summary>
internal sealed record LineageReportLocation(
    SemanticObjectUsage Usage,
    SemanticUsageLocation Location,
    ReportInventory? Report,
    PageInventory? Page,
    VisualInventory? Visual,
    string? VisualCardId,
    IReadOnlyList<SemanticUsageEvidence> Evidence);

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
    public string? SemanticModel { get; init; }

    public string? Table { get; init; }

    public SemanticObjectUsage? Usage { get; init; }

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
