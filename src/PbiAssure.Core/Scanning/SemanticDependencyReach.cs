using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Transitive reach over the semantic dependency graph, for bounding what a limitation can bear on.
/// The edges are the ones the classifier itself traverses, so "reachable from here" means the same
/// thing to a limitation's reach as it does to a usage state.
/// </summary>
internal static class SemanticDependencyReach
{
    /// <summary>
    /// Every object reachable from <paramref name="seeds"/> over the model's edges, as
    /// <see cref="FieldIdentity"/> keys. A seed is included only if some path leads back to it.
    /// </summary>
    public static HashSet<string> Closure(
        IReadOnlyList<SemanticDependencyEdge> dependencies,
        string modelName,
        IEnumerable<string> seeds)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in dependencies.Where(edge =>
                     string.Equals(edge.SemanticModel, modelName, StringComparison.OrdinalIgnoreCase)))
        {
            var source = FieldIdentity.Create(edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName);
            if (!adjacency.TryGetValue(source, out var targets))
            {
                adjacency[source] = targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            targets.Add(FieldIdentity.Create(edge.ToTable, edge.ToObjectName, edge.ToObjectType, edge.ToHierarchyName));
        }

        var reach = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(seeds);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current) || !adjacency.TryGetValue(current, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                reach.Add(target);
                queue.Enqueue(target);
            }
        }

        return reach;
    }
}
