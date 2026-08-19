using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Registry verification against the second real Power BI Desktop-authored semantic model.
///
/// <see cref="DesktopSemanticConstructsFixtureTests"/> covers roles, perspectives and cultures. This
/// fixture exists for one thing the first could not show: how a DAX user-defined function body writes a
/// reference to a table, a column and a measure. The first fixture's function used only its own
/// parameter, which proved serialization but not reference syntax.
///
/// <see cref="FunctionDependencyTests"/> asserts what the functions mean. This file asserts the shape of
/// what Desktop emitted, so a future Desktop version moving or renaming anything fails here rather than
/// silently reducing coverage.
///
/// The fixture is preserved Desktop output. See its README for provenance and limits.
/// </summary>
public sealed class DesktopUdfReferencesFixtureTests
{
    /// <summary>
    /// The invariant that gives this fixture its purpose: nothing Desktop emitted may fall through to the
    /// unrecognised fallback.
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
    /// Pins the emitted paths. This model has no roles and no perspectives, so it also confirms that
    /// those files are absent rather than empty when the author declared none.
    /// </summary>
    [Theory]
    [InlineData("definition/functions.tmdl", "function", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/cultures/en-US.tmdl", "culture", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/model.tmdl", "modelDefinition", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/database.tmdl", "database", ConstructClassifications.SemanticNotYetAnalyzed)]
    [InlineData("definition/tables/Sales.tmdl", "table", ConstructClassifications.Analyzed)]
    [InlineData("definition.pbism", "semanticModelSettings", ConstructClassifications.Packaging)]
    [InlineData("TMDLScripts/Script 1.tmdl", "tmdlEditorScript", ConstructClassifications.Packaging)]
    public void DesktopEmittedPathsClassifyAsTheRegistryDeclares(
        string modelRelativePath,
        string expectedConstructType,
        string expectedClassification)
    {
        Assert.Contains(
            DesktopDefinitionArtifacts(),
            artifact => string.Equals(
                artifact.ModelRelativePath, modelRelativePath, StringComparison.OrdinalIgnoreCase));

        var rule = SemanticDefinitionFileRegistry.Classify(modelRelativePath);

        Assert.Equal(expectedConstructType, rule.ConstructType);
        Assert.Equal(expectedClassification, rule.Classification);
    }

    /// <summary>
    /// Five functions were authored through the TMDL view, each in its own script. All five must survive
    /// the round trip into a single functions.tmdl; a parser that stopped at the first declaration would
    /// still satisfy a weaker "functions are parsed" assertion.
    /// </summary>
    [Fact]
    public void AllFiveAuthoredFunctionsSurviveIntoOneDefinitionFile()
    {
        var model = Assert.Single(ScanFixture().SemanticModels);

        Assert.Equal(5, model.FunctionCount);
        Assert.All(model.Functions, function =>
            Assert.EndsWith("definition/functions.tmdl", function.RelativePath, StringComparison.Ordinal));
    }

    /// <summary>
    /// The five TMDL view scripts that authored the functions are preserved so the fixture records how it
    /// was made, but they are editor state, not model definition, and must produce no limitation.
    /// </summary>
    [Fact]
    public void TmdlViewEditorScriptsProduceNoLimitation()
    {
        var scripts = DesktopDefinitionArtifacts()
            .Where(artifact => artifact.ModelRelativePath.StartsWith("TMDLScripts/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(5, scripts.Length);
        Assert.DoesNotContain(
            ScanFixture().AnalysisLimitations,
            limitation => limitation.ArtifactPath.Contains("TMDLScripts", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Functions are now analysed as definitions, but a UDF can also be called from a visual calculation
    /// or a report-level measure and neither is read, so the limitation must stay and must keep saying
    /// that dependencies may exist. Narrowing this to NoKnownDependencyEffect would claim coverage of
    /// consumers that are not read.
    /// </summary>
    [Fact]
    public void TheFunctionLimitationIsStillEmittedAndStillQualifies()
    {
        var limitation = Assert.Single(
            ScanFixture().AnalysisLimitations,
            item => item.ConstructType == "function");

        Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
    }

    /// <summary>
    /// model.tmdl carries a ref line for every table and culture but none for functions. Nothing may
    /// depend on a ref function line existing.
    /// </summary>
    [Fact]
    public void ModelTmdlCarriesNoRefFunctionLine()
    {
        var modelTmdl = System.IO.File.ReadAllText(Path.Combine(
            FixturePath(),
            "desktop-udf-references.SemanticModel",
            "definition",
            "model.tmdl"));

        Assert.Contains("ref table Sales", modelTmdl, StringComparison.Ordinal);
        Assert.DoesNotContain("ref function", modelTmdl, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private sealed record DefinitionArtifact(string RelativePath, string ModelRelativePath);

    /// <summary>
    /// Enumerates the fixture's definition artifacts using the same universe production uses — the
    /// scanner's own artifact discovery and the registry's own extension set.
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

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(),
        "tests",
        "fixtures",
        "desktop-udf-references");

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
