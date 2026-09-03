using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// The browser application links the design system as stylesheet files; the generated report has
/// to carry the same bytes inline because it travels as a single document. Two copies is the price
/// of that, so these tests make drift a build failure rather than a slow divergence into two
/// products that no longer look related.
/// </summary>
public sealed class DesignSystemSourceTests
{
    private const string RegenerationHint =
        "Edit the stylesheet, then run: node scripts/Sync-DesignTokens.mjs";

    [Fact]
    public void CompiledCoreMatchesTheStylesheetTheApplicationLinks()
    {
        Assert.Equal(
            ReadStylesheet("src", "PbiAssure.Web", "wwwroot", "css", "core.css"),
            Normalise(DesignSystem.Core));
    }

    [Fact]
    public void CompiledReportPresentationMatchesItsStylesheet()
    {
        Assert.Equal(
            ReadStylesheet("src", "PbiAssure.Reporting", "Styles", "report.css"),
            Normalise(DesignSystem.Report));
    }

    /// <summary>
    /// The tokens are the contract between the two surfaces. If a status colour, the accent or a
    /// type step disappears, one of them silently falls back to an inherited value.
    /// </summary>
    [Theory]
    [InlineData("--pa-accent")]
    [InlineData("--pa-canvas")]
    [InlineData("--pa-surface")]
    [InlineData("--pa-line")]
    [InlineData("--pa-text")]
    [InlineData("--pa-error")]
    [InlineData("--pa-warning")]
    [InlineData("--pa-review")]
    [InlineData("--pa-info")]
    [InlineData("--pa-used")]
    [InlineData("--pa-indirect")]
    [InlineData("--pa-structural")]
    [InlineData("--pa-branch")]
    [InlineData("--pa-unused")]
    [InlineData("--pa-font-sans")]
    [InlineData("--pa-font-mono")]
    public void EveryTokenIsDefinedForBothThemes(string token)
    {
        var core = Normalise(DesignSystem.Core);
        var light = core[..core.IndexOf(":root[data-theme=\"dark\"]", StringComparison.Ordinal)];
        var dark = core[core.IndexOf(":root[data-theme=\"dark\"]", StringComparison.Ordinal)..];

        Assert.Contains($"{token}:", light, StringComparison.Ordinal);
        if (token is "--pa-font-sans" or "--pa-font-mono")
        {
            return;
        }

        // Colours are re-picked for dark rather than inherited, and the explicit choice and the
        // prefers-color-scheme fallback have to agree, so each one appears twice.
        Assert.Equal(2, CountOccurrences(dark, $"{token}:"));
    }

    /// <summary>
    /// Colour alone never carries a classification: each status badge also gets a distinct glyph.
    /// </summary>
    [Theory]
    [InlineData("badge-error", "--pa-icon-error")]
    [InlineData("badge-warning", "--pa-icon-warning")]
    [InlineData("badge-review", "--pa-icon-review")]
    [InlineData("badge-information", "--pa-icon-info")]
    [InlineData("badge-used", "--pa-icon-used")]
    [InlineData("badge-indirect", "--pa-icon-indirect")]
    [InlineData("badge-structural", "--pa-icon-structural")]
    [InlineData("badge-unused-branch", "--pa-icon-branch")]
    [InlineData("badge-unused", "--pa-icon-unused")]
    public void EveryStatusBadgeCarriesItsOwnGlyph(string badgeClass, string glyphToken)
    {
        var core = Normalise(DesignSystem.Core);
        var rule = core[core.IndexOf($".{badgeClass} {{", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf('}', StringComparison.Ordinal)];

        Assert.Contains($"--pa-badge-glyph: var({glyphToken})", rule, StringComparison.Ordinal);
        Assert.Contains($"{glyphToken}: url(\"data:image/svg+xml", core, StringComparison.Ordinal);
    }

    private static string ReadStylesheet(params string[] relativeSegments)
    {
        var path = Path.Combine([FindRepositoryRoot(), .. relativeSegments]);
        Assert.True(File.Exists(path), $"{path} was not found. {RegenerationHint}");
        return Normalise(File.ReadAllText(path));
    }

    private static string Normalise(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static int CountOccurrences(string content, string value)
    {
        var count = 0;
        for (var index = content.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = content.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
