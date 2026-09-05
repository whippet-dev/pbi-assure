using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// The tables directory is registered as fully analysed, which holds for every construct the table
/// parser reads. A table-level <c>defaultDetailRowsDefinition</c> is not one of them: its DAX can name
/// columns and measures, only the measure-owned form is parsed, and the file-level classification had
/// no way to say so — leaving a column referenced only from there ApparentlyUnused at full confidence.
///
/// Roles and perspectives deny by default, listing reference-free children and treating anything else
/// as unaccounted. Tables cannot: their property surface is large and mostly reference-free, so denying
/// by default would mark nearly every model partial. This accounting is therefore an explicit list of
/// constructs positively identified as dependency-bearing and unparsed.
/// </summary>
public sealed class TableConstructAccountingTests
{
    private const string DetailRows =
        "\tdefaultDetailRowsDefinition =\n" +
        "\t\t\tSELECTCOLUMNS ( Fact, \"S\", Fact[Secret] )\n";

    [Fact]
    public void TableLevelDetailRowsProducesADependencyBearingLimitation()
    {
        var inventory = Scan(tableLevelConstruct: DetailRows);

        var limitation = Assert.Single(
            inventory.AnalysisLimitations,
            candidate => candidate.LimitationId == "PBI-LIMIT-MODEL-TABLE-REFERENCES");
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);
        Assert.Equal(AnalysisLimitationCauses.ConstructNotSupported, limitation.Cause);
        Assert.Equal("Model", limitation.SemanticModel);
        Assert.Equal("Fact", limitation.Table);
        Assert.Contains("defaultDetailRowsDefinition", limitation.Reason, StringComparison.Ordinal);

        var table = Assert.Single(Assert.Single(inventory.SemanticModels).Tables);
        Assert.False(table.DependencyContentFullyAccountedFor);
        Assert.Equal(["defaultDetailRowsDefinition"], table.UnanalyzedDependencyConstructs);
    }

    /// <summary>
    /// TMDL does not require a table's properties to precede its child objects, and the construct is
    /// just as dependency-bearing when it follows them. Detection matches on the table's child indent
    /// rather than on position, so both layouts are seen.
    /// </summary>
    [Fact]
    public void TableLevelDetailRowsIsFoundAfterChildObjectsToo()
    {
        var inventory = Scan(tableLevelConstruct: string.Empty, trailingTableConstruct: DetailRows);

        Assert.Contains(
            inventory.AnalysisLimitations,
            candidate => candidate.LimitationId == "PBI-LIMIT-MODEL-TABLE-REFERENCES");
        Assert.Equal(
            ClassificationConfidences.QualifiedByLimitation,
            Usage(inventory, "Secret").ClassificationConfidence);
    }

    /// <summary>
    /// The usage state is unchanged — this slice does not parse the DAX, so no dependency edge appears
    /// and the column really does have no analysed use. Only the confidence in that absence moves.
    /// </summary>
    [Fact]
    public void ReferencedColumnStaysApparentlyUnusedButNoLongerEstablished()
    {
        var secret = Usage(Scan(tableLevelConstruct: DetailRows), "Secret");

        Assert.Equal(SemanticUsageStates.ApparentlyUnused, secret.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, secret.ClassificationConfidence);
    }

    [Fact]
    public void TableWithOnlyRecognisedConstructsEmitsNoLimitation()
    {
        var inventory = Scan(tableLevelConstruct: "\tlineageTag: 1\n\tisHidden\n\tdataCategory: Time\n");

        Assert.DoesNotContain(
            inventory.AnalysisLimitations,
            candidate => candidate.LimitationId == "PBI-LIMIT-MODEL-TABLE-REFERENCES");
        Assert.True(Assert.Single(Assert.Single(inventory.SemanticModels).Tables)
            .DependencyContentFullyAccountedFor);

        var secret = Usage(inventory, "Secret");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, secret.UsageState);
        Assert.Equal(ClassificationConfidences.Established, secret.ClassificationConfidence);
    }

    /// <summary>The measure-owned form is parsed, so it still produces a real edge and stays Established.</summary>
    [Fact]
    public void MeasureOwnedDetailRowsStillParsesAsARealDependency()
    {
        var inventory = Scan(
            tableLevelConstruct: string.Empty,
            measureSuffix: "\t\tdetailRowsDefinition = SELECTCOLUMNS ( Fact, \"S\", Fact[Secret] )\n",
            // The report makes Total reachable, so Secret lands on a positive state rather than on
            // UsedOnlyByUnusedBranch, which is itself an absence state.
            withReport: true);

        Assert.DoesNotContain(
            inventory.AnalysisLimitations,
            candidate => candidate.LimitationId == "PBI-LIMIT-MODEL-TABLE-REFERENCES");
        Assert.Contains(
            inventory.SemanticDependencies,
            edge => edge.FromObjectName == "Total" && edge.ToObjectName == "Secret");

        var secret = Usage(inventory, "Secret");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, secret.UsageState);
        Assert.Equal(ClassificationConfidences.Established, secret.ClassificationConfidence);
    }

    /// <summary>
    /// Role and perspective accounting is a different, deny-by-default mechanism and must be untouched:
    /// a role whose every child is accounted for still refines to NoKnownDependencyEffect.
    /// </summary>
    [Fact]
    public void RoleAndPerspectiveAccountingIsUnchanged()
    {
        var inventory = Scan(
            tableLevelConstruct: string.Empty,
            roleFile: "role Reader\n\tmodelPermission: read\n\n\ttablePermission Fact = Fact[Secret] = 1\n",
            perspectiveFile: "perspective Core\n\n\tperspectiveTable Fact\n\n\t\tperspectiveColumn Secret\n");

        var model = Assert.Single(inventory.SemanticModels);
        Assert.True(Assert.Single(model.Roles).DependencyContentFullyAccountedFor);
        Assert.True(Assert.Single(model.Perspectives).DependencyContentFullyAccountedFor);
        Assert.DoesNotContain(
            inventory.AnalysisLimitations,
            candidate => candidate.LimitationId == "PBI-LIMIT-MODEL-TABLE-REFERENCES");

        // A role filter and perspective membership are analysed, so the column is required by the model.
        Assert.Equal(SemanticUsageStates.StructurallyRequired, Usage(inventory, "Secret").UsageState);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == objectName);

    private static ProjectInventory Scan(
        string tableLevelConstruct,
        string measureSuffix = "",
        string trailingTableConstruct = "",
        string? roleFile = null,
        string? perspectiveFile = null,
        bool withReport = false)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Fact.tmdl",
                "table Fact\n" + tableLevelConstruct + "\n" +
                "\tmeasure Total = SUM ( Fact[Amount] )\n" + measureSuffix + "\n" +
                "\tcolumn Amount\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: Amount\n\n" +
                "\tcolumn Secret\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: Secret\n\n" +
                trailingTableConstruct),
        };

        if (withReport)
        {
            files.Add(File("Model.Report/definition.pbir",
                "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"));
            files.Add(File("Model.Report/definition/pages/pages.json",
                "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File("Model.Report/definition/pages/p1/page.json",
                "{\"name\":\"p1\",\"displayName\":\"Page 1\"}"));
            files.Add(File("Model.Report/definition/pages/p1/visuals/v1/visual.json",
                "{\"name\":\"v1\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"Total\"}}," +
                "\"queryRef\":\"Fact.Total\"}]}}}}}"));
        }

        if (roleFile is not null)
        {
            files.Add(File("Model.SemanticModel/definition/roles/Reader.tmdl", roleFile));
        }

        if (perspectiveFile is not null)
        {
            files.Add(File("Model.SemanticModel/definition/perspectives/Core.tmdl", perspectiveFile));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
