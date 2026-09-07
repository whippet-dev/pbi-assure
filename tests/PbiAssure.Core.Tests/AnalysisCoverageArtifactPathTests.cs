using System.Text;
using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// PBIP paths contain spaces, so a comma-separated list of them wrapped at a space inside the next
/// path's project name — the following artifact appeared to begin at the end of the previous one's last
/// line. Each path now occupies its own block, which is the only way a wrapped path cannot share a line
/// with its neighbour.
/// </summary>
public sealed class AnalysisCoverageArtifactPathTests
{
    private const string FirstPath = "Sales & Returns Sample v201912.Report/definition/pages/one/visuals/a/visual.json";
    private const string SecondPath = "Sales & Returns Sample v201912.Report/definition/pages/two/visuals/b/visual.json";

    [Fact]
    public void EveryArtifactPathIsRenderedInFull()
    {
        var html = Render(FirstPath, SecondPath);

        Assert.Contains(Encoded(FirstPath), html, StringComparison.Ordinal);
        Assert.Contains(Encoded(SecondPath), html, StringComparison.Ordinal);
    }

    [Fact]
    public void EachPathIsItsOwnCodeBlockWithNoSeparatorBetweenThem()
    {
        var html = Render(FirstPath, SecondPath);
        var artifacts = ArtifactParagraph(html);

        // Adjacent blocks with nothing between them: no comma can land on a line of its own, and no
        // path can begin on the previous path's wrapped line.
        Assert.Contains("</code><code>", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("</code>, <code>", artifacts, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(artifacts, "<code>"));
        Assert.Contains(".coverage-artifacts code { display: block;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PathsContainingSpacesStayDistinct()
    {
        var artifacts = ArtifactParagraph(Render(FirstPath, SecondPath));

        // Each block starts at a path and ends at that same path: nothing shares a block.
        var blocks = Regex.Matches(artifacts, "<code>(.*?)</code>").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal([Encoded(FirstPath), Encoded(SecondPath)], blocks);
        Assert.All(blocks, block => Assert.DoesNotContain("</code>", block, StringComparison.Ordinal));
    }

    [Fact]
    public void ASinglePathStillRendersSensibly()
    {
        var artifacts = ArtifactParagraph(Render(FirstPath));

        Assert.Contains("File: ", artifacts, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(artifacts, "<code>"));
        Assert.Contains(Encoded(FirstPath), artifacts, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Encoded(string path) => path.Replace("&", "&amp;", StringComparison.Ordinal);

    private static string ArtifactParagraph(string html)
    {
        var match = Regex.Match(html, "<p class=\"coverage-artifacts\">(.*?)</p>", RegexOptions.Singleline);
        Assert.True(match.Success, "Expected a coverage-artifacts paragraph.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Renders a real scan with its limitations replaced, so one group carries the paths under test
    /// without needing a fixture that happens to raise the same limitation twice.
    /// </summary>
    private static string Render(params string[] artifactPaths)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Fact.tmdl",
                "table Fact\n\n\tcolumn Value\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: Value\n"),
        };
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Artifact paths", files));

        return HtmlReportRenderer.Render(inventory with
        {
            AnalysisLimitations = artifactPaths.Select(path => new AnalysisLimitation(
                LimitationId: "PBI-LIMIT-REPORT-UNRECOGNIZED",
                Cause: AnalysisLimitationCauses.ConstructNotSupported,
                SupportState: ConstructSupportStates.Unrecognized,
                ConstructType: "unrecognizedReportDefinitionFile",
                Scope: AnalysisLimitationScopes.Report,
                SemanticModel: "Model",
                Table: null,
                ObjectName: null,
                ArtifactPath: path,
                EvidencePath: AnalysisLimitation.WholeFileEvidence,
                DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
                Concerns: [AnalysisConcerns.Dependency],
                Reason: "Shared reason, so both paths land in one group.")).ToArray(),
        });
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}
