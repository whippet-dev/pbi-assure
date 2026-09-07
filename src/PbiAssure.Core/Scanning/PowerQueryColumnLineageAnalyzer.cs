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

    /// <summary>
    /// Finds the semantic column a query output column feeds, following the query's own renames until a
    /// column claims that name.
    ///
    /// The name to match on is the column's persisted <c>sourceColumn</c>, not its display name: a
    /// column renamed in the model keeps the query-side name in <c>sourceColumn</c>, so matching by
    /// display name silently lost the evidence for every renamed column.
    /// </summary>
    private static string? ResolveSemanticColumn(
        string referencedColumn,
        SemanticTableInventory table,
        IReadOnlyDictionary<string, string> renames)
    {
        var current = referencedColumn;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(current))
        {
            var matches = table.Columns
                .Where(candidate => string.Equals(
                    QueryColumnName(candidate), current, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 1)
            {
                return matches[0].Name;
            }

            if (matches.Length > 1)
            {
                // More than one column claims this query column. Picking one would state a lineage the
                // metadata does not establish, so nothing is recorded for it.
                return null;
            }

            if (!renames.TryGetValue(current, out current!))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// The name this column has in the query that produces it. TMDL persists that as <c>sourceColumn</c>,
    /// which differs from the column name whenever the column was renamed in the model. Absent for
    /// calculated columns and anything Desktop had no source name for, where the column name is the only
    /// name there is. Never inferred from anything else.
    /// </summary>
    private static string QueryColumnName(SemanticColumnInventory column) =>
        string.IsNullOrWhiteSpace(column.SourceColumn) ? column.Name : column.SourceColumn;

    private static string UsageIdentity(PowerQueryUsage usage) =>
        string.Join('\u001f', usage.QueryName, usage.SourceKind, usage.Partition ?? string.Empty);
}
