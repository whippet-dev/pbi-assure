using System.Text;
using System.Text.Json;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ConfiguredCustomThemeRuleTests
{
    [Fact]
    public void AbsentOrResolvedConfiguredCustomThemeDoesNotProduceFinding()
    {
        var baseOnly = Scan("BaseOnly", customThemeName: null, customItems: [], files: []);
        var sparseValid = Scan("Sparse", "Sparse.json", [Item("Sparse.json", "Sparse.json")],
            [File("Sparse.Report/StaticResources/RegisteredResources/Sparse.json", "{ \"name\": \"Sparse\" }")]);

        Assert.DoesNotContain(baseOnly.Findings, finding => finding.RuleId == "PBI-COMPAT-002");
        Assert.DoesNotContain(sparseValid.Findings, finding => finding.RuleId == "PBI-COMPAT-002");
        Assert.Equal(ThemeResolutionOutcomes.Resolved, Assert.Single(sparseValid.Reports).Theme.CustomSource!.ResolutionOutcome);
    }

    [Fact]
    public void MissingConfiguredPackageItemProducesScopedCompatibilityFinding()
    {
        var inventory = Scan("MissingPackage", "Missing.json", [], []);
        var theme = Assert.Single(inventory.Reports).Theme.CustomSource!;
        var finding = Assert.Single(inventory.Findings, item => item.RuleId == "PBI-COMPAT-002");

        Assert.Equal(ThemeResolutionOutcomes.PackageItemNotFound, theme.ResolutionOutcome);
        Assert.Equal("Configured custom theme unavailable", AssuranceRuleCatalog.Find("PBI-COMPAT-002")!.FriendlyName);
        Assert.Equal(AssuranceCategories.Compatibility, finding.Category);
        Assert.Equal(FindingSeverities.Warning, finding.Severity);
        Assert.Equal(AssessmentTypes.Finding, finding.AssessmentType);
        Assert.Equal("MissingPackage", finding.Report);
        Assert.Equal("Missing.json", finding.ObjectName);
        Assert.Equal(["$.themeCollection.customTheme"], finding.EvidencePaths);
        Assert.Contains("no matching resource package item", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Power BI Desktop", finding.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOrInvalidConfiguredThemeFileProducesFindingWithStructuredOutcome()
    {
        var missing = Scan("MissingFile", "Missing.json", [Item("Missing.json", "Missing.json")], []);
        var invalid = Scan("InvalidJson", "Broken.json", [Item("Broken.json", "Broken.json")],
            [File("InvalidJson.Report/StaticResources/RegisteredResources/Broken.json", "{ not json")]);

        Assert.Equal(ThemeResolutionOutcomes.ResourceFileMissing, Assert.Single(missing.Reports).Theme.CustomSource!.ResolutionOutcome);
        Assert.Equal(ThemeResolutionOutcomes.InvalidJson, Assert.Single(invalid.Reports).Theme.CustomSource!.ResolutionOutcome);
        Assert.Contains(missing.Findings, item => item.RuleId == "PBI-COMPAT-002" && item.Message.Contains("was not found", StringComparison.Ordinal));
        Assert.Contains(invalid.Findings, item => item.RuleId == "PBI-COMPAT-002" && item.Message.Contains("read as JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void AmbiguousConfiguredResourceProducesOneFindingInsteadOfChoosingAnArbitraryResource()
    {
        var inventory = Scan("Ambiguous", "Theme.json", [Item("Theme.json", "One.json"), Item("Theme.json", "Two.json")], []);
        var theme = Assert.Single(inventory.Reports).Theme.CustomSource!;

        Assert.Equal(ThemeResolutionOutcomes.AmbiguousPackageItem, theme.ResolutionOutcome);
        Assert.Null(theme.ResourcePath);
        Assert.Single(inventory.Findings, item => item.RuleId == "PBI-COMPAT-002");
    }

    [Fact]
    public void UnselectedRegisteredThemeResourceDoesNotProduceFinding()
    {
        var inventory = Scan("Selected", "Current.json", [Item("Current.json", "Current.json"), Item("Old.json", "Old.json")],
            [File("Selected.Report/StaticResources/RegisteredResources/Current.json", "{ \"name\": \"Current\" }")]);

        Assert.Equal(ThemeResolutionOutcomes.Resolved, Assert.Single(inventory.Reports).Theme.CustomSource!.ResolutionOutcome);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-COMPAT-002");
    }

    [Fact]
    public void FindingRemainsScopedToTheReportWithTheUnavailableConfiguredTheme()
    {
        var files = ReportFiles("Broken", "Missing.json", [], [])
            .Concat(ReportFiles("Valid", "Valid.json", [Item("Valid.json", "Valid.json")],
                [File("Valid.Report/StaticResources/RegisteredResources/Valid.json", "{ \"name\": \"Valid\" }")]));
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two reports", files));

        var finding = Assert.Single(inventory.Findings, item => item.RuleId == "PBI-COMPAT-002");
        Assert.Equal("Broken", finding.Report);
    }

    [Fact]
    public void FindingHtmlEscapesConfiguredThemeNameAndResolutionOutcomeIsAdditiveInInventoryJson()
    {
        var inventory = Scan("Escaped", "<Missing & Theme>.json", [], []);
        var html = HtmlReportRenderer.Render(inventory);
        var json = JsonSerializer.Serialize(Assert.Single(inventory.Reports).Theme.CustomSource);

        Assert.Contains("&lt;Missing &amp; Theme&gt;.json", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<Missing & Theme>.json", html, StringComparison.Ordinal);
        Assert.Contains("\"ResolutionOutcome\":\"PackageItemNotFound\"", json, StringComparison.Ordinal);
    }

    private static ProjectInventory Scan(
        string reportName,
        string? customThemeName,
        IReadOnlyList<(string Name, string Path)> customItems,
        IReadOnlyList<ProjectFileContent> files) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource(reportName, ReportFiles(reportName, customThemeName, customItems, files)));

    private static IEnumerable<ProjectFileContent> ReportFiles(
        string reportName,
        string? customThemeName,
        IReadOnlyList<(string Name, string Path)> customItems,
        IReadOnlyList<ProjectFileContent> customFiles)
    {
        var customTheme = customThemeName is null
            ? string.Empty
            : $", \"customTheme\": {{ \"name\": {JsonSerializer.Serialize(customThemeName)}, \"type\": \"RegisteredResources\" }}";
        var customPackage = customItems.Count == 0
            ? string.Empty
            : $", {{ \"name\": \"RegisteredResources\", \"type\": \"RegisteredResources\", \"items\": [{string.Join(", ", customItems.Select(item => $"{{ \"name\": {JsonSerializer.Serialize(item.Name)}, \"path\": {JsonSerializer.Serialize(item.Path)}, \"type\": \"CustomTheme\" }}"))}] }}";
        yield return File($"{reportName}.pbip", "{}");
        yield return File($"{reportName}.Report/definition.pbir", "{}");
        yield return File($"{reportName}.Report/definition/report.json", $$"""
            {
              "themeCollection": {
                "baseTheme": { "name": "Base", "type": "SharedResources" }{{customTheme}}
              },
              "resourcePackages": [
                { "name": "SharedResources", "type": "SharedResources", "items": [{ "name": "Base", "path": "BaseThemes/Base.json", "type": "BaseTheme" }] }{{customPackage}}
              ]
            }
            """);
        yield return File($"{reportName}.Report/StaticResources/SharedResources/BaseThemes/Base.json", "{ \"name\": \"Base\" }");
        foreach (var file in customFiles) yield return file;
    }

    private static (string Name, string Path) Item(string name, string path) => (name, path);

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
}
