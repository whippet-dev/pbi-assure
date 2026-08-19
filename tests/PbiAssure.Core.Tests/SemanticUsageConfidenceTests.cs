using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// The uncertainty-propagation rule: when a model contains metadata that was not analysed and could
/// bear on usage, the absence-based states in that model are marked as qualified while keeping their
/// computed value.
///
/// Usage states are never changed here. Confidence is an orthogonal axis, deliberately not a sixth
/// usage state, so existing consumers keep working unchanged.
/// </summary>
public sealed class SemanticUsageConfidenceTests
{
    // ---- Absence states qualify -------------------------------------------------------------

    [Theory]
    [InlineData(SemanticUsageStates.ApparentlyUnused)]
    [InlineData(SemanticUsageStates.UsedOnlyByUnusedBranch)]
    public void AbsenceStatesAreQualifiedByAConstructThatMayCreateDependencies(string state)
    {
        var usage = Assert.Single(Apply(
            [Usage("Sales", state)],
            [Limitation("Sales", ConstructDependencyImpacts.MayCreateDependencies)]));

        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, usage.ClassificationConfidence);
    }

    [Theory]
    [InlineData(SemanticUsageStates.ApparentlyUnused)]
    [InlineData(SemanticUsageStates.UsedOnlyByUnusedBranch)]
    public void AbsenceStatesAreQualifiedWhenTheDependencyEffectIsUnknown(string state)
    {
        var usage = Assert.Single(Apply(
            [Usage("Sales", state)],
            [Limitation("Sales", ConstructDependencyImpacts.DependencyEffectUnknown)]));

        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, usage.ClassificationConfidence);
    }

    // ---- Positive states do not ------------------------------------------------------------

    /// <summary>
    /// Skipped metadata can only add references, so it cannot retract evidence already collected. This
    /// is a conservative product rule grounded in every construct known today being referential, not a
    /// proof about constructs nobody has seen — see the design's discussion of interpretive constructs.
    /// </summary>
    [Theory]
    [InlineData(SemanticUsageStates.DirectlyUsed)]
    [InlineData(SemanticUsageStates.IndirectlyUsed)]
    [InlineData(SemanticUsageStates.StructurallyRequired)]
    public void PositiveStatesAreNotQualifiedByCurrentlyKnownImpacts(string state)
    {
        foreach (var impact in new[]
                 {
                     ConstructDependencyImpacts.MayCreateDependencies,
                     ConstructDependencyImpacts.DependencyEffectUnknown,
                     ConstructDependencyImpacts.NoKnownDependencyEffect,
                 })
        {
            var usage = Assert.Single(Apply([Usage("Sales", state)], [Limitation("Sales", impact)]));

            Assert.Equal(state, usage.UsageState);
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
        }
    }

    // ---- NoKnownDependencyEffect qualifies nothing -------------------------------------------

    [Fact]
    public void AConstructWithNoKnownDependencyEffectQualifiesNothing()
    {
        var usages = Apply(
            [
                Usage("Sales", SemanticUsageStates.ApparentlyUnused),
                Usage("Sales", SemanticUsageStates.UsedOnlyByUnusedBranch),
                Usage("Sales", SemanticUsageStates.DirectlyUsed),
            ],
            [Limitation("Sales", ConstructDependencyImpacts.NoKnownDependencyEffect)]);

        Assert.All(
            usages,
            usage => Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    [Fact]
    public void NoLimitationsLeavesEveryObjectEstablished()
    {
        var usages = Apply(
            [
                Usage("Sales", SemanticUsageStates.ApparentlyUnused),
                Usage("Sales", SemanticUsageStates.DirectlyUsed),
            ],
            []);

        Assert.All(
            usages,
            usage => Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    // ---- Combination and scoping ------------------------------------------------------------

    [Fact]
    public void OneQualifyingLimitationAmongManyIsEnough()
    {
        var usage = Assert.Single(Apply(
            [Usage("Sales", SemanticUsageStates.ApparentlyUnused)],
            [
                Limitation("Sales", ConstructDependencyImpacts.NoKnownDependencyEffect),
                Limitation("Sales", ConstructDependencyImpacts.NoKnownDependencyEffect),
                Limitation("Sales", ConstructDependencyImpacts.MayCreateDependencies),
            ]));

        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, usage.ClassificationConfidence);
    }

    [Fact]
    public void AQualifyingLimitationInOneModelDoesNotQualifyAnother()
    {
        var usages = Apply(
            [
                Usage("First", SemanticUsageStates.ApparentlyUnused),
                Usage("Second", SemanticUsageStates.ApparentlyUnused),
            ],
            [Limitation("First", ConstructDependencyImpacts.MayCreateDependencies)]);

        Assert.Equal(
            ClassificationConfidences.QualifiedByLimitation,
            usages.Single(u => u.SemanticModel == "First").ClassificationConfidence);
        Assert.Equal(
            ClassificationConfidences.Established,
            usages.Single(u => u.SemanticModel == "Second").ClassificationConfidence);
    }

    // ---- The reserved interpretive value ----------------------------------------------------

    /// <summary>
    /// The design reserves MayInvalidateExistingEvidence for a construct that changes how existing
    /// evidence should be read rather than only adding references, and states that this value — and only
    /// this value — would also qualify positive states. Pinned at the rule level so the extensibility is
    /// real rather than aspirational. No registry entry uses it; see the test below.
    /// </summary>
    [Theory]
    [InlineData(SemanticUsageStates.DirectlyUsed)]
    [InlineData(SemanticUsageStates.IndirectlyUsed)]
    [InlineData(SemanticUsageStates.StructurallyRequired)]
    [InlineData(SemanticUsageStates.ApparentlyUnused)]
    [InlineData(SemanticUsageStates.UsedOnlyByUnusedBranch)]
    public void AnInterpretiveConstructWouldQualifyEveryState(string state)
    {
        var usage = Assert.Single(Apply(
            [Usage("Sales", state)],
            [Limitation("Sales", ConstructDependencyImpacts.MayInvalidateExistingEvidence)]));

        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, usage.ClassificationConfidence);
    }

    /// <summary>
    /// Guards the claim above: the interpretive value is reserved, not in use. If a future registry entry
    /// adopts it, that must be a deliberate decision that also updates this test.
    /// </summary>
    [Fact]
    public void NoRegistryRuleCurrentlyUsesTheInterpretiveImpact()
    {
        Assert.DoesNotContain(
            SemanticDefinitionFileRegistry.Rules.Append(SemanticDefinitionFileRegistry.Fallback),
            rule => rule.DependencyImpact == ConstructDependencyImpacts.MayInvalidateExistingEvidence);
    }

    // ---- Integration through the scanner ----------------------------------------------------

    [Fact]
    public void ScanningAProjectWithUnanalysedSecurityMetadataQualifiesItsAbsenceStates()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Confidence", [
            File("Sales.pbip", "{}"),
            File("Sales.SemanticModel/definition.pbism", "{}"),
            File("Sales.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n"),
            // Contains a construct this version does not recognise, so its dependency-bearing content
            // cannot be shown to be fully accounted for and the limitation stays qualifying.
            File("Sales.SemanticModel/definition/roles/Reader.tmdl", "role Reader\n\tmysteryBlock Thing\n"),
        ]));

        var usage = Assert.Single(inventory.SemanticObjectUsages);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, usage.ClassificationConfidence);
    }

    /// <summary>
    /// Power BI Desktop emits model.tmdl, database.tmdl and a culture file for every model. Those carry
    /// no known dependency effect, so a project whose only unanalysed metadata is those files must not
    /// be caveated — otherwise the mechanism would fire on every real project and mean nothing.
    /// </summary>
    [Fact]
    public void AProjectWhoseOnlyLimitationsHaveNoKnownEffectIsNotQualified()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Confidence", [
            File("Sales.pbip", "{}"),
            File("Sales.SemanticModel/definition.pbism", "{}"),
            File("Sales.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n"),
            File("Sales.SemanticModel/definition/model.tmdl", "model Model"),
            File("Sales.SemanticModel/definition/database.tmdl", "database Sales"),
            File("Sales.SemanticModel/definition/cultures/en-US.tmdl", "cultureInfo en-US"),
        ]));

        Assert.NotEmpty(inventory.AnalysisLimitations);
        Assert.All(
            inventory.SemanticObjectUsages,
            usage => Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    // ---- Against real Desktop-authored models ------------------------------------------------

    /// <summary>
    /// The real-world counterpart of the synthetic case above. Power BI Desktop emits model.tmdl,
    /// database.tmdl and a culture file for every model, so this Desktop-authored fixture reports
    /// limitations while having no unanalysed construct that could bear on usage. Nothing may be
    /// qualified — if this fails, the mechanism fires on every real project and means nothing.
    /// </summary>
    [Theory]
    [InlineData("tab-order-states")]
    [InlineData("grouped-tab-order")]
    [InlineData("model-reference-context")]
    public void DesktopModelsWithOnlyAlwaysPresentLimitationsAreNotQualified(string fixtureName)
    {
        var inventory = ProjectScanner.Scan(FixturePath(fixtureName));

        Assert.NotEmpty(inventory.AnalysisLimitations);
        Assert.All(
            inventory.SemanticObjectUsages,
            usage => Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    /// <summary>
    /// States the rule itself against real Desktop output rather than asserting a particular object's
    /// state. Deliberately generic: the fixture contains security metadata PBI Assure does not yet parse,
    /// and when it gains that support the underlying usage states will change. This assertion stays
    /// correct either way, because it compares confidence against the rule rather than against a
    /// remembered answer.
    /// </summary>
    [Fact]
    public void ConfidenceMatchesTheRuleAcrossTheDesktopConstructsFixture()
    {
        var inventory = ProjectScanner.Scan(FixturePath("desktop-semantic-constructs"));

        var modelsWithQualifyingLimitations = inventory.AnalysisLimitations
            .Where(limitation => limitation.DependencyImpact
                is ConstructDependencyImpacts.MayCreateDependencies
                or ConstructDependencyImpacts.DependencyEffectUnknown)
            .Select(limitation => limitation.SemanticModel!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(inventory.SemanticObjectUsages);
        foreach (var usage in inventory.SemanticObjectUsages)
        {
            var isAbsenceState = usage.UsageState
                is SemanticUsageStates.ApparentlyUnused
                or SemanticUsageStates.UsedOnlyByUnusedBranch;
            var expected = isAbsenceState && modelsWithQualifyingLimitations.Contains(usage.SemanticModel)
                ? ClassificationConfidences.QualifiedByLimitation
                : ClassificationConfidences.Established;

            Assert.Equal(expected, usage.ClassificationConfidence);
        }
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string FixturePath(string fixtureName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return Path.Combine(directory.FullName, "tests", "fixtures", fixtureName);
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }

    private static SemanticObjectUsage[] Apply(
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<AnalysisLimitation> limitations) =>
        SemanticUsageConfidenceQualifier.Apply(usages, limitations);

    private static SemanticObjectUsage Usage(string model, string state) => new(
        SemanticModel: model,
        Table: "Fact",
        ObjectName: $"Object{state}",
        ObjectType: SemanticObjectTypes.Column,
        HierarchyName: null,
        DirectReportReferences: [],
        UsageState: state);

    private static AnalysisLimitation Limitation(string model, string impact) => new(
        LimitationId: "PBI-LIMIT-TEST",
        Cause: AnalysisLimitationCauses.ConstructNotSupported,
        SupportState: ConstructSupportStates.NotYetAnalyzed,
        ConstructType: "test",
        Scope: AnalysisLimitationScopes.SemanticModel,
        SemanticModel: model,
        Table: null,
        ObjectName: null,
        ArtifactPath: $"{model}.SemanticModel/definition/test.tmdl",
        EvidencePath: AnalysisLimitation.WholeFileEvidence,
        DependencyImpact: impact,
        Concerns: [],
        Reason: "Test limitation.");

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
}
