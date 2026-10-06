using System.Runtime.CompilerServices;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

/// <summary>
/// The scan's published dependency graph, indexed once per inventory so that presentation code can ask
/// "what points at this object?" without scanning every edge for every object it renders.
///
/// Two keyings are kept on purpose. <see cref="ObjectKey"/> matches the comparisons the usage-reason
/// wording has always made — model, table, name and type, case-insensitively, without the hierarchy —
/// so that wording is unchanged by being indexed. <see cref="NodeKey"/> is the scanner's own node
/// identity, hierarchy included, which lineage needs because two hierarchies may share a level name.
/// Both keyings include a report measure's owning report, as the scanner's identity does, so two
/// reports' same-named report measures are never one entry. Every list preserves the inventory's order,
/// so "the first matching edge" means what it did before.
///
/// Reachability is read from <see cref="ProjectInventory.ReportScopedNodeReachability"/>, the scanner's
/// one row per node. The published <see cref="ProjectInventory.SemanticNodeReachability"/> is its lossy
/// schema-0.26 projection: for an ordinary node its row is the same, so it only fills in where an
/// inventory was not produced by a scan; a published report-measure row merges every report's measure
/// of that name and is never used.
/// </summary>
internal sealed class SemanticGraphIndex
{
    private static readonly ConditionalWeakTable<ProjectInventory, SemanticGraphIndex> Cache = new();

    private readonly Dictionary<string, List<SemanticDependencyEdge>> incomingByObject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SemanticDependencyEdge>> outgoingByObject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SemanticDependencyEdge>> incomingByNode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SemanticDependencyEdge>> outgoingByNode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SemanticDependencyEdge>> relationshipEndpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SemanticNodeReachability> reachabilityByObject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SemanticNodeReachability> reachabilityByNode = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SemanticNodeReachability> nodes = [];

    private SemanticGraphIndex(ProjectInventory inventory)
    {
        foreach (var edge in inventory.SemanticDependencies)
        {
            Add(incomingByObject, ObjectKey(edge.SemanticModel, edge.ToTable, edge.ToObjectName, edge.ToObjectType, edge.ToReport), edge);
            Add(outgoingByObject, ObjectKey(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromReport), edge);
            Add(incomingByNode, TargetKey(edge), edge);
            Add(outgoingByNode, SourceKey(edge), edge);
            if (edge.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint)
            {
                Add(relationshipEndpoints, string.Join('\u001e', edge.SemanticModel, edge.FromObjectName), edge);
            }
        }

        var published = inventory.SemanticNodeReachability
            .Where(node => node.ObjectType != SemanticObjectTypes.ReportMeasure);
        foreach (var node in inventory.ReportScopedNodeReachability.Concat(published))
        {
            reachabilityByObject.TryAdd(ObjectKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.Report), node);
            var nodeKey = NodeKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName, node.Report);
            if (reachabilityByNode.TryAdd(nodeKey, node))
            {
                nodes.Add(node);
            }
        }
    }

    /// <summary>Every node with reachability, one per node identity, report-scoped rows first.</summary>
    public IReadOnlyList<SemanticNodeReachability> Nodes => nodes;

    public static SemanticGraphIndex For(ProjectInventory inventory) =>
        Cache.GetValue(inventory, static item => new SemanticGraphIndex(item));

    /// <summary>
    /// Model, table, name and type, without the hierarchy; for a report measure, also its owning report.
    /// </summary>
    public static string ObjectKey(string model, string table, string objectName, string objectType, string? report = null) =>
        report is null
            ? string.Join('\u001e', model, table, objectName, objectType)
            : string.Join('\u001e', model, table, objectName, objectType, report);

    /// <summary>
    /// The scanner's node identity: the model, then the field identity it uses for every node, and for a
    /// report measure the report that owns it.
    /// </summary>
    public static string NodeKey(string model, string table, string objectName, string objectType, string? hierarchyName, string? report = null) =>
        SemanticNodeIdentity.Create(model, table, objectName, objectType, hierarchyName, report);

    public static string SourceKey(SemanticDependencyEdge edge) =>
        NodeKey(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName, edge.FromReport);

    public static string TargetKey(SemanticDependencyEdge edge) =>
        NodeKey(edge.SemanticModel, edge.ToTable, edge.ToObjectName, edge.ToObjectType, edge.ToHierarchyName, edge.ToReport);

    /// <summary>
    /// Whether the edge's report-measure ends name an owning report, and the edge's own evidence — the
    /// owning report's extension file — lies in that report. A report-measure edge between two report
    /// measures stays within one report. The scanner always satisfies this; an edge that does not is
    /// credited to no report's measure.
    /// </summary>
    public static bool HasConsistentOwnership(SemanticDependencyEdge edge)
    {
        var fromReportMeasure = edge.FromObjectType == SemanticObjectTypes.ReportMeasure;
        var toReportMeasure = edge.ToObjectType == SemanticObjectTypes.ReportMeasure;
        return (!fromReportMeasure || IsEvidenceOf(edge, edge.FromReport)) &&
               (!toReportMeasure || IsEvidenceOf(edge, edge.ToReport)) &&
               (!fromReportMeasure || !toReportMeasure ||
                string.Equals(edge.FromReport, edge.ToReport, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEvidenceOf(SemanticDependencyEdge edge, string? report) =>
        report is not null &&
        edge.EvidencePath.Replace('\\', '/').StartsWith(report + "/", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<SemanticDependencyEdge> IncomingToObject(string model, string table, string objectName, string objectType) =>
        incomingByObject.GetValueOrDefault(ObjectKey(model, table, objectName, objectType)) ?? (IReadOnlyList<SemanticDependencyEdge>)[];

    public IReadOnlyList<SemanticDependencyEdge> OutgoingFromObject(string model, string table, string objectName, string objectType) =>
        outgoingByObject.GetValueOrDefault(ObjectKey(model, table, objectName, objectType)) ?? (IReadOnlyList<SemanticDependencyEdge>)[];

    public IReadOnlyList<SemanticDependencyEdge> IncomingToNode(string nodeKey) =>
        incomingByNode.GetValueOrDefault(nodeKey) ?? (IReadOnlyList<SemanticDependencyEdge>)[];

    public IReadOnlyList<SemanticDependencyEdge> OutgoingFromNode(string nodeKey) =>
        outgoingByNode.GetValueOrDefault(nodeKey) ?? (IReadOnlyList<SemanticDependencyEdge>)[];

    /// <summary>Every endpoint edge a relationship has, in inventory order.</summary>
    public IReadOnlyList<SemanticDependencyEdge> RelationshipEndpoints(string model, string relationshipName) =>
        relationshipEndpoints.GetValueOrDefault(string.Join('\u001e', model, relationshipName)) ?? (IReadOnlyList<SemanticDependencyEdge>)[];

    /// <summary>
    /// The first reachability entry for an object, matched without its hierarchy. A report measure is
    /// found only with its owning report.
    /// </summary>
    public SemanticNodeReachability? ReachabilityOfObject(string model, string table, string objectName, string objectType, string? report = null) =>
        reachabilityByObject.GetValueOrDefault(ObjectKey(model, table, objectName, objectType, report));

    public SemanticNodeReachability? ReachabilityOfNode(string nodeKey) =>
        reachabilityByNode.GetValueOrDefault(nodeKey);

    private static void Add(Dictionary<string, List<SemanticDependencyEdge>> index, string key, SemanticDependencyEdge edge)
    {
        if (!index.TryGetValue(key, out var edges))
        {
            edges = [];
            index.Add(key, edges);
        }

        edges.Add(edge);
    }
}
