using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Registry verification against a real Power BI Desktop-authored semantic model.
///
/// <see cref="AnalysisLimitationTests"/> proves the registry is internally consistent using synthetic
/// paths. This file proves the registry's declared paths match what Power BI Desktop actually emits, for
/// the constructs whose shape was previously only established from Microsoft documentation: roles,
/// perspectives, cultures, DAX user-defined functions, and the model-level files present in every model.
///
/// The fixture is preserved Desktop output. See its README for provenance and for what it does and does
/// not prove. Deliberately absent: any test asserting that a column referenced only by a row-level
/// security filter is ApparentlyUnused. That is the known current deficiency, not desired behaviour.
/// </summary>
public sealed class DesktopSemanticConstructsFixtureTests
{
    /// <summary>
    /// The invariant that gives this fixture its purpose: nothing Power BI Desktop emitted may fall
    /// through to the unrecognised fallback. A failure here means Desktop emits a path the registry does
    /// not know about.
    /// </summary>
    [Fact]
    public void EveryDesktopEmittedDefinitionArtifactIsClassifiedByADeclaredRegistryRule()
    {
        var unclassified = DesktopDefinitionArtifacts()
            .Where(artifact => SemanticDefinitionFileRegistry
                .Classify(artifact.ModelRelativePath)
                .Classification == ConstructClassifications.Unrecognized)
            .Select(artifact => artifact.ModelRelativePath)
            .ToArray();

        Assert.Empty(unclassified);
    }

    [Fact]
    public void EveryDesktopEmittedDefinitionArtifactMatchesExactlyOneRegistryRule()
    {
        foreach (var artifact in DesktopDefinitionArtifacts())
        {
            Assert.Single(SemanticDefinitionFileRegistry.MatchingRules(artifact.ModelRelativePath));
        }
    }

    /// <summary>
    /// Pins the emitted shape of each construct. These are the paths that were documentation-only before
    /// this fixture existed; if a future Desktop version moves any of them, this fails.
    /// </summary>
    [Theory]
    [InlineData("definition/roles/RegionalManager.tmdl", "role", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/roles/DynamicUser.tmdl", "role", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/perspectives/SalesView.tmdl", "perspective", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/cultures/en-US.tmdl", "culture", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/functions.tmdl", "function", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/model.tmdl", "modelDefinition", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/database.tmdl", "database", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/tables/Sales.tmdl", "table", ConstructClassifications.Analyzed)]
    [InlineData("definition/relationships.tmdl", "relationship", ConstructClassifications.Analyzed)]
    [InlineData("definition.pbism", "semanticModelSettings", ConstructClassifications.Packaging)]
    [InlineData("TMDLScripts/Script 1.tmdl", "tmdlEditorScript", ConstructClassifications.Packaging)]
    public void DesktopEmittedPathsClassifyAsTheRegistryDeclares(
        string modelRelativePath,
        string expectedConstructType,
        string expectedClassification)
    {
        // The fixture must actually contain the path, so this cannot pass against a path Desktop no
        // longer emits.
        Assert.Contains(
            DesktopDefinitionArtifacts(),
            artifact => string.Equals(
                artifact.ModelRelativePath, modelRelativePath, StringComparison.OrdinalIgnoreCase));

        var rule = SemanticDefinitionFileRegistry.Classify(modelRelativePath);

        Assert.Equal(expectedConstructType, rule.ConstructType);
        Assert.Equal(expectedClassification, rule.Classification);
    }

    /// <summary>
    /// Roles are one file per object, not a single aggregate file. Both role files are fully accounted
    /// for by the parser, so neither produces a hypothetical role coverage item.
    /// </summary>
    [Fact]
    public void FullyAccountedRoleFilesProduceNoRoleCoverageItems()
    {
        var roleLimitations = ScanFixture().AnalysisLimitations
            .Where(limitation => limitation.ConstructType == "role")
            .ToArray();

        Assert.Empty(roleLimitations);
    }

    /// <summary>
    /// TMDL view editor scripts carry the .tmdl extension inside the semantic-model folder, so they are
    /// enumerated as definition artifacts, but they are not model definition and must not be reported.
    /// </summary>
    [Fact]
    public void TmdlViewEditorScriptsProduceNoLimitation()
    {
        var scripts = DesktopDefinitionArtifacts()
            .Where(artifact => artifact.ModelRelativePath.StartsWith("TMDLScripts/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(scripts);
        Assert.DoesNotContain(
            ScanFixture().AnalysisLimitations,
            limitation => limitation.ArtifactPath.Contains("TMDLScripts", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Power BI Desktop emits model.tmdl, database.tmdl and a culture file for every model, so if any of
    /// them could invalidate a usage conclusion then every model would have to be caveated and the
    /// signal would be worthless. Each is still reported as unanalysed; none of them qualifies anything.
    /// </summary>
    [Fact]
    public void TheFilesEmittedForEveryModelAreRecordedButCarryNoDependencyImpact()
    {
        var alwaysPresent = new[] { "modelDefinition", "database", "culture" };

        var limitations = ScanFixture().AnalysisLimitations
            .Where(limitation => alwaysPresent.Contains(limitation.ConstructType, StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(alwaysPresent.Length, limitations.Length);
        Assert.All(limitations, limitation =>
        {
            // Still visible to the user as metadata that was not analysed.
            Assert.Equal(ConstructClassifications.SemanticNotYetAnalyzed, SemanticDefinitionFileRegistry
                .Classify(ModelRelative(limitation.ArtifactPath)).Classification);
            // But unable to caveat a usage conclusion.
            Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
        });
    }

    /// <summary>
    /// The constructs that genuinely can reference model objects must keep saying so, otherwise the
    /// previous assertion would be trivially satisfiable by neutering every impact value.
    /// </summary>
    /// <summary>
    /// Roles and perspectives are absent here: their object-naming content is analysed and the fixture's
    /// files contain nothing else that could reference an object, so their impact is narrowed by artifact
    /// evidence. Function metadata is still unread, so it must keep saying so — otherwise the previous
    /// assertion would be satisfiable by neutering every impact value.
    /// </summary>
    [Fact]
    public void ConstructsThatCanReferenceModelObjectsStillCarryDependencyImpact()
    {
        var limitation = Assert.Single(
            ScanFixture().AnalysisLimitations,
            item => item.ConstructType == "function");

        Assert.Equal(
            ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
    }

    /// <summary>
    /// The auto date-table relationship Desktop generated makes the Date column structurally required.
    /// This pins the established generated-artefact semantics against real Desktop output rather than a
    /// hand-built model.
    /// </summary>
    [Fact]
    public void TheAutoDateTableRelationshipMakesTheSourceDateColumnStructurallyRequired()
    {
        var usage = Assert.Single(
            ScanFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == "Date");

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private sealed record DefinitionArtifact(string RelativePath, string ModelRelativePath);

    /// <summary>
    /// Enumerates the fixture's definition artifacts using the same universe production uses — the
    /// scanner's own artifact discovery and the registry's own extension set — so this cannot drift into
    /// a separate hardcoded approximation of what counts as a definition artifact.
    /// </summary>
    private static DefinitionArtifact[] DesktopDefinitionArtifacts()
    {
        var source = new PhysicalProjectFileSource(FixturePath());
        var inventory = ScanFixture();
        var artifact = Assert.Single(
            inventory.Artifacts,
            a => a.Kind == ArtifactKinds.SemanticModel);
        var prefix = artifact.RelativePath.TrimEnd('/') + "/";

        return source.EnumerateFiles(artifact.RelativePath)
            .Where(file => SemanticDefinitionFileRegistry.IsDefinitionArtifact(file.RelativePath))
            .Select(file => new DefinitionArtifact(
                file.RelativePath,
                file.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? file.RelativePath[prefix.Length..]
                    : file.RelativePath))
            .ToArray();
    }

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(FixturePath());

    private static string ModelRelative(string artifactPath) =>
        artifactPath["desktop-semantic-constructs.SemanticModel/".Length..];

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(),
        "tests",
        "fixtures",
        "desktop-semantic-constructs");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
