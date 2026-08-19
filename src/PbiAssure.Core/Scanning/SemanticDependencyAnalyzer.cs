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

        // A DAX user-defined function is a graph node without a usage row, exactly like a report-level
        // measure, so it needs the same treatment: known to traversal, absent from the reported results.
        var functionNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in semanticModels)
        {
            AnalyzeModel(model, initialUsages, dependencies, unresolved, structuralRoots, functionNodes);
        }

        AnalyzeReportMeasures(
            semanticModels, initialUsages, reports, dependencies, unresolved,
            reportMeasureNodes, reportMeasureRoots);

        var distinctDependencies = dependencies.Distinct().ToArray();
        var classifiedUsages = ClassifyObjects(
            initialUsages, distinctDependencies, structuralRoots, reportMeasureNodes, reportMeasureRoots, functionNodes);
        var tableUsages = ClassifyTables(
            semanticModels, classifiedUsages, distinctDependencies, structuralRoots,
            reportMeasureNodes, reportMeasureRoots, functionNodes);

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
            var model = ReportModelBinder.FindLocalModel(report, semanticModels);
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
                if (SemanticReportReferencePolicy.EstablishesDirectUsage(reference) &&
                    reference.ObjectType == SemanticObjectTypes.Measure &&
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
        ISet<string> structuralRoots,
        ISet<string> functionNodes)
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
            AddFieldParameterMetadataRoots(model, table, structuralRoots);
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

        AnalyzeRoles(model, lookup, dependencies, unresolved, structuralRoots);
        AnalyzePerspectives(model, lookup, dependencies, unresolved, structuralRoots);
        AnalyzeFunctions(model, lookup, dependencies, unresolved, functionNodes);
    }

    private static void AddFieldParameterMetadataRoots(
        SemanticModelInventory model,
        SemanticTableInventory table,
        ISet<string> structuralRoots)
    {
        if (table.FieldParameter is null)
        {
            return;
        }

        var generatedFieldsColumnName = $"{table.Name} Fields";
        foreach (var column in table.Columns.Where(column =>
                     column.IsHidden &&
                     string.Equals(column.Name, generatedFieldsColumnName, StringComparison.OrdinalIgnoreCase)))
        {
            structuralRoots.Add(NodeKey(model.Name, Target(table.Name, column.Name, SemanticObjectTypes.Column)));
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

        foreach (var partition in table.Partitions.Where(partition =>
                     partition.Expression is not null &&
                     string.Equals(partition.SourceType, "calculated", StringComparison.OrdinalIgnoreCase)))
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
        AddDaxDependencies(
            model,
            currentTable.Name,
            currentTable.RelativePath,
            source,
            expression,
            SemanticDependencyKinds.Dax,
            lookup,
            dependencies,
            unresolved,
            structuralRoots: null);
    }

    /// <summary>
    /// Extracts model references from a DAX expression and records them as dependency edges.
    ///
    /// The resolution context and the evidence path are separate, because a role filter is written in the
    /// context of the table its permission names while living in a different file. Unqualified references
    /// such as <c>[Region]</c> resolve against <paramref name="contextTableName"/>, which is how Power BI
    /// Desktop's role serialization is read without rewriting the expression text.
    ///
    /// When <paramref name="structuralRoots"/> is supplied, every resolved target also becomes a
    /// model-structure root, so traversal continues from it exactly as it does from a relationship
    /// endpoint.
    /// </summary>
    private static void AddDaxDependencies(
        SemanticModelInventory model,
        string contextTableName,
        string evidencePath,
        SemanticNode source,
        string expression,
        string dependencyKind,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        ISet<string>? structuralRoots)
    {
        foreach (var reference in DaxReferenceExtractor.Extract(
                     expression, lookup.TableNames, lookup.FunctionNames))
        {
            // A call to a user-defined function reaches everything that function's body references, so
            // the edge is recorded and — where the caller itself is a root-producing context — the
            // function becomes a root too, otherwise nothing beyond it would be reachable.
            if (reference.IsFunctionReference)
            {
                var function = Target(string.Empty, reference.ObjectName, SemanticObjectTypes.Function);
                dependencies.Add(CreateEdge(
                    model.Name,
                    source,
                    function,
                    SemanticDependencyKinds.FunctionCall,
                    evidencePath,
                    reference.Text));
                structuralRoots?.Add(NodeKey(model.Name, function));
                continue;
            }

            if (lookup.TryResolveDax(reference, contextTableName, out var target, out var reason))
            {
                dependencies.Add(CreateEdge(
                    model.Name,
                    source,
                    target,
                    dependencyKind,
                    evidencePath,
                    reference.Text));
                structuralRoots?.Add(NodeKey(model.Name, target));
            }
            else
            {
                unresolved.Add(CreateUnresolved(
                    model.Name,
                    source,
                    dependencyKind,
                    reference.Text,
                    reason,
                    evidencePath));
            }
        }
    }

    /// <summary>
    /// A DAX user-defined function is a **definition**, not active model behaviour: nothing in the model
    /// requires it to exist. That is what separates it from a role filter or a perspective member, both
    /// of which are model-structure roots. A function is therefore a dependency **node** — what it
    /// references becomes reachable only when something reachable calls it, which means an uncalled
    /// function's references correctly land on an unused branch.
    ///
    /// Two things about the reference context differ from a measure's:
    ///
    /// - A function has **no owning table**, so an unqualified name has no local column to resolve
    ///   against. Microsoft documents that an unqualified name inside a function body is interpreted as a
    ///   measure reference, so no table context is invented here.
    /// - **Parameters are local symbols and shadow model objects.** A parameter named the same as a table
    ///   must not produce a table reference, so parameter names are removed from the visible table set
    ///   before the body is read.
    /// </summary>
    private static void AnalyzeFunctions(
        SemanticModelInventory model,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        ISet<string> functionNodes)
    {
        if (model.Functions.Count == 0)
        {
            return;
        }

        var declaredFunctions = model.Functions
            .Select(function => function.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Every name is registered before any body is read, so a call to a function declared later in
        // the file resolves the same as a call to one declared earlier.
        foreach (var function in model.Functions)
        {
            functionNodes.Add(NodeKey(
                model.Name, Target(string.Empty, function.Name, SemanticObjectTypes.Function)));
        }

        foreach (var function in model.Functions)
        {
            var source = Target(string.Empty, function.Name, SemanticObjectTypes.Function);
            var localSymbols = function.Parameters
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var visibleTables = lookup.TableNames
                .Where(table => !localSymbols.Contains(table))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var reference in DaxReferenceExtractor.Extract(
                         function.Expression, visibleTables, declaredFunctions))
            {
                if (reference.IsFunctionReference)
                {
                    dependencies.Add(CreateEdge(
                        model.Name,
                        source,
                        Target(string.Empty, reference.ObjectName, SemanticObjectTypes.Function),
                        SemanticDependencyKinds.FunctionCall,
                        function.RelativePath,
                        reference.Text));
                    continue;
                }

                if (reference.Table is null && localSymbols.Contains(reference.ObjectName))
                {
                    continue;
                }

                if (lookup.TryResolveDax(reference, string.Empty, out var target, out var reason))
                {
                    dependencies.Add(CreateEdge(
                        model.Name, source, target, SemanticDependencyKinds.Dax,
                        function.RelativePath, reference.Text));
                }
                else
                {
                    unresolved.Add(CreateUnresolved(
                        model.Name, source, SemanticDependencyKinds.Dax, reference.Text,
                        $"Function '{function.Name}': {reason}", function.RelativePath));
                }
            }
        }
    }

    /// <summary>
    /// A perspective is a curated subset of the model that an author deliberately exposed, and which
    /// drives the Personalize visuals experience: a report reader may add any of its members to a visual
    /// at run time. Saved report metadata cannot prove which members a reader picks, so each exposed
    /// object becomes a model-structure root — the same treatment field-parameter choices already
    /// receive, and for the same reason.
    ///
    /// Membership is exactly what the perspective lists. Naming a table does not expose its fields
    /// unless includeAll is set, which Microsoft documents as including every column, hierarchy and
    /// measure of that table.
    /// </summary>
    private static void AnalyzePerspectives(
        SemanticModelInventory model,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        ISet<string> structuralRoots)
    {
        foreach (var perspective in model.Perspectives)
        {
            var source = Target(string.Empty, perspective.Name, SemanticObjectTypes.Perspective);
            foreach (var perspectiveTable in perspective.Tables)
            {
                var table = model.Tables.FirstOrDefault(item => string.Equals(
                    item.Name, perspectiveTable.Table, StringComparison.OrdinalIgnoreCase));
                if (table is null)
                {
                    unresolved.Add(CreateUnresolved(
                        model.Name,
                        source,
                        SemanticDependencyKinds.PerspectiveMember,
                        perspectiveTable.Table,
                        $"Perspective '{perspective.Name}': table '{perspectiveTable.Table}' was not found.",
                        perspective.RelativePath));
                    continue;
                }

                // The table itself is exposed. Containing-table edges point from objects to their table,
                // so rooting the table cannot reach its fields — exposure stays as narrow as declared.
                AddPerspectiveMember(
                    model, source, Target(table.Name, table.Name, SemanticObjectTypes.Table),
                    perspective.RelativePath, table.Name, dependencies, structuralRoots);

                foreach (var member in PerspectiveMembers(table, perspectiveTable))
                {
                    if (lookup.TryResolveQualified(table.Name, member, out var target, out var reason))
                    {
                        AddPerspectiveMember(
                            model, source, target, perspective.RelativePath,
                            $"{table.Name}[{member}]", dependencies, structuralRoots);
                    }
                    else
                    {
                        unresolved.Add(CreateUnresolved(
                            model.Name,
                            source,
                            SemanticDependencyKinds.PerspectiveMember,
                            $"{table.Name}[{member}]",
                            $"Perspective '{perspective.Name}': {reason}",
                            perspective.RelativePath));
                    }
                }

                foreach (var level in PerspectiveHierarchyLevels(table, perspectiveTable))
                {
                    AddPerspectiveMember(
                        model, source, level.Node, perspective.RelativePath, level.Text,
                        dependencies, structuralRoots);
                }
            }
        }
    }

    /// <summary>
    /// Column and measure names a perspective exposes for one table. includeAll widens this to every
    /// column and measure the table declares; otherwise only what is listed.
    /// </summary>
    private static IEnumerable<string> PerspectiveMembers(
        SemanticTableInventory table,
        SemanticPerspectiveTableInventory perspectiveTable)
    {
        if (!perspectiveTable.IncludeAll)
        {
            return perspectiveTable.Columns.Concat(perspectiveTable.Measures);
        }

        return table.Columns.Select(column => column.Name)
            .Concat(table.Measures.Select(measure => measure.Name));
    }

    private static IEnumerable<(SemanticNode Node, string Text)> PerspectiveHierarchyLevels(
        SemanticTableInventory table,
        SemanticPerspectiveTableInventory perspectiveTable)
    {
        var hierarchies = perspectiveTable.IncludeAll
            ? table.Hierarchies
            : table.Hierarchies.Where(hierarchy => perspectiveTable.Hierarchies.Contains(
                hierarchy.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var hierarchy in hierarchies)
        {
            foreach (var level in hierarchy.Levels)
            {
                yield return (
                    Target(table.Name, level.Name, SemanticObjectTypes.HierarchyLevel, hierarchy.Name),
                    $"{table.Name}[{hierarchy.Name}]");
            }
        }
    }

    private static void AddPerspectiveMember(
        SemanticModelInventory model,
        SemanticNode source,
        SemanticNode target,
        string evidencePath,
        string evidenceText,
        List<SemanticDependencyEdge> dependencies,
        ISet<string> structuralRoots)
    {
        dependencies.Add(CreateEdge(
            model.Name, source, target, SemanticDependencyKinds.PerspectiveMember,
            evidencePath, evidenceText));
        structuralRoots.Add(NodeKey(model.Name, target));
    }

    /// <summary>
    /// Role table permissions are active model behaviour: an object needed to evaluate a security filter
    /// cannot be removed safely. Each resolved reference becomes a model-structure root, the same
    /// mechanism relationship endpoints use, so ordinary traversal produces the classification rather
    /// than any RLS-specific rule.
    ///
    /// Only table permissions are interpreted. Other role content is not, which is why roles remain a
    /// partially analysed construct in the definition-file registry.
    /// </summary>
    private static void AnalyzeRoles(
        SemanticModelInventory model,
        ModelLookup lookup,
        List<SemanticDependencyEdge> dependencies,
        List<UnresolvedSemanticDependency> unresolved,
        ISet<string> structuralRoots)
    {
        foreach (var role in model.Roles)
        {
            var source = Target(string.Empty, role.Name, SemanticObjectTypes.Role);
            foreach (var permission in role.TablePermissions)
            {
                if (!lookup.TableNames.Contains(permission.Table))
                {
                    unresolved.Add(CreateUnresolved(
                        model.Name,
                        source,
                        SemanticDependencyKinds.TablePermission,
                        permission.Table,
                        $"Role '{role.Name}': table '{permission.Table}' was not found.",
                        role.RelativePath));
                    continue;
                }

                AddDaxDependencies(
                    model,
                    permission.Table,
                    role.RelativePath,
                    source,
                    permission.FilterExpression,
                    SemanticDependencyKinds.TablePermission,
                    lookup,
                    dependencies,
                    unresolved,
                    structuralRoots);
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
        IReadOnlySet<string> reportMeasureRoots,
        IReadOnlySet<string> functionNodes)
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
        knownNodes.UnionWith(functionNodes);

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
        IReadOnlySet<string> reportMeasureRoots,
        IReadOnlySet<string> functionNodes)
    {
        var knownNodes = semanticModels
            .SelectMany(model => model.Tables.Select(table => NodeKey(
                model.Name,
                Target(table.Name, table.Name, SemanticObjectTypes.Table))))
            .Concat(usages.Select(usage => NodeKey(usage.SemanticModel, Source(usage))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        knownNodes.UnionWith(reportMeasureNodes);
        knownNodes.UnionWith(functionNodes);
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
            FunctionNames = model.Functions
                .Select(function => function.Name)
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

        public HashSet<string> FunctionNames { get; }

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
