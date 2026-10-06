using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Inactive-relationship activation evidence belongs to one semantic model. A USERELATIONSHIP call is
/// resolved within its own model, and whether its DAX source is reached from a report is that model's
/// own fact: a same-named object in another model of the same project never decides it.
///
/// Every model here has Sales[OrderDate], Sales[ShipDate], Sales[Amount] and Date[Date], an active
/// OrderDate relationship and an inactive ShipDate relationship, so two models share every name. Each
/// model is bound to its own report. Each two-model scan is also compared with scanning each model on
/// its own, which is the answer it must give.
/// </summary>
public sealed class UserRelationshipModelIsolationTests
{
    private const string Shipped = "CALCULATE([Sales], USERELATIONSHIP(Sales[ShipDate], Date[Date]))";
    private const string Activated = SemanticRelationshipActivationStates.ActivatedByReportUsedDax;
    private const string ReferencedOnly = SemanticRelationshipActivationStates.ReferencedOnlyByUnusedDax;

    private static readonly string[] Standard =
    [
        Relationship("rOrder", "OrderDate", isActive: true),
        Relationship("rShip", "ShipDate", isActive: false),
    ];

    // ---- A-D. Same names in both models; which sources are report-used --------------------------

    /// <summary>
    /// The first model listed is placed first in the project; "Zeta"/"Alpha" puts it last alphabetically,
    /// so the result cannot depend on which model the scanner happens to order first.
    /// </summary>
    [Theory]
    [InlineData("ModelA", "ModelB", true, false)]
    [InlineData("ModelA", "ModelB", false, true)]
    [InlineData("ModelA", "ModelB", true, true)]
    [InlineData("ModelA", "ModelB", false, false)]
    [InlineData("Zeta", "Alpha", true, false)]
    [InlineData("Zeta", "Alpha", false, true)]
    [InlineData("Zeta", "Alpha", true, true)]
    [InlineData("Zeta", "Alpha", false, false)]
    public void EachModelsActivationIsDecidedByItsOwnSource(string first, string second, bool firstUsed, bool secondUsed)
    {
        var models = new[]
        {
            new ModelSpec(first, [("Shipped Sales", Shipped)], Standard, firstUsed ? ["Shipped Sales"] : []),
            new ModelSpec(second, [("Shipped Sales", Shipped)], Standard, secondUsed ? ["Shipped Sales"] : []),
        };
        var inventory = Scan(models);

        Assert.Equal(Expected(firstUsed), Activation(inventory, first, "rShip"));
        Assert.Equal(Expected(secondUsed), Activation(inventory, second, "rShip"));
        Assert.Null(Relationship(inventory, first, "rOrder").Activation);
        Assert.Null(Relationship(inventory, second, "rOrder").Activation);
        AssertEachModelMatchesItsSoloScan(inventory, models);
    }

    // ---- E. A same-named source with no USERELATIONSHIP in the other model -------------------------

    [Theory]
    [InlineData("ModelA", "ModelB")]
    [InlineData("Zeta", "Alpha")]
    public void ASameNamedSourceInAnotherModelCannotActivateARelationship(string usedName, string callerName)
    {
        // The used model's Shipped Sales calls no USERELATIONSHIP; the other model's does, and is unused.
        var models = new[]
        {
            new ModelSpec(usedName, [("Shipped Sales", "[Sales] * 1")], Standard, ["Shipped Sales"]),
            new ModelSpec(callerName, [("Shipped Sales", Shipped)], Standard, []),
        };
        var inventory = Scan(models);

        Assert.Equal((ReferencedOnly, "Shipped Sales:False"), Activation(inventory, callerName, "rShip"));
        Assert.Equal((SemanticRelationshipActivationStates.NoDetectedActivation, ""), Activation(inventory, usedName, "rShip"));
        AssertEachModelMatchesItsSoloScan(inventory, models);
    }

    // ---- F. The relationship exists only in one model -----------------------------------------------

    [Fact]
    public void AModelWithoutTheRelationshipReceivesNoActivationEvidence()
    {
        var models = new[]
        {
            new ModelSpec("ModelA", [("Shipped Sales", Shipped)], Standard, ["Shipped Sales"]),
            new ModelSpec("ModelB", [("Shipped Sales", Shipped)], [Relationship("rOrder", "OrderDate", isActive: true)], ["Shipped Sales"]),
        };
        var inventory = Scan(models);

        Assert.Equal(Expected(true), Activation(inventory, "ModelA", "rShip"));
        var modelB = Model(inventory, "ModelB");
        Assert.Equal(["rOrder"], modelB.Relationships.Select(relationship => relationship.Name).ToArray());
        Assert.All(modelB.Relationships, relationship => Assert.Null(relationship.Activation));
        AssertEachModelMatchesItsSoloScan(inventory, models);
    }

    // ---- G. A differently configured model resolves its own relationship ----------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADifferentlyConfiguredModelUsesItsOwnSourceReachability(bool otherUsed)
    {
        // ModelB has ShipDate active and OrderDate inactive; its Shipped Sales activates OrderDate.
        var models = new[]
        {
            new ModelSpec("ModelA", [("Shipped Sales", Shipped)], Standard, ["Shipped Sales"]),
            new ModelSpec(
                "ModelB",
                [("Shipped Sales", "CALCULATE([Sales], USERELATIONSHIP(Sales[OrderDate], Date[Date]))")],
                [Relationship("rShip", "ShipDate", isActive: true), Relationship("rOrder", "OrderDate", isActive: false)],
                otherUsed ? ["Shipped Sales"] : []),
        };
        var inventory = Scan(models);

        Assert.Equal(Expected(true), Activation(inventory, "ModelA", "rShip"));
        Assert.Equal(Expected(otherUsed), Activation(inventory, "ModelB", "rOrder"));
        Assert.Null(Relationship(inventory, "ModelB", "rShip").Activation);
        AssertEachModelMatchesItsSoloScan(inventory, models);
    }

    // ---- H. Distinct names ---------------------------------------------------------------------------

    [Fact]
    public void DistinctSourceNamesAreUnchanged()
    {
        var models = new[]
        {
            new ModelSpec("ModelA", [("Shipped Sales", Shipped)], Standard, ["Shipped Sales"]),
            new ModelSpec("ModelB", [("Shipped Sales B", Shipped)], Standard, []),
        };
        var inventory = Scan(models);

        Assert.Equal(Expected(true), Activation(inventory, "ModelA", "rShip"));
        Assert.Equal((ReferencedOnly, "Shipped Sales B:False"), Activation(inventory, "ModelB", "rShip"));
        AssertEachModelMatchesItsSoloScan(inventory, models);
    }

    // ---- Single model: existing USERELATIONSHIP semantics ------------------------------------------

    /// <summary>
    /// One model with an active OrderDate relationship and inactive ShipDate and ShipDate2 relationships.
    /// The cases the Desktop fixture and the extractor tests do not already pin at scan level.
    /// </summary>
    [Theory]
    [InlineData("reversed and quoted", "CALCULATE([Sales], USERELATIONSHIP('Date'[Date], 'Sales'[ShipDate]))", "Caller", "Caller:True", "")]
    [InlineData("two calls", "CALCULATE([Sales], USERELATIONSHIP(Sales[ShipDate], Date[Date]), USERELATIONSHIP(Sales[ShipDate2], Date[Date]))", "Caller", "Caller:True", "Caller:True")]
    [InlineData("downstream caller", "CALCULATE([Sales], USERELATIONSHIP(Sales[ShipDate], Date[Date]))", "Outer", "Caller:True", "")]
    [InlineData("unused caller", "CALCULATE([Sales], USERELATIONSHIP(Sales[ShipDate], Date[Date]))", null, "Caller:False", "")]
    [InlineData("names the active relationship", "CALCULATE([Sales], USERELATIONSHIP(Sales[OrderDate], Date[Date]))", "Caller", "", "")]
    [InlineData("no matching relationship", "CALCULATE([Sales], USERELATIONSHIP(Sales[Amount], Date[Date]))", "Caller", "", "")]
    [InlineData("one argument", "CALCULATE([Sales], USERELATIONSHIP(Sales[ShipDate]))", "Caller", "", "")]
    [InlineData("commented out", "CALCULATE([Sales]) // USERELATIONSHIP(Sales[ShipDate], Date[Date])", "Caller", "", "")]
    public void SingleModelActivationIsUnchanged(string scenario, string expression, string? used, string shipSources, string ship2Sources)
    {
        var model = new ModelSpec(
            "Model",
            [("Caller", expression), ("Outer", "[Caller] * 2")],
            [.. Standard, Relationship("rShip2", "ShipDate2", isActive: false)],
            used is null ? [] : [used]);
        var inventory = Scan(model);

        Assert.Equal((scenario, Expect(shipSources)), (scenario, Activation(inventory, "Model", "rShip")));
        Assert.Equal((scenario, Expect(ship2Sources)), (scenario, Activation(inventory, "Model", "rShip2")));
        Assert.Null(Relationship(inventory, "Model", "rOrder").Activation);
        Assert.Empty(inventory.AnalysisLimitations);

        static (string, string) Expect(string sources) => sources.Length == 0
            ? (SemanticRelationshipActivationStates.NoDetectedActivation, "")
            : (sources.EndsWith(":True", StringComparison.Ordinal) ? Activated : ReferencedOnly, sources);
    }

    /// <summary>
    /// A call whose endpoint does not exist creates no activation evidence. The unresolved reference is
    /// still a model limitation, and the model's absence conclusions stay qualified by it.
    /// </summary>
    [Theory]
    [InlineData("Sales[Missing]")]
    [InlineData("Nope[ShipDate]")]
    public void AnUnresolvedEndpointStaysConservative(string endpoint)
    {
        var model = new ModelSpec(
            "Model",
            [("Caller", $"CALCULATE([Sales], USERELATIONSHIP({endpoint}, Date[Date]))"), ("Unused", "SUM(Sales[Control])")],
            Standard,
            ["Caller"]);
        var inventory = Scan(model);

        Assert.Equal((SemanticRelationshipActivationStates.NoDetectedActivation, ""), Activation(inventory, "Model", "rShip"));
        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal((endpoint, UnresolvedSemanticDependencyResolutionOutcomes.NotFound), (unresolved.ReferenceText, unresolved.ResolutionOutcome));
        Assert.Contains(inventory.AnalysisLimitations, limitation => limitation.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        var unused = Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "Unused");
        Assert.Equal(
            (SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            (unused.UsageState, unused.ClassificationConfidence));
    }

    [Fact]
    public void TheJsonContractIsUnchanged()
    {
        var inventory = Scan(
            new ModelSpec("ModelA", [("Shipped Sales", Shipped)], Standard, ["Shipped Sales"]),
            new ModelSpec("ModelB", [("Shipped Sales", Shipped)], Standard, []));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(inventory));
        Assert.Equal("0.26", json.RootElement.GetProperty("SchemaVersion").GetString());
        var activation = json.RootElement.GetProperty("SemanticModels").EnumerateArray()
            .SelectMany(model => model.GetProperty("Relationships").EnumerateArray())
            .Select(relationship => relationship.GetProperty("Activation"))
            .First(item => item.ValueKind == JsonValueKind.Object);
        Assert.Equal(["State", "Sources"], activation.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(
            ["Table", "ObjectName", "ObjectType", "ReachableFromReport"],
            activation.GetProperty("Sources").EnumerateArray().First().EnumerateObject().Select(property => property.Name).ToArray());
    }

    // ---- Guards --------------------------------------------------------------------------------------

    /// <summary>
    /// Each model's facts in the combined scan are exactly those of scanning that model with its own
    /// report alone: relationships with their activation evidence, usage states and confidence,
    /// reachability, dependencies, table states, unresolved dependencies, limitations and lineage paths.
    /// </summary>
    private static void AssertEachModelMatchesItsSoloScan(ProjectInventory combined, IReadOnlyList<ModelSpec> models)
    {
        foreach (var model in models)
        {
            var solo = Scan(model);
            Assert.Equal(Facts(solo, model.Name), Facts(combined, model.Name));
        }
    }

    private static string[] Facts(ProjectInventory inventory, string model)
    {
        bool Owns(string? semanticModel) => string.Equals(semanticModel, model, StringComparison.OrdinalIgnoreCase);
        var lineage = SemanticLineageProjection.Build(inventory);
        var paths = lineage.Cards
            .Where(card => card.Kind == LineageFocusKind.SemanticObject && Owns(card.SemanticModel))
            .Select(card => $"{card.NodeKey} | {card.Path!.Status} | {string.Join(" > ", card.Path.Steps.Select(step => step.NodeKey))}" +
                $" | {card.Path.Endpoint?.Location} | {card.Path.EndpointLocationCount} | {card.Path.OtherReportReachingConsumers}" +
                $" | {string.Join(",", card.Path.OnlyReachedFrom.Items.Select(item => item.NodeKey))}")
            .Order(StringComparer.Ordinal);
        return
        [
            Json(Model(inventory, model).Relationships),
            Json(inventory.SemanticObjectUsages.Where(usage => Owns(usage.SemanticModel))),
            Json(inventory.SemanticNodeReachability.Where(node => Owns(node.SemanticModel))),
            Json(inventory.SemanticDependencies.Where(edge => Owns(edge.SemanticModel))),
            Json(inventory.SemanticTableUsages.Where(table => Owns(table.SemanticModel))),
            Json(inventory.UnresolvedSemanticDependencies.Where(dependency => Owns(dependency.SemanticModel))),
            Json(inventory.AnalysisLimitations.Where(limitation => Owns(limitation.SemanticModel))),
            string.Join('\n', paths),
        ];
    }

    private static string Json<T>(IEnumerable<T> items) => JsonSerializer.Serialize(items.ToArray());

    // ---- Helpers -------------------------------------------------------------------------------------

    private sealed record ModelSpec(
        string Name,
        (string Name, string Expression)[] Measures,
        string[] Relationships,
        string[] Used);

    private static (string State, string Sources) Expected(bool used) =>
        used ? (Activated, "Shipped Sales:True") : (ReferencedOnly, "Shipped Sales:False");

    private static (string State, string Sources) Activation(ProjectInventory inventory, string model, string relationship)
    {
        var activation = Relationship(inventory, model, relationship).Activation;
        Assert.NotNull(activation);
        return (activation.State, string.Join(", ", activation.Sources.Select(source => $"{source.ObjectName}:{source.ReachableFromReport}")));
    }

    private static SemanticRelationshipInventory Relationship(ProjectInventory inventory, string model, string name) =>
        Assert.Single(Model(inventory, model).Relationships, relationship => relationship.Name == name);

    private static SemanticModelInventory Model(ProjectInventory inventory, string name) =>
        Assert.Single(inventory.SemanticModels, model => model.Name == name);

    private static string Relationship(string name, string fromColumn, bool isActive) =>
        $"relationship {name}\n" + (isActive ? string.Empty : "\tisActive: false\n") +
        $"\tfromColumn: Sales.{fromColumn}\n\ttoColumn: Date.Date\n\n";

    private static ProjectInventory Scan(params ModelSpec[] models)
    {
        var files = new List<ProjectFileContent>();
        foreach (var model in models)
        {
            var sales = new StringBuilder("table Sales\n");
            foreach (var column in new[] { "OrderDate", "ShipDate", "ShipDate2", "Amount", "Control" })
            {
                sales.Append(Column(column));
            }

            sales.Append("\tmeasure Sales = SUM(Sales[Amount])\n\n");
            foreach (var (name, expression) in model.Measures)
            {
                sales.Append("\tmeasure '").Append(name).Append("' = ").Append(expression).Append("\n\n");
            }

            files.Add(File($"{model.Name}.SemanticModel/definition.pbism", "{}"));
            files.Add(File($"{model.Name}.SemanticModel/definition/tables/Sales.tmdl", sales.ToString()));
            files.Add(File($"{model.Name}.SemanticModel/definition/tables/Date.tmdl", "table Date\n" + Column("Date")));
            files.Add(File($"{model.Name}.SemanticModel/definition/relationships.tmdl", string.Concat(model.Relationships)));

            var report = $"R{model.Name}.Report";
            files.Add(File($"{report}/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../" + model.Name + ".SemanticModel\"}}}"));
            files.Add(File($"{report}/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File($"{report}/definition/pages/p1/page.json", "{\"name\":\"p1\",\"displayName\":\"Overview\"}"));
            if (model.Used.Length > 0)
            {
                var projections = string.Join(',', model.Used.Select((measure, index) =>
                    "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Sales\"}},\"Property\":\"" + measure + "\"}},\"queryRef\":\"q" + index + "\"}"));
                files.Add(File($"{report}/definition/pages/p1/visuals/v1/visual.json",
                    "{\"name\":\"v1\",\"position\":{\"x\":0,\"y\":0,\"z\":0,\"width\":100,\"height\":100}," +
                    "\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" + projections + "]}}}}}"));
            }
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("USERELATIONSHIP model isolation", files));
    }

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n\n";

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
