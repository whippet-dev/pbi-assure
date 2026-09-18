using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A measure's kpi block is exposed by the engine as hidden measures named "_&lt;Measure&gt; Goal",
/// "_&lt;Measure&gt; Status" and "_&lt;Measure&gt; Trend". The Microsoft IT Spend sample binds a matrix
/// to Fact[_Actual/Plan Goal] and Fact[_Actual/Plan Status] and references the Goal from the status
/// expression, none of which exist as persisted measures. Those references resolve to the measure
/// that owns the KPI; a name that only looks like one keeps the ordinary unresolved outcome.
/// </summary>
public sealed class KpiComponentReferenceTests
{
    [Fact]
    public void ReportReferencesToKpiComponentsResolveToTheOwningMeasure()
    {
        var inventory = Scan(Visual("_Actual/Plan Goal", "_Actual/Plan Status", "_Trended Trend"));

        Assert.Empty(inventory.UnresolvedSemanticReferences);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-MODEL-001");

        var owner = Usage(inventory, "Actual/Plan");
        Assert.Equal(SemanticUsageStates.DirectlyUsed, owner.UsageState);
        Assert.Equal(ClassificationConfidences.Established, owner.ClassificationConfidence);
        // One piece of evidence per component the visual binds; the evidence keeps the visual's path.
        Assert.Equal(2, owner.DirectReportReferences.Count);
        Assert.All(owner.DirectReportReferences, evidence =>
            Assert.Equal(UsageContexts.Projection, evidence.UsageContext));
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Trended").UsageState);

        // The KPI's own expressions then carry usage on, as they already did for a directly bound owner.
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Plan").UsageState);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Trend Source").UsageState);
        // No component was invented as an object of its own.
        Assert.DoesNotContain(inventory.SemanticObjectUsages, usage =>
            usage.ObjectName is "_Actual/Plan Goal" or "_Actual/Plan Status" or "_Trended Trend");
    }

    [Fact]
    public void OwnerKpiExpressionNamingItsOwnComponentIsNoLongerUnresolved()
    {
        var inventory = Scan(Visual("Actual"));

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.DoesNotContain(inventory.AnalysisLimitations, limitation =>
            limitation.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        Assert.All(inventory.SemanticObjectUsages, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
        AssertDaxDependency(inventory, "Actual/Plan", "Actual/Plan", "'Fact'[_Actual/Plan Goal]");
    }

    [Fact]
    public void DaxReferencesToKpiComponentsResolveToTheOwningMeasure()
    {
        var inventory = Scan(Visual("Status Consumer", "Unqualified Consumer"));

        AssertDaxDependency(inventory, "Status Consumer", "Trended", "'Fact'[_Trended Status]");
        AssertDaxDependency(inventory, "Unqualified Consumer", "Trended", "[_Trended Goal]");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Trended").UsageState);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
    }

    [Fact]
    public void LookalikeNamesWithoutAMatchingKpiComponentStayUnresolved()
    {
        // Lookalike has no kpi block; Actual/Plan has a kpi block with no trend expression.
        var inventory = Scan(
            Visual("_Lookalike Goal", "_Actual/Plan Trend"),
            "measure 'Bad Consumer' = 'Fact'[_Lookalike Goal] + 'Fact'[_Actual/Plan Trend]");

        Assert.Equal(
            ["_Actual/Plan Trend", "_Lookalike Goal"],
            inventory.UnresolvedSemanticReferences.Select(reference => reference.ObjectName)
                .Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, inventory.Findings.Count(finding => finding.RuleId == "PBI-MODEL-001"));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Lookalike").UsageState);
        // Its status expression names itself, which is the existing self-reference; nothing from the
        // report reaches it.
        Assert.Contains(Usage(inventory, "Actual/Plan").UsageState,
            new[] { SemanticUsageStates.ApparentlyUnused, SemanticUsageStates.UsedOnlyByUnusedBranch });

        Assert.Equal(
            ["'Fact'[_Actual/Plan Trend]", "'Fact'[_Lookalike Goal]"],
            inventory.UnresolvedSemanticDependencies
                .Select(dependency => dependency.ReferenceText).Order(StringComparer.Ordinal).ToArray());
        Assert.All(inventory.UnresolvedSemanticDependencies, dependency =>
            Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, dependency.ResolutionOutcome));
        Assert.Contains(inventory.AnalysisLimitations, limitation =>
            limitation.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
    }

    [Fact]
    public void APersistedMeasureUnderAComponentNameIsThatMeasure()
    {
        var inventory = Scan(
            Visual("_Persisted Goal"),
            "measure 'Persisted Consumer' = 'Fact'[_Persisted Goal]");

        Assert.Empty(inventory.UnresolvedSemanticReferences);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "_Persisted Goal").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Persisted").UsageState);
        AssertDaxDependency(inventory, "Persisted Consumer", "_Persisted Goal", "'Fact'[_Persisted Goal]");
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Persisted Consumer" && edge.ToObjectName == "Persisted");
    }

    [Fact]
    public void OrdinaryMeasureResolutionIsUnchanged()
    {
        var inventory = Scan(Visual("Actual", "Missing Measure"));

        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Actual").UsageState);
        var unresolved = Assert.Single(inventory.UnresolvedSemanticReferences);
        Assert.Equal("Missing Measure", unresolved.ObjectName);
        Assert.Single(inventory.Findings, finding => finding.RuleId == "PBI-MODEL-001");
        AssertDaxDependency(inventory, "Actual/Plan", "Plan", "'Fact'[Plan]");
    }

    [Fact]
    public void ComponentNamesFollowTheDefinedKpiExpressions()
    {
        var model = Assert.Single(Scan(Visual("Actual")).SemanticModels);
        var table = Assert.Single(model.Tables);

        Assert.Equal(["_Actual/Plan Goal", "_Actual/Plan Status"],
            KpiComponentReference.ComponentNames(Measure(table, "Actual/Plan")).ToArray());
        Assert.Equal(["_Trended Goal", "_Trended Status", "_Trended Trend"],
            KpiComponentReference.ComponentNames(Measure(table, "Trended")).ToArray());
        Assert.Empty(KpiComponentReference.ComponentNames(Measure(table, "Lookalike")));

        Assert.Equal("Actual/Plan", KpiComponentReference.FindOwningMeasure(table, "_actual/plan goal")?.Name);
        Assert.Null(KpiComponentReference.FindOwningMeasure(table, "_Actual/Plan Trend"));
        Assert.Null(KpiComponentReference.FindOwningMeasure(table, "_Lookalike Goal"));
        Assert.Null(KpiComponentReference.FindOwningMeasure(table, "_Persisted Goal"));
        Assert.Null(KpiComponentReference.FindOwningMeasure(table, "Actual/Plan Goal"));
        Assert.Null(KpiComponentReference.FindOwningMeasure(table, "_ Goal"));
        Assert.Null(KpiComponentReference.FindOwningMeasure(model, "Other", "_Actual/Plan Goal"));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Visual(params string[] measures)
    {
        var projections = string.Join(",", measures.Select(measure =>
            "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"" +
            measure + "\"}},\"queryRef\":\"Fact." + measure + "\"}"));
        return "{\"name\":\"v\",\"visual\":{\"visualType\":\"pivotTable\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
               projections + "]}}}}}";
    }

    private static ProjectInventory Scan(string visualJson, string extraMeasure = "")
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.Report/definition.pbir"] = "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
            ["Model.Report/definition/report.json"] = "{}",
            ["Model.Report/definition/pages/p/page.json"] = "{\"name\":\"p\"}",
            ["Model.Report/definition/pages/p/visuals/v/visual.json"] = visualJson,
            ["Model.SemanticModel/definition.pbism"] = "{}",
            ["Model.SemanticModel/definition/tables/Fact.tmdl"] = $$"""
                table Fact
                    measure 'Actual/Plan' = DIVIDE([Actual], [Plan])
                        kpi
                            targetExpression = 'Fact'[Plan]
                            statusExpression = ```
                                var x = 'Fact'[Actual/Plan] / 'Fact'[_Actual/Plan Goal]
                                return IF(ISBLANK(x), BLANK(), IF(x < 1, 1, 0))
                                ```

                    measure Plan = 1
                    measure Actual = 1
                    measure Trended = 1
                        kpi
                            targetExpression = 'Fact'[Plan]
                            statusExpression = 'Fact'[Actual]
                            trendExpression = 'Fact'[Trend Source]

                    measure 'Trend Source' = 1
                    measure Lookalike = 1
                    measure Persisted = 1
                        kpi
                            targetExpression = 'Fact'[Plan]

                    measure '_Persisted Goal' = 2
                    measure 'Status Consumer' = 'Fact'[_Trended Status]
                    measure 'Unqualified Consumer' = [_Trended Goal]
                    {{extraMeasure}}
                    column Key
                        dataType: int64
                """,
        };
        return ProjectScanner.Scan(new InMemoryProjectFileSource("KPI components", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }

    private static void AssertDaxDependency(ProjectInventory inventory, string source, string target, string evidenceText) =>
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.Dax &&
            edge.FromObjectName == source &&
            edge.ToObjectName == target &&
            edge.EvidenceText == evidenceText);

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage =>
            usage.Table == "Fact" && usage.ObjectName == objectName);

    private static SemanticMeasureInventory Measure(SemanticTableInventory table, string name) =>
        Assert.Single(table.Measures, measure => measure.Name == name);
}
