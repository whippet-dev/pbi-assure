using System.Globalization;
using System.Text;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    private static void AppendSummary(
        StringBuilder html,
        ProjectInventory inventory,
        AnalysisCoverage coverage,
        AssuranceFinding[] mainFindings)
    {
        html.AppendLine("    <section id=\"summary\" class=\"report-section\" data-report-section=\"summary\" aria-labelledby=\"summary-heading\">");
        html.AppendLine("      <h2 id=\"summary-heading\" tabindex=\"-1\">Summary</h2>");
        html.AppendLine("      <p class=\"section-intro\">What needs attention and where to investigate next.</p>");
        html.AppendLine("      <div class=\"summary-groups\">");
        AppendOverviewAttention(html, inventory, mainFindings);
        AppendOverviewReviews(html, inventory);
        AppendOverviewUsage(html, inventory);
        AppendOverviewConfidence(html, coverage);
        AppendOverviewSnapshot(html, inventory);
        html.AppendLine("      </div>");
        AppendScope(html);
        html.AppendLine("    </section>");
    }

    private static void AppendOverviewAttention(StringBuilder html, ProjectInventory inventory, AssuranceFinding[] findings)
    {
        html.AppendLine("        <section class=\"summary-group summary-group-assurance\" aria-labelledby=\"summary-attention-heading\">");
        html.AppendLine("          <h3 id=\"summary-attention-heading\">Needs attention</h3>");
        if (findings.Length == 0)
        {
            html.AppendLine("          <p class=\"overview-conclusion\">No primary assurance findings identified.</p>");
            html.AppendLine("          <p class=\"group-explanation\">No non-accessibility issues or review items were identified by the current automated checks. Manual review is still recommended.</p>");
        }
        else
        {
            html.AppendLine("          <dl class=\"metrics overview-attention-metrics\">");
            AppendMetric(html, "Errors", findings.Count(finding => finding.Severity == FindingSeverities.Error), "metric-error");
            AppendMetric(html, "Warnings", findings.Count(finding => finding.Severity == FindingSeverities.Warning), "metric-warning");
            AppendMetric(html, "Review required", findings.Count(finding => finding.AssessmentType == AssessmentTypes.ReviewRequired), "metric-review");
            html.AppendLine("          </dl>");
            html.Append("          <p class=\"overview-attention-scope\"><a href=\"#findings\">Open ")
                .Append(findings.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(findings.Length, "primary assurance finding", "primary assurance findings")).AppendLine("</a></p>");
            html.AppendLine("          <p class=\"group-explanation\">Severity counts and human-review assessments may overlap. Accessibility is counted separately.</p>");
            html.AppendLine("          <ul class=\"overview-findings\">");
            // Retain the original ordinal so the short list opens the existing finding, not a new copy.
            foreach (var item in findings.Select((finding, index) => (Finding: finding, Index: index))
                         .OrderBy(item => item.Finding.Severity == FindingSeverities.Error ? 0 : item.Finding.Severity == FindingSeverities.Warning ? 1 : 2)
                         .Take(2))
            {
                html.Append("            <li><span class=\"badge ").Append(SeverityClass(item.Finding.Severity)).Append("\">")
                    .Append(Encode(item.Finding.Severity)).Append("</span><a href=\"#")
                    .Append(FindingAnchor(item.Index)).Append("\">").Append(Encode(item.Finding.Message)).Append("</a>");
                var location = CreateFindingRenderItem(inventory, item.Finding);
                if (!string.IsNullOrWhiteSpace(location.PageLabel))
                {
                    html.Append("<span class=\"overview-finding-location\">").Append(Encode(location.PageLabel)).Append("</span>");
                }
                html.AppendLine("</li>");
            }
            html.AppendLine("          </ul>");
        }
        html.AppendLine("        </section>");
    }

    private static void AppendOverviewReviews(StringBuilder html, ProjectInventory inventory)
    {
        var accessibilityCount = inventory.Findings.Count(IsAccessibilityFinding);
        var deviations = inventory.Reports.Sum(report => report.ThemeReview.Deviations.Count);
        var consistency = inventory.Reports.Sum(report => report.ThemeReview.ConsistencyObservations.Count);
        var compared = inventory.Reports.SelectMany(report => report.Pages).SelectMany(page => page.Visuals)
            .SelectMany(visual => visual.PersistedFormatting).Count(item => item.ThemeComparison?.State is
                ThemeFormattingComparisonStates.SavedValueMatchesTheme or ThemeFormattingComparisonStates.SavedValueDiffersFromTheme);
        html.AppendLine("        <section class=\"summary-group summary-group-reviews\" aria-labelledby=\"summary-reviews-heading\">");
        html.AppendLine("          <h3 id=\"summary-reviews-heading\">Other reviews</h3>");
        html.AppendLine("          <div class=\"overview-reviews\">");
        html.AppendLine("            <div><a href=\"#theme-review\">Theme review</a>");
        if (deviations > 0 || consistency > 0)
        {
            html.Append("              <p>").Append(deviations.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(deviations, "theme deviation", "theme deviations")).Append(" · ")
                .Append(consistency.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(consistency, "consistency observation", "consistency observations")).AppendLine("</p>");
        }
        else
        {
            html.AppendLine("              <p>No theme review items identified.</p>");
        }
        html.Append("              <p class=\"group-explanation\">");
        if (compared == 0)
        {
            html.Append("No theme settings could be compared automatically.");
        }
        else
        {
            html.Append(compared.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(compared, "saved visual setting compared", "saved visual settings compared"))
                .Append(" against the theme.");
        }
        html.AppendLine("</p></div>");
        html.AppendLine("            <div><a href=\"#accessibility-review\">Accessibility review</a>");
        html.Append("              <p>").Append(accessibilityCount == 0 ? "No accessibility observations identified." :
            $"{accessibilityCount.ToString("N0", CultureInfo.InvariantCulture)} accessibility {Pluralize(accessibilityCount, "observation", "observations")}").AppendLine("</p>");
        html.AppendLine("              <p class=\"group-explanation\">Accessibility observations are counted separately; manual review is still needed.</p></div>");
        html.AppendLine("          </div>");
        html.AppendLine("        </section>");
    }

    private static void AppendOverviewUsage(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("        <section class=\"summary-group summary-group-semantic\" aria-labelledby=\"summary-usage-heading\">");
        html.AppendLine("          <h3 id=\"summary-usage-heading\">Model usage</h3>");
        html.Append("          <p class=\"group-explanation\">Usage of ").Append(inventory.DeveloperSemanticObjectCount.ToString("N0", CultureInfo.InvariantCulture))
            .AppendLine(" authored semantic objects in this project. Open a count to review that state.</p>");
        html.AppendLine("          <ul class=\"overview-usage\">");
        foreach (var state in OverviewUsageStates)
        {
            html.Append("            <li><a href=\"#semantic-usage?state=").Append(state).Append("\" data-usage-shortcut=\"").Append(state)
                .Append("\" class=\"metric metric-").Append(state switch
                {
                    SemanticUsageStates.DirectlyUsed => "used",
                    SemanticUsageStates.IndirectlyUsed => "indirect",
                    SemanticUsageStates.StructurallyRequired => "structural",
                    SemanticUsageStates.UsedOnlyByUnusedBranch => "branch",
                    _ => "unused",
                }).Append("\"><span>").Append(UsageLabel(state)).Append("</span><strong>")
                .Append(inventory.DeveloperSemanticObjectCountForState(state).ToString("N0", CultureInfo.InvariantCulture)).AppendLine("</strong></a></li>");
        }
        html.AppendLine("          </ul>");
        if (inventory.DeveloperApparentlyUnusedSemanticObjectCount > 0)
        {
            html.AppendLine("          <p class=\"summary-caution\"><strong>Apparently unused objects require review before any removal.</strong> External reports and dynamic behaviour may still use them.</p>");
        }
        else
        {
            html.AppendLine("          <p class=\"overview-zero\">No apparently unused authored objects identified in this project.</p>");
        }
        html.AppendLine("        </section>");
    }

    private static readonly string[] OverviewUsageStates = [
        SemanticUsageStates.DirectlyUsed, SemanticUsageStates.IndirectlyUsed, SemanticUsageStates.StructurallyRequired,
        SemanticUsageStates.UsedOnlyByUnusedBranch, SemanticUsageStates.ApparentlyUnused];

    private static void AppendOverviewConfidence(StringBuilder html, AnalysisCoverage coverage)
    {
        html.AppendLine("        <section class=\"summary-group summary-group-confidence\" aria-labelledby=\"summary-confidence-heading\">");
        html.AppendLine("          <h3 id=\"summary-confidence-heading\">Analysis confidence</h3>");
        if (coverage.QualifiedObjectCount > 0)
        {
            html.Append("          <p class=\"overview-confidence\"><strong>").Append(coverage.QualifiedObjectCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append(' ').Append(Pluralize(coverage.QualifiedObjectCount, "object result", "object results"))
                .Append("</strong> <span class=\"confidence-flag confidence-flag-sample\">").Append(CoverageMarkerLabel).AppendLine("</span></p>");
            html.AppendLine("          <p class=\"group-explanation\">Missing or partly understood metadata could affect these results. Their usage states remain unchanged.</p>");
        }
        else
        {
            html.AppendLine("          <p class=\"overview-confidence\"><strong>No identified limitations</strong></p>");
            html.AppendLine("          <p class=\"group-explanation\">No object results have a qualifying limitation in this scan. This does not cover usage outside the analysed project.</p>");
        }
        if (coverage.HasCoverage)
        {
            html.AppendLine("          <p class=\"overview-route\"><a href=\"#analysis-coverage\">Review analysis coverage</a></p>");
        }
        html.AppendLine("        </section>");
    }

    private static void AppendOverviewSnapshot(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("        <section class=\"summary-group summary-group-project\" aria-labelledby=\"summary-snapshot-heading\">");
        html.AppendLine("          <h3 id=\"summary-snapshot-heading\">Project snapshot</h3>");
        html.AppendLine("          <dl class=\"metrics overview-snapshot\">");
        AppendMetric(html, "Semantic models", inventory.SemanticModelCount);
        AppendMetric(html, "Reports", inventory.ReportCount);
        AppendMetric(html, "Pages", inventory.PageCount);
        AppendMetric(html, "Visuals", inventory.VisualCount);
        AppendMetric(html, "Authored semantic objects", inventory.DeveloperSemanticObjectCount);
        html.AppendLine("          </dl>");
        if (inventory.PowerQueryCount > 0)
        {
            html.Append("          <p class=\"group-explanation overview-query-count\">Power Query: ")
                .Append(inventory.PowerQueryCount.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(inventory.PowerQueryCount, "query", "queries")).Append(" · ")
                .Append(inventory.DistinctConnectorFamilyCount.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(inventory.DistinctConnectorFamilyCount, "recognised data source type", "recognised data source types")).AppendLine("</p>");
        }
        html.AppendLine("        </section>");
    }
}
