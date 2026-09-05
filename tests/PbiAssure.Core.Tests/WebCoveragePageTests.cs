using System.Reflection;
using System.Text.RegularExpressions;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Truth maintenance for the public Coverage page.
///
/// Coverage makes specific, checkable claims about what this version analyses. Prose is written by
/// hand — a generator would not produce sentences like "does not determine query folding" — but the
/// load-bearing specifics are asserted here against production constants, so the day someone widens
/// a capability and forgets the page, the build says so.
///
/// Deliberately not asserted: the coverage manifest, which is a test fixture contract with internal
/// construct ids, and the counts that were kept out of public copy on purpose.
/// </summary>
public sealed class WebCoveragePageTests
{
    [Fact]
    public void CoverageIsARoutedScopePageAboutLocalProjectFiles()
    {
        var coverage = ReadWeb("Pages/Coverage.razor");

        Assert.Contains("@page \"/coverage\"", coverage, StringComparison.Ordinal);
        Assert.Contains("<h1 id=\"coverage-title\" tabindex=\"-1\">What PBI Assure analyses</h1>", coverage, StringComparison.Ordinal);

        // The scope rail, the legend and the closing boundary section are the page's frame.
        foreach (var frame in new[] { "coverage-scope", "How to read this page", "Where the boundary sits" })
        {
            Assert.Contains(frame, coverage, StringComparison.Ordinal);
        }

        // Four bands, each a section rather than a card.
        foreach (var band in new[] { "Model intelligence", "Query intelligence", "Report intelligence", "Reviews &amp; confidence" })
        {
            Assert.Contains(band, coverage, StringComparison.Ordinal);
        }

        // Every row must carry one of the two neutral labels, and boundaries must never become a third.
        Assert.Equal(
            Regex.Count(coverage, "class=\"coverage-row\""),
            Regex.Count(coverage, "class=\"coverage-status\""));
        Assert.DoesNotContain("coverage-status\">Unsupported", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("Needs persistence evidence", coverage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The five states are the product's identity. A sixth would need a decision about how to say it,
    /// not a silent omission from the page that explains them.
    /// </summary>
    [Fact]
    public void UsageStatesOnCoverageMatchProduction()
    {
        var coverage = ReadWeb("Pages/Coverage.razor");
        var produced = ConstantsOf(typeof(SemanticUsageStates));

        Assert.Equal(
            ["ApparentlyUnused", "DirectlyUsed", "IndirectlyUsed", "StructurallyRequired", "UsedOnlyByUnusedBranch"],
            produced.Order(StringComparer.Ordinal));

        foreach (var label in new[]
                 {
                     "Directly used", "Indirectly used", "Structurally required",
                     "Only used by unused items", "Apparently unused",
                 })
        {
            Assert.Contains($"<dt>{label}</dt>", coverage, StringComparison.Ordinal);
        }

        // The usage glyph vocabulary is reused here and nowhere else on the page.
        foreach (var glyph in new[] { "metric-used", "metric-indirect", "metric-structural", "metric-branch", "metric-unused" })
        {
            Assert.Contains(glyph, coverage, StringComparison.Ordinal);
        }

        Assert.Contains("It is not permission to delete the object.", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibilityChecksOnCoverageMatchTheRuleCatalog()
    {
        var coverage = ReadWeb("Pages/Coverage.razor");
        var rules = AssuranceRuleCatalog.ActiveRules
            .Where(rule => rule.Category == AssuranceCategories.Accessibility)
            .ToArray();

        Assert.Equal(5, rules.Length);
        foreach (var rule in rules)
        {
            Assert.Contains(rule.FriendlyName, coverage, StringComparison.Ordinal);
        }

        Assert.Contains("not WCAG certification", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void PowerQueryLineageTransformationsOnCoverageMatchProduction()
    {
        var coverage = ReadWeb("Pages/Coverage.razor");
        var supported = Regex.Matches(ReadCore("Scanning/MColumnLineageExtractor.cs"), "\"(Table\\.[A-Za-z]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(supported);
        foreach (var transformation in supported)
        {
            Assert.Contains($"<code>{transformation}</code>", coverage, StringComparison.Ordinal);
        }

        // The page names them rather than counting them, and never claims general M lineage.
        Assert.Contains("This is not general M lineage.", coverage, StringComparison.Ordinal);
        Assert.Contains("does not determine query folding", coverage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Theme Review is the row most at risk of overclaiming, so the page states its narrowness exactly.
    /// If production widens either the recognised property set or the single theme comparison, this
    /// fails and the copy has to be rewritten rather than quietly becoming an understatement.
    /// </summary>
    [Fact]
    public void ThemeReviewClaimsStayAsNarrowAsProduction()
    {
        var coverage = ReadWeb("Pages/Coverage.razor");
        var comparison = PrivateConstants("PbiAssure.Core.Scanning.ThemeFormattingComparisonAnalyzer");

        Assert.Equal("title.fontSize", comparison["SupportedProperty"]);
        Assert.Equal("clusteredColumnChart", comparison["SupportedPbirVisualType"]);
        Assert.Contains("title font size on clustered column charts", coverage, StringComparison.Ordinal);

        var recognised = Regex.Matches(ReadCore("Scanning/PbirVisualFormattingParser.cs"), "new\\(\"title\\.[A-Za-z]+\", \"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(["Title font size", "Title text colour", "Title background colour"], recognised);
        foreach (var property in recognised)
        {
            Assert.Contains(property, coverage, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("does not reproduce Power BI's formatting engine", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfidenceVocabularyAndDeliberateOmissionsHold()
    {
        var coverage = ReadWeb("Pages/Coverage.razor");
        var confidences = ConstantsOf(typeof(ClassificationConfidences));

        Assert.Equal(["Established", "QualifiedByLimitation"], confidences.Order(StringComparer.Ordinal));
        Assert.Contains("qualified by limitation", coverage, StringComparison.Ordinal);
        Assert.Contains("established", coverage, StringComparison.Ordinal);

        // Counts we deliberately kept out of public copy, and a claim the architecture cannot support.
        Assert.DoesNotContain("of the 32", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("29 connector", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing is silently skipped", coverage, StringComparison.Ordinal);
        Assert.Contains("rather than being silently treated as understood", coverage, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string[] ConstantsOf(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false })
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    private static Dictionary<string, string> PrivateConstants(string typeName)
    {
        var type = typeof(ProjectInventory).Assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"{typeName} not found.");

        return type
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
    }

    private static string ReadWeb(string relativePath) => ReadRepositoryFile("src", "PbiAssure.Web", relativePath);

    private static string ReadCore(string relativePath) => ReadRepositoryFile("src", "PbiAssure.Core", relativePath);

    private static string ReadRepositoryFile(string first, string second, string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, first, second, relativePath));
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
