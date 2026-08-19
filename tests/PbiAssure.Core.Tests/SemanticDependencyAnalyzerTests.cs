using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Dedicated regression tests for <see cref="SemanticDependencyAnalyzer"/>.
///
/// These call the analyzer directly with small hand-built model inventories so that a failure names
/// the dependency engine rather than the scanner. Behaviours that depend on TMDL or PBIR parsing are
/// deliberately left to <see cref="ProjectScannerTests"/>, which already covers them end to end; this
/// file pins the graph semantics that no parser can demonstrate on its own — precedence ordering when
/// an object qualifies for two states, traversal termination, model scoping and reference ambiguity.
///
/// Usages are built by <see cref="Usages"/>, which mirrors the reconciler's object enumeration. The
/// <c>HandBuiltUsagesMatchTheReconciler</c> test guards that mirroring so these fixtures cannot drift
/// away from the usage set the product actually analyses.
/// </summary>
public sealed class SemanticDependencyAnalyzerTests
{
    [Fact]
    public void HandBuiltUsagesMatchTheReconciler()
    {
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount"), Column("Category")],
                measures: [Measure("Total", "SUM(Fact[Amount])")],
                hierarchies: [Hierarchy("Geo", ("Level1", "Category"))],
                calculationGroup: CalculationGroup(("Item1", "SELECTEDMEASURE()"))));

        var (reconciled, _) = SemanticUsageReconciler.Reconcile([model], []);
        var handBuilt = Usages(model);

        Assert.Equal(
            reconciled.Select(Identity).OrderBy(value => value, StringComparer.Ordinal),
            handBuilt.Select(Identity).OrderBy(value => value, StringComparer.Ordinal));
    }

    // ---- State precedence -------------------------------------------------------------------
    // docs/usage-classification.md fixes the order as
    // DirectlyUsed > IndirectlyUsed > StructurallyRequired > UsedOnlyByUnusedBranch > ApparentlyUnused.
    // Each test below puts one object in two states at once and pins which one wins.

    [Fact]
    public void DirectReportUseWinsOverBeingARelationshipEndpoint()
    {
        var model = ModelWith(
            "Sales",
            [Relationship("r1", "Fact", "StoreID", "Store", "StoreID")],
            Table("Fact", columns: [Column("StoreID")]),
            Table("Store", columns: [Column("StoreID")]));
        var usages = Usages(model, directlyUsed: [("Store", "StoreID")]);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Equal(SemanticUsageStates.DirectlyUsed, State(analysis, "Store", "StoreID"));
    }

    [Fact]
    public void ReachabilityFromADirectRootWinsOverBeingARelationshipEndpoint()
    {
        // Store[StoreID] is both a relationship endpoint (structural root) and reachable from the
        // directly used measure. IndirectlyUsed must win over StructurallyRequired.
        var model = ModelWith(
            "Sales",
            [Relationship("r1", "Fact", "StoreID", "Store", "StoreID")],
            Table("Fact", columns: [Column("StoreID")]),
            Table(
                "Store",
                columns: [Column("StoreID")],
                measures: [Measure("Store Count", "COUNTROWS(VALUES(Store[StoreID]))")]));
        var usages = Usages(model, directlyUsed: [("Store", "Store Count")]);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Store", "StoreID"));
    }

    [Fact]
    public void StructuralRequirementWinsOverBeingReferencedOnlyByAnUnusedBranch()
    {
        // Store[StoreID] is a relationship endpoint and is also referenced by a measure nobody uses.
        var model = ModelWith(
            "Sales",
            [Relationship("r1", "Fact", "StoreID", "Store", "StoreID")],
            Table("Fact", columns: [Column("StoreID")]),
            Table(
                "Store",
                columns: [Column("StoreID")],
                measures: [Measure("Unused Store Count", "COUNTROWS(VALUES(Store[StoreID]))")]));
        var usages = Usages(model);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Equal(SemanticUsageStates.StructurallyRequired, State(analysis, "Store", "StoreID"));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, State(analysis, "Store", "Unused Store Count"));
    }

    [Fact]
    public void IncomingEdgeFromAnUnusedObjectSeparatesUnusedBranchFromApparentlyUnused()
    {
        // Amount is referenced only by an unused measure; Untouched is referenced by nothing at all.
        // This is the boundary the product's most consequential wording depends on.
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount"), Column("Untouched")],
                measures: [Measure("Unused Total", "SUM(Fact[Amount])")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(analysis, "Fact", "Amount"));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, State(analysis, "Fact", "Untouched"));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, State(analysis, "Fact", "Unused Total"));
    }

    [Fact]
    public void IndirectUseFollowsAChainOfMeasuresRatherThanOnlyDirectNeighbours()
    {
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount")],
                measures:
                [
                    Measure("Level1", "[Level2] * 2"),
                    Measure("Level2", "[Level3] * 2"),
                    Measure("Level3", "SUM(Fact[Amount])"),
                ]));
        var usages = Usages(model, directlyUsed: [("Fact", "Level1")]);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Equal(SemanticUsageStates.DirectlyUsed, State(analysis, "Fact", "Level1"));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "Level2"));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "Level3"));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "Amount"));
    }

    // ---- Traversal safety -------------------------------------------------------------------

    [Fact]
    public void MutuallyReferencingMeasuresDoNotMakeTraversalLoopForever()
    {
        // A model can contain a reference cycle. Traverse must terminate and still classify.
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount")],
                measures:
                [
                    Measure("A", "[B] + SUM(Fact[Amount])"),
                    Measure("B", "[A] + 1"),
                ]));
        var usages = Usages(model, directlyUsed: [("Fact", "A")]);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Equal(SemanticUsageStates.DirectlyUsed, State(analysis, "Fact", "A"));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "B"));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "Amount"));
    }

    [Fact]
    public void ASelfReferencingMeasureDoesNotMakeTraversalLoopForever()
    {
        var model = Model(
            "Sales",
            Table("Fact", columns: [Column("Amount")], measures: [Measure("Recursive", "[Recursive] + 1")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(analysis, "Fact", "Recursive"));
    }

    // ---- Model scoping ----------------------------------------------------------------------

    [Fact]
    public void UseInOneModelDoesNotClassifyTheSameNamesInAnotherModel()
    {
        var first = Model(
            "First",
            Table("Fact", columns: [Column("Amount")], measures: [Measure("Total", "SUM(Fact[Amount])")]));
        var second = Model(
            "Second",
            Table("Fact", columns: [Column("Amount")], measures: [Measure("Total", "SUM(Fact[Amount])")]));
        var usages = Usages(first, directlyUsed: [("Fact", "Total")]).Concat(Usages(second)).ToArray();

        var analysis = SemanticDependencyAnalyzer.Analyze([first, second], usages, []);

        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "Amount", model: "First"));
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(analysis, "Fact", "Amount", model: "Second"));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, State(analysis, "Fact", "Total", model: "Second"));
    }

    [Fact]
    public void DependencyEdgesCarryTheModelTheyWereObservedIn()
    {
        var first = Model("First", Table("Fact", columns: [Column("Amount")], measures: [Measure("Total", "SUM(Fact[Amount])")]));
        var second = Model("Second", Table("Fact", columns: [Column("Amount")], measures: [Measure("Total", "SUM(Fact[Amount])")]));

        var analysis = SemanticDependencyAnalyzer.Analyze(
            [first, second],
            Usages(first).Concat(Usages(second)).ToArray(),
            []);

        var daxEdges = analysis.Dependencies
            .Where(edge => edge.DependencyKind == SemanticDependencyKinds.Dax && edge.FromObjectName == "Total")
            .ToArray();
        Assert.Equal(2, daxEdges.Length);
        Assert.Contains(daxEdges, edge => edge.SemanticModel == "First");
        Assert.Contains(daxEdges, edge => edge.SemanticModel == "Second");
    }

    // ---- Reference resolution and its refusals ----------------------------------------------
    // The product's stated contract is that an unresolvable reference is retained as evidence and
    // never guessed at. These pin the exact conditions under which the analyzer refuses to resolve.

    [Fact]
    public void AnUnqualifiedReferenceMatchingBothAMeasureAndALocalColumnIsLeftUnresolved()
    {
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount")],
                measures: [Measure("Amount", "1"), Measure("Uses", "[Amount] + 1")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        var unresolved = Assert.Single(
            analysis.UnresolvedDependencies,
            item => item.FromObjectName == "Uses");
        Assert.Equal(SemanticDependencyKinds.Dax, unresolved.DependencyKind);
        Assert.Contains("ambiguous", unresolved.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            analysis.Dependencies,
            edge => edge.FromObjectName == "Uses" && edge.DependencyKind == SemanticDependencyKinds.Dax);
    }

    [Fact]
    public void AQualifiedReferenceMatchingBothAColumnAndAMeasureIsLeftUnresolved()
    {
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount")],
                measures: [Measure("Amount", "1"), Measure("Uses", "SUM(Fact[Amount])")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        var unresolved = Assert.Single(
            analysis.UnresolvedDependencies,
            item => item.FromObjectName == "Uses");
        Assert.Contains("matches both", unresolved.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnqualifiedReferenceResolvesToTheMeasureWhenNoLocalColumnCompetes()
    {
        var model = Model(
            "Sales",
            Table("Fact", columns: [Column("Amount")], measures: [Measure("Total", "SUM(Fact[Amount])"), Measure("Uses", "[Total] * 2")]));
        var usages = Usages(model, directlyUsed: [("Fact", "Uses")]);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Empty(analysis.UnresolvedDependencies);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(analysis, "Fact", "Total"));
    }

    [Fact]
    public void AMissingSortByColumnIsRetainedAsEvidenceRatherThanInventingANode()
    {
        var model = Model(
            "Sales",
            Table("Fact", columns: [Column("Month", sortByColumn: "MonthNumber")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        var unresolved = Assert.Single(analysis.UnresolvedDependencies);
        Assert.Equal(SemanticDependencyKinds.SortBy, unresolved.DependencyKind);
        Assert.Equal("MonthNumber", unresolved.ReferenceText);
        Assert.DoesNotContain(
            analysis.Dependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.SortBy);
        Assert.DoesNotContain(analysis.ObjectUsages, usage => usage.ObjectName == "MonthNumber");
    }

    [Fact]
    public void AMissingRelationshipEndpointIsRetainedAndDoesNotSeedAStructuralRoot()
    {
        var model = ModelWith(
            "Sales",
            [Relationship("r1", "Fact", "MissingKey", "Store", "StoreID")],
            Table("Fact", columns: [Column("Amount")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        Assert.Equal(2, analysis.UnresolvedDependencies.Length);
        Assert.All(
            analysis.UnresolvedDependencies,
            item => Assert.Equal(SemanticDependencyKinds.RelationshipEndpoint, item.DependencyKind));
        Assert.All(
            analysis.UnresolvedDependencies,
            item => Assert.Contains("r1", item.Reason, StringComparison.Ordinal));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, State(analysis, "Fact", "Amount"));
    }

    // ---- Edge emission ----------------------------------------------------------------------

    [Fact]
    public void EveryObjectGetsAContainingTableEdge()
    {
        var model = Model(
            "Sales",
            Table("Fact", columns: [Column("Amount")], measures: [Measure("Total", "1")]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        var containing = analysis.Dependencies
            .Where(edge => edge.DependencyKind == SemanticDependencyKinds.ContainingTable)
            .ToArray();
        Assert.Equal(2, containing.Length);
        Assert.All(containing, edge => Assert.Equal("Fact", edge.ToTable));
        Assert.All(containing, edge => Assert.Equal(SemanticObjectTypes.Table, edge.ToObjectType));
    }

    [Fact]
    public void SortByAndHierarchyLevelsProduceTheirOwnEdgeKinds()
    {
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Month", sortByColumn: "MonthNumber"), Column("MonthNumber")],
                hierarchies: [Hierarchy("Calendar", ("Month Level", "Month"))]));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        Assert.Contains(
            analysis.Dependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.SortBy &&
                edge.FromObjectName == "Month" &&
                edge.ToObjectName == "MonthNumber");
        var level = Assert.Single(
            analysis.Dependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.HierarchyLevel);
        Assert.Equal("Month Level", level.FromObjectName);
        Assert.Equal("Calendar", level.FromHierarchyName);
        Assert.Equal("Month", level.ToObjectName);
    }

    [Fact]
    public void DependencyEdgesAreDeduplicatedAndDeterministicallyOrdered()
    {
        var model = Model(
            "Sales",
            Table(
                "Fact",
                columns: [Column("Amount")],
                // Two references to the same column from one expression must yield one edge.
                measures: [Measure("Total", "SUM(Fact[Amount]) + MIN(Fact[Amount])")]));

        var first = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);
        var second = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        Assert.Single(
            first.Dependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.Dax && edge.FromObjectName == "Total");
        Assert.Equal(first.Dependencies, second.Dependencies);
    }

    // ---- Table classification ---------------------------------------------------------------

    [Fact]
    public void TableStateFollowsTheObjectsItContains()
    {
        var model = ModelWith(
            "Sales",
            [Relationship("r1", "Fact", "StoreID", "Store", "StoreID")],
            Table("Fact", columns: [Column("StoreID")], measures: [Measure("Total", "1")]),
            Table("Store", columns: [Column("StoreID")]),
            Table("Orphan", columns: [Column("Ignored")]));
        var usages = Usages(model, directlyUsed: [("Fact", "Total")]);

        var analysis = SemanticDependencyAnalyzer.Analyze([model], usages, []);

        Assert.Equal(SemanticUsageStates.DirectlyUsed, TableState(analysis, "Fact"));
        Assert.Equal(SemanticUsageStates.StructurallyRequired, TableState(analysis, "Store"));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, TableState(analysis, "Orphan"));
    }

    [Fact]
    public void EveryModelTableIsClassifiedEvenWithNoColumnsOrMeasures()
    {
        var model = Model("Sales", Table("Empty"));

        var analysis = SemanticDependencyAnalyzer.Analyze([model], Usages(model), []);

        Assert.Equal(SemanticUsageStates.ApparentlyUnused, TableState(analysis, "Empty"));
        Assert.Empty(analysis.ObjectUsages);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string State(
        SemanticDependencyAnalysis analysis,
        string table,
        string objectName,
        string model = "Sales")
    {
        return analysis.ObjectUsages.Single(usage =>
            usage.SemanticModel == model &&
            usage.Table == table &&
            usage.ObjectName == objectName).UsageState;
    }

    private static string TableState(SemanticDependencyAnalysis analysis, string table, string model = "Sales") =>
        analysis.TableUsages.Single(usage => usage.SemanticModel == model && usage.Table == table).UsageState;

    private static string Identity(SemanticObjectUsage usage) =>
        string.Join('|', usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.HierarchyName);

    /// <summary>
    /// Builds the usage set the reconciler would produce for a model, marking the named objects as
    /// directly referenced by a report. Kept in step with the reconciler by
    /// <c>HandBuiltUsagesMatchTheReconciler</c>.
    /// </summary>
    private static SemanticObjectUsage[] Usages(
        SemanticModelInventory model,
        (string Table, string ObjectName)[]? directlyUsed = null)
    {
        var direct = (directlyUsed ?? [])
            .Select(item => string.Join('|', item.Table, item.ObjectName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return model.Tables
            .SelectMany(table => table.Columns
                .Select(column => (table.Name, column.Name, Type: SemanticObjectTypes.Column, Hierarchy: (string?)null))
                .Concat(table.Measures
                    .Select(measure => (table.Name, measure.Name, Type: SemanticObjectTypes.Measure, Hierarchy: (string?)null)))
                .Concat(table.Hierarchies
                    .SelectMany(hierarchy => hierarchy.Levels
                        .Select(level => (table.Name, level.Name, Type: SemanticObjectTypes.HierarchyLevel, Hierarchy: (string?)hierarchy.Name))))
                .Concat((table.CalculationGroup?.Items ?? [])
                    .Select(item => (table.Name, item.Name, Type: SemanticObjectTypes.CalculationItem, Hierarchy: (string?)null))))
            .Select(item =>
            {
                var isDirect = direct.Contains(string.Join('|', item.Item1, item.Item2));
                IReadOnlyList<SemanticUsageEvidence> evidence = isDirect
                    ?
                    [
                        new SemanticUsageEvidence(
                            Report: "Report",
                            Page: "page1",
                            Visual: "visual1",
                            ArtifactPath: "Report/definition/pages/page1/visuals/visual1/visual.json",
                            UsageContext: "Visual",
                            Role: null,
                            EvidencePath: "$.visual.query"),
                    ]
                    : [];
                return new SemanticObjectUsage(
                    SemanticModel: model.Name,
                    Table: item.Item1,
                    ObjectName: item.Item2,
                    ObjectType: item.Type,
                    HierarchyName: item.Hierarchy,
                    DirectReportReferences: evidence,
                    UsageState: isDirect
                        ? SemanticUsageStates.DirectlyUsed
                        : SemanticUsageStates.ApparentlyUnused);
            })
            .ToArray();
    }

    private static SemanticModelInventory Model(string name, params SemanticTableInventory[] tables) =>
        new(name, $"{name}.SemanticModel", tables, [], []);

    private static SemanticModelInventory ModelWith(
        string name,
        IReadOnlyList<SemanticRelationshipInventory> relationships,
        params SemanticTableInventory[] tables) =>
        new(name, $"{name}.SemanticModel", tables, relationships, []);

    private static SemanticTableInventory Table(
        string name,
        IReadOnlyList<SemanticColumnInventory>? columns = null,
        IReadOnlyList<SemanticMeasureInventory>? measures = null,
        IReadOnlyList<SemanticHierarchyInventory>? hierarchies = null,
        SemanticCalculationGroupInventory? calculationGroup = null) =>
        new(
            Name: name,
            RelativePath: $"Sales.SemanticModel/definition/tables/{name}.tmdl",
            IsHidden: false,
            IsPrivate: false,
            IsSystemGenerated: false,
            SystemGeneratedKind: null,
            Columns: columns ?? [],
            Measures: measures ?? [],
            Hierarchies: hierarchies ?? [],
            Partitions: [],
            CalculationGroup: calculationGroup,
            FieldParameter: null);

    private static SemanticColumnInventory Column(string name, string? sortByColumn = null) =>
        new(name, DataType: "string", IsHidden: false, SourceColumn: name, SortByColumn: sortByColumn, Expression: null);

    private static SemanticMeasureInventory Measure(string name, string expression) =>
        new(name, expression, FormatString: null, IsHidden: false);

    private static SemanticHierarchyInventory Hierarchy(string name, params (string Level, string Column)[] levels) =>
        new(name, IsHidden: false, levels.Select(level => new SemanticHierarchyLevelInventory(level.Level, level.Column)).ToArray());

    private static SemanticCalculationGroupInventory CalculationGroup(params (string Name, string Expression)[] items) =>
        new(
            Precedence: null,
            SelectionExpression: null,
            MultipleOrEmptySelectionExpression: null,
            items.Select(item => new SemanticCalculationItemInventory(item.Name, item.Expression, null, null)).ToArray());

    private static SemanticRelationshipInventory Relationship(
        string name,
        string fromTable,
        string fromColumn,
        string toTable,
        string toColumn) =>
        new(
            Name: name,
            IsActive: true,
            CrossFilteringBehavior: "oneDirection",
            FromCardinality: "many",
            FromTable: fromTable,
            FromColumn: fromColumn,
            ToCardinality: "one",
            ToTable: toTable,
            ToColumn: toColumn);
}
