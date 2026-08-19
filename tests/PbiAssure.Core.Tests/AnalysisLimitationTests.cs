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

    [Fact]
    public void NoTwoRegistryRulesShareAPattern()
    {
        var duplicates = SemanticDefinitionFileRegistry.Rules
            .GroupBy(rule => $"{rule.MatchKind}|{rule.Pattern}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
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
        var source = BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/relationships.tmdl", string.Empty),
            ("definition/expressions.tmdl", string.Empty),
            ("definition/roles.tmdl", "role Reader"),
            ("definition/model.tmdl", "model Model"),
            ("definition/perspectives.tmdl", "perspective P"),
            ("definition/cultures/en-US.tmdl", "cultureInfo en-US"),
            ("definition/database.tmdl", "database Sales"),
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

            Assert.Equal(shouldBeLimited, limitedPaths.Contains(path));
        }

        // Nothing may be reported that is not part of the classified universe.
        Assert.All(limitedPaths, path => Assert.Contains(path, definitionArtifacts));
    }

    // ---- Integration through ProjectScanner -------------------------------------------------

    [Fact]
    public void RoleMetadataIsReportedAsALimitationAgainstItsOwnFile()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/roles.tmdl", "role Reader")));

        var limitation = Assert.Single(inventory.AnalysisLimitations);
        Assert.Equal("PBI-LIMIT-MODEL-ROLE", limitation.LimitationId);
        Assert.Equal("Sales.SemanticModel/definition/roles.tmdl", limitation.ArtifactPath);
        Assert.Equal("Sales", limitation.SemanticModel);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);
        Assert.Equal(AnalysisLimitationCauses.ConstructNotSupported, limitation.Cause);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Contains(AnalysisConcerns.Security, limitation.Concerns);
        Assert.Null(limitation.Table);
        Assert.Null(limitation.ObjectName);
    }

    [Fact]
    public void PackagingArtifactsDoNotProduceLimitations()
    {
        var inventory = ProjectScanner.Scan(BuildModelSource(
            "Sales",
            ("definition/tables/Sales.tmdl", "table Sales"),
            ("definition/database.tmdl", "database Sales")));

        Assert.Empty(inventory.AnalysisLimitations);
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
