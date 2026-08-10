using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class SemanticTablePowerQueryEnricher
{
    public static SemanticTablePowerQueryContext[] Build(IReadOnlyList<PowerQueryUsage> usages)
    {
        return usages
            .Where(usage =>
                usage.SourceKind == PowerQuerySourceKinds.TablePartition &&
                usage.Table is not null &&
                usage.QueryRole is not null)
            .Select(usage => new SemanticTablePowerQueryContext(
                usage.SemanticModel,
                usage.Table!,
                usage.QueryName,
                usage.Partition,
                usage.QueryRole!,
                usage.HasDynamicReferences,
                usage.ReferencedBy
                    .Select(reference => reference.FromQueryName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(context => context.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(context => context.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(context => context.Partition, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
