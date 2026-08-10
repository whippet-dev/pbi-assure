using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

internal static class SemanticUsagePresentation
{
    public static string? DescribeReason(ProjectInventory inventory, SemanticObjectUsage usage)
    {
        if (usage.UsageState is SemanticUsageStates.DirectlyUsed or SemanticUsageStates.ApparentlyUnused)
        {
            return null;
        }

        var incoming = inventory.SemanticDependencies.Where(dependency =>
            string.Equals(dependency.SemanticModel, usage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dependency.ToTable, usage.Table, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dependency.ToObjectName, usage.ObjectName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dependency.ToObjectType, usage.ObjectType, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var relationship = incoming.FirstOrDefault(dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint);
        if (relationship is not null)
        {
            var otherEndpoint = inventory.SemanticDependencies.FirstOrDefault(dependency =>
                dependency.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint &&
                string.Equals(dependency.SemanticModel, relationship.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dependency.FromObjectName, relationship.FromObjectName, StringComparison.OrdinalIgnoreCase) &&
                (!string.Equals(dependency.ToTable, usage.Table, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(dependency.ToObjectName, usage.ObjectName, StringComparison.OrdinalIgnoreCase)));
            return otherEndpoint is null
                ? "Used as a relationship key"
                : $"Relationship key between {usage.Table}[{usage.ObjectName}] and {otherEndpoint.ToTable}[{otherEndpoint.ToObjectName}]";
        }

        var sortBy = incoming.FirstOrDefault(dependency => dependency.DependencyKind == SemanticDependencyKinds.SortBy);
        if (sortBy is not null)
        {
            return $"Sorts {sortBy.FromTable}[{sortBy.FromObjectName}]";
        }

        var fieldParameter = incoming.FirstOrDefault(dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.FieldParameter);
        if (fieldParameter is not null)
        {
            return $"Available through field parameter {fieldParameter.FromTable}";
        }

        var calculationGroupItem = incoming.FirstOrDefault(dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.CalculationGroupItem);
        if (calculationGroupItem is not null)
        {
            return $"Available through calculation group {calculationGroupItem.FromTable}";
        }

        var dax = incoming.FirstOrDefault(dependency => dependency.DependencyKind is
            SemanticDependencyKinds.Dax or SemanticDependencyKinds.ReportMeasure);
        if (dax is not null)
        {
            var prefix = usage.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch
                ? "Referenced only by unused object"
                : "Referenced by";
            return $"{prefix} {dax.FromTable}[{dax.FromObjectName}]";
        }

        return null;
    }
}
