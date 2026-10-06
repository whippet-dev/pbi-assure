using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A report measure is its own report's graph node: model, owning report path and field identity. The
/// scanner keeps two views of reachability. The report-scoped view has a row for every node and is the
/// source of truth. The published schema-0.26 rows are its lossy projection: unchanged in shape and in
/// which rows exist, with same-named report measures of different reports sharing one row.
/// </summary>
public sealed class ReportScopedReachabilityTests
{
    private const string ReportMeasure = SemanticObjectTypes.ReportMeasure;

    // ---- Serialization A, B, C: same-named report measures over BaseA and BaseB --------------------

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void EachReportsMeasureIsItsOwnNodeAndThePublishedRowCombinesThem(bool placedInA, bool placedInB)
    {
        var inventory = Scan(
            Report("A", [Measure("RM", "[BaseA]")], placedInA ? ["RM"] : []),
            Report("B", [Measure("RM", "[BaseB]")], placedInB ? ["RM"] : []));

        // Report-scoped: two nodes, each reached exactly when its own report places it.
        var scoped = inventory.ReportScopedNodeReachability
            .Where(node => node.ObjectType == ReportMeasure)
            .ToDictionary(node => node.Report!, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["A.Report", "B.Report"], scoped.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(placedInA, scoped["A.Report"].ReachableFromReport);
        Assert.Equal(placedInB, scoped["B.Report"].ReachableFromReport);
        Assert.All(scoped.Values, node => Assert.False(node.ReachableFromModelStructure));

        // Published: both have edges, so the name had a row before and has exactly one now, reached
        // when either report's measure is. Ownership is not in it.
        var published = Assert.Single(inventory.SemanticNodeReachability, node => node.ObjectType == ReportMeasure);
        Assert.Equal(("Model", "Sales", "RM", (string?)null), (published.SemanticModel, published.Table, published.ObjectName, published.HierarchyName));
        Assert.Equal(placedInA || placedInB, published.ReachableFromReport);
        Assert.False(published.ReachableFromModelStructure);
        Assert.Null(published.Report);
        AssertPublishedRowsAreTheProjection(inventory);

        // Each base measure and column is classified by its own report's measure alone. An unplaced
        // report measure is still their consumer, so they are in an unused branch, not apparently unused.
        var reached = SemanticUsageStates.IndirectlyUsed;
        var branch = SemanticUsageStates.UsedOnlyByUnusedBranch;
        Assert.Equal(placedInA ? reached : branch, State(inventory, "BaseA"));
        Assert.Equal(placedInA ? reached : branch, State(inventory, "Amount"));
        Assert.Equal(placedInB ? reached : branch, State(inventory, "BaseB"));
        Assert.Equal(placedInB ? reached : branch, State(inventory, "Qty"));
    }

    // ---- D. A report measure with no edges ----------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AReportMeasureWithoutEdgesHasAScopedRowAndNoPublishedRow(bool placed)
    {
        var inventory = Scan(
            Report("A", [Measure("Const", "1")], placed ? ["Const"] : []),
            Report("B", [Measure("Const", "2")], []));

        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.FromObjectName == "Const" || edge.ToObjectName == "Const");
        var scoped = inventory.ReportScopedNodeReachability.Where(node => node.ObjectType == ReportMeasure).ToArray();
        Assert.Equal(
            [("A.Report", placed), ("B.Report", false)],
            scoped.Select(node => (node.Report!, node.ReachableFromReport)).ToArray());

        // Before report measures were scoped, a report measure without edges had no published row, used
        // or not. It still has none.
        Assert.DoesNotContain(inventory.SemanticNodeReachability, node => node.ObjectType == ReportMeasure);
        AssertPublishedRowsAreTheProjection(inventory);
    }

    // ---- Before/after compatibility -----------------------------------------------------------------

    /// <summary>
    /// An unused report measure, a used one, edge-free constants (one used, one not) and a name two
    /// reports share. The expected rows are exactly what the scanner published before report measures
    /// were scoped (captured from commit cbbec08); none of these cases changes.
    /// </summary>
    [Fact]
    public void PublishedRowsAreThoseThatWerePublishedBeforeReportMeasuresWereScoped()
    {
        var inventory = Scan(
            Report("A", [Measure("Used", "[BaseA]"), Measure("Unused", "[BaseB]"), Measure("Const", "1"), Measure("ConstUnused", "2"), Measure("Same", "[BaseA]")],
                ["Used", "Const"]),
            Report("B", [Measure("Same", "[BaseB]"), Measure("Const", "3")], ["Same"]));

        Assert.Equal(
            [
                ("Amount", "Column", true, false),
                ("BaseA", "Measure", true, false),
                ("BaseB", "Measure", true, false),
                ("Qty", "Column", true, false),
                ("Sales", "Table", true, false),
                ("Same", ReportMeasure, true, false),
                ("Unused", ReportMeasure, false, false),
                ("Used", ReportMeasure, true, false),
            ],
            inventory.SemanticNodeReachability
                .Select(node => (node.ObjectName, node.ObjectType, node.ReachableFromReport, node.ReachableFromModelStructure))
                .ToArray());

        // The report-scoped view holds every report measure, with its owner.
        Assert.Equal(
            [
                ("A.Report", "Const", true), ("B.Report", "Const", false), ("A.Report", "ConstUnused", false),
                ("A.Report", "Same", false), ("B.Report", "Same", true), ("A.Report", "Unused", false), ("A.Report", "Used", true),
            ],
            inventory.ReportScopedNodeReachability
                .Where(node => node.ObjectType == ReportMeasure)
                .Select(node => (node.Report!, node.ObjectName, node.ReachableFromReport))
                .ToArray());
        AssertPublishedRowsAreTheProjection(inventory);
    }

    /// <summary>
    /// E. Only A's report measure is placed. Before, the merged node made B's base measure and column
    /// look reached; that was the defect. They are the only published change: every other row is what was
    /// published before (captured from commit cbbec08).
    /// </summary>
    [Fact]
    public void TheOnlyPublishedChangeIsTheCorrectedClassification()
    {
        var inventory = Scan(Report("A", [Measure("RM", "[BaseA]")], ["RM"]), Report("B", [Measure("RM", "[BaseB]")], []));
        var before = new[]
        {
            ("Amount", "Column", true), ("BaseA", "Measure", true), ("BaseB", "Measure", true),
            ("Qty", "Column", true), ("RM", ReportMeasure, true), ("Sales", "Table", true),
        };
        var corrected = new HashSet<string> { "BaseB", "Qty" };

        Assert.Equal(
            before.Select(row => (row.Item1, row.Item2, corrected.Contains(row.Item1) ? false : row.Item3)).ToArray(),
            inventory.SemanticNodeReachability.Select(node => (node.ObjectName, node.ObjectType, node.ReachableFromReport)).ToArray());
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(inventory, "BaseB"));
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(inventory, "Qty"));
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Assert.Single(inventory.SemanticTableUsages).UsageState);
    }

    // ---- Nested report measures ----------------------------------------------------------------------

    /// <summary>
    /// A: RM1 = [BaseA], RM2 = [RM1], RM2 placed. B defines the same names over BaseB, plus RM3 = [RM1]
    /// without declaring the reference. Declared references resolve within their own report only, and a
    /// bare name that is not declared is not resolved at all.
    /// </summary>
    [Fact]
    public void NestedReportMeasuresResolveOnlyWithinTheirOwnReport()
    {
        var inventory = Scan(
            Report("A", [Measure("RM1", "[BaseA]"), Measure("RM2", "[RM1]", "RM1")], ["RM2"]),
            Report("B", [Measure("RM1", "[BaseB]"), Measure("RM2", "[RM1]", "RM1"), Measure("RM3", "[RM1]")], []));

        var nested = inventory.SemanticDependencies
            .Where(edge => edge.FromObjectType == ReportMeasure && edge.ToObjectType == ReportMeasure)
            .Select(edge => (edge.FromReport, edge.FromObjectName, edge.ToReport, edge.ToObjectName))
            .Distinct()
            .ToArray();
        Assert.Equal([("A.Report", "RM2", "A.Report", "RM1"), ("B.Report", "RM2", "B.Report", "RM1")], nested);
        Assert.All(inventory.SemanticDependencies.Where(edge => edge.FromObjectType == ReportMeasure), edge =>
            Assert.StartsWith(edge.FromReport + "/", edge.EvidencePath, StringComparison.Ordinal));
        var undeclared = Assert.Single(inventory.UnresolvedSemanticDependencies, item => item.FromObjectName == "RM3");
        Assert.Equal(("B.Report", "[RM1]"), (undeclared.FromReport, undeclared.ReferenceText));
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.FromObjectName == "RM3");

        Assert.Equal(
            [("A.Report", "RM1", true), ("A.Report", "RM2", true), ("B.Report", "RM1", false), ("B.Report", "RM2", false), ("B.Report", "RM3", false)],
            inventory.ReportScopedNodeReachability
                .Where(node => node.ObjectType == ReportMeasure)
                .Select(node => (node.Report!, node.ObjectName, node.ReachableFromReport))
                .OrderBy(row => row.Item1, StringComparer.Ordinal).ThenBy(row => row.Item2, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(inventory, "BaseA"));
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(inventory, "BaseB"));
        AssertPublishedRowsAreTheProjection(inventory);

        // Lineage walks A.RM2 → A.RM1 → BaseA → Amount within report A, and B's chain within report B.
        var lineage = SemanticLineageProjection.Build(inventory);
        var a1 = lineage.CardForReportMeasure("A", "Sales", "RM1")!;
        var a2 = lineage.CardForReportMeasure("A", "Sales", "RM2")!;
        var b1 = lineage.CardForReportMeasure("B", "Sales", "RM1")!;
        var b2 = lineage.CardForReportMeasure("B", "Sales", "RM2")!;
        var amount = Card(lineage, inventory, "Amount").Path!;
        Assert.Equal(LineagePathStatus.ReachedThroughModel, amount.Status);
        Assert.Equal(
            [("Sales[Amount]", (string?)null, (string?)null), ("Sales[BaseA]", null, null), ("Sales[RM1]", a1.Id, "A"), ("Sales[RM2]", a2.Id, "A")],
            amount.Steps.Select(step => (step.Name, step.ObjectType == ReportMeasure ? step.CardId : null, step.Report)).ToArray());
        Assert.Equal("A", amount.Endpoint!.Location.Report);

        Assert.Equal(LineagePathStatus.ReachedThroughModel, a1.Path!.Status);
        Assert.Equal((a2.Id, "A", true), Single(a1.UsedBy));
        Assert.Equal((b2.Id, "B", false), Single(b1.UsedBy));
        Assert.Equal(LineagePathStatus.NotFound, b1.Path!.Status);
        var head = Assert.Single(b1.Path.OnlyReachedFrom.Items);
        Assert.Equal((b2.Id, "B"), (head.CardId, head.Report));
        Assert.Equal(["[RM1]"], lineage.CardForReportMeasure("B", "Sales", "RM3")!.NotResolved.Items.Select(item => item.Dependency.ReferenceText).ToArray());
        Assert.Empty(b1.NotResolved.Items);
    }

    // ---- One target used by two reports' report measures ----------------------------------------------

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public void AUsageReasonIsNeverAnotherReportsMeasure(string placedReport)
    {
        var inventory = Scan(
            Report("A", [Measure("RM", "[BaseA]")], placedReport == "A" ? ["RM"] : []),
            Report("B", [Measure("RM", "[BaseA]")], placedReport == "B" ? ["RM"] : []));
        var otherReport = placedReport == "A" ? "B" : "A";

        var baseA = Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "BaseA");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, baseA.UsageState);
        var reason = SemanticUsagePresentation.ExplainReason(inventory, baseA)!;
        Assert.Equal("Referenced by Sales[RM]", reason.Text);
        Assert.Equal($"{placedReport}.Report", reason.Dependency!.FromReport);
        Assert.StartsWith($"{placedReport}.Report/", reason.Dependency.EvidencePath, StringComparison.Ordinal);

        // Lineage lists both reports' measures as consumers, each with its own reachability, and the
        // path runs through the placed report's measure to that report's visual.
        var lineage = SemanticLineageProjection.Build(inventory);
        var placed = lineage.CardForReportMeasure(placedReport, "Sales", "RM")!;
        var unplaced = lineage.CardForReportMeasure(otherReport, "Sales", "RM")!;
        var card = Card(lineage, inventory, "BaseA");
        Assert.Equal(
            [(placed.Id, placedReport, true, true), (unplaced.Id, otherReport, false, false)],
            card.UsedBy.Items.Select(item => (item.CardId, item.Report!, item.ReachableFromReport == true, item.IsReasonSource)).ToArray());
        var step = Assert.Single(card.Path!.Steps, item => item.ObjectType == ReportMeasure);
        Assert.Equal((placed.Id, placedReport), (step.CardId, step.Report));
        Assert.Equal(placedReport, card.Path.Endpoint!.Location.Report);
        Assert.Equal(0, card.Path.OtherReportReachingConsumers);
    }

    /// <summary>
    /// The scanner always records which report owns a report-measure edge. Where an inventory's owner is
    /// missing, or contradicts the edge's own evidence path, no report's measure is credited with the
    /// edge: lineage neither links it, nor claims its reachability, nor walks a path through it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LineageFailsClosedWhenOwnershipIsMissingOrContradictsItsEvidence(bool contradicts)
    {
        var scanned = Scan(Report("A", [Measure("RM", "[BaseA]")], ["RM"]), Report("B", [Measure("RM", "[BaseB]")], []));
        var inventory = scanned with
        {
            SemanticDependencies = scanned.SemanticDependencies
                .Select(edge => edge.FromObjectType != ReportMeasure
                    ? edge
                    : contradicts
                        ? edge with { EvidencePath = edge.EvidencePath.Replace(edge.FromReport!, edge.FromReport == "A.Report" ? "B.Report" : "A.Report", StringComparison.Ordinal) }
                        : edge with { FromReport = null })
                .ToArray(),
        };
        var lineage = SemanticLineageProjection.Build(inventory);

        var baseA = Card(lineage, inventory, "BaseA");
        var consumer = Assert.Single(baseA.UsedBy.Items);
        Assert.Equal("Sales[RM]", consumer.Name);
        Assert.Equal(((string?)null, (string?)null, (bool?)null), (consumer.CardId, consumer.Report, consumer.ReachableFromReport));
        Assert.Equal(LineagePathStatus.NotFound, baseA.Path!.Status);
        Assert.Empty(baseA.Path.OnlyReachedFrom.Items);
        Assert.Equal(LineagePathStatus.NotFound, Card(lineage, inventory, "Amount").Path!.Status);
        Assert.Null(SemanticUsagePresentation.ExplainReason(inventory, Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "BaseA")));
    }

    // ---- Different semantic models -------------------------------------------------------------------

    [Fact]
    public void SameNamedReportMeasuresOnDifferentModelsAreSeparateNodes()
    {
        var inventory = Scan(
            [Report("A", [Measure("RM", "[BaseA]")], ["RM"], model: "M1"), Report("B", [Measure("RM", "[BaseA]")], [], model: "M2")],
            models: ["M1", "M2"]);

        Assert.Equal(
            [("M1", "A.Report", true), ("M2", "B.Report", false)],
            inventory.ReportScopedNodeReachability
                .Where(node => node.ObjectType == ReportMeasure)
                .Select(node => (node.SemanticModel, node.Report!, node.ReachableFromReport))
                .ToArray());
        Assert.Equal(
            [("M1", true), ("M2", false)],
            inventory.SemanticNodeReachability
                .Where(node => node.ObjectType == ReportMeasure)
                .Select(node => (node.SemanticModel, node.ReachableFromReport))
                .ToArray());
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, State(inventory, "BaseA", "M1"));
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, State(inventory, "BaseA", "M2"));
        Assert.All(inventory.SemanticDependencies.Where(edge => edge.FromObjectType == ReportMeasure), edge =>
            Assert.Equal(edge.FromReport == "A.Report" ? "M1" : "M2", edge.SemanticModel));
        AssertPublishedRowsAreTheProjection(inventory);
    }

    // ---- Exports and the JSON contract ----------------------------------------------------------------

    [Fact]
    public void ExportsShowEachReportsOwnResult()
    {
        var inventory = Scan(Report("A", [Measure("RM", "[BaseA]")], ["RM"]), Report("B", [Measure("RM", "[BaseB]")], []));

        var rows = ReadCsv(SemanticUsageCsvRenderer.Render(inventory));
        var header = rows[0];
        string Cell(string objectName, string column) =>
            Assert.Single(rows.Skip(1), row => row[Array.IndexOf(header, "Object")] == objectName)[Array.IndexOf(header, column)];
        Assert.Equal("Indirectly used", Cell("BaseA", "SemanticUsage"));
        Assert.Equal("Indirectly used", Cell("Amount", "SemanticUsage"));
        Assert.Equal("Used only by unused branch", Cell("BaseB", "SemanticUsage"));
        Assert.Equal("Used only by unused branch", Cell("Qty", "SemanticUsage"));
        Assert.Equal("Referenced only by unused object Sales[RM]", Cell("BaseB", "SemanticReason"));
        Assert.Equal("Yes", Cell("BaseB", "ReviewCandidate"));
        Assert.Equal("No", Cell("BaseA", "ReviewCandidate"));

        // Apparently unused lists only ApparentlyUnused objects: BaseB and Qty still have a consumer.
        Assert.DoesNotContain(inventory.SemanticObjectUsages, usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        Assert.Contains(ApparentlyUnusedReportRenderer.ZeroStateHeading, ApparentlyUnusedReportRenderer.Render(inventory), StringComparison.Ordinal);
    }

    [Fact]
    public void TheJsonContractIsUnchanged()
    {
        var inventory = Scan(
            Report("A", [Measure("RM1", "[BaseA]"), Measure("RM2", "[RM1]", "RM1")], ["RM2"]),
            Report("B", [Measure("RM1", "[BaseB]"), Measure("RM3", "[RM1]")], []));
        Assert.NotEmpty(inventory.ReportScopedNodeReachability);
        Assert.NotEmpty(inventory.UnresolvedSemanticDependencies);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(inventory));
        var root = json.RootElement;
        Assert.Equal("0.26", root.GetProperty("SchemaVersion").GetString());
        Assert.False(root.TryGetProperty("ReportScopedNodeReachability", out _));
        Assert.False(root.TryGetProperty("ReportMeasureUsages", out _));
        Assert.Equal(
            ["SemanticModel", "Table", "ObjectName", "ObjectType", "HierarchyName", "ReachableFromReport", "ReachableFromModelStructure"],
            PropertyNames(root.GetProperty("SemanticNodeReachability")));
        Assert.Equal(
            ["SemanticModel", "FromTable", "FromObjectName", "FromObjectType", "FromHierarchyName", "ToTable", "ToObjectName",
                "ToObjectType", "ToHierarchyName", "DependencyKind", "EvidencePath", "EvidenceText"],
            PropertyNames(root.GetProperty("SemanticDependencies")));
        Assert.Equal(
            ["SemanticModel", "FromTable", "FromObjectName", "FromObjectType", "FromHierarchyName", "DependencyKind", "ReferenceText",
                "Reason", "EvidencePath", "ResolutionOutcome"],
            PropertyNames(root.GetProperty("UnresolvedSemanticDependencies")));
    }

    // ---- Helpers --------------------------------------------------------------------------------------

    /// <summary>
    /// The published rows are keyed by their visible identity without duplicates, an ordinary row is its
    /// report-scoped row, and a report-measure row combines every report's measure of that name.
    /// </summary>
    private static void AssertPublishedRowsAreTheProjection(ProjectInventory inventory)
    {
        var published = inventory.SemanticNodeReachability.ToDictionary(
            node => SemanticGraphIndex.NodeKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName),
            StringComparer.OrdinalIgnoreCase);
        var owners = inventory.ReportScopedNodeReachability.ToLookup(
            node => SemanticGraphIndex.NodeKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            inventory.ReportScopedNodeReachability.Count,
            inventory.ReportScopedNodeReachability
                .Select(node => SemanticGraphIndex.NodeKey(node.SemanticModel, node.Table, node.ObjectName, node.ObjectType, node.HierarchyName, node.Report))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        foreach (var (key, row) in published)
        {
            var nodes = owners[key].ToArray();
            Assert.NotEmpty(nodes);
            Assert.Null(row.Report);
            Assert.Equal(nodes.Any(node => node.ReachableFromReport), row.ReachableFromReport);
            Assert.Equal(nodes.Any(node => node.ReachableFromModelStructure), row.ReachableFromModelStructure);
            if (row.ObjectType != ReportMeasure)
            {
                Assert.Equal(row, Assert.Single(nodes));
            }
        }

        Assert.All(inventory.ReportScopedNodeReachability, node =>
            Assert.Equal(node.ObjectType == ReportMeasure, node.Report is not null));
    }

    private static (string? CardId, string? Report, bool? Reached) Single(LineageGroup<LineageNeighbour> group)
    {
        var item = Assert.Single(group.Items);
        return (item.CardId, item.Report, item.ReachableFromReport);
    }

    private static string[] PropertyNames(JsonElement array) =>
        array.EnumerateArray().First().EnumerateObject().Select(property => property.Name).ToArray();

    private static string State(ProjectInventory inventory, string objectName, string model = "Model") =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.SemanticModel == model && usage.ObjectName == objectName).UsageState;

    private static LineageCard Card(SemanticLineageProjection lineage, ProjectInventory inventory, string objectName) =>
        lineage.CardFor(Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == objectName))!;

    private static string Measure(string name, string expression, params string[] declaredReportMeasures) =>
        "{\"name\":\"" + name + "\",\"dataType\":\"Decimal\",\"expression\":\"" + expression + "\"," +
        "\"references\":{\"unrecognizedReferences\":false,\"measures\":[" +
        string.Join(',', declaredReportMeasures.Select(reference => "{\"schema\":\"extension\",\"entity\":\"Sales\",\"name\":\"" + reference + "\"}")) +
        "]}}";

    private static (string Name, string Model, string[] Measures, string[] Placed) Report(
        string name, string[] measures, string[] placed, string model = "Model") =>
        (name, model, measures, placed);

    private static ProjectInventory Scan(params (string Name, string Model, string[] Measures, string[] Placed)[] reports) =>
        Scan(reports, models: ["Model"]);

    private static ProjectInventory Scan(
        IReadOnlyList<(string Name, string Model, string[] Measures, string[] Placed)> reports,
        IReadOnlyList<string> models)
    {
        var files = new List<ProjectFileContent>();
        foreach (var model in models)
        {
            files.Add(File($"{model}.SemanticModel/definition.pbism", "{}"));
            files.Add(File($"{model}.SemanticModel/definition/tables/Sales.tmdl",
                "table Sales\n" + Column("Amount") + Column("Qty") +
                "\tmeasure BaseA = SUM(Sales[Amount])\n\tmeasure BaseB = SUM(Sales[Qty])\n"));
        }

        foreach (var (name, model, measures, placed) in reports)
        {
            files.Add(File($"{name}.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../" + model + ".SemanticModel\"}}}"));
            files.Add(File($"{name}.Report/definition/reportExtensions.json",
                "{\"$schema\":\"https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json\"," +
                "\"name\":\"extension\",\"entities\":[{\"name\":\"Sales\",\"measures\":[" + string.Join(',', measures) + "]}]}"));
            files.Add(File($"{name}.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File($"{name}.Report/definition/pages/p1/page.json", "{\"name\":\"p1\",\"displayName\":\"Overview\"}"));
            if (placed.Length > 0)
            {
                var projections = string.Join(',', placed.Select((measure, index) =>
                    "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Sales\"}},\"Property\":\"" + measure + "\"}},\"queryRef\":\"q" + index + "\"}"));
                files.Add(File($"{name}.Report/definition/pages/p1/visuals/v1/visual.json",
                    "{\"name\":\"v1\",\"position\":{\"x\":0,\"y\":0,\"z\":0,\"width\":100,\"height\":100}," +
                    "\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" + projections + "]}}}}}"));
            }
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Report-scoped reachability", files));
    }

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));

    private static List<string[]> ReadCsv(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (quoted && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n' && !quoted)
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                rows.Add(row.ToArray());
                row.Clear();
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
