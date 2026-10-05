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
/// Every list preserves the inventory's order, so "the first matching edge" means what it did before.
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

    private SemanticGraphIndex(ProjectInventory inventory)
    {
        foreach (var edge in inventory.SemanticDependencies)
        {
            Add(incomingByObject, ObjectKey(edge.SemanticModel, edge.ToTable, edge.ToObjectName, edge.ToObjectType), edge);
            Add(outgoingByObject, ObjectKey(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType), edge);
            Add(incomingByNode, TargetKey(edge), edge);
            Add(outgoingByNode, SourceKey(edge), edge);
            if (edge.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint)
            {
                Add(relationshipEndpoints, string.Join('\u001e', edge.SemanticModel, edge.FromObjectName), edge);
            }
        }

        foreach (var node in inventory.SemanticNodeReachability)
        {
            reachabilityByObject.TryAdd(ObjectKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType), node);
            reachabilityByNode.TryAdd(
                NodeKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName),
                node);
        }
    }

    public static SemanticGraphIndex For(ProjectInventory inventory) =>
        Cache.GetValue(inventory, static item => new SemanticGraphIndex(item));

    public static string ObjectKey(string model, string table, string objectName, string objectType) =>
        string.Join('\u001e', model, table, objectName, objectType);

    /// <summary>The scanner's node identity: the model, then the field identity it uses for every node.</summary>
    public static string NodeKey(string model, string table, string objectName, string objectType, string? hierarchyName) =>
        string.Join('\u001e', model, FieldIdentity.Create(table, objectName, objectType, hierarchyName));

    public static string SourceKey(SemanticDependencyEdge edge) =>
        NodeKey(edge.SemanticModel, edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName);

    public static string TargetKey(SemanticDependencyEdge edge) =>
        NodeKey(edge.SemanticModel, edge.ToTable, edge.ToObjectName, edge.ToObjectType, edge.ToHierarchyName);

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

    /// <summary>The first published reachability entry for an object, matched without its hierarchy.</summary>
    public SemanticNodeReachability? ReachabilityOfObject(string model, string table, string objectName, string objectType) =>
        reachabilityByObject.GetValueOrDefault(ObjectKey(model, table, objectName, objectType));

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
