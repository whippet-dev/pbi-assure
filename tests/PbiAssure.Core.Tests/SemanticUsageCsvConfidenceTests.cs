using PbiAssure.Core.Inventory;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// An absence conclusion the scan knows is qualified must not reach a reviewer's spreadsheet looking
/// like one PBI Assure is sure of. The CSV is where deletion decisions actually get made, so the
/// distinction the report and the Data Catalogue already keep has to survive the export.
/// </summary>
public sealed class SemanticUsageCsvConfidenceTests
{
    private const string Model = "Sales";
    private const string OtherModel = "Other";

    [Fact]
    public void EstablishedAbsenceCarriesNoQualifyingLimitations()
    {
        var row = SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established));

        Assert.Equal("Apparently unused", row["SemanticUsage"]);
        Assert.Equal(ClassificationConfidences.Established, row["ClassificationConfidence"]);
        Assert.Equal(string.Empty, row["QualifyingLimitations"]);
    }

    [Theory]
    [InlineData(SemanticUsageStates.ApparentlyUnused, "Apparently unused")]
    [InlineData(SemanticUsageStates.UsedOnlyByUnusedBranch, "Used only by unused branch")]
    public void QualifiedAbsenceNamesTheLimitationsBehindIt(string usageState, string expectedLabel)
    {
        var row = SingleRow(
            Usage("Fact", "Amount", usageState, ClassificationConfidences.QualifiedByLimitation),
            Limitation("PBI-LIMIT-MODEL-FUNCTION", Model, ConstructDependencyImpacts.MayCreateDependencies));

        Assert.Equal(expectedLabel, row["SemanticUsage"]);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, row["ClassificationConfidence"]);
        Assert.Equal("PBI-LIMIT-MODEL-FUNCTION", row["QualifyingLimitations"]);
    }

    [Fact]
    public void PositiveEstablishedUsageIsUnchangedApartFromTheNewColumns()
    {
        var qualifying = Limitation("PBI-LIMIT-MODEL-FUNCTION", Model, ConstructDependencyImpacts.MayCreateDependencies);
        var usage = Usage("Fact", "Amount", SemanticUsageStates.DirectlyUsed, ClassificationConfidences.Established);

        var withLimitation = SingleRow(usage, qualifying);
        var withoutLimitation = SingleRow(usage);

        // A limitation that qualifies absences must leave a positive result alone.
        Assert.Equal(ClassificationConfidences.Established, withLimitation["ClassificationConfidence"]);
        Assert.Equal(string.Empty, withLimitation["QualifyingLimitations"]);
        foreach (var column in new[] { "Report", "Table", "Object", "ObjectType", "SemanticUsage", "ReviewCandidate" })
        {
            Assert.Equal(withoutLimitation[column], withLimitation[column]);
        }
    }

    [Fact]
    public void SeveralQualifyingLimitationsAreOrderedAndDeduplicated()
    {
        var row = SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            Limitation("PBI-LIMIT-MODEL-TABLE-REFERENCES", Model, ConstructDependencyImpacts.MayCreateDependencies),
            Limitation("PBI-LIMIT-MODEL-FUNCTION", Model, ConstructDependencyImpacts.DependencyEffectUnknown),
            // Same identifier raised twice for different artifacts: one entry, not two.
            Limitation("PBI-LIMIT-MODEL-FUNCTION", Model, ConstructDependencyImpacts.MayCreateDependencies),
            Limitation("PBI-LIMIT-MODEL-QUERY-REFERENCES", Model, ConstructDependencyImpacts.MayCreateDependencies));

        Assert.Equal(
            "PBI-LIMIT-MODEL-FUNCTION | PBI-LIMIT-MODEL-QUERY-REFERENCES | PBI-LIMIT-MODEL-TABLE-REFERENCES",
            row["QualifyingLimitations"]);
    }

    [Fact]
    public void LimitationsFromAnotherModelAndNonQualifyingImpactsDoNotLeakIn()
    {
        var row = SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            Limitation("PBI-LIMIT-MODEL-FUNCTION", Model, ConstructDependencyImpacts.MayCreateDependencies),
            Limitation("PBI-LIMIT-OTHER-MODEL", OtherModel, ConstructDependencyImpacts.MayCreateDependencies),
            Limitation("PBI-LIMIT-HARMLESS", Model, ConstructDependencyImpacts.NoKnownDependencyEffect));

        Assert.Equal("PBI-LIMIT-MODEL-FUNCTION", row["QualifyingLimitations"]);
    }

    [Fact]
    public void ReviewCandidateBehaviourIsUnchangedByConfidence()
    {
        var qualifying = Limitation("PBI-LIMIT-MODEL-FUNCTION", Model, ConstructDependencyImpacts.MayCreateDependencies);

        // Absence states stay review candidates whether or not the conclusion is qualified.
        Assert.Equal("Yes", SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established))["ReviewCandidate"]);
        Assert.Equal("Yes", SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            qualifying)["ReviewCandidate"]);
        Assert.Equal("Yes", SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.UsedOnlyByUnusedBranch, ClassificationConfidences.QualifiedByLimitation),
            qualifying)["ReviewCandidate"]);
        Assert.Equal("No", SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.DirectlyUsed, ClassificationConfidences.Established))["ReviewCandidate"]);
    }

    [Fact]
    public void FormulaNeutralisationAndEscapingStillApplyToTheNewColumns()
    {
        var csv = Render(
            [Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation)],
            [Limitation("=cmd|' /c calc'!A1", Model, ConstructDependencyImpacts.MayCreateDependencies),
             Limitation("PBI-LIMIT-\"QUOTED\",COMMA", Model, ConstructDependencyImpacts.MayCreateDependencies)]);

        // Leading '=' is neutralised, and the quote/comma field is quoted with doubled quotes.
        Assert.Contains("'=cmd|' /c calc'!A1", csv, StringComparison.Ordinal);
        Assert.Contains("\"\"QUOTED\"\"", csv, StringComparison.Ordinal);
        var row = SingleRow(
            Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            Limitation("PBI-LIMIT-\"QUOTED\",COMMA", Model, ConstructDependencyImpacts.MayCreateDependencies));
        Assert.Equal("PBI-LIMIT-\"QUOTED\",COMMA", row["QualifyingLimitations"]);
    }

    [Fact]
    public void ColumnOrderIsStableAndTheNewColumnsAreAppended()
    {
        var header = ReadCsv(Render(
            [Usage("Fact", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established)],
            []))[0];

        Assert.Equal("ReviewCandidate", header[^3]);
        Assert.Equal("ClassificationConfidence", header[^2]);
        Assert.Equal("QualifyingLimitations", header[^1]);
        Assert.Equal(15, header.Length);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static Dictionary<string, string> SingleRow(
        SemanticObjectUsage usage,
        params AnalysisLimitation[] limitations)
    {
        var rows = ReadCsv(Render([usage], limitations));
        var header = rows[0];
        var values = Assert.Single(rows.Skip(1).ToArray());
        return header.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);
    }

    private static string Render(SemanticObjectUsage[] usages, AnalysisLimitation[] limitations) =>
        SemanticUsageCsvRenderer.Render(new ProjectInventory(
            SchemaVersion: "0.26",
            RootPath: "Synthetic",
            ScannedAtUtc: DateTimeOffset.UnixEpoch,
            Artifacts: [],
            Reports: [],
            SemanticModels: [],
            SemanticObjectUsages: usages,
            SemanticTableUsages: [],
            SemanticDependencies: [],
            PowerQueryUsages: [],
            PowerQueryDependencies: [],
            PowerQueryColumnUsages: [],
            SemanticTablePowerQueryContexts: [],
            DataSources: [],
            UnresolvedSemanticReferences: [],
            UnresolvedSemanticDependencies: [],
            Findings: [])
        {
            AnalysisLimitations = limitations,
        });

    private static SemanticObjectUsage Usage(string table, string name, string state, string confidence) =>
        new(Model, table, name, SemanticObjectTypes.Column, null, [], state)
        {
            ClassificationConfidence = confidence,
        };

    private static AnalysisLimitation Limitation(string limitationId, string semanticModel, string dependencyImpact) =>
        new(
            LimitationId: limitationId,
            Cause: AnalysisLimitationCauses.ConstructNotSupported,
            SupportState: ConstructSupportStates.PartiallyAnalyzed,
            ConstructType: "function",
            Scope: AnalysisLimitationScopes.SemanticModel,
            SemanticModel: semanticModel,
            Table: null,
            ObjectName: null,
            ArtifactPath: $"{semanticModel}.SemanticModel/definition/functions.tmdl",
            EvidencePath: AnalysisLimitation.WholeFileEvidence,
            DependencyImpact: dependencyImpact,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: "Synthetic limitation.");

    private static List<string[]> ReadCsv(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add([.. row]);
                    row.Clear();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        return rows;
    }
}
