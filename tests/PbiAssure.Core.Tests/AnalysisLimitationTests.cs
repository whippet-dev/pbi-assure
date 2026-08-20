using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Tests for registry-driven definition-artifact classification and the analysis limitations it emits.
///
/// These deliberately assert either registry-to-behaviour consistency or a structural invariant, never a
/// specific construct's support status and never a known-wrong usage outcome. When support for a
/// construct is added, its registry rule changes classification and the same tests assert the opposite
/// behaviour without needing to be renamed or reinterpreted.
/// </summary>
public sealed class AnalysisLimitationTests
{
    // ---- Registry: totality and unambiguity ------------------------------------------------

    [Fact]
    public void ClassificationIsTotalForAnyDefinitionPath()
    {
        string[] paths =
        [
            "definition/tables/Sales.tmdl",
            "definition/roles.tmdl",
            "definition.pbism",
            "definition/something-nobody-has-invented-yet.tmdl",
            "definition/nested/deeply/odd.tmdl",
            "model.bim",
            string.Empty,
        ];

        foreach (var path in paths)
        {
            var rule = SemanticDefinitionFileRegistry.Classify(path);

            Assert.NotNull(rule);
            Assert.Contains(rule.Classification, KnownClassifications);
        }
    }

    /// <summary>
    /// Proves match unambiguity rather than pattern-string distinctness. A directory rule and an exact
    /// rule can overlap without their pattern strings being equal — for example "definition/tables" and
    /// "definition/tables/Sales.tmdl" — in which case classification would silently depend on
    /// declaration order. This exercises the real matcher against each rule's own representative path.
    /// </summary>
    [Fact]
    public void NoPathMatchesMoreThanOneRegistryRule()
    {
        foreach (var rule in SemanticDefinitionFileRegistry.Rules)
        {
            var path = RepresentativePath(rule);
            var match = Assert.Single(SemanticDefinitionFileRegistry.MatchingRules(path));

            Assert.Equal(rule.LimitationId, match.LimitationId);
        }
    }

    /// <summary>
    /// The representative paths above are generated from the rules themselves, so they cannot detect an
    /// overlap that only a differently shaped path would expose. This adds paths chosen to sit on the
    /// boundaries between rules.
    /// </summary>
    [Fact]
    public void NoBoundaryPathMatchesMoreThanOneRegistryRule()
    {
        string[] boundaryPaths =
        [
            "definition/tables/Sales.tmdl",
            "definition/tables/nested/Sales.tmdl",
            "definition/roles/Role1.tmdl",
            "definition/roles.tmdl",
            "definition/perspectives/P1.tmdl",
            "definition/cultures/en-US.tmdl",
            "definition/model.tmdl",
            "definition/database.tmdl",
            "definition/relationships.tmdl",
            "definition/expressions.tmdl",
            "definition/dataSources.tmdl",
            "definition/functions.tmdl",
            "definition.pbism",
            "model.bim",
            "TMDLScripts/Script1.tmdl",
            "definition/unknown.tmdl",
        ];

        foreach (var path in boundaryPaths)
        {
            Assert.True(
                SemanticDefinitionFileRegistry.MatchingRules(path).Count <= 1,
                $"More than one registry rule matched '{path}'.");
        }
    }

    [Fact]
    public void EveryRegistryRuleMatchesItsOwnRepresentativePath()
    {
        foreach (var rule in SemanticDefinitionFileRegistry.Rules)
        {
            var matched = SemanticDefinitionFileRegistry.Classify(RepresentativePath(rule));

            Assert.Equal(rule.LimitationId, matched.LimitationId);
        }
    }

    [Fact]
    public void EveryRegistryRuleUsesKnownVocabulary()
    {
        foreach (var rule in SemanticDefinitionFileRegistry.Rules.Append(SemanticDefinitionFileRegistry.Fallback))
        {
            Assert.Contains(rule.Classification, KnownClassifications);
            Assert.Contains(rule.SupportState, KnownSupportStates);
            Assert.Contains(rule.DependencyImpact, KnownDependencyImpacts);
            Assert.False(string.IsNullOrWhiteSpace(rule.LimitationId));
            Assert.False(string.IsNullOrWhiteSpace(rule.ConstructType));
            Assert.False(string.IsNullOrWhiteSpace(rule.Reason));
        }
    }

    // ---- Registry-to-behaviour consistency --------------------------------------------------
    // Driven from the registry rather than from a hardcoded construct list, so behaviour follows the
    // registry automatically when a construct moves between classifications.

    [Fact]
    public void RulesClassifiedAnalyzedProduceNoLimitation()
    {
        AssertLimitationEmission(ConstructClassifications.Analyzed, expectLimitation: false);
    }

    [Fact]
    public void RulesClassifiedPackagingProduceNoLimitation()
    {
        AssertLimitationEmission(ConstructClassifications.Packaging, expectLimitation: false);
    }

    [Fact]
    public void RulesClassifiedSemanticNotYetAnalyzedProduceALimitation()
    {
        AssertLimitationEmission(ConstructClassifications.SemanticNotYetAnalyzed, expectLimitation: true);
    }

    [Fact]
    public void UnrecognizedDefinitionArtifactsProduceALimitation()
    {
        var limitations = DetectFor("definition/something-nobody-has-invented-yet.tmdl");

        var limitation = Assert.Single(limitations);
        Assert.Equal(ConstructSupportStates.Unrecognized, limitation.SupportState);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
    }

    // ---- The backbone invariant -------------------------------------------------------------

    [Fact]
    public void EveryDefinitionArtifactIsClassifiedByTheConstructRegistry()
    {
        // The documented TMDL folder shape, plus an editor script and an unrecognised file.
        var source = BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/relationships.tmdl", string.Empty),
            ("definition/expressions.tmdl", string.Empty),
            ("definition/dataSources.tmdl", string.Empty),
            ("definition/functions.tmdl", string.Empty),
            ("definition/roles/Role1.tmdl", "role Role1"),
            ("definition/perspectives/Perspective1.tmdl", "perspective Perspective1"),
            ("definition/cultures/en-US.tmdl", "cultureInfo en-US"),
            ("definition/model.tmdl", "model Model"),
            ("definition/database.tmdl", "database Sales"),
            ("TMDLScripts/Untitled 1.tmdl", "table Scratch"),
            ("definition/who-knows.tmdl", "surprise"));

        var inventory = ProjectScanner.Scan(source);

        var definitionArtifacts = source.Files
            .Where(file => file.RelativePath.StartsWith("Sales.SemanticModel/", StringComparison.OrdinalIgnoreCase))
            .Where(file => SemanticDefinitionFileRegistry.IsDefinitionArtifact(file.RelativePath))
            .Select(file => file.RelativePath)
            .ToArray();

        var limitedPaths = inventory.AnalysisLimitations
            .Select(limitation => limitation.ArtifactPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in definitionArtifacts)
        {
            var modelRelativePath = path["Sales.SemanticModel/".Length..];
            var rule = SemanticDefinitionFileRegistry.Classify(modelRelativePath);
            var shouldBeLimited = rule.Classification is
                ConstructClassifications.SemanticNotYetAnalyzed or ConstructClassifications.Unrecognized;

            // A role is registry-classified as partially analysed in general, but a particular role file
            // is omitted when its parser inventory proves every encountered child was accounted for.
            if (rule.ConstructType == "role")
            {
                shouldBeLimited = false;
            }

            Assert.Equal(shouldBeLimited, limitedPaths.Contains(path));
        }

        // Nothing may be reported that is not part of the classified universe.
        Assert.All(limitedPaths, path => Assert.Contains(path, definitionArtifacts));
    }

    // ---- Integration through ProjectScanner -------------------------------------------------

    /// <summary>
    /// Uses the documented role-per-file shape: Microsoft documents roles as one file per role inside a
    /// roles/ sub-folder, not a single definition/roles.tmdl.
    /// </summary>
    [Fact]
    public void FullyAccountedRoleMetadataDoesNotProduceALimitation()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/roles/RegionalManager.tmdl", "role RegionalManager")));

        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.ConstructType == "role");
    }

    [Fact]
    public void FullyAccountedRoleFilesDoNotProduceLimitations()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/roles/Role1.tmdl", "role Role1"),
            ("definition/roles/Role2.tmdl", "role Role2")));

        Assert.DoesNotContain(inventory.AnalysisLimitations, limitation => limitation.ConstructType == "role");
    }

    /// <summary>
    /// A semantic model stored in TMSL rather than TMDL is not read at all, so the limitation must say
    /// so rather than leaving the model silently empty.
    /// </summary>
    [Fact]
    public void ATmslModelDefinitionIsReportedAsALimitation()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource("Sales", ("model.bim", "{}")));

        var limitation = Assert.Single(inventory.AnalysisLimitations);
        Assert.Equal("PBI-LIMIT-MODEL-TMSL", limitation.LimitationId);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
    }

    /// <summary>
    /// TMDL view editor scripts live inside the semantic-model folder and carry the .tmdl extension, so
    /// they are enumerated as definition artifacts, but they are not model definition and must not be
    /// reported as unanalysed semantic constructs.
    /// </summary>
    [Fact]
    public void EditorScriptsDoNotProduceLimitations()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("TMDLScripts/Untitled 1.tmdl", "table Scratch")));

        Assert.Empty(inventory.AnalysisLimitations);
    }

    [Fact]
    public void PackagingArtifactsDoNotProduceLimitations()
    {
        // definition.pbism is contributed by the shared model-source helper.
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales")));

        Assert.Empty(inventory.AnalysisLimitations);
    }

    [Fact]
    public void TheDatabaseDefinitionIsRecordedRatherThanTreatedAsPackaging()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/database.tmdl", "database Sales")));

        var limitation = Assert.Single(inventory.AnalysisLimitations);
        Assert.Equal("PBI-LIMIT-MODEL-DATABASE", limitation.LimitationId);
        // Recorded as unanalysed, but every observed instance holds only a compatibility level, so it
        // cannot invalidate a usage conclusion.
        Assert.Equal(ConstructSupportStates.NotYetAnalyzed, limitation.SupportState);
        Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
    }

    [Fact]
    public void DocumentedRootDefinitionFilesAreRecognizedRatherThanUnknown()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/dataSources.tmdl", string.Empty),
            ("definition/functions.tmdl", string.Empty)));

        Assert.Equal(2, inventory.AnalysisLimitations.Count);
        Assert.DoesNotContain(
            inventory.AnalysisLimitations,
            limitation => limitation.SupportState == ConstructSupportStates.Unrecognized);
        Assert.Contains(inventory.AnalysisLimitations, limitation => limitation.ConstructType == "dataSource");
        Assert.Contains(inventory.AnalysisLimitations, limitation => limitation.ConstructType == "function");
    }

    [Fact]
    public void AProjectWithOnlySupportedArtifactsHasNoLimitations()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/relationships.tmdl", string.Empty),
            ("definition/expressions.tmdl", string.Empty)));

        Assert.Empty(inventory.AnalysisLimitations);
    }

    [Fact]
    public void LimitationsAreScopedToTheSemanticModelThatContainsThem()
    {
        var files = new List<ProjectFileContent>
        {
            File("Sales.pbip", "{}"),
        };
        files.AddRange(ModelFiles("WithRoles", ("definition/tables/Sales.tmdl", "table Sales"), ("definition/roles.tmdl", "role Reader")));
        files.AddRange(ModelFiles("WithoutRoles", ("definition/tables/Other.tmdl", "table Other")));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));

        var limitation = Assert.Single(inventory.AnalysisLimitations);
        Assert.Equal("WithRoles", limitation.SemanticModel);
        Assert.StartsWith("WithRoles.SemanticModel/", limitation.ArtifactPath, StringComparison.Ordinal);
    }

    [Fact]
    public void AProjectWithNoSemanticModelHasNoLimitations()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Report only", [
            File("Sales.pbip", "{}"),
            File("Sales.Report/definition.pbir", "{}"),
        ]));

        Assert.Empty(inventory.AnalysisLimitations);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static readonly string[] KnownClassifications =
    [
        ConstructClassifications.Analyzed,
        ConstructClassifications.SemanticNotYetAnalyzed,
        ConstructClassifications.Packaging,
        ConstructClassifications.Unrecognized,
    ];

    private static readonly string[] KnownSupportStates =
    [
        ConstructSupportStates.Analyzed,
        ConstructSupportStates.NotYetAnalyzed,
        ConstructSupportStates.PartiallyAnalyzed,
        ConstructSupportStates.Unrecognized,
    ];

    private static readonly string[] KnownDependencyImpacts =
    [
        ConstructDependencyImpacts.MayCreateDependencies,
        ConstructDependencyImpacts.DependencyEffectUnknown,
        ConstructDependencyImpacts.NoKnownDependencyEffect,
        ConstructDependencyImpacts.MayInvalidateExistingEvidence,
    ];

    private static void AssertLimitationEmission(string classification, bool expectLimitation)
    {
        var rules = SemanticDefinitionFileRegistry.Rules
            .Where(rule => rule.Classification == classification)
            .ToArray();

        Assert.NotEmpty(rules);

        foreach (var rule in rules)
        {
            var path = RepresentativePath(rule);
            var limitations = DetectFor(path);

            if (expectLimitation)
            {
                var limitation = Assert.Single(limitations);
                Assert.Equal(rule.LimitationId, limitation.LimitationId);
                Assert.Equal(rule.ConstructType, limitation.ConstructType);
            }
            else
            {
                Assert.Empty(limitations);
            }
        }
    }

    /// <summary>
    /// Runs detection directly against a single model-relative definition path. This works on paths
    /// rather than file content, so registry rules can be exercised without authoring valid TMDL for
    /// every construct.
    /// </summary>
    private static AnalysisLimitation[] DetectFor(string modelRelativePath)
    {
        var source = new InMemoryProjectFileSource("Detect", [
            File($"Sales.SemanticModel/{modelRelativePath}", "content"),
        ]);
        var artifacts = new[]
        {
            new ArtifactInventory(ArtifactKinds.SemanticModel, "Sales", "Sales.SemanticModel", DefinitionFileCount: 1),
        };

        return AnalysisLimitationDetector.Detect(source, artifacts);
    }

    private static string RepresentativePath(SemanticDefinitionFileRule rule) =>
        rule.MatchKind == SemanticDefinitionFileMatch.ExactPath
            ? rule.Pattern
            : $"{rule.Pattern}/Representative.tmdl";

    private static InMemoryProjectFileSource BuildModelSource(
        string modelName,
        params (string RelativePath, string Content)[] files)
    {
        var contents = new List<ProjectFileContent> { File($"{modelName}.pbip", "{}") };
        contents.AddRange(ModelFiles(modelName, files));
        return new InMemoryProjectFileSource(modelName, contents);
    }

    private static IEnumerable<ProjectFileContent> ModelFiles(
        string modelName,
        params (string RelativePath, string Content)[] files)
    {
        yield return File($"{modelName}.SemanticModel/definition.pbism", "{}");
        foreach (var file in files)
        {
            yield return File($"{modelName}.SemanticModel/{file.RelativePath}", file.Content);
        }
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
}
