using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Report-format schema declarations describe the evidence PBI Assure has for its PBIR parser. They
/// must remain observational: a new declaration can be useful to show without preventing the
/// property-wise parser from reading the report.
/// </summary>
public sealed class PbirSchemaObservationTests
{
    [Fact]
    public void DesktopAuthoredFixturePinsTheVerifiedReportSideBaselines()
    {
        var root = Path.Combine(RepositoryRoot(), "tests", "fixtures", "desktop-udf-measure-consumer");
        var inventory = ProjectScanner.Scan(root);
        var observations = Assert.Single(inventory.Reports).SchemaObservations;

        AssertAllExact(observations, ReportSchemaArtifactKinds.DefinitionProperties, "definitionProperties", "2.0.0");
        AssertAllExact(observations, ReportSchemaArtifactKinds.VersionMetadata, "versionMetadata", "1.0.0");
        AssertAllExact(observations, ReportSchemaArtifactKinds.Report, "report", "3.3.0");
        AssertAllExact(observations, ReportSchemaArtifactKinds.PagesMetadata, "pagesMetadata", "1.1.0");
        AssertAllExact(observations, ReportSchemaArtifactKinds.Page, "page", "2.1.0");
        AssertAllExact(observations, ReportSchemaArtifactKinds.VisualContainer, "visualContainer", "2.11.0");
    }

    [Fact]
    public void DesktopAuthoredOlsFixtureAddsASecondExactVisualContainerBaseline()
    {
        var root = Path.Combine(RepositoryRoot(), "tests", "fixtures", "desktop-ols-evidence");
        var observations = Assert.Single(ProjectScanner.Scan(root).Reports).SchemaObservations;

        AssertAllExact(observations, ReportSchemaArtifactKinds.VisualContainer, "visualContainer", "2.12.0");
    }

    [Theory]
    [InlineData("3.2.9")]
    [InlineData("3.3.1")]
    [InlineData("3.4.0")]
    [InlineData("4.0.0")]
    public void RecognisedButUnverifiedReportVersionsContinueToParse(string version)
    {
        var exact = Scan();
        var unverified = Scan(reportSchema: Schema("report", version, definitionArtifact: true));
        var observation = Observation(unverified, ReportSchemaArtifactKinds.Report);

        Assert.Equal(ReportSchemaObservationStates.RecognisedUnverifiedVersion, observation.State);
        Assert.Equal(version, observation.SchemaVersion);
        Assert.Equal("3.3.0", observation.VerifiedBaselineVersion);
        Assert.Equal(exact.Reports[0].PageCount, unverified.Reports[0].PageCount);
        Assert.Equal(exact.Reports[0].VisualCount, unverified.Reports[0].VisualCount);
        Assert.Equal(exact.Reports[0].LandingPageName, unverified.Reports[0].LandingPageName);
        Assert.Equal(exact.Reports[0].Theme.BaseSource.ThemeName, unverified.Reports[0].Theme.BaseSource.ThemeName);
        Assert.Equal(
            exact.Findings.Select(finding => (finding.RuleId, finding.Message, finding.Severity)),
            unverified.Findings.Select(finding => (finding.RuleId, finding.Message, finding.Severity)));
        Assert.Equal(
            exact.SemanticObjectUsages.Select(usage => (usage.SemanticModel, usage.Table, usage.ObjectName, usage.UsageState)),
            unverified.SemanticObjectUsages.Select(usage => (usage.SemanticModel, usage.Table, usage.ObjectName, usage.UsageState)));
    }

    [Fact]
    public void UnknownMissingAndMalformedSchemaMetadataRemainDistinct()
    {
        var unknown = Observation(Scan(reportSchema: Schema("unexpectedReport", "1.0.0", definitionArtifact: true)), ReportSchemaArtifactKinds.Report);
        var missing = Observation(Scan(includeReportSchema: false), ReportSchemaArtifactKinds.Report);
        var nonString = Observation(Scan(reportSchema: "42", reportSchemaIsJson: true), ReportSchemaArtifactKinds.Report);
        var malformed = Observation(Scan(reportSchema: "not a schema URI"), ReportSchemaArtifactKinds.Report);

        Assert.Equal(ReportSchemaObservationStates.UnknownFamily, unknown.State);
        Assert.Equal("unexpectedReport", unknown.SchemaFamily);
        Assert.Equal(ReportSchemaObservationStates.MetadataMissing, missing.State);
        Assert.Equal(ReportSchemaObservationStates.MetadataMalformed, nonString.State);
        Assert.Equal("42", nonString.RawSchemaUri);
        Assert.Equal(ReportSchemaObservationStates.MetadataMalformed, malformed.State);

        Assert.Throws<InvalidDataException>(() => Scan(reportJsonOverride: "{ definitely not JSON"));
    }

    [Fact]
    public void VersionMetadataKeepsSchemaEvidenceSeparateFromPbirDefinitionVersion()
    {
        var report = Assert.Single(Scan().Reports);
        var observation = Assert.Single(report.SchemaObservations, observation =>
            observation.ArtifactKind == ReportSchemaArtifactKinds.VersionMetadata);

        Assert.Equal("2.0.0", report.PbirDefinitionVersion);
        Assert.EndsWith("definition/version.json", report.VersionMetadataPath, StringComparison.Ordinal);
        Assert.Equal("versionMetadata", observation.SchemaFamily);
        Assert.Equal("1.0.0", observation.SchemaVersion);
        Assert.Equal(ReportSchemaObservationStates.VerifiedExact, observation.State);
        Assert.Equal("4.0", report.ModelConnection.Version);
    }

    [Fact]
    public void DesktopAuthoredBookmarkFixturesPinExactBookmarkSchemaBaselines()
    {
        foreach (var fixture in new[]
                 {
                     "desktop-bookmark-evidence-stale",
                     "desktop-bookmark-evidence-live-carrier",
                 })
        {
            var root = Path.Combine(RepositoryRoot(), "tests", "fixtures", fixture);
            var observations = Assert.Single(ProjectScanner.Scan(root).Reports).SchemaObservations;

            AssertAllExact(observations, ReportSchemaArtifactKinds.BookmarksMetadata, "bookmarksMetadata", "1.0.0");
            AssertAllExact(observations, ReportSchemaArtifactKinds.Bookmark, "bookmark", "2.1.0");
        }
    }

    [Fact]
    public void OlderBookmarkSchemasAndReportExtensionsRemainRecognisedButUnverified()
    {
        var report = Assert.Single(Scan(includeBookmarksAndExtensions: true).Reports);

        Assert.Equal(ReportSchemaObservationStates.VerifiedExact,
            Assert.Single(report.SchemaObservations,
                observation => observation.ArtifactKind == ReportSchemaArtifactKinds.BookmarksMetadata).State);
        Assert.All(report.SchemaObservations.Where(observation => observation.ArtifactKind is
            ReportSchemaArtifactKinds.Bookmark or
            ReportSchemaArtifactKinds.ReportExtension), observation =>
            Assert.Equal(ReportSchemaObservationStates.RecognisedUnverifiedVersion, observation.State));
    }

    [Fact]
    public void DifferentBookmarkSchemaVersionsRemainRecognisedButUnverified()
    {
        var report = Assert.Single(Scan(
            includeBookmarksAndExtensions: true,
            bookmarksMetadataVersion: "1.1.0",
            bookmarkVersion: "2.2.0").Reports);

        Assert.All(report.SchemaObservations.Where(observation => observation.ArtifactKind is
            ReportSchemaArtifactKinds.BookmarksMetadata or
            ReportSchemaArtifactKinds.Bookmark), observation =>
            Assert.Equal(ReportSchemaObservationStates.RecognisedUnverifiedVersion, observation.State));
    }

    [Fact]
    public void ExactObservationsAreSilentButNonExactObservationsAppearAsCoverageNotFindings()
    {
        var exact = Scan();
        var unverified = Scan(reportSchema: Schema("report", "3.4.0", definitionArtifact: true));

        var exactHtml = HtmlReportRenderer.Render(exact);
        var unverifiedHtml = HtmlReportRenderer.Render(unverified);

        Assert.DoesNotContain(Assert.Single(exact.Reports).SchemaObservations, observation =>
            observation.State != ReportSchemaObservationStates.VerifiedExact);
        Assert.DoesNotContain("PBI Assure recorded report-format metadata it has not verified exactly.", exactHtml, StringComparison.Ordinal);
        Assert.Contains("id=\"analysis-coverage\"", unverifiedHtml, StringComparison.Ordinal);
        Assert.Contains("Report definition", unverifiedHtml, StringComparison.Ordinal);
        Assert.Contains("PBI Assure has not verified this report-format version yet.", unverifiedHtml, StringComparison.Ordinal);
        Assert.Equal(
            exact.Findings.Select(finding => (finding.RuleId, finding.Message, finding.Severity)),
            unverified.Findings.Select(finding => (finding.RuleId, finding.Message, finding.Severity)));
        Assert.DoesNotContain(unverified.Findings, finding => finding.RuleId.StartsWith("PBI-COMPAT", StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatedVisualObservationsAreGroupedAndRawEvidenceIsEscaped()
    {
        var inventory = Scan(
            visualSchema: "<not-a-schema-uri>",
            visualCount: 2);
        var report = Assert.Single(inventory.Reports);
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(2, report.SchemaObservations.Count(observation =>
            observation.ArtifactKind == ReportSchemaArtifactKinds.VisualContainer));
        Assert.Equal(1, CountOccurrences(html, ">Visual definitions<"));
        Assert.Contains("&lt;not-a-schema-uri&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<not-a-schema-uri>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaCoverageRemainsScopedToTheReportThatDeclaredIt()
    {
        var files = Files("First", Schema("report", "3.4.0", definitionArtifact: true), visualCount: 1)
            .Concat(Files("Second", Schema("report", "3.3.0", definitionArtifact: true), visualCount: 1))
            .ToArray();
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two reports", files));
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Equal(2, inventory.Reports.Count);
        Assert.Contains("Report: First", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Report: Second</h3>", html, StringComparison.Ordinal);
        Assert.Single(inventory.Reports.Single(report => report.Name == "First").SchemaObservations, observation =>
            observation.ArtifactKind == ReportSchemaArtifactKinds.Report &&
            observation.State == ReportSchemaObservationStates.RecognisedUnverifiedVersion);
        Assert.DoesNotContain(inventory.Reports.Single(report => report.Name == "Second").SchemaObservations, observation =>
            observation.State != ReportSchemaObservationStates.VerifiedExact);
    }

    [Fact]
    public void SchemaObservationsAreAnAdditiveJsonInventoryContractAndCsvIsUnaffected()
    {
        var inventory = Scan(reportSchema: Schema("report", "3.4.0", definitionArtifact: true));
        var json = System.Text.Json.JsonSerializer.Serialize(inventory);

        Assert.Contains("\"SchemaVersion\":\"0.25\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SchemaObservations\"", json, StringComparison.Ordinal);
        Assert.Contains("\"RecognisedUnverifiedVersion\"", json, StringComparison.Ordinal);
    }

    private static ProjectInventory Scan(
        string? reportSchema = null,
        bool reportSchemaIsJson = false,
        bool includeReportSchema = true,
        int visualCount = 1,
        string? visualSchema = null,
        bool includeBookmarksAndExtensions = false,
        string bookmarksMetadataVersion = "1.0.0",
        string bookmarkVersion = "1.0.0",
        string? reportJsonOverride = null) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource(
            "Schema observations",
            Files("Fixture", reportSchema, reportSchemaIsJson, includeReportSchema, visualCount, visualSchema, includeBookmarksAndExtensions, bookmarksMetadataVersion, bookmarkVersion, reportJsonOverride)));

    private static IEnumerable<ProjectFileContent> Files(
        string name,
        string? reportSchema = null,
        bool reportSchemaIsJson = false,
        bool includeReportSchema = true,
        int visualCount = 1,
        string? visualSchema = null,
        bool includeBookmarksAndExtensions = false,
        string bookmarksMetadataVersion = "1.0.0",
        string bookmarkVersion = "1.0.0",
        string? reportJsonOverride = null)
    {
        yield return File($"{name}.pbip", "{}");
        yield return File($"{name}.Report/definition.pbir", $$"""{ "$schema": "{{Schema("definitionProperties", "2.0.0")}}", "version": "4.0" }""");
        yield return File($"{name}.Report/definition/version.json", $$"""{ "$schema": "{{Schema("versionMetadata", "1.0.0", definitionArtifact: true)}}", "version": "2.0.0" }""");
        yield return File($"{name}.Report/definition/report.json", reportJsonOverride ?? ReportJson(
            includeReportSchema ? reportSchema ?? Schema("report", "3.3.0", definitionArtifact: true) : null,
            reportSchemaIsJson));
        yield return File($"{name}.Report/definition/pages/pages.json", $$"""{ "$schema": "{{Schema("pagesMetadata", "1.1.0", definitionArtifact: true)}}", "pageOrder": ["page"], "activePageName": "page", "landingPageName": "page" }""");
        yield return File($"{name}.Report/definition/pages/page/page.json", $$"""{ "$schema": "{{Schema("page", "2.1.0", definitionArtifact: true)}}", "name": "page", "displayName": "Overview" }""");
        for (var index = 1; index <= visualCount; index++)
        {
            yield return File($"{name}.Report/definition/pages/page/visuals/v{index}/visual.json", $$"""{ "$schema": "{{visualSchema ?? Schema("visualContainer", "2.11.0", definitionArtifact: true)}}", "name": "v{{index}}", "position": { "x": {{index * 10}}, "y": 0, "width": 100, "height": 100 }, "visual": { "visualType": "card" } }""");
        }

        yield return File($"{name}.Report/StaticResources/SharedResources/BaseThemes/Fixture.json", "{ \"name\": \"Fixture theme\", \"dataColors\": [\"#111111\"] }");

        if (includeBookmarksAndExtensions)
        {
            yield return File($"{name}.Report/definition/bookmarks/bookmarks.json", $$"""{ "$schema": "{{Schema("bookmarksMetadata", bookmarksMetadataVersion, definitionArtifact: true)}}", "bookmarkOrder": ["bookmark"] }""");
            yield return File($"{name}.Report/definition/bookmarks/bookmark/bookmark.json", $$"""{ "$schema": "{{Schema("bookmark", bookmarkVersion, definitionArtifact: true)}}", "name": "bookmark", "displayName": "Bookmark" }""");
            yield return File($"{name}.Report/definition/reportExtensions.json", $$"""{ "$schema": "{{Schema("reportExtension", "1.0.0", definitionArtifact: true)}}", "entities": [] }""");
        }
    }

    private static string ReportJson(string? schema, bool schemaIsJson)
    {
        var schemaProperty = schema is null
            ? string.Empty
            : schemaIsJson ? $"\"$schema\": {schema}," : $"\"$schema\": \"{schema}\",";
        return $$"""
            { {{schemaProperty}}
              "themeCollection": {
                "baseTheme": { "name": "Fixture", "type": "SharedResources" }
              },
              "resourcePackages": [
                { "name": "SharedResources", "type": "SharedResources", "items": [
                  { "name": "Fixture", "path": "BaseThemes/Fixture.json", "type": "BaseTheme" }
                ] }
              ]
            }
            """;
    }

    private static string Schema(string family, string version, bool definitionArtifact = false) =>
        $"https://developer.microsoft.com/json-schemas/fabric/item/report/{(definitionArtifact ? "definition/" : string.Empty)}{family}/{version}/schema.json";

    private static ReportSchemaObservation Observation(ProjectInventory inventory, string artifactKind) =>
        Assert.Single(Assert.Single(inventory.Reports).SchemaObservations, observation => observation.ArtifactKind == artifactKind);

    private static void AssertAllExact(
        IEnumerable<ReportSchemaObservation> observations,
        string artifactKind,
        string family,
        string version)
    {
        var matching = observations.Where(observation => observation.ArtifactKind == artifactKind).ToArray();
        Assert.NotEmpty(matching);
        Assert.All(matching, observation =>
        {
            Assert.Equal(ReportSchemaObservationStates.VerifiedExact, observation.State);
            Assert.Equal(family, observation.SchemaFamily);
            Assert.Equal(version, observation.SchemaVersion);
        });
    }

    private static ProjectFileContent File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));

    private static int CountOccurrences(string value, string search) =>
        value.Split(search, StringSplitOptions.None).Length - 1;

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
