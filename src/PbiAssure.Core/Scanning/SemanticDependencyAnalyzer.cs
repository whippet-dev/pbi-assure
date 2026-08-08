using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class SemanticDependencyAnalyzer
{
    public static SemanticDependencyAnalysis Analyze(
        IReadOnlyList<SemanticModelInventory> semanticModels,
        IReadOnlyList<SemanticObjectUsage> initialUsages,
        IReadOnlyList<ReportInventory> reports)
    {
        var dependencies = new List<SemanticDependencyEdge>();
        var unresolved = new List<UnresolvedSemanticDependency>();
        var structuralRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reportMeasureNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reportMeasureRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in semanticModels)
        {
            AnalyzeModel(model, initialUsages, dependencies, unresolved, structuralRoots);
        }

        AnalyzeReportMeasures(
            semanticModels, initialUsages, reports, dependencies, unresolved,
            reportMeasureNodes, reportMeasureRoots);

        var distinctDependencies = dependencies.Distinct().ToArray();
        var classifiedUsages = ClassifyObjects(
            initialUsages, distinctDependencies, structuralRoots, reportMeasureNodes, reportMeasureRoots);
        var tableUsages = ClassifyTables(
            semanticModels, classifiedUsages, distinctDependencies, structuralRoots,
            reportMeasureNodes, reportMeasureRoots);

        return new SemanticDependencyAnalysis(
            ObjectUsages: classifiedUsages,
            TableUsages: tableUsages,
            Dependencies: distinctDependencies
                .OrderBy(edge => edge.SemanticModel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.FromTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.FromObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.DependencyKind, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToObjectName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            UnresolvedDependencies: unresolved.Distinct().ToArray());
    }

    private static void AnalyzeReportMeasures(
        IReadOnlyList<SemanticModelInventory> semanticModels,
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<ReportInventory> reports,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        HashSet<string> reportMeasureNodes,
        HashSet<string> reportMeasureRoots)
    {
        foreach (var report in reports)
        {
            var model = semanticModels.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, report.Name, StringComparison.OrdinalIgnoreCase));
            if (model is null || report.ReportMeasures.Count == 0)
            {
                continue;
            }

            var modelUsages = usages.Where(usage =>
                string.Equals(usage.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var modelMeasures = modelUsages
                .Where(usage => usage.ObjectType == SemanticObjectTypes.Measure)
                .ToDictionary(usage => QualifiedKey(usage.Table, usage.ObjectName), Source,
                    StringComparer.OrdinalIgnoreCase);
            var reportMeasures = report.ReportMeasures.ToDictionary(
                measure => QualifiedKey(measure.Entity, measure.Name),
                measure => Target(measure.Entity, measure.Name, SemanticObjectTypes.ReportMeasure),
                StringComparer.OrdinalIgnoreCase);

            foreach (var source in reportMeasures.Values)
            {
                reportMeasureNodes.Add(NodeKey(model.Name, source));
            }

            foreach (var reference in EnumerateReportFieldReferences(report))
            {
                if (reference.ObjectType == SemanticObjectTypes.Measure &&
                    reportMeasures.TryGetValue(QualifiedKey(reference.Table, reference.ObjectName), out var root))
                {
                    reportMeasureRoots.Add(NodeKey(model.Name, root));
                }
            }

            foreach (var measure in report.ReportMeasures)
            {
                var source = reportMeasures[QualifiedKey(measure.Entity, measure.Name)];
                foreach (var reference in measure.References)
                {
                    var lookup = reference.IsReportMeasureReference ? reportMeasures : modelMeasures;
                    if (lookup.TryGetValue(QualifiedKey(reference.Entity, reference.Name), out var target))
                    {
                        dependencies.Add(CreateEdge(
                            model.Name, source, target, SemanticDependencyKinds.ReportMeasure,
                            measure.RelativePath, $"{reference.Entity}[{reference.Name}]"));
                    }
                    else
                    {
                        unresolved.Add(CreateUnresolved(
                            model.Name, source, SemanticDependencyKinds.ReportMeasure,
                            $"{reference.Entity}[{reference.Name}]",
                            reference.IsReportMeasureReference
                                ? $"Report measure '{reference.Entity}[{reference.Name}]' was not found in extension '{reference.Schema}'."
                                : $"Model measure '{reference.Entity}[{reference.Name}]' was not found.",
                            measure.RelativePath));
                    }
                }
            }
        }
    }

    private static IEnumerable<VisualFieldReference> EnumerateReportFieldReferences(ReportInventory report) =>
        report.FieldReferences
            .Concat(report.Pages.SelectMany(page => page.FieldReferences))
            .Concat(report.Pages.SelectMany(page => page.Visuals.SelectMany(visual => visual.FieldReferences)));

    private static void AnalyzeModel(
        SemanticModelInventory model,
        IReadOnlyList<SemanticObjectUsage> allUsages,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        ISet<string> structuralRoots)
    {
        var usages = allUsages
            .Where(usage => string.Equals(usage.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var lookup = new ModelLookup(model, usages);

        foreach (var usage in usages)
        {
            dependencies.Add(CreateEdge(
                model.Name,
                Source(usage),
                Target(usage.Table, usage.Table, SemanticObjectTypes.Table),
                SemanticDependencyKinds.ContainingTable,
                lookup.TablePaths[usage.Table],
                usage.Table));
        }

        foreach (var table in model.Tables)
        {
            AnalyzeTable(model, table, lookup, dependencies, unresolved);
        }

        var relationshipsPath = Path.Combine(model.RelativePath, "definition", "relationships.tmdl");
        foreach (var relationship in model.Relationships)
        {
            var source = Target(string.Empty, relationship.Name, SemanticObjectTypes.Relationship);
            AddStructuralEndpoint(
                model,
                relationship,
                relationship.FromTable,
                relationship.FromColumn,
                source,
                relationshipsPath,
                lookup,
                dependencies,
                unresolved,
                structuralRoots);
            AddStructuralEndpoint(
                model,
                relationship,
                relationship.ToTable,
                relationship.ToColumn,
                source,
                relationshipsPath,
                lookup,
                dependencies,
                unresolved,
                structuralRoots);
        }
    }

    private static void AnalyzeTable(
        SemanticModelInventory model,
        SemanticTableInventory table,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved)
    {
        foreach (var column in table.Columns)
        {
            var source = Target(table.Name, column.Name, SemanticObjectTypes.Column);
            if (column.Expression is not null)
            {
                AddDaxDependencies(model, table, source, column.Expression, lookup, dependencies, unresolved);
            }

            if (column.SortByColumn is null)
            {
                continue;
            }

            if (lookup.TryResolveQualified(table.Name, column.SortByColumn, out var sortTarget, out var reason))
            {
                dependencies.Add(CreateEdge(
                    model.Name,
                    source,
                    sortTarget,
                    SemanticDependencyKinds.SortBy,
                    table.RelativePath,
                    column.SortByColumn));
            }
            else
            {
                unresolved.Add(CreateUnresolved(
                    model.Name,
                    source,
                    SemanticDependencyKinds.SortBy,
                    column.SortByColumn,
                    reason,
                    table.RelativePath));
            }
        }

        foreach (var measure in table.Measures)
        {
            AddDaxDependencies(
                model,
                table,
                Target(table.Name, measure.Name, SemanticObjectTypes.Measure),
                measure.Expression,
                lookup,
                dependencies,
                unresolved);
        }

        foreach (var hierarchy in table.Hierarchies)
        {
            foreach (var level in hierarchy.Levels)
            {
                if (level.Column is null)
                {
                    continue;
                }

                var source = Target(
                    table.Name,
                    level.Name,
                    SemanticObjectTypes.HierarchyLevel,
                    hierarchy.Name);
                if (lookup.TryResolveQualified(table.Name, level.Column, out var levelTarget, out var reason))
                {
                    dependencies.Add(CreateEdge(
                        model.Name,
                        source,
                        levelTarget,
                        SemanticDependencyKinds.HierarchyLevel,
                        table.RelativePath,
                        level.Column));
                }
                else
                {
                    unresolved.Add(CreateUnresolved(
                        model.Name,
                        source,
                        SemanticDependencyKinds.HierarchyLevel,
                        level.Column,
                        reason,
                        table.RelativePath));
                }
            }
        }

        if (table.FieldParameter is not null)
        {
            AnalyzeFieldParameter(model, table, table.FieldParameter, lookup, dependencies, unresolved);
        }

        if (table.CalculationGroup is not null)
        {
            AnalyzeCalculationGroup(model, table, table.CalculationGroup, lookup, dependencies, unresolved);
        }

        foreach (var partition in table.Partitions.Where(partition => partition.Expression is not null))
        {
            if (string.Equals(
                    partition.Expression,
                    table.FieldParameter?.Expression,
                    StringComparison.Ordinal))
            {
                continue;
            }

            AddDaxDependencies(
                model,
                table,
                Target(table.Name, table.Name, SemanticObjectTypes.Table),
                partition.Expression!,
                lookup,
                dependencies,
                unresolved);
        }
    }

    private static void AnalyzeFieldParameter(
        SemanticModelInventory model,
        SemanticTableInventory table,
        SemanticFieldParameterInventory parameter,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved)
    {
        var source = Target(table.Name, table.Name, SemanticObjectTypes.Table);
        foreach (var entry in parameter.Entries)
        {
            if (lookup.TryResolveQualified(entry.Table, entry.ObjectName, out var target, out var reason))
            {
                dependencies.Add(CreateEdge(
                    model.Name,
                    source,
                    target,
                    SemanticDependencyKinds.FieldParameter,
                    table.RelativePath,
                    entry.ReferenceText));
            }
            else
            {
                unresolved.Add(CreateUnresolved(
                    model.Name,
                    source,
                    SemanticDependencyKinds.FieldParameter,
                    entry.ReferenceText,
                    $"Field parameter '{parameter.Name}': {reason}",
                    table.RelativePath));
            }
        }
    }

    private static void AnalyzeCalculationGroup(
        SemanticModelInventory model,
        SemanticTableInventory table,
        SemanticCalculationGroupInventory calculationGroup,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved)
    {
        var tableNode = Target(table.Name, table.Name, SemanticObjectTypes.Table);
        foreach (var item in calculationGroup.Items)
        {
            var itemNode = Target(table.Name, item.Name, SemanticObjectTypes.CalculationItem);
            dependencies.Add(CreateEdge(
                model.Name,
                tableNode,
                itemNode,
                SemanticDependencyKinds.CalculationGroupItem,
                table.RelativePath,
                item.Name));
            AddDaxDependencies(model, table, itemNode, item.Expression, lookup, dependencies, unresolved);
            if (item.FormatStringExpression is not null)
            {
                AddDaxDependencies(
                    model,
                    table,
                    itemNode,
                    item.FormatStringExpression,
                    lookup,
                    dependencies,
                    unresolved);
            }
        }

        if (calculationGroup.SelectionExpression is not null)
        {
            AddDaxDependencies(
                model,
                table,
                tableNode,
                calculationGroup.SelectionExpression,
                lookup,
                dependencies,
                unresolved);
        }

        if (calculationGroup.MultipleOrEmptySelectionExpression is not null)
        {
            AddDaxDependencies(
                model,
                table,
                tableNode,
                calculationGroup.MultipleOrEmptySelectionExpression,
                lookup,
                dependencies,
                unresolved);
        }
    }

    private static void AddDaxDependencies(
        SemanticModelInventory model,
        SemanticTableInventory currentTable,
        SemanticNode source,
        string expression,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved)
    {
        foreach (var reference in DaxReferenceExtractor.Extract(expression, lookup.TableNames))
        {
            if (lookup.TryResolveDax(reference, currentTable.Name, out var target, out var reason))
            {
                dependencies.Add(CreateEdge(
                    model.Name,
                    source,
                    target,
                    SemanticDependencyKinds.Dax,
                    currentTable.RelativePath,
                    reference.Text));
            }
            else
            {
                unresolved.Add(CreateUnresolved(
                    model.Name,
                    source,
                    SemanticDependencyKinds.Dax,
                    reference.Text,
                    reason,
                    currentTable.RelativePath));
            }
        }
    }

    private static void AddStructuralEndpoint(
        SemanticModelInventory model,
        SemanticRelationshipInventory relationship,
        string table,
        string column,
        SemanticNode source,
        string evidencePath,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        ISet<string> structuralRoots)
    {
        if (lookup.TryResolveQualified(table, column, out var target, out var reason))
        {
            dependencies.Add(CreateEdge(
                model.Name,
                source,
                target,
                SemanticDependencyKinds.RelationshipEndpoint,
                evidencePath,
                $"{table}[{column}]"));
            structuralRoots.Add(NodeKey(model.Name, target));
        }
        else
        {
            unresolved.Add(CreateUnresolved(
                model.Name,
                source,
                SemanticDependencyKinds.RelationshipEndpoint,
                $"{table}[{column}]",
                $"Relationship '{relationship.Name}': {reason}",
                evidencePath));
        }
    }

    private static SemanticObjectUsage[] ClassifyObjects(
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<SemanticDependencyEdge> dependencies,
        IReadOnlySet<string> structuralRoots,
        IReadOnlySet<string> reportMeasureNodes,
        IReadOnlySet<string> reportMeasureRoots)
    {
        var knownNodes = usages
            .Select(usage => NodeKey(usage.SemanticModel, Source(usage)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var usage in usages)
        {
            knownNodes.Add(NodeKey(
                usage.SemanticModel,
                Target(usage.Table, usage.Table, SemanticObjectTypes.Table)));
        }
        knownNodes.UnionWith(reportMeasureNodes);

        var adjacency = BuildAdjacency(dependencies, knownNodes);
        var directRoots = usages
            .Where(usage => usage.IsDirectlyReferencedByReport)
            .Select(usage => NodeKey(usage.SemanticModel, Source(usage)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        directRoots.UnionWith(reportMeasureRoots);
        var directlyReachable = Traverse(directRoots, adjacency);
        var structurallyReachable = Traverse(structuralRoots, adjacency);
        var incomingTargets = dependencies
            .Where(edge => knownNodes.Contains(NodeKey(edge.SemanticModel, Source(edge))))
            .Select(edge => NodeKey(edge.SemanticModel, Target(edge)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return usages
            .Select(usage =>
            {
                var key = NodeKey(usage.SemanticModel, Source(usage));
                var state = usage.IsDirectlyReferencedByReport
                    ? SemanticUsageStates.DirectlyUsed
                    : directlyReachable.Contains(key)
                        ? SemanticUsageStates.IndirectlyUsed
                        : structurallyReachable.Contains(key)
                            ? SemanticUsageStates.StructurallyRequired
                            : incomingTargets.Contains(key)
                                ? SemanticUsageStates.UsedOnlyByUnusedBranch
                                : SemanticUsageStates.ApparentlyUnused;
                return usage with { UsageState = state };
            })
            .ToArray();
    }

    private static SemanticTableUsage[] ClassifyTables(
        IReadOnlyList<SemanticModelInventory> semanticModels,
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<SemanticDependencyEdge> dependencies,
        IReadOnlySet<string> structuralRoots,
        IReadOnlySet<string> reportMeasureNodes,
        IReadOnlySet<string> reportMeasureRoots)
    {
        var knownNodes = semanticModels
            .SelectMany(model => model.Tables.Select(table => NodeKey(
                model.Name,
                Target(table.Name, table.Name, SemanticObjectTypes.Table))))
            .Concat(usages.Select(usage => NodeKey(usage.SemanticModel, Source(usage))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        knownNodes.UnionWith(reportMeasureNodes);
        var adjacency = BuildAdjacency(dependencies, knownNodes);
        var directRoots = usages
            .Where(usage => usage.IsDirectlyReferencedByReport)
            .Select(usage => NodeKey(usage.SemanticModel, Source(usage)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        directRoots.UnionWith(reportMeasureRoots);
        var directlyReachable = Traverse(directRoots, adjacency);
        var structurallyReachable = Traverse(structuralRoots, adjacency);
        var unusedBranchTableTargets = dependencies
            .Where(edge => edge.DependencyKind != SemanticDependencyKinds.ContainingTable &&
                           edge.ToObjectType == SemanticObjectTypes.Table &&
                           knownNodes.Contains(NodeKey(edge.SemanticModel, Source(edge))))
            .Select(edge => NodeKey(edge.SemanticModel, Target(edge)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return semanticModels
            .SelectMany(model => model.Tables.Select(table =>
            {
                var tableKey = NodeKey(
                    model.Name,
                    Target(table.Name, table.Name, SemanticObjectTypes.Table));
                var tableObjectUsages = usages.Where(usage =>
                    string.Equals(usage.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(usage.Table, table.Name, StringComparison.OrdinalIgnoreCase));
                var state = tableObjectUsages.Any(usage => usage.IsDirectlyReferencedByReport)
                    ? SemanticUsageStates.DirectlyUsed
                    : directlyReachable.Contains(tableKey)
                        ? SemanticUsageStates.IndirectlyUsed
                        : structurallyReachable.Contains(tableKey)
                            ? SemanticUsageStates.StructurallyRequired
                            : unusedBranchTableTargets.Contains(tableKey) ||
                              tableObjectUsages.Any(usage => usage.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch)
                                ? SemanticUsageStates.UsedOnlyByUnusedBranch
                                : SemanticUsageStates.ApparentlyUnused;
                return new SemanticTableUsage(model.Name, table.Name, state);
            }))
            .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, HashSet<string>> BuildAdjacency(
        IReadOnlyList<SemanticDependencyEdge> dependencies,
        HashSet<string> knownNodes)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in dependencies)
        {
            var source = NodeKey(edge.SemanticModel, Source(edge));
            if (!knownNodes.Contains(source))
            {
                continue;
            }

            if (!adjacency.TryGetValue(source, out var targets))
            {
                targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                adjacency.Add(source, targets);
            }

            targets.Add(NodeKey(edge.SemanticModel, Target(edge)));
        }

        return adjacency;
    }

    private static HashSet<string> Traverse(
        IEnumerable<string> roots,
        Dictionary<string, HashSet<string>> adjacency)
    {
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

    private static SemanticDependencyEdge CreateEdge(
        string model,
        SemanticNode source,
        SemanticNode target,
        string kind,
        string evidencePath,
        string evidenceText)
    {
        return new SemanticDependencyEdge(
            model,
            source.Table,
            source.ObjectName,
            source.ObjectType,
            source.HierarchyName,
            target.Table,
            target.ObjectName,
            target.ObjectType,
            target.HierarchyName,
            kind,
            evidencePath,
            evidenceText);
    }

    private static UnresolvedSemanticDependency CreateUnresolved(
        string model,
        SemanticNode source,
        string kind,
        string referenceText,
        string reason,
        string evidencePath)
    {
        return new UnresolvedSemanticDependency(
            model,
            source.Table,
            source.ObjectName,
            source.ObjectType,
            source.HierarchyName,
            kind,
            referenceText,
            reason,
            evidencePath);
    }

    private static string NodeKey(string model, SemanticNode node)
    {
        return string.Join('\u001e', model, FieldIdentity.Create(
            node.Table,
            node.ObjectName,
            node.ObjectType,
            node.HierarchyName));
    }

    private static SemanticNode Source(SemanticObjectUsage usage)
    {
        return Target(usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName);
    }

    private static SemanticNode Source(SemanticDependencyEdge edge)
    {
        return Target(edge.FromTable, edge.FromObjectName, edge.FromObjectType, edge.FromHierarchyName);
    }

    private static SemanticNode Target(SemanticDependencyEdge edge)
    {
        return Target(edge.ToTable, edge.ToObjectName, edge.ToObjectType, edge.ToHierarchyName);
    }

    private static SemanticNode Target(
        string table,
        string objectName,
        string objectType,
        string? hierarchyName = null)
    {
        return new SemanticNode(table, objectName, objectType, hierarchyName);
    }

    private static string QualifiedKey(string table, string objectName) =>
        string.Join('\u001f', table, objectName);

    private sealed class ModelLookup
    {
        private readonly Dictionary<string, SemanticNode> columns;
        private readonly Dictionary<string, SemanticNode> measuresByQualifiedName;
        private readonly Dictionary<string, SemanticNode[]> measuresByName;

        public ModelLookup(SemanticModelInventory model, IReadOnlyList<SemanticObjectUsage> usages)
        {
            TableNames = model.Tables
                .Select(table => table.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            TablePaths = model.Tables.ToDictionary(
                table => table.Name,
                table => table.RelativePath,
                StringComparer.OrdinalIgnoreCase);
            columns = usages
                .Where(usage => usage.ObjectType == SemanticObjectTypes.Column)
                .ToDictionary(
                    usage => QualifiedKey(usage.Table, usage.ObjectName),
                    usage => Source(usage),
                    StringComparer.OrdinalIgnoreCase);
            measuresByQualifiedName = usages
                .Where(usage => usage.ObjectType == SemanticObjectTypes.Measure)
                .ToDictionary(
                    usage => QualifiedKey(usage.Table, usage.ObjectName),
                    usage => Source(usage),
                    StringComparer.OrdinalIgnoreCase);
            measuresByName = usages
                .Where(usage => usage.ObjectType == SemanticObjectTypes.Measure)
                .GroupBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(Source).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public HashSet<string> TableNames { get; }

        public Dictionary<string, string> TablePaths { get; }

        public bool TryResolveDax(
            DaxReferenceExtractor.DaxReference reference,
            string currentTable,
            out SemanticNode target,
            out string reason)
        {
            if (reference.IsTableReference)
            {
                if (reference.Table is not null && TableNames.Contains(reference.Table))
                {
                    target = Target(reference.Table, reference.Table, SemanticObjectTypes.Table);
                    reason = string.Empty;
                    return true;
                }

                target = null!;
                reason = $"Table '{reference.Table}' was not found.";
                return false;
            }

            if (reference.Table is not null)
            {
                return TryResolveQualified(reference.Table, reference.ObjectName, out target, out reason);
            }

            measuresByName.TryGetValue(reference.ObjectName, out var measures);
            columns.TryGetValue(QualifiedKey(currentTable, reference.ObjectName), out var localColumn);
            var candidateCount = (measures?.Length ?? 0) + (localColumn is null ? 0 : 1);
            if (candidateCount == 1)
            {
                target = localColumn ?? measures![0];
                reason = string.Empty;
                return true;
            }

            target = null!;
            reason = candidateCount == 0
                ? $"No measure or '{currentTable}' column named '{reference.ObjectName}' was found."
                : $"The unqualified reference '{reference.ObjectName}' is ambiguous.";
            return false;
        }

        public bool TryResolveQualified(
            string table,
            string objectName,
            out SemanticNode target,
            out string reason)
        {
            var key = QualifiedKey(table, objectName);
            var hasColumn = columns.TryGetValue(key, out var column);
            var hasMeasure = measuresByQualifiedName.TryGetValue(key, out var measure);
            if (hasColumn ^ hasMeasure)
            {
                target = hasColumn ? column! : measure!;
                reason = string.Empty;
                return true;
            }

            target = null!;
            reason = hasColumn
                ? $"'{table}[{objectName}]' matches both a column and a measure."
                : $"'{table}[{objectName}]' was not found.";
            return false;
        }

    }

    private sealed record SemanticNode(
        string Table,
        string ObjectName,
        string ObjectType,
        string? HierarchyName);
}

internal sealed record SemanticDependencyAnalysis(
    SemanticObjectUsage[] ObjectUsages,
    SemanticTableUsage[] TableUsages,
    SemanticDependencyEdge[] Dependencies,
    UnresolvedSemanticDependency[] UnresolvedDependencies);
