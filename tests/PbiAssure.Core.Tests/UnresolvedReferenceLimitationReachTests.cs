using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// An Ambiguous reference is a missing edge whose target is one of a known set. Adding that edge could
/// make used only the candidate it leads to and whatever that candidate reaches, so the limitation
/// qualifies exactly the union of the candidates' closures and nothing else in the model. A NotFound
/// reference, or an Ambiguous one whose candidate set the resolver could not prove complete, still
/// reaches the whole model.
/// </summary>
public sealed class UnresolvedReferenceLimitationReachTests
{
    private const string LimitationId = "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE";

    // ---- 1 & 3. Two persisted columns of one name: both candidates, nothing else --------------

    [Fact]
    public void ATwoCandidateAmbiguityQualifiesBothCandidatesAndLeavesTheRestEstablished()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUMX(Table2, [Amount])\n" +
                    "\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Amount") + Column("Unrelated"),
            projectedMeasure: "Probe");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Equal(
            [Key("Table1", "Amount", SemanticObjectTypes.Column), Key("Table2", "Amount", SemanticObjectTypes.Column)],
            unresolved.CandidateTargets!.Order(StringComparer.Ordinal).ToArray());

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);
        Assert.Equal(
            [Key("Table1", "Amount", SemanticObjectTypes.Column), Key("Table2", "Amount", SemanticObjectTypes.Column)],
            ObjectsIn(limitation.Reach!));

        foreach (var table in new[] { "Table1", "Table2" })
        {
            var candidate = Usage(inventory, table, "Amount");
            Assert.Equal(SemanticUsageStates.ApparentlyUnused, candidate.UsageState);
            Assert.Equal(ClassificationConfidences.QualifiedByLimitation, candidate.ClassificationConfidence);
            Assert.Equal(LimitationId, Assert.Single(SemanticUsageConfidenceQualifier.Qualifying(candidate, inventory.AnalysisLimitations)).LimitationId);
        }

        foreach (var (table, name) in new[] { ("Table1", "UnusedMeasure"), ("Table2", "Unrelated") })
        {
            var unrelated = Usage(inventory, table, name);
            Assert.Equal(SemanticUsageStates.ApparentlyUnused, unrelated.UsageState);
            Assert.Equal(ClassificationConfidences.Established, unrelated.ClassificationConfidence);
            Assert.Empty(SemanticUsageConfidenceQualifier.Qualifying(unrelated, inventory.AnalysisLimitations));
        }
    }

    // ---- 2. The closure behind a candidate is qualified too ----------------------------------

    [Fact]
    public void ObjectsReachableFromACandidateAreQualifiedAndTheRestAreNot()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUMX(Table2, [Amount])\n" +
                    "\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Base") + Column("Unrelated") +
                    "\tcolumn Amount = Table2[Base] * 2\n\t\tdataType: int64\n",
            projectedMeasure: "Probe");

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Contains(Key("Table2", "Base", SemanticObjectTypes.Column), limitation.Reach!);
        Assert.DoesNotContain(Key("Table2", "Unrelated", SemanticObjectTypes.Column), limitation.Reach!);

        // Base is reached only through the candidate column, so it sits on that unused branch: qualified.
        var reached = Usage(inventory, "Table2", "Base");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, reached.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, reached.ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table2", "Amount").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table2", "Unrelated").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table1", "UnusedMeasure").ClassificationConfidence);
    }

    // ---- 4. The source's own state does not move the reach -----------------------------------

    [Fact]
    public void TheReachIsTheSameWhetherTheSourceIsDirectlyUsedOrNot()
    {
        var used = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUMX(Table2, [Amount])\n" +
                    "\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Amount"),
            projectedMeasure: "Probe");
        var unused = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUMX(Table2, [Amount])\n" +
                    "\tmeasure Wrapper = [Probe]\n" +
                    "\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Amount"));

        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(used, "Table1", "Probe").UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, Usage(unused, "Table1", "Probe").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(unused, "Table1", "Wrapper").UsageState);

        foreach (var inventory in new[] { used, unused })
        {
            var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
            Assert.Equal(
                [Key("Table1", "Amount", SemanticObjectTypes.Column), Key("Table2", "Amount", SemanticObjectTypes.Column)],
                ObjectsIn(limitation.Reach!));
            Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table1", "Amount").ClassificationConfidence);
            Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table2", "Amount").ClassificationConfidence);
            // The edge is outgoing from the source: its own state rests on what reaches it, not on
            // where its reference leads, so the source is never in doubt because of its own reference.
            Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table1", "Probe").ClassificationConfidence);
            Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table1", "UnusedMeasure").ClassificationConfidence);
        }

        Assert.Equal(ClassificationConfidences.Established, Usage(unused, "Table1", "Wrapper").ClassificationConfidence);
    }

    // ---- The other bounded forms: measure against measure, column against measure -------------

    [Fact]
    public void ASharedMeasureNameAcrossTablesQualifiesExactlyThoseMeasures()
    {
        var inventory = Scan(
            table1: "table Table1\n\tmeasure Total = 1\n\tmeasure Probe = [Total] + 0\n\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n\tmeasure Total = 2\n" + Column("Unrelated"),
            projectedMeasure: "Probe");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Equal(
            [Key("Table1", "Total", SemanticObjectTypes.Measure), Key("Table2", "Total", SemanticObjectTypes.Measure)],
            unresolved.CandidateTargets!.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table1", "Total").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table2", "Total").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table1", "UnusedMeasure").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table2", "Unrelated").ClassificationConfidence);
    }

    [Fact]
    public void AQualifiedNameMatchingAColumnAndAMeasureQualifiesExactlyThoseTwo()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Dup") + Column("Orphan") +
                    "\tmeasure Dup = 1\n\tmeasure Probe = Table1[Dup]\n",
            table2: "table Table2\n" + Column("Unrelated"),
            projectedMeasure: "Probe");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Equal(
            [Key("Table1", "Dup", SemanticObjectTypes.Column), Key("Table1", "Dup", SemanticObjectTypes.Measure)],
            unresolved.CandidateTargets!.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation,
            Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "Dup" && usage.ObjectType == SemanticObjectTypes.Column).ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation,
            Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "Dup" && usage.ObjectType == SemanticObjectTypes.Measure).ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table1", "Orphan").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table2", "Unrelated").ClassificationConfidence);
    }

    // ---- 5. Not provably complete: a report measure can also mean another report measure -------

    [Fact]
    public void AnAmbiguityInsideAReportMeasureStaysModelWide()
    {
        var inventory = Scan(
            table1: "table Table1\n\tmeasure Total = 1\n\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n\tmeasure Total = 2\n" + Column("Unrelated"),
            reportMeasureExpression: "[Total]");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticDependencyKinds.ReportMeasure, unresolved.DependencyKind);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Null(unresolved.CandidateTargets);

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Null(limitation.Reach);
        foreach (var (table, name) in new[] { ("Table1", "Total"), ("Table2", "Total"), ("Table1", "UnusedMeasure"), ("Table2", "Unrelated") })
        {
            Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, table, name).ClassificationConfidence);
        }
    }

    // ---- 6 & 9. NotFound is untouched and still qualifies the unrelated -----------------------

    [Fact]
    public void ANotFoundReferenceStillReachesTheWholeModel()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUM(Table1[Missing])\n\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Unrelated"),
            projectedMeasure: "Probe");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        Assert.Null(unresolved.CandidateTargets);
        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Null(limitation.Reach);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table1", "UnusedMeasure").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Table2", "Unrelated").ClassificationConfidence);
    }

    [Fact]
    public void AModelWideDoubtBesideABoundedOneStillQualifiesTheUnrelated()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUMX(Table2, [Amount])\n" +
                    "\tmeasure Broken = SUM(Table1[Missing])\n" +
                    "\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Amount"),
            projectedMeasure: "Probe");

        var limitations = inventory.AnalysisLimitations.Where(item => item.LimitationId == LimitationId).ToArray();
        Assert.Equal(2, limitations.Length);
        Assert.Single(limitations, item => item.Reach is null && item.Reason.Contains("(NotFound)", StringComparison.Ordinal));
        Assert.Single(limitations, item => item.Reach is not null && item.Reason.Contains("(Ambiguous)", StringComparison.Ordinal));

        var unrelated = Usage(inventory, "Table1", "UnusedMeasure");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, unrelated.ClassificationConfidence);
        // Only the model-wide doubt applies to it; the bounded one does not.
        var qualifying = Assert.Single(SemanticUsageConfidenceQualifier.Qualifying(unrelated, inventory.AnalysisLimitations));
        Assert.Contains("(NotFound)", qualifying.Reason, StringComparison.Ordinal);
        Assert.Equal(2, SemanticUsageConfidenceQualifier.Qualifying(Usage(inventory, "Table2", "Amount"), inventory.AnalysisLimitations).Count());
    }

    // ---- 8. Exports agree with the confidence they sit beside --------------------------------

    [Fact]
    public void TheCsvAndTheReportAgreeWithTheBoundedConfidence()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = SUMX(Table2, [Amount])\n\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Amount") + Column("Unrelated"),
            projectedMeasure: "Probe");

        var rows = SemanticUsageCsvRenderer.Render(inventory).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split(','))
            .ToArray();
        var header = rows[0].ToList();
        var objectIndex = header.IndexOf("Object");
        var confidenceIndex = header.IndexOf("ClassificationConfidence");
        var qualifyingIndex = header.IndexOf("QualifyingLimitations");
        var amount = rows.Single(row => row[objectIndex] == "Amount" && row[header.IndexOf("Table")] == "Table2");
        var unrelated = rows.Single(row => row[objectIndex] == "Unrelated");
        var unusedMeasure = rows.Single(row => row[objectIndex] == "UnusedMeasure");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, amount[confidenceIndex]);
        Assert.Equal(LimitationId, amount[qualifyingIndex]);
        Assert.Equal(ClassificationConfidences.Established, unrelated[confidenceIndex]);
        Assert.Equal(string.Empty, unrelated[qualifyingIndex]);
        Assert.Equal(ClassificationConfidences.Established, unusedMeasure[confidenceIndex]);
        Assert.Equal(string.Empty, unusedMeasure[qualifyingIndex]);

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Equal(2, html.Split("class=\"confidence-flag\"").Length - 1);
        Assert.Contains("used or unused result for 2 of 5 model objects", html, StringComparison.Ordinal);
    }

    // ---- Duplicates, functions, and the public shape ---------------------------------------

    [Fact]
    public void RepeatedOccurrencesMergeIntoOneRecordWithTheUnionOfTheirCandidates()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") +
                    "\tmeasure Probe = [Amount] + SUMX(Table2, [Amount])\n\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n" + Column("Amount"),
            projectedMeasure: "Probe");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("[Amount]", unresolved.ReferenceText);
        Assert.Equal(2, unresolved.CandidateTargets!.Count);
        Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Table1", "UnusedMeasure").ClassificationConfidence);
    }

    /// <summary>
    /// A function body's ambiguity is bounded like any other. The function file's own limitation is a
    /// separate matter and keeps its conservative reading when a body left a reference unresolved, so
    /// the unrelated measure is still qualified — by that limitation alone.
    /// </summary>
    [Fact]
    public void AnAmbiguityInAFunctionBodyIsBoundedWhileTheFunctionLimitationIsNot()
    {
        var inventory = Scan(
            table1: "table Table1\n\tmeasure Total = 1\n\tmeasure UnusedMeasure = 1\n",
            table2: "table Table2\n\tmeasure Total = 2\n",
            functions: "function Probe = () => [Total]\n");

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticObjectTypes.Function, unresolved.FromObjectType);
        Assert.Equal(2, unresolved.CandidateTargets!.Count);
        Assert.NotNull(Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId).Reach);
        Assert.Null(Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-FUNCTION").Reach);

        var unrelated = Usage(inventory, "Table1", "UnusedMeasure");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, unrelated.ClassificationConfidence);
        Assert.Equal("PBI-LIMIT-MODEL-FUNCTION",
            Assert.Single(SemanticUsageConfidenceQualifier.Qualifying(unrelated, inventory.AnalysisLimitations)).LimitationId);
    }

    [Fact]
    public void CandidatesAndReachStayOutOfThePublicJson()
    {
        var inventory = Scan(
            table1: "table Table1\n" + Column("Amount") + "\tmeasure Probe = SUMX(Table2, [Amount])\n",
            table2: "table Table2\n" + Column("Amount"),
            projectedMeasure: "Probe");

        var json = JsonSerializer.Serialize(inventory);
        Assert.Equal("0.26", inventory.SchemaVersion);
        Assert.DoesNotContain("CandidateTargets", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Reach\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ResolutionOutcome\":\"Ambiguous\"", json, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";

    private static string Key(string table, string objectName, string objectType) =>
        FieldIdentity.Create(table, objectName, objectType);

    // Containing-table edges bring each candidate's table into the closure; tables carry no
    // confidence, so the reach is compared on the objects that do.
    private static string[] ObjectsIn(IReadOnlySet<string> reach) => reach
        .Where(key => !key.StartsWith(SemanticObjectTypes.Table + "", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == objectName);

    private static ProjectInventory Scan(
        string table1,
        string table2,
        string? projectedMeasure = null,
        string? reportMeasureExpression = null,
        string? functions = null)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Table1.tmdl", table1),
            File("Model.SemanticModel/definition/tables/Table2.tmdl", table2),
        };
        if (functions is not null)
        {
            files.Add(File("Model.SemanticModel/definition/functions.tmdl", functions));
        }

        if (projectedMeasure is not null || reportMeasureExpression is not null)
        {
            files.Add(File("Model.Report/definition.pbir",
                "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"));
            files.Add(File("Model.Report/definition/pages/pages.json",
                "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File("Model.Report/definition/pages/p1/page.json",
                "{\"name\":\"p1\",\"displayName\":\"Page 1\"}"));
        }

        if (projectedMeasure is not null)
        {
            files.Add(File("Model.Report/definition/pages/p1/visuals/v1/visual.json",
                "{\"name\":\"v1\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Table1\"}},\"Property\":\"" +
                projectedMeasure + "\"}},\"queryRef\":\"Table1." + projectedMeasure + "\"}]}}}}}"));
        }

        if (reportMeasureExpression is not null)
        {
            files.Add(File("Model.Report/definition/reportExtensions.json",
                "{\"$schema\":\"https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json\"," +
                "\"name\":\"extension\",\"entities\":[{\"name\":\"Table1\",\"measures\":[{\"name\":\"Local\",\"dataType\":\"Decimal\"," +
                "\"expression\":\"" + reportMeasureExpression + "\",\"references\":{\"unrecognizedReferences\":false,\"measures\":[]}}]}]}"));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Unresolved reach", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
