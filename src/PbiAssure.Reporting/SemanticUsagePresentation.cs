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
        // A relationship endpoint is the one reason kind whose edge *creates* the requirement rather
        // than carrying reachability from a predecessor: the column is seeded as a model-structure root,
        // and the edge's source is a relationship, not a model object with a reachability of its own. It
        // therefore explains StructurallyRequired and nothing else. Where a report also reaches the
        // column the card says "Indirectly used", and the relationship fact — true as it is — does not
        // explain that. The edge stays in the graph and explains the object again whenever its state is
        // StructurallyRequired.
        if (usage.UsageState == SemanticUsageStates.StructurallyRequired)
        {
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

            var objectLevelPermission = incoming.FirstOrDefault(dependency =>
                dependency.DependencyKind == SemanticDependencyKinds.ObjectLevelPermission);
            if (objectLevelPermission is not null)
            {
                return $"Needed by the {objectLevelPermission.FromObjectName} object-level security permission";
            }
        }

        // Every remaining kind carries reachability from a real predecessor, so each is eligible only
        // when that predecessor's own reachability matches the state being explained. The wording and
        // the order the kinds are tried in are unchanged.
        var sortBy = FirstSupporting(inventory, usage, incoming, SemanticDependencyKinds.SortBy);
        if (sortBy is not null)
        {
            return $"Sorts {sortBy.FromTable}[{sortBy.FromObjectName}]";
        }

        var fieldParameter = FirstSupporting(inventory, usage, incoming, SemanticDependencyKinds.FieldParameter);
        if (fieldParameter is not null)
        {
            return $"Available through field parameter {fieldParameter.FromTable}";
        }

        var calculationGroupItem = FirstSupporting(
            inventory, usage, incoming, SemanticDependencyKinds.CalculationGroupItem);
        if (calculationGroupItem is not null)
        {
            return $"Available through calculation group {calculationGroupItem.FromTable}";
        }

        // An incoming reference and the evidence for a classification are different things. An uncalled
        // function genuinely references a column without being why that column is indirectly used, so
        // only predecessors whose own reachability matches the state are eligible to explain it.
        //
        // Where several are eligible they are all truthful; one is shown, chosen by qualified name so
        // the explanation never depends on the order dependencies were parsed in.
        var dax = FirstSupporting(
            inventory, usage, incoming,
            SemanticDependencyKinds.Dax, SemanticDependencyKinds.ReportMeasure);
        if (dax is not null)
        {
            var prefix = usage.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch
                ? "Referenced only by unused object"
                : "Referenced by";
            return $"{prefix} {dax.FromTable}[{dax.FromObjectName}]";
        }

        return null;
    }

    /// <summary>
    /// The eligible incoming dependency of the given kinds, chosen by qualified name so the explanation
    /// never depends on the order dependencies were parsed in.
    /// </summary>
    private static SemanticDependencyEdge? FirstSupporting(
        ProjectInventory inventory,
        SemanticObjectUsage usage,
        IReadOnlyList<SemanticDependencyEdge> incoming,
        params string[] kinds) =>
        incoming
            .Where(dependency => kinds.Contains(dependency.DependencyKind, StringComparer.Ordinal))
            .Where(dependency => SupportsClassification(inventory, usage, dependency))
            .OrderBy(dependency => dependency.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.FromObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.FromObjectType, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Whether this dependency's source can explain the object's current usage state.
    ///
    /// The reachability comes from the scanner, which computed it while assigning the state; nothing is
    /// traversed or re-derived here. That matters for a path running through a node with no usage row of
    /// its own — a report measure or a DAX user-defined function — which a rule based on the states of
    /// public objects could not follow.
    /// </summary>
    private static bool SupportsClassification(
        ProjectInventory inventory,
        SemanticObjectUsage usage,
        SemanticDependencyEdge dependency)
    {
        var source = inventory.SemanticNodeReachability.FirstOrDefault(node =>
            string.Equals(node.SemanticModel, dependency.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Table, dependency.FromTable, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.ObjectName, dependency.FromObjectName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.ObjectType, dependency.FromObjectType, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            return false;
        }

        return usage.UsageState switch
        {
            // Reached from a report, so the predecessor must be reached from one too.
            SemanticUsageStates.IndirectlyUsed => source.ReachableFromReport,
            // Required by the model rather than by a report.
            SemanticUsageStates.StructurallyRequired => source.ReachableFromModelStructure,
            // The state exists precisely because nothing live reaches the object, so a live predecessor
            // would contradict it.
            SemanticUsageStates.UsedOnlyByUnusedBranch =>
                !source.ReachableFromReport && !source.ReachableFromModelStructure,
            _ => false,
        };
    }
}
