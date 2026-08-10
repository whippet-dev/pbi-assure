using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PowerQueryColumnLineageAnalyzer
{
    public static PowerQueryColumnUsage[] Analyze(
        IReadOnlyList<SemanticModelInventory> semanticModels,
        IReadOnlyList<PowerQueryUsage> queryUsages)
    {
        var results = new List<PowerQueryColumnUsage>();
        foreach (var model in semanticModels)
        {
            var modelUsages = queryUsages.Where(usage =>
                string.Equals(usage.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var knownNames = modelUsages.Select(usage => usage.QueryName)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var loadedSources = modelUsages.Where(usage =>
                    usage.SourceKind == PowerQuerySourceKinds.TablePartition && usage.Table is not null)
                .GroupBy(usage => usage.QueryName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            var tableLookup = model.Tables.ToDictionary(table => table.Name, StringComparer.OrdinalIgnoreCase);
            var renames = modelUsages.ToDictionary(
                usage => UsageIdentity(usage),
                usage => MColumnLineageExtractor.ReadOutputRenames(usage.Expression),
                StringComparer.OrdinalIgnoreCase);

            foreach (var consumer in modelUsages)
            {
                foreach (var reference in MColumnLineageExtractor.Extract(
                             consumer.Expression, consumer.QueryName, knownNames))
                {
                    if (!loadedSources.TryGetValue(reference.SourceQuery, out var sourceUsages))
                    {
                        continue;
                    }

                    foreach (var source in sourceUsages)
                    {
                        if (source.Table is null || !tableLookup.TryGetValue(source.Table, out var sourceTable))
                        {
                            continue;
                        }

                        var semanticColumn = ResolveSemanticColumn(
                            reference.SourceColumn,
                            sourceTable,
                            renames[UsageIdentity(source)]);
                        if (semanticColumn is null)
                        {
                            continue;
                        }

                        results.Add(new PowerQueryColumnUsage(
                            model.Name,
                            source.QueryName,
                            source.Table,
                            source.Partition,
                            semanticColumn,
                            string.Equals(semanticColumn, reference.SourceColumn, StringComparison.OrdinalIgnoreCase)
                                ? null
                                : reference.SourceColumn,
                            consumer.QueryName,
                            consumer.Table,
                            consumer.Partition,
                            reference.UsageKind,
                            reference.MFunction,
                            reference.StepName,
                            consumer.ArtifactPath));
                    }
                }
            }
        }

        return results
            .GroupBy(usage => string.Join(
                '\u001f',
                usage.SemanticModel,
                usage.SourceQuery,
                usage.SourceTable,
                usage.SourceColumn,
                usage.ConsumerQuery,
                usage.UsageKind), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.SourceTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.SourceColumn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ConsumerQuery, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.UsageKind, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ResolveSemanticColumn(
        string referencedColumn,
        SemanticTableInventory table,
        IReadOnlyDictionary<string, string> renames)
    {
        var current = referencedColumn;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(current))
        {
            var column = table.Columns.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, current, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
            {
                return column.Name;
            }

            if (!renames.TryGetValue(current, out current!))
            {
                return null;
            }
        }

        return null;
    }

    private static string UsageIdentity(PowerQueryUsage usage) =>
        string.Join('\u001f', usage.QueryName, usage.SourceKind, usage.Partition ?? string.Empty);
}
