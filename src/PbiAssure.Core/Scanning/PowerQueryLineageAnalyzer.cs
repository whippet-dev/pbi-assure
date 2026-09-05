using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PowerQueryLineageAnalyzer
{
    public static (PowerQueryUsage[] Usages, PowerQueryDependencyEdge[] Dependencies, DataSourceInventory[] DataSources) Analyze(
        IReadOnlyList<SemanticModelInventory> semanticModels)
    {
        var usages = new List<PowerQueryUsage>();
        var dependencies = new List<PowerQueryDependencyEdge>();
        var dataSources = new List<DataSourceInventory>();
        foreach (var model in semanticModels)
        {
            AnalyzeModel(model, usages, dependencies, dataSources);
        }

        return (
            usages.OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.QueryName, StringComparer.OrdinalIgnoreCase).ToArray(),
            dependencies.Distinct().OrderBy(edge => edge.SemanticModel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.FromQueryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToQueryName, StringComparer.OrdinalIgnoreCase).ToArray(),
            dataSources.Distinct().OrderBy(source => source.SemanticModel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.QueryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.ConnectorFamily, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void AnalyzeModel(
        SemanticModelInventory model,
        List<PowerQueryUsage> allUsages,
        List<PowerQueryDependencyEdge> allDependencies,
        List<DataSourceInventory> allDataSources)
    {
        var sources = model.Tables.SelectMany(table => table.Partitions
                .Where(partition => string.Equals(partition.SourceType, "m", StringComparison.OrdinalIgnoreCase) &&
                                    !string.IsNullOrWhiteSpace(partition.Expression))
                .Select(partition => new QuerySource(
                    table.Name, PowerQuerySourceKinds.TablePartition, table.Name, partition.Name,
                    partition.Expression!, table.RelativePath, IsLoaded: true,
                    IsParameter: false, ParameterType: null, IsParameterRequired: null)))
            .Concat(model.NamedExpressions.Select(expression => new QuerySource(
                expression.Name, PowerQuerySourceKinds.NamedExpression, null, null,
                expression.Expression, expression.RelativePath, IsLoaded: false,
                expression.IsParameter, expression.ParameterType, expression.IsParameterRequired)))
            .ToArray();
        var knownNames = sources.Select(source => source.QueryName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var sourcesByName = sources.GroupBy(source => source.QueryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            foreach (var connector in MConnectorExtractor.Extract(source.Expression))
            {
                allDataSources.Add(new DataSourceInventory(
                    model.Name, source.QueryName, source.SourceKind, source.Table, source.Partition,
                    connector.Family, connector.Function, connector.LocationKind, source.ArtifactPath));
            }

            foreach (var targetName in MReferenceExtractor.Extract(source.Expression, knownNames)
                         .Where(name => !string.Equals(name, source.QueryName, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var target in sourcesByName[targetName])
                {
                    allDependencies.Add(new PowerQueryDependencyEdge(
                        model.Name, source.QueryName, source.SourceKind, source.Table, source.Partition,
                        target.QueryName, target.SourceKind, source.ArtifactPath));
                }
            }
        }

        var modelDependencies = allDependencies.Where(edge => edge.SemanticModel == model.Name).ToArray();
        var reachable = Traverse(
            sources.Where(source => source.IsLoaded).Select(source => source.QueryName), modelDependencies);
        var modelHasUnresolvableScope = sources.Any(source =>
            MReferenceExtractor.HasUnresolvableBindingScope(source.Expression));

        foreach (var source in sources)
        {
            var referencedBy = modelDependencies.Where(edge =>
                    string.Equals(edge.ToQueryName, source.QueryName, StringComparison.OrdinalIgnoreCase))
                .Select(edge => new PowerQueryReferenceEvidence(
                    edge.FromQueryName, edge.FromSourceKind, edge.FromTable, edge.FromPartition, edge.ArtifactPath))
                .Distinct().ToArray();
            var hasDynamicReferences = MReferenceExtractor.HasDynamicReferences(source.Expression);
            // Scope doubt lives in the expression that does the referencing, not in the query being
            // judged: a binding the flat model mis-scopes suppresses a reference to some *other* query,
            // which is what would make that query look orphaned. So an unresolvable let anywhere in the
            // model withholds the confident orphan conclusion, exactly as a dynamic reference does.
            // Usage states are untouched; only the role, and with it PBI-QUERY-002, is withheld.
            allUsages.Add(new PowerQueryUsage(
                model.Name, source.QueryName, source.SourceKind, source.Table, source.Partition,
                source.Expression, source.ArtifactPath,
                source.IsLoaded ? PowerQueryUsageStates.LoadedToModel
                    : reachable.Contains(source.QueryName) ? PowerQueryUsageStates.SupportingQuery
                    : PowerQueryUsageStates.ApparentlyUnused,
                QueryRole(source, referencedBy, hasDynamicReferences || modelHasUnresolvableScope),
                hasDynamicReferences, referencedBy)
            {
                IsParameter = source.IsParameter,
                ParameterType = source.ParameterType,
                IsParameterRequired = source.IsParameterRequired,
                RefreshPolicyTables = RefreshPolicyTables(model, source, knownNames),
            });
        }
    }

    private static string? QueryRole(
        QuerySource source,
        PowerQueryReferenceEvidence[] referencedBy,
        bool hasDynamicReferences)
    {
        if (source.IsParameter)
        {
            return null;
        }

        if (source.IsLoaded)
        {
            return referencedBy.Length > 0
                ? PowerQueryRoles.LoadedAndSupporting
                : PowerQueryRoles.LoadedOnly;
        }

        if (referencedBy.Length > 0)
        {
            return PowerQueryRoles.HelperOrStaging;
        }

        return hasDynamicReferences
            ? null
            : PowerQueryRoles.ApparentlyOrphaned;
    }

    private static string[] RefreshPolicyTables(
        SemanticModelInventory model,
        QuerySource source,
        IReadOnlyCollection<string> knownNames)
    {
        if (!source.IsParameter)
        {
            return [];
        }

        return model.Tables
            .Where(table => !string.IsNullOrWhiteSpace(table.RefreshPolicy?.SourceExpression))
            .Where(table => MReferenceExtractor.Extract(table.RefreshPolicy!.SourceExpression!, knownNames)
                .Contains(source.QueryName, StringComparer.OrdinalIgnoreCase))
            .Select(table => table.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> Traverse(
        IEnumerable<string> roots,
        IReadOnlyList<PowerQueryDependencyEdge> dependencies)
    {
        var adjacency = dependencies.GroupBy(edge => edge.FromQueryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToQueryName).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(roots);
        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current) || !adjacency.TryGetValue(current, out var targets))
            {
                continue;
            }
            foreach (var target in targets)
            {
                queue.Enqueue(target);
            }
        }
        return visited;
    }

    private sealed record QuerySource(
        string QueryName,
        string SourceKind,
        string? Table,
        string? Partition,
        string Expression,
        string ArtifactPath,
        bool IsLoaded,
        bool IsParameter,
        string? ParameterType,
        bool? IsParameterRequired);
}
