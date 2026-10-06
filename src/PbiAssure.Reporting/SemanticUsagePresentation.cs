using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

/// <summary>The wording of a usage reason, with the dependency it was drawn from where there is one.</summary>
internal sealed record SemanticUsageReason(string Text, SemanticDependencyEdge? Dependency);

internal static class SemanticUsagePresentation
{
    public static string? DescribeReason(ProjectInventory inventory, SemanticObjectUsage usage) =>
        ExplainReason(inventory, usage)?.Text;

    /// <summary>
    /// The reason shown for a usage state and the edge it names. The edge lets lineage list the object
    /// the reason names first, without choosing it a second time by different rules.
    /// </summary>
    public static SemanticUsageReason? ExplainReason(ProjectInventory inventory, SemanticObjectUsage usage)
    {
        if (usage.UsageState is SemanticUsageStates.DirectlyUsed or SemanticUsageStates.ApparentlyUnused)
        {
            return null;
        }

        var index = SemanticGraphIndex.For(inventory);
        var incoming = index.IncomingToObject(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType);
        // A relationship endpoint is the one reason kind whose edge *creates* the requirement rather
        // than carrying reachability from a predecessor: the column is seeded as a model-structure root,
        // and the edge's source is a relationship, not a model object with a reachability of its own. It
        // therefore explains StructurallyRequired and nothing else. Where a report also reaches the
        // column the card says "Indirectly used", and the relationship fact — true as it is — does not
        // explain that. The edge stays in the graph and explains the object again whenever its state is
        // StructurallyRequired.
        if (usage.UsageState == SemanticUsageStates.StructurallyRequired)
        {
            if (usage.StructuralRequirementProvenance ==
                StructuralRequirementProvenances.SystemGeneratedAutoDateTime)
            {
                return new("Required only by Power BI-generated Auto Date/Time structure", null);
            }

            var refreshPolicy = incoming.FirstOrDefault(dependency =>
                dependency.DependencyKind == SemanticDependencyKinds.IncrementalRefreshPolicy);
            if (refreshPolicy is not null)
            {
                return new($"Needed by the {refreshPolicy.FromTable} incremental refresh change-detection setting", refreshPolicy);
            }

            var relationship = incoming
                .Where(dependency => dependency.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint)
                .OrderBy(dependency => dependency.StructuralProvenance ==
                                      StructuralRequirementProvenances.SystemGeneratedAutoDateTime)
                .ThenBy(dependency => dependency.FromObjectName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (relationship is not null)
            {
                var otherEndpoint = index.RelationshipEndpoints(relationship.SemanticModel, relationship.FromObjectName)
                    .FirstOrDefault(dependency =>
                        !string.Equals(dependency.ToTable, usage.Table, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(dependency.ToObjectName, usage.ObjectName, StringComparison.OrdinalIgnoreCase));
                return new(
                    otherEndpoint is null
                        ? "Used as a relationship key"
                        : $"Relationship key between {usage.Table}[{usage.ObjectName}] and {otherEndpoint.ToTable}[{otherEndpoint.ToObjectName}]",
                    relationship);
            }

            var objectLevelPermission = incoming.FirstOrDefault(dependency =>
                dependency.DependencyKind == SemanticDependencyKinds.ObjectLevelPermission);
            if (objectLevelPermission is not null)
            {
                return new($"Needed by the {objectLevelPermission.FromObjectName} object-level security permission", objectLevelPermission);
            }
        }

        // Every remaining kind carries reachability from a real predecessor, so each is eligible only
        // when that predecessor's own reachability matches the state being explained. The wording and
        // the order the kinds are tried in are unchanged.
        var sortBy = FirstSupporting(index, usage, incoming, SemanticDependencyKinds.SortBy);
        if (sortBy is not null)
        {
            return new($"Sorts {sortBy.FromTable}[{sortBy.FromObjectName}]", sortBy);
        }

        var fieldParameter = FirstSupporting(index, usage, incoming, SemanticDependencyKinds.FieldParameter);
        if (fieldParameter is not null)
        {
            return new($"Available through field parameter {fieldParameter.FromTable}", fieldParameter);
        }

        var calculationGroupItem = FirstSupporting(
            index, usage, incoming, SemanticDependencyKinds.CalculationGroupItem);
        if (calculationGroupItem is not null)
        {
            return new($"Available through calculation group {calculationGroupItem.FromTable}", calculationGroupItem);
        }

        // An incoming reference and the evidence for a classification are different things. An uncalled
        // function genuinely references a column without being why that column is indirectly used, so
        // only predecessors whose own reachability matches the state are eligible to explain it.
        //
        // Where several are eligible they are all truthful; one is shown, chosen by qualified name so
        // the explanation never depends on the order dependencies were parsed in.
        var dax = FirstSupporting(
            index, usage, incoming,
            SemanticDependencyKinds.Dax, SemanticDependencyKinds.ReportMeasure);
        if (dax is not null)
        {
            var prefix = usage.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch
                ? "Referenced only by unused object"
                : "Referenced by";
            return new($"{prefix} {dax.FromTable}[{dax.FromObjectName}]", dax);
        }

        // Role filters and perspective membership make their targets model-structure roots directly.
        // Unlike ordinary dependencies, their source nodes are not predecessors that traversal reaches;
        // the retained edge and the target's published structural reachability are the evidence for this
        // state. They are considered only after the established relationship, OLS and predecessor-based
        // explanations above, so they do not displace those existing reasons.
        if (usage.UsageState == SemanticUsageStates.StructurallyRequired)
        {
            var roleFilter = FirstDirectStructuralRoot(
                index, usage, incoming,
                SemanticDependencyKinds.TablePermission, SemanticObjectTypes.Role);
            if (roleFilter is not null)
            {
                return new($"Needed by the {roleFilter.FromObjectName} security filter", roleFilter);
            }

            var perspective = FirstDirectStructuralRoot(
                index, usage, incoming,
                SemanticDependencyKinds.PerspectiveMember, SemanticObjectTypes.Perspective);
            if (perspective is not null)
            {
                return new($"Included in the {perspective.FromObjectName} perspective", perspective);
            }

            var aggregationTarget = FirstSupporting(
                index, usage, incoming, SemanticDependencyKinds.AggregationMapping);
            if (aggregationTarget is not null)
            {
                return new(
                    $"Used as the detail column in {DescribeAggregationMapping(inventory, aggregationTarget)} from {aggregationTarget.FromTable}[{aggregationTarget.FromObjectName}]",
                    aggregationTarget);
            }

            var aggregationSource = FirstAggregationMappingOwnedBy(index, usage);
            if (aggregationSource is not null)
            {
                return new(
                    $"Needed by {DescribeAggregationMapping(inventory, aggregationSource)} to {aggregationSource.ToTable}[{aggregationSource.ToObjectName}]",
                    aggregationSource);
            }
        }

        return null;
    }

    private static SemanticDependencyEdge? FirstAggregationMappingOwnedBy(
        SemanticGraphIndex index,
        SemanticObjectUsage usage) =>
        index.OutgoingFromObject(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType)
            .Where(dependency => dependency.DependencyKind == SemanticDependencyKinds.AggregationMapping)
            .Where(dependency => SupportsClassification(index, usage, dependency))
            .OrderBy(dependency => dependency.ToTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.ToObjectName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static string DescribeAggregationMapping(
        ProjectInventory inventory,
        SemanticDependencyEdge dependency)
    {
        var summarization = inventory.SemanticModels
            .Where(model => string.Equals(model.Name, dependency.SemanticModel, StringComparison.OrdinalIgnoreCase))
            .SelectMany(model => model.Tables)
            .Where(table => string.Equals(table.Name, dependency.FromTable, StringComparison.OrdinalIgnoreCase))
            .SelectMany(table => table.Columns)
            .Where(column => string.Equals(column.Name, dependency.FromObjectName, StringComparison.OrdinalIgnoreCase))
            .Select(column => column.AlternateOf?.Summarization)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(summarization)
            ? "an aggregation mapping"
            : $"a {char.ToUpperInvariant(summarization[0])}{summarization[1..]} aggregation mapping";
    }

    /// <summary>
    /// Finds a direct model-structure root reason. Role-filter and perspective edges are intentionally
    /// different from normal incoming dependencies: their target, rather than their source, is seeded
    /// as a structural root by the scanner. The published target reachability confirms that this edge is
    /// explaining the displayed structural state without Reporting recreating graph traversal.
    /// </summary>
    private static SemanticDependencyEdge? FirstDirectStructuralRoot(
        SemanticGraphIndex index,
        SemanticObjectUsage usage,
        IReadOnlyList<SemanticDependencyEdge> incoming,
        string dependencyKind,
        string sourceObjectType)
    {
        var target = index.ReachabilityOfObject(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType);
        if (target is null || !target.ReachableFromModelStructure)
        {
            return null;
        }

        return incoming
            .Where(dependency => dependency.DependencyKind == dependencyKind &&
                                 dependency.FromObjectType == sourceObjectType)
            .OrderBy(dependency => dependency.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.FromObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.FromObjectType, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// The eligible incoming dependency of the given kinds, chosen by qualified name so the explanation
    /// never depends on the order dependencies were parsed in. Same-named report measures in different
    /// reports are told apart by their owning report.
    /// </summary>
    private static SemanticDependencyEdge? FirstSupporting(
        SemanticGraphIndex index,
        SemanticObjectUsage usage,
        IReadOnlyList<SemanticDependencyEdge> incoming,
        params string[] kinds) =>
        incoming
            .Where(dependency => kinds.Contains(dependency.DependencyKind, StringComparer.Ordinal))
            .Where(dependency => SupportsClassification(index, usage, dependency))
            .OrderBy(dependency => dependency.FromTable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.FromObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dependency => dependency.FromObjectType, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.FromReport ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    /// <summary>
    /// Whether this dependency's source can explain the object's current usage state.
    ///
    /// The reachability comes from the scanner, which computed it while assigning the state; nothing is
    /// traversed or re-derived here. That matters for a path running through a node with no usage row of
    /// its own — a report measure or a DAX user-defined function — which a rule based on the states of
    /// public objects could not follow. A report measure's reachability is its own report's: a same-named
    /// report measure in another report never supports the state, and nor does an edge whose owning
    /// report is missing or contradicts its own evidence.
    /// </summary>
    private static bool SupportsClassification(
        SemanticGraphIndex index,
        SemanticObjectUsage usage,
        SemanticDependencyEdge dependency)
    {
        var source = SemanticGraphIndex.HasConsistentOwnership(dependency)
            ? index.ReachabilityOfObject(
                dependency.SemanticModel, dependency.FromTable, dependency.FromObjectName, dependency.FromObjectType,
                dependency.FromReport)
            : null;
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
