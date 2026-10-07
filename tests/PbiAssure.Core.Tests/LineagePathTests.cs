using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Lineage slice 2: path to report, and report-measure cards.
///
/// A path is read from what the scanner published — dependency edges, reachability and direct report
/// evidence — and must agree with the existing usage state rather than compete with it: directly used
/// objects end at their own report location, indirectly used ones are reached through report-reachable
/// predecessors, and everything else has no path, with the existing reason for that.
/// </summary>
public sealed class LineagePathTests
{
    // ---- Invariants over every fixture ------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SemanticLineageTests.Fixtures), MemberType = typeof(SemanticLineageTests))]
    public void EveryPathAgreesWithTheExistingClassificationAndPublishedEdges(string fixture)
    {
        var inventory = ScanFixture(fixture);
        var lineage = SemanticLineageProjection.Build(inventory);
        var structural = new[] { SemanticObjectTypes.Relationship, SemanticObjectTypes.Role, SemanticObjectTypes.Perspective, SemanticObjectTypes.RefreshPolicy };
        var reachability = inventory.ReportScopedNodeReachability.ToDictionary(
            node => SemanticGraphIndex.NodeKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName, node.Report),
            StringComparer.OrdinalIgnoreCase);

        foreach (var card in lineage.Cards.Where(card => card.Kind != LineageFocusKind.Visual))
        {
            var path = card.Path!;
            Assert.Equal(card.NodeKey, path.Steps[0].NodeKey, StringComparer.OrdinalIgnoreCase);
            Assert.Null(path.Steps[0].CardId);
            switch (card.Usage?.UsageState)
            {
                case SemanticUsageStates.DirectlyUsed:
                    Assert.Equal(LineagePathStatus.DirectlyUsed, path.Status);
                    Assert.Same(card.UsedInReport.Items[0], path.Endpoint);
                    Assert.Equal(card.Usage.DirectReportLocationCount, path.EndpointLocationCount);
                    break;
                case SemanticUsageStates.IndirectlyUsed:
                    Assert.Equal(LineagePathStatus.ReachedThroughModel, path.Status);
                    break;
                case null:
                    // Functions and report measures: a path exists exactly when they are reached.
                    Assert.Equal(card.ReachedFromReport == true, path.Status != LineagePathStatus.NotFound);
                    break;
                default:
                    Assert.Equal(LineagePathStatus.NotFound, path.Status);
                    break;
            }

            if (path.Status == LineagePathStatus.NotFound)
            {
                Assert.Single(path.Steps);
                Assert.Null(path.Endpoint);
                continue;
            }

            Assert.NotNull(path.Endpoint);
            Assert.Equal(path.Steps.Count, path.Steps.Select(step => step.NodeKey).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            for (var index = 1; index < path.Steps.Count; index++)
            {
                var (previous, step) = (path.Steps[index - 1], path.Steps[index]);
                Assert.DoesNotContain(step.ObjectType, structural);
                Assert.True(reachability[step.NodeKey].ReachableFromReport, step.Name);
                Assert.Contains(inventory.SemanticDependencies, edge =>
                    string.Equals(SemanticGraphIndex.SourceKey(edge), step.NodeKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(SemanticGraphIndex.TargetKey(edge), previous.NodeKey, StringComparison.OrdinalIgnoreCase));
            }

            // The endpoint is the last step's own policy-filtered direct evidence. A report measure's node is
            // its own report's, so its endpoint is that report's evidence, never a same-named one's elsewhere.
            var last = path.Steps[^1];
            var lastEvidence = inventory.SemanticObjectUsages
                .Where(usage => string.Equals(SemanticLineageProjection.NodeKey(usage), last.NodeKey, StringComparison.OrdinalIgnoreCase))
                .SelectMany(usage => usage.DirectReportLocations)
                .Concat(inventory.ReportMeasureUsages
                    .Where(usage => string.Equals(
                        SemanticGraphIndex.NodeKey(usage.SemanticModel, usage.Entity, usage.Name, SemanticObjectTypes.ReportMeasure, null, usage.ReportPath),
                        last.NodeKey, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(usage => usage.DirectReportLocations))
                .ToArray();
            Assert.Contains(path.Endpoint.Location, lastEvidence);
            Assert.Equal(lastEvidence.Length, path.EndpointLocationCount);
            Assert.InRange(path.OtherReportReachingConsumers, 0, Math.Max(0, card.UsedBy.TotalCount - 1));

            // A visual endpoint's own card lists the last step among the objects it uses.
            if (path.Endpoint.VisualCardId is { } visualCardId)
            {
                var visual = Assert.Single(lineage.Cards, item => item.Id == visualCardId);
                Assert.Contains(visual.Uses.Items, use => string.Equals(use.Object.NodeKey, last.NodeKey, StringComparison.OrdinalIgnoreCase) &&
                    (last.CardId is null || use.Object.CardId == last.CardId));
            }
        }
    }

    [Theory]
    [MemberData(nameof(SemanticLineageTests.Fixtures), MemberType = typeof(SemanticLineageTests))]
    public void PathsAreTheSameOnEveryBuild(string fixture)
    {
        static string[] Describe(SemanticLineageProjection lineage) => lineage.Cards
            .Where(card => card.Path is not null)
            .Select(card => string.Join(" > ", card.Path!.Steps.Select(step => step.NodeKey)) +
                $" | {card.Path.Status} | {card.Path.Endpoint?.Location} | {card.Path.EndpointLocationCount} | {card.Path.OtherReportReachingConsumers}" +
                $" | {string.Join(",", card.Path.OnlyReachedFrom.Items.Select(item => item.NodeKey))}")
            .ToArray();

        Assert.Equal(Describe(SemanticLineageProjection.Build(ScanFixture(fixture))), Describe(SemanticLineageProjection.Build(ScanFixture(fixture))));
    }

    // ---- A. Direct use ------------------------------------------------------------------------------

    [Fact]
    public void ADirectlyUsedObjectEndsAtItsFirstReportLocationAndCountsTheRest()
    {
        var inventory = ScanFixture("desktop-field-parameter-evidence");
        var lineage = SemanticLineageProjection.Build(inventory);
        var metric = Card(lineage, inventory, "Metric", "Metric");

        Assert.Equal(LineagePathStatus.DirectlyUsed, metric.Path!.Status);
        Assert.Single(metric.Path.Steps);
        Assert.Equal(2, metric.Path.EndpointLocationCount);
        var article = Article(inventory, metric);
        Assert.Contains("data-lineage-path=\"direct\"", article, StringComparison.Ordinal);
        Assert.Contains("Used directly in 2 report locations.", article, StringComparison.Ordinal);
        Assert.DoesNotContain("other report location", article, StringComparison.Ordinal);
    }

    [Fact]
    public void AmongSeveralLocationsTheEndpointFollowsPageOrderThenPosition()
    {
        var inventory = ScanProject(
            [("Sales", "table Sales\n" + Column("Amount") + "\tmeasure Total = SUM(Sales[Amount])\n")],
            [
                Page("p2", "Second", 1, Visual("lower", 0, 300, ("Measure", "Sales", "Total"))),
                Page("p1", "First", 0,
                    Visual("lower", 0, 300, ("Measure", "Sales", "Total")),
                    Visual("upper", 0, 10, ("Measure", "Sales", "Total"))),
            ]);
        var lineage = SemanticLineageProjection.Build(inventory);
        var total = Card(lineage, inventory, "Sales", "Total");

        Assert.Equal(LineagePathStatus.DirectlyUsed, total.Path!.Status);
        Assert.Equal(("p1", "upper"), (total.Path.Endpoint!.Location.Page, total.Path.Endpoint.Location.Visual));
        Assert.Equal(3, total.Path.EndpointLocationCount);

        var amount = Card(lineage, inventory, "Sales", "Amount");
        Assert.Equal(["Sales[Amount]", "Sales[Total]"], amount.Path!.Steps.Select(step => step.Name).ToArray());
        Assert.Equal(("p1", "upper"), (amount.Path.Endpoint!.Location.Page, amount.Path.Endpoint.Location.Visual));
        Assert.Contains("Sales[Total] is also used in 2 other report locations.", Article(inventory, amount), StringComparison.Ordinal);
    }

    // ---- B / C. Indirect one hop and multi-hop -------------------------------------------------------

    [Fact]
    public void AnIndirectlyUsedColumnIsReachedThroughTheMeasureThatUsesIt()
    {
        var inventory = ScanFixture("desktop-field-parameter-evidence");
        var lineage = SemanticLineageProjection.Build(inventory);
        var amount = Card(lineage, inventory, "Sales", "Amount");

        Assert.Equal(LineagePathStatus.ReachedThroughModel, amount.Path!.Status);
        Assert.Equal(["Sales[Amount]", "Sales[Total Amount]"], amount.Path.Steps.Select(step => step.Name).ToArray());
        Assert.True(amount.Path.Steps[1].HasOnlyDefaultRelationship);
        Assert.Equal(Card(lineage, inventory, "Sales", "Total Amount").Id, amount.Path.Steps[1].CardId);
        Assert.NotNull(amount.Path.Endpoint!.Visual);

        var article = Article(inventory, amount);
        Assert.Contains("<section class=\"lineage-path\" data-lineage-path=\"model\"><h3>Path to report</h3>", article, StringComparison.Ordinal);
        Assert.Contains($"<li class=\"lineage-path-step\" data-lineage-path-step=\"model\" data-lineage-state=\"DirectlyUsed\"><a class=\"lineage-node\" href=\"#{amount.Path.Steps[1].CardId}\">Sales[Total Amount]</a>", article, StringComparison.Ordinal);
        Assert.Contains($"<li class=\"lineage-path-step lineage-endpoint\" data-lineage-path-step=\"report\"><a class=\"lineage-node\" href=\"#visual-{amount.Path.Endpoint.VisualCardId![4..]}-summary\">", article, StringComparison.Ordinal);
        // The focus is the card's centre: in the path it is only the start mark, named for assistive technology.
        Assert.Contains("data-lineage-path-step=\"focus\"><span class=\"visually-hidden\">Sales[Amount]</span></li>", article, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CalculatedSourceColumn", "Fact[CalculatedSourceColumn]|Fact[CalculatedDependencyColumn]|Fact[DirectlyUsedMeasure]")]
    [InlineData("DynamicFormatStringOnlyColumn", "Fact[DynamicFormatStringOnlyColumn]|Fact[DynamicFormatStringMeasure]|Fact[DirectlyUsedMeasure]")]
    [InlineData("ReportRootedUdfSource", "Fact[ReportRootedUdfSource]|ReportRootedCoverageFunction|Fact[ReportRootedUdfMeasure]")]
    public void MultiHopPathsFollowRealChains(string column, string expected)
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);

        Assert.Equal(expected, string.Join("|", Card(lineage, inventory, "Fact", column).Path!.Steps.Select(step => step.Name)));
    }

    // ---- D / E. Field parameters and calculation groups keep their table hop ------------------------

    [Fact]
    public void ACalculationGroupPathKeepsItsTableHopAndTerminates()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var steps = Card(lineage, inventory, "Fact", "CalculationItemOnlyColumn").Path!.Steps;

        Assert.Equal(
            ["Fact[CalculationItemOnlyColumn]", "Time Intelligence[Coverage measure only]", "Time Intelligence", "Time Intelligence[Time Calculation]"],
            steps.Select(step => step.Name).ToArray());
        Assert.Equal(SemanticObjectTypes.Table, steps[2].ObjectType);
        Assert.Equal(["calculation item"], steps[2].RelationshipLabels);
        Assert.Equal(["in table"], steps[3].RelationshipLabels);
        Assert.Null(steps[2].CardId);
    }

    [Fact]
    public void AFieldParameterPathPassesThroughTheParameterTable()
    {
        var inventory = ScanProject(
            [
                ("Sales", "table Sales\n" + Column("Amount") +
                    "\tmeasure Shown = SUM(Sales[Amount])\n\tmeasure 'Only via parameter' = SUM(Sales[Amount]) * 2\n"),
                ("Metric", FieldParameterTable("Metric", "Only via parameter")),
            ],
            [Page("p1", "Page 1", 0, Visual("slicer", 0, 0, ("Column", "Metric", "Metric")), Visual("card", 0, 300, ("Measure", "Sales", "Shown")))]);
        var lineage = SemanticLineageProjection.Build(inventory);
        var onlyViaParameter = Card(lineage, inventory, "Sales", "Only via parameter");

        Assert.Equal(SemanticUsageStates.IndirectlyUsed, onlyViaParameter.Usage!.UsageState);
        var steps = onlyViaParameter.Path!.Steps;
        Assert.Equal(["Sales[Only via parameter]", "Metric", "Metric[Metric]"], steps.Select(step => step.Name).ToArray());
        Assert.Equal(["field parameter"], steps[1].RelationshipLabels);
        Assert.Equal(["in table"], steps[2].RelationshipLabels);
        Assert.Equal("slicer", onlyViaParameter.Path.Endpoint!.Location.Visual);
        Assert.Contains("<span class=\"lineage-relationship\">field parameter</span>", Article(inventory, onlyViaParameter), StringComparison.Ordinal);
    }

    [Fact]
    public void ASortByPathIsShownWithItsLabel()
    {
        var inventory = ScanFixture("desktop-field-parameter-evidence");
        var lineage = SemanticLineageProjection.Build(inventory);
        var order = Card(lineage, inventory, "Metric", "Metric Order").Path!;

        Assert.Equal(["Metric[Metric Order]", "Metric[Metric]"], order.Steps.Select(step => step.Name).ToArray());
        Assert.Equal(["sort by"], order.Steps[1].RelationshipLabels);
    }

    [Fact]
    public void UnusedCalculationItemsHaveNoPath()
    {
        var inventory = ScanFixture("desktop-calculation-group-selection-evidence");
        var lineage = SemanticLineageProjection.Build(inventory);

        foreach (var item in inventory.SemanticObjectUsages.Where(usage => usage.ObjectType == SemanticObjectTypes.CalculationItem))
        {
            var path = lineage.CardFor(item)!.Path!;
            Assert.Equal(LineagePathStatus.NotFound, path.Status);
            Assert.NotEmpty(path.OnlyReachedFrom.Items);
        }

        Assert.Equal(LineagePathStatus.ReachedThroughModel, Card(lineage, inventory, "Sales", "Amount").Path!.Status);
    }

    // ---- F. Functions ---------------------------------------------------------------------------------

    [Fact]
    public void APathRunsThroughAFunctionWhereTheEvidenceDoes()
    {
        var inventory = ScanFixture("desktop-udf-measure-consumer");
        var lineage = SemanticLineageProjection.Build(inventory);
        var steps = Card(lineage, inventory, "Sales", "Amount").Path!.Steps;

        Assert.Equal(["Sales[Amount]", "Sales[Total Amount]", "Doubled", "Sales[UDF Result]"], steps.Select(step => step.Name).ToArray());
        Assert.Equal(SemanticObjectTypes.Function, steps[2].ObjectType);
        Assert.Equal(["function call"], steps[3].RelationshipLabels);

        var uncalled = Assert.Single(lineage.Cards, card => card.Kind == LineageFocusKind.Function && card.Title == "TotalOf");
        Assert.Equal(LineagePathStatus.NotFound, uncalled.Path!.Status);
    }

    // ---- G. Report measures ---------------------------------------------------------------------------

    [Fact]
    public void AReportMeasureHasACardReportContextAndAConcreteEndpoint()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var active = lineage.CardForReportMeasure("PbiAssureCoverage", "Fact", "ActiveReportMeasure")!;

        Assert.Equal(LineageFocusKind.ReportMeasure, active.Kind);
        Assert.Equal("PbiAssureCoverage", active.ReportName);
        Assert.Null(active.Usage);
        Assert.Equal(LineagePathStatus.DirectlyUsed, active.Path!.Status);
        Assert.NotNull(active.Path.Endpoint!.VisualCardId);

        // Visual and report measure are one relation, as for model objects.
        var visual = Assert.Single(lineage.Cards, card => card.Id == active.Path.Endpoint.VisualCardId);
        Assert.Contains(visual.Uses.Items, use => use.Object.CardId == active.Id);

        var html = HtmlReportRenderer.Render(inventory);
        var article = Article(html, active.Id);
        Assert.Contains("data-lineage-card=\"report-measure\"", article, StringComparison.Ordinal);
        Assert.Contains("Report measure · Table Fact · Report PbiAssureCoverage", System.Net.WebUtility.HtmlDecode(article), StringComparison.Ordinal);
        Assert.Contains("<span class=\"lineage-reach\">Reached from a report</span>", article, StringComparison.Ordinal);
        Assert.DoesNotContain("badge-used", article, StringComparison.Ordinal);
        // Report pages lists the report measure with an id and a way into its lineage.
        Assert.Contains($"id=\"{active.DetailsAnchor}\"", html, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"#sum-{active.Id[4..]}\">Fact[ActiveReportMeasure]</a>", html, StringComparison.Ordinal);

        var unused = lineage.CardForReportMeasure("PbiAssureCoverage", "Fact", "UnusedReportMeasure")!;
        Assert.Equal(LineagePathStatus.NotFound, unused.Path!.Status);
        Assert.Contains("<span class=\"lineage-reach\">Not reached from a report</span>", Article(html, unused.Id), StringComparison.Ordinal);
    }

    [Fact]
    public void ModelObjectsReachedThroughAReportMeasureEndAtItsVisual()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var source = Card(lineage, inventory, "Fact", "ReportMeasureActiveSource").Path!;
        var active = lineage.CardForReportMeasure("PbiAssureCoverage", "Fact", "ActiveReportMeasure")!;

        Assert.Equal(["Fact[ReportMeasureActiveSource]", "Fact[ActiveReportMeasure]"], source.Steps.Select(step => step.Name).ToArray());
        Assert.Equal(SemanticObjectTypes.ReportMeasure, source.Steps[1].ObjectType);
        Assert.Equal(active.Id, source.Steps[1].CardId);
        Assert.Equal(active.Path!.Endpoint!.Location, source.Endpoint!.Location);
    }

    [Fact]
    public void SameNamedReportMeasuresInTwoReportsKeepTheirLineageInTheirOwnReport()
    {
        // A.Local = SUM(Amount) is placed; B.Local = SUM(Qty) is not. Each is its own report's node.
        var inventory = ReportMeasureUsageTests.ScanTwoReports();
        var lineage = SemanticLineageProjection.Build(inventory);
        var inA = lineage.CardForReportMeasure("A", "Sales", "Local")!;
        var inB = lineage.CardForReportMeasure("B", "Sales", "Local")!;

        Assert.NotEqual(inA.Id, inB.Id);
        Assert.NotEqual(inA.NodeKey, inB.NodeKey, StringComparer.OrdinalIgnoreCase);
        Assert.Same(inA, lineage.CardForNode(inA.NodeKey!));
        Assert.Same(inB, lineage.CardForNode(inB.NodeKey!));
        Assert.Equal(["Sales[Amount]"], inA.DependsOn.Items.Select(item => item.Name).ToArray());
        Assert.Equal(["Sales[Qty]"], inB.DependsOn.Items.Select(item => item.Name).ToArray());
        Assert.True(inA.ReachedFromReport);
        Assert.False(inB.ReachedFromReport);
        Assert.Equal(0, inB.UsedInReport.TotalCount);
        Assert.Equal(LineagePathStatus.NotFound, inB.Path!.Status);

        // Qty is used only by B's unplaced measure, so it is in an unused branch that starts there.
        var qty = Card(lineage, inventory, "Sales", "Qty");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, qty.Usage!.UsageState);
        Assert.Equal(LineagePathStatus.NotFound, qty.Path!.Status);
        var head = Assert.Single(qty.Path.OnlyReachedFrom.Items);
        Assert.Equal((inB.Id, "B", false), (head.CardId, head.Report, head.ReachableFromReport));

        // Each neighbour is one report's own report measure, linked to that report's card.
        var amountUser = Assert.Single(Card(lineage, inventory, "Sales", "Amount").UsedBy.Items);
        Assert.Equal((inA.Id, "A", true), (amountUser.CardId, amountUser.Report, amountUser.ReachableFromReport));
        var qtyUser = Assert.Single(qty.UsedBy.Items);
        Assert.Equal((inB.Id, "B", false), (qtyUser.CardId, qtyUser.Report, qtyUser.ReachableFromReport));

        var html = System.Net.WebUtility.HtmlDecode(HtmlReportRenderer.Render(inventory));
        var articleB = Article(html, inB.Id);
        Assert.Contains("<span class=\"lineage-reach\">Not reached from a report</span>", articleB, StringComparison.Ordinal);
        var qtyArticle = Article(html, qty.Id);
        var qtyPath = PathSection(qtyArticle);
        Assert.Contains("No path to the report found in this project.", qtyPath, StringComparison.Ordinal);
        Assert.Contains("Only used through: ", qtyPath, StringComparison.Ordinal);
        Assert.Contains("Report B</span>", qtyArticle, StringComparison.Ordinal);

        // The caveats that explained a shared usage result are gone: there is no shared result.
        Assert.DoesNotContain("also defines a report measure named", html, StringComparison.Ordinal);
        Assert.DoesNotContain("This card shows only this report's own", html, StringComparison.Ordinal);
        Assert.DoesNotContain("usage result is shared", html, StringComparison.Ordinal);
        Assert.DoesNotContain("count them as one item", html, StringComparison.Ordinal);
        Assert.DoesNotContain("same-named report measures as one item", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Report A: RM = [BaseA] over Amount. Report B: RM = [BaseB] over Qty. Both reports are bound to one
    /// model and each RM is its own report's node, so whichever is placed, each report's RM, base measure
    /// and column are classified, explained and walked by that report alone.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void SameNamedReportMeasuresNeverLeakAcrossReports(bool placedInA, bool placedInB)
    {
        var inventory = ScanSharedReportMeasures(placedInA, placedInB);
        var lineage = SemanticLineageProjection.Build(inventory);
        var cards = new Dictionary<string, LineageCard>
        {
            ["A"] = lineage.CardForReportMeasure("A", "Sales", "RM")!,
            ["B"] = lineage.CardForReportMeasure("B", "Sales", "RM")!,
        };
        var placed = new Dictionary<string, bool> { ["A"] = placedInA, ["B"] = placedInB };
        var bases = new Dictionary<string, (string Measure, string Column)> { ["A"] = ("BaseA", "Amount"), ["B"] = ("BaseB", "Qty") };

        Assert.NotEqual(cards["A"].Id, cards["B"].Id);
        foreach (var report in new[] { "A", "B" })
        {
            var card = cards[report];

            // The card is this report's own: its dependencies, locations and reachability.
            Assert.Equal([$"Sales[{bases[report].Measure}]"], card.DependsOn.Items.Select(item => item.Name).ToArray());
            Assert.Equal(placed[report], card.ReachedFromReport);
            Assert.Equal(placed[report] ? 1 : 0, card.UsedInReport.TotalCount);
            Assert.All(card.UsedInReport.Items, location => Assert.Equal(report, location.Location.Report));
            Assert.Equal(placed[report] ? LineagePathStatus.DirectlyUsed : LineagePathStatus.NotFound, card.Path!.Status);

            foreach (var name in new[] { bases[report].Measure, bases[report].Column })
            {
                var item = Card(lineage, inventory, "Sales", name);
                var path = item.Path!;
                if (placed[report])
                {
                    // Reached through this report's RM, ending at this report's visual and counting only it.
                    Assert.Equal(SemanticUsageStates.IndirectlyUsed, item.Usage!.UsageState);
                    Assert.Equal(LineagePathStatus.ReachedThroughModel, path.Status);
                    var step = Assert.Single(path.Steps, step => step.ObjectType == SemanticObjectTypes.ReportMeasure);
                    Assert.Equal((card.Id, report), (step.CardId, step.Report));
                    Assert.Equal(report, path.Endpoint!.Location.Report);
                    Assert.Equal(1, path.EndpointLocationCount);
                }
                else
                {
                    // Never reached through the other report's RM, placed or not: the genuine consumer is
                    // this report's unplaced RM, where the unused branch starts.
                    Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, item.Usage!.UsageState);
                    Assert.Equal(LineagePathStatus.NotFound, path.Status);
                    Assert.Null(path.Endpoint);
                    var head = Assert.Single(path.OnlyReachedFrom.Items);
                    Assert.Equal((card.Id, report), (head.CardId, head.Report));
                }
            }

            // The base measure's consumer and its reason are this report's RM, linked to this report's card.
            var baseCard = Card(lineage, inventory, "Sales", bases[report].Measure);
            var consumer = Assert.Single(baseCard.UsedBy.Items);
            Assert.Equal((card.Id, report, placed[report]), (consumer.CardId, consumer.Report, consumer.ReachableFromReport));
            Assert.True(consumer.IsReasonSource);
            Assert.Equal(placed[report] ? "Referenced by Sales[RM]" : "Referenced only by unused object Sales[RM]", baseCard.Reason);
        }

        // No visual card lists the other report's RM.
        foreach (var visual in lineage.Cards.Where(card => card.Kind == LineageFocusKind.Visual))
        {
            Assert.All(visual.Uses.Items, use => Assert.Equal(visual.Report!.Name == "A" ? cards["A"].Id : cards["B"].Id, use.Object.CardId));
        }

        var html = HtmlReportRenderer.Render(inventory);
        var ids = System.Text.RegularExpressions.Regex.Matches(html, "\\sid=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- H / I / J / K. No path --------------------------------------------------------------------

    [Fact]
    public void AnObjectOnlyUsedByUnusedItemsNamesWhereItsBranchStarts()
    {
        var inventory = ScanFixture("desktop-field-parameter-evidence");
        var lineage = SemanticLineageProjection.Build(inventory);
        var cost = Card(lineage, inventory, "Sales", "Cost");

        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, cost.Usage!.UsageState);
        Assert.Equal(LineagePathStatus.NotFound, cost.Path!.Status);
        var head = Assert.Single(cost.Path.OnlyReachedFrom.Items);
        Assert.Equal(("Sales[Unused Control]", SemanticUsageStates.ApparentlyUnused), (head.Name, head.UsageState));
        var text = System.Net.WebUtility.HtmlDecode(PathSection(Article(inventory, cost)));
        Assert.Contains("No path to the report found in this project.", text, StringComparison.Ordinal);
        Assert.Contains("Only used through: ", text, StringComparison.Ordinal);
        Assert.Contains(">Sales[Unused Control]</a> (Apparently unused)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnApparentlyUnusedObjectHasNoPathAndNothingInvented()
    {
        var inventory = ScanFixture("desktop-field-parameter-evidence");
        var lineage = SemanticLineageProjection.Build(inventory);
        var notes = Card(lineage, inventory, "Sales", "Notes");

        Assert.Equal(LineagePathStatus.NotFound, notes.Path!.Status);
        Assert.Equal(0, notes.Path.OnlyReachedFrom.TotalCount);
        Assert.False(notes.Path.RequiredByModelStructure);
        Assert.False(notes.Path.ChecksLimited);
        Assert.Equal(
            "<section class=\"lineage-path\" data-lineage-path=\"none\"><h3>Path to report</h3><p class=\"lineage-path-none\">No path to the report found in this project.</p></section>",
            PathSection(Article(inventory, notes)));
    }

    [Fact]
    public void AStructurallyRequiredObjectShowsItsStructureAndNoPath()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var key = Card(lineage, inventory, "Fact", "RelationshipKey").Path!;

        Assert.Equal(LineagePathStatus.NotFound, key.Status);
        Assert.True(key.RequiredByModelStructure);
        Assert.Equal(2, key.StructuralSources.TotalCount);
        Assert.Contains("Required by the model structure: ",
            PathSection(Article(inventory, Card(lineage, inventory, "Fact", "RelationshipKey"))), StringComparison.Ordinal);

        // A pinned structural object with no structural edge or reason still says what it is.
        var calculationGroup = ScanFixture("desktop-calculation-group-selection-evidence");
        var defaultRate = Card(SemanticLineageProjection.Build(calculationGroup), calculationGroup, "Rates", "DefaultRate");
        Assert.Contains("<p class=\"lineage-path-note\">Required by the model structure.</p>", PathSection(Article(calculationGroup, defaultRate)), StringComparison.Ordinal);
    }

    [Fact]
    public void AQualifiedObjectWithoutAPathSaysChecksAreLimited()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var qualified = inventory.SemanticObjectUsages.First(usage =>
            usage.UsageState == SemanticUsageStates.ApparentlyUnused &&
            usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);
        var card = lineage.CardFor(qualified)!;

        Assert.True(card.Path!.ChecksLimited);
        var section = PathSection(Article(inventory, card));
        Assert.Contains("No path to the report found in this project.", section, StringComparison.Ordinal);
        Assert.Contains("<span class=\"confidence-flag\">Checks limited", section, StringComparison.Ordinal);
    }

    [Fact]
    public void UnresolvedReferencesNeverBecomePathSteps()
    {
        var inventory = ScanProject(
            [
                ("T1", "table T1\n" + Column("Amount") + "\tmeasure Probe = SUMX(T2, [Amount])\n"),
                ("T2", "table T2\n" + Column("Amount")),
            ],
            [Page("p1", "Page 1", 0, Visual("v1", 0, 0, ("Measure", "T1", "Probe")))]);
        var lineage = SemanticLineageProjection.Build(inventory);

        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, Assert.Single(inventory.UnresolvedSemanticDependencies).ResolutionOutcome);
        foreach (var table in new[] { "T1", "T2" })
        {
            var candidate = Card(lineage, inventory, table, "Amount").Path!;
            Assert.Equal(LineagePathStatus.NotFound, candidate.Status);
            Assert.Equal(0, candidate.OnlyReachedFrom.TotalCount);
        }
    }

    // ---- L. Cycles ----------------------------------------------------------------------------------

    [Fact]
    public void CyclesTerminateWithAndWithoutAReportPath()
    {
        var inventory = ScanProject(
            [
                ("M", "table M\n" + Column("Dummy") +
                    "\tmeasure A = [B] + 1\n\tmeasure B = [A] + 1\n\tmeasure C = [A]\n" +
                    "\tmeasure X = [Y] + 1\n\tmeasure Y = [X] + 1\n"),
            ],
            [Page("p1", "Page 1", 0, Visual("v1", 0, 0, ("Measure", "M", "C")))]);
        var lineage = SemanticLineageProjection.Build(inventory);

        Assert.Equal(["M[B]", "M[A]", "M[C]"], Card(lineage, inventory, "M", "B").Path!.Steps.Select(step => step.Name).ToArray());
        var x = Card(lineage, inventory, "M", "X").Path!;
        Assert.Equal(LineagePathStatus.NotFound, x.Status);
        // A cycle has no start, so the immediate referrer is named instead.
        Assert.Equal(["M[Y]"], x.OnlyReachedFrom.Items.Select(item => item.Name).ToArray());
    }

    // ---- Alternatives and copy ----------------------------------------------------------------------

    [Fact]
    public void OtherItemsCountTheFocusesOtherReportReachingConsumersOnly()
    {
        var inventory = ScanFixture("pbi-assure-coverage");
        var lineage = SemanticLineageProjection.Build(inventory);
        var baseAmount = Card(lineage, inventory, "Fact", "BaseAmount");

        var reachingConsumers = baseAmount.UsedBy.Items.Count(item => item.ReachableFromReport == true);
        Assert.Equal(reachingConsumers - 1, baseAmount.Path!.OtherReportReachingConsumers);
        // The count is of other consumers, not of routes, and the copy says so.
        var article = Article(inventory, baseAmount);
        Assert.Contains($"{reachingConsumers - 1} other items that use this are also reached from a report.", article, StringComparison.Ordinal);
        Assert.DoesNotContain("Also reached through", article, StringComparison.Ordinal);
    }

    [Fact]
    public void PathCopyNeverCallsOnePathThePathOrClaimsCompleteness()
    {
        foreach (var fixture in new[] { "pbi-assure-coverage", "desktop-field-parameter-evidence", "desktop-calculation-group-selection-evidence" })
        {
            var html = ReportHtml.LineageSection(HtmlReportRenderer.Render(ScanFixture(fixture)));
            var text = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " "));
            foreach (var phrase in new[] { "the path", "only path", "all paths", "every path", "complete", "safe to delete", "definitely unused" })
            {
                Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private static LineageCard Card(SemanticLineageProjection lineage, ProjectInventory inventory, string table, string objectName) =>
        lineage.CardFor(Assert.Single(inventory.SemanticObjectUsages, usage =>
            usage.Table == table && usage.ObjectName == objectName && usage.HierarchyName is null))!;

    private static string Article(ProjectInventory inventory, LineageCard card) =>
        Article(HtmlReportRenderer.Render(inventory), card.Id);

    private static string Article(string html, string id)
    {
        var start = html.IndexOf($"<article id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected lineage article '{id}'.");
        return html[start..html.IndexOf("</article>", start, StringComparison.Ordinal)];
    }

    private static string PathSection(string article)
    {
        var start = article.IndexOf("<section class=\"lineage-path\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected a path section.");
        var end = article.IndexOf("</section>", start, StringComparison.Ordinal) + "</section>".Length;
        return article[start..end];
    }

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";

    private static ProjectInventory ScanSharedReportMeasures(bool placedInA, bool placedInB)
    {
        const string extension = "{\"$schema\":\"https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json\"," +
            "\"name\":\"extension\",\"entities\":[{\"name\":\"Sales\",\"measures\":[{\"name\":\"RM\",\"dataType\":\"Decimal\"," +
            "\"expression\":\"[BASE]\",\"references\":{\"unrecognizedReferences\":false,\"measures\":[]}}]}]}";
        var files = new List<ProjectFileContent>
        {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl",
                "table Sales\n" + Column("Amount") + Column("Qty") +
                "\tmeasure BaseA = SUM(Sales[Amount])\n\tmeasure BaseB = SUM(Sales[Qty])\n"),
        };
        foreach (var (report, baseMeasure, placed) in new[] { ("A", "BaseA", placedInA), ("B", "BaseB", placedInB) })
        {
            files.Add(File($"{report}.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"));
            files.Add(File($"{report}.Report/definition/reportExtensions.json", extension.Replace("BASE", baseMeasure, StringComparison.Ordinal)));
            files.Add(File($"{report}.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File($"{report}.Report/definition/pages/p1/page.json", "{\"name\":\"p1\",\"displayName\":\"Overview\"}"));
            if (placed)
            {
                files.Add(File($"{report}.Report/definition/pages/p1/visuals/v1/visual.json", Visual("v1", 0, 0, ("Measure", "Sales", "RM")).Json));
            }
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Shared report measures", files));
    }

    private static string FieldParameterTable(string table, string measure) =>
        $"table {table}\n" +
        $"\tcolumn {table}\n\t\tsummarizeBy: none\n\t\tsourceColumn: [Value1]\n\t\tsortByColumn: '{table} Order'\n\n" +
        $"\t\trelatedColumnDetails\n\t\t\tgroupByColumn: '{table} Fields'\n\n" +
        $"\tcolumn '{table} Fields'\n\t\tisHidden\n\t\tsummarizeBy: none\n\t\tsourceColumn: [Value2]\n\t\tsortByColumn: '{table} Order'\n\n" +
        "\t\textendedProperty ParameterMetadata =\n\t\t\t\t{\n\t\t\t\t  \"version\": 3,\n\t\t\t\t  \"kind\": 2\n\t\t\t\t}\n\n" +
        $"\tcolumn '{table} Order'\n\t\tisHidden\n\t\tsummarizeBy: sum\n\t\tsourceColumn: [Value3]\n\n" +
        $"\tpartition {table} = calculated\n\t\tmode: import\n\t\tsource =\n\t\t\t\t{{\n\t\t\t\t    (\"{measure}\", NAMEOF('Sales'[{measure}]), 0)\n\t\t\t\t}}\n";

    private static (string Name, string Json) Visual(string name, double x, double y, params (string Kind, string Table, string Property)[] fields)
    {
        var projections = string.Join(',', fields.Select((field, index) =>
            $"{{\"field\":{{\"{field.Kind}\":{{\"Expression\":{{\"SourceRef\":{{\"Entity\":\"{field.Table}\"}}}},\"Property\":\"{field.Property}\"}}}},\"queryRef\":\"q{index}\"}}"));
        return (name, $"{{\"name\":\"{name}\",\"position\":{{\"x\":{x},\"y\":{y},\"z\":0,\"width\":100,\"height\":100}}," +
            $"\"visual\":{{\"visualType\":\"card\",\"query\":{{\"queryState\":{{\"Values\":{{\"projections\":[{projections}]}}}}}}}}}}");
    }

    private static (string Name, string DisplayName, int Order, (string Name, string Json)[] Visuals) Page(
        string name, string displayName, int order, params (string Name, string Json)[] visuals) =>
        (name, displayName, order, visuals);

    private static ProjectInventory ScanProject(
        IReadOnlyList<(string Table, string Definition)> tables,
        IReadOnlyList<(string Name, string DisplayName, int Order, (string Name, string Json)[] Visuals)> pages)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/pages.json",
                $"{{\"pageOrder\":[{string.Join(',', pages.OrderBy(page => page.Order).Select(page => $"\"{page.Name}\""))}]}}"),
        };
        files.AddRange(tables.Select(table => File($"Model.SemanticModel/definition/tables/{table.Table}.tmdl", table.Definition)));
        foreach (var page in pages)
        {
            files.Add(File($"Model.Report/definition/pages/{page.Name}/page.json", $"{{\"name\":\"{page.Name}\",\"displayName\":\"{page.DisplayName}\"}}"));
            files.AddRange(page.Visuals.Select(visual => File($"Model.Report/definition/pages/{page.Name}/visuals/{visual.Name}/visual.json", visual.Json)));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Paths", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));

    private static ProjectInventory ScanFixture(string fixture) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", fixture));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
