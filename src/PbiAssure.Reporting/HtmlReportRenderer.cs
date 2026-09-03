using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using PbiAssure.Core.Assurance;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    public static string Render(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var html = new StringBuilder(capacity: 256_000);
        // Built once and threaded through: the model summary, the navigation entry and every
        // object-level marker all read from the same result.
        var coverage = AnalysisCoveragePresentation.Build(inventory);
        var mainFindings = inventory.Findings.Where(IsMainFinding).ToArray();
        var accessibilityFindings = inventory.Findings.Where(IsAccessibilityFinding).ToArray();
        AppendDocumentStart(html, inventory, coverage);
        AppendSummary(html, inventory, coverage, mainFindings);
        AppendSemanticUsage(html, inventory, coverage);
        AppendPowerQueryLineage(html, inventory);
        AppendRelationships(html, inventory);
        AppendRowLevelSecurity(html, inventory, coverage);
        AppendReportInventory(html, inventory);
        AppendFindings(html, inventory, mainFindings);
        AppendAnalysisCoverage(html, coverage);
        AppendThemeReview(html, inventory);
        AppendAccessibilityReview(html, inventory, accessibilityFindings);
        AppendDocumentEnd(html, inventory);
        return html.ToString();
    }

    private static void AppendDocumentStart(
        StringBuilder html,
        ProjectInventory inventory,
        AnalysisCoverage coverage)
    {
        var projectName = ProjectName(inventory);

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en-GB\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("  <title>PBI Assure report — ").Append(Encode(projectName)).AppendLine("</title>");
        html.Append("  <link rel=\"icon\" href=\"").Append(BrandIdentity.FaviconDataUri).AppendLine("\">");
        html.AppendLine("  <style>");
        html.AppendLine(DesignSystem.Core);
        html.AppendLine(DesignSystem.Report);
        html.AppendLine("  </style>");
        html.AppendLine("  <script>");
        html.AppendLine(AppearanceBootstrapScript);
        html.AppendLine("  </script>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <a class=\"skip-link\" href=\"#main-content\">Skip to main content</a>");
        html.AppendLine("  <header class=\"site-header\">");
        html.AppendLine("    <div class=\"content\">");
        html.AppendLine("      <div class=\"site-header-bar\">");
        html.Append("        <span class=\"brand\">").Append(BrandIdentity.MarkSvg)
            .AppendLine("PBI Assure<span class=\"brand-qualifier\">Report</span></span>");
        AppendAppearanceControl(html, "        ");
        html.AppendLine("      </div>");
        html.AppendLine("      <div class=\"site-header-identity\">");
        html.AppendLine("      <p class=\"eyebrow\">Model intelligence</p>");
        html.Append("      <h1>").Append(Encode(projectName)).AppendLine("</h1>");
        html.AppendLine("      <p class=\"lede\">A read-only review of this Power BI project.</p>");
        html.AppendLine("      <dl class=\"report-meta\">");
        AppendScanTimestamp(html, inventory.ScannedAtUtc);
        AppendDefinition(html, "Inventory schema", inventory.SchemaVersion);
        AppendDefinition(html, "Source project", DisplayPath(inventory.RootPath));
        html.AppendLine("      </dl>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");
        html.AppendLine("  </header>");
        html.AppendLine("  <div class=\"content report-workspace\">");
        html.AppendLine("      <nav class=\"section-navigator\" aria-label=\"Report sections\">");
        html.AppendLine("        <ul class=\"section-nav\">");
        AppendSectionNavigationItem(html, "summary", "Summary", "Overview and key counts");
        AppendSectionNavigationItem(html, "semantic-usage", "Semantic model", "Model objects and usage");
        AppendSectionNavigationItem(html, "power-query", "Power Query", "Queries, sources and dependencies");
        AppendSectionNavigationItem(html, "relationships", "Model relationships", "Table connections and filtering");
        if (inventory.SemanticModels.Any(model => model.Roles.Count > 0))
        {
            AppendSectionNavigationItem(html, "row-level-security", "Security roles", "Roles, filters and object permissions");
        }

        AppendSectionNavigationItem(html, "reports", "Report pages", "Pages, visuals and fields");
        AppendSectionNavigationItem(html, "findings", "Findings", "Issues and review items");
        if (coverage.HasCoverage)
        {
            AppendSectionNavigationItem(html, "analysis-coverage", "Analysis coverage", "What was and was not checked");
        }

        AppendSectionNavigationItem(html, "theme-review", "Theme Review", "Design and theme review");
        AppendSectionNavigationItem(html, "accessibility-review", "Accessibility review", "Supporting accessibility analysis");
        html.AppendLine("        </ul>");
        html.AppendLine("      </nav>");
        html.AppendLine("  <main id=\"main-content\" class=\"report-content\" tabindex=\"-1\">");
    }

    private static void AppendSummary(
        StringBuilder html,
        ProjectInventory inventory,
        AnalysisCoverage coverage,
        AssuranceFinding[] mainFindings)
    {
        html.AppendLine("    <section id=\"summary\" class=\"report-section\" data-report-section=\"summary\" aria-labelledby=\"summary-heading\">");
        html.AppendLine("      <h2 id=\"summary-heading\" tabindex=\"-1\">Summary</h2>");
        html.AppendLine("      <p class=\"section-intro\">Start here for model usage, project structure, Power Query context and assurance observations.</p>");
        html.AppendLine("      <div class=\"summary-groups\">");
        html.AppendLine("        <section class=\"summary-group summary-group-semantic\" aria-labelledby=\"summary-semantic-heading\" aria-describedby=\"summary-semantic-help\">");
        html.AppendLine("          <h3 id=\"summary-semantic-heading\">Semantic usage</h3>");
        html.AppendLine("          <p id=\"summary-semantic-help\" class=\"group-explanation\">How columns, measures and other objects in your model are used by the report and by one another.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Directly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.DirectlyUsed), "metric-used");
        AppendMetric(html, "Indirectly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.IndirectlyUsed), "metric-indirect");
        AppendMetric(html, "Structurally required", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.StructurallyRequired), "metric-structural");
        AppendMetric(html, "Only used by unused items", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.UsedOnlyByUnusedBranch), "metric-branch");
        AppendMetric(html, "Apparently unused", inventory.DeveloperApparentlyUnusedSemanticObjectCount, "metric-unused");
        html.AppendLine("      </dl>");
        html.AppendLine("          <p class=\"summary-caution\"><strong>Check apparently unused objects before removing them:</strong> PBI Assure could not find anything in this project that uses them. External reports, other models or dynamic behaviour may still depend on them.</p>");
        if (coverage.QualifiedObjectCount > 0)
        {
            html.Append("          <p class=\"summary-coverage-note\">PBI Assure could not check every source of usage in this project, so ")
                .Append(coverage.QualifiedObjectCount.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(coverage.QualifiedObjectCount, "of these results is", "of these results are"))
                .Append(" marked <span class=\"confidence-flag confidence-flag-sample\">")
                .Append(CoverageMarkerLabel)
                .AppendLine("</span>. <a href=\"#analysis-coverage\">Review analysis coverage</a>.</p>");
        }
        AppendSummaryDefinitions(html, "What these usage states mean", [
            ("Directly used", "Used somewhere in the report, such as a visual, filter, tooltip or drillthrough setting."),
            ("Indirectly used", "Not used directly in the report, but needed by something that is."),
            ("Structurally required", "Needed for the model to work, for example in a relationship, hierarchy or sort-by setting."),
            ("Only used by unused items", "Only used by other model items that themselves have no detected report usage."),
            ("Apparently unused", "PBI Assure could not find anything in this project that uses it. Check before removing it because external reports and dynamic behaviour may not be visible here.")]);
        html.AppendLine("        </section>");
        html.AppendLine("        <section class=\"summary-group summary-group-project\" aria-labelledby=\"summary-project-heading\" aria-describedby=\"summary-project-help\">");
        html.AppendLine("          <h3 id=\"summary-project-heading\">Project</h3>");
        html.AppendLine("          <p id=\"summary-project-help\" class=\"group-explanation\">A count of the main report and semantic-model content found in the analysed project.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Reports", inventory.ReportCount);
        AppendMetric(html, "Pages", inventory.PageCount);
        AppendMetric(html, "Visuals", inventory.VisualCount);
        if (inventory.ReportMeasureCount > 0)
        {
            AppendMetric(html, "Report measures", inventory.ReportMeasureCount);
        }
        AppendMetric(html, "Your model objects", inventory.DeveloperSemanticObjectCount);
        if (inventory.SystemGeneratedSemanticObjectCount > 0)
        {
            AppendMetric(html, "System-generated model objects", inventory.SystemGeneratedSemanticObjectCount);
        }
        html.AppendLine("      </dl>");
        AppendSummaryDefinitions(html, "What these project numbers count", [
            ("Reports", "Power BI reports found in the project."),
            ("Pages", "Report pages found across those reports."),
            ("Visuals", "Visuals placed across all report pages."),
            ("Report measures", "DAX measures defined in the report itself, rather than in its semantic model."),
            ("Your model objects", "Columns, measures, hierarchy levels and calculation items created as part of the model."),
            ("System-generated model objects", "Model objects created automatically by Power BI, such as objects in local date tables.")]);
        html.AppendLine("        </section>");
        if (inventory.PowerQueryCount > 0 || inventory.DataSourceCount > 0)
        {
            html.AppendLine("        <section class=\"summary-group summary-group-power-query\" aria-labelledby=\"summary-power-query-heading\" aria-describedby=\"summary-power-query-help\">");
            html.AppendLine("          <h3 id=\"summary-power-query-heading\">Power Query</h3>");
            html.AppendLine("          <p id=\"summary-power-query-help\" class=\"group-explanation\">Power Query queries, data source types and dependencies found in this project.</p>");
            html.AppendLine("      <dl class=\"metrics\">");
            if (inventory.PowerQueryCount > 0)
            {
                AppendMetric(html, "Power Query queries", inventory.PowerQueryCount);
            }
            if (inventory.DataSourceCount > 0)
            {
                AppendMetric(html, "Data source types", inventory.DistinctConnectorFamilyCount);
            }
            html.AppendLine("      </dl>");
            AppendSummaryDefinitions(html, "What these Power Query numbers count", [
                ("Power Query queries", "Power Query queries found in this project."),
                ("Data source types", "Different types of recognised data sources used by those queries. This is not the number of individual connections.")]);
            html.AppendLine("        </section>");
        }
        html.AppendLine("        <section class=\"summary-group summary-group-assurance\" aria-labelledby=\"summary-assurance-heading\" aria-describedby=\"summary-assurance-help\">");
        html.AppendLine("          <h3 id=\"summary-assurance-heading\">Assurance</h3>");
        html.AppendLine("          <p id=\"summary-assurance-help\" class=\"group-explanation\">Findings from non-accessibility automated checks across the report, semantic model and Power Query. Start with errors, then warnings and items that need a person to review them.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Errors", mainFindings.Count(finding => finding.Severity == FindingSeverities.Error), "metric-error");
        AppendMetric(html, "Warnings", mainFindings.Count(finding => finding.Severity == FindingSeverities.Warning), "metric-warning");
        AppendMetric(html, "Review required", mainFindings.Count(finding => finding.AssessmentType == AssessmentTypes.ReviewRequired), "metric-review");
        AppendMetric(html, "Total findings", mainFindings.Length);
        html.AppendLine("      </dl>");
        AppendSummaryDefinitions(html, "What these finding numbers mean", [
            ("Errors", "Higher-confidence issues that would normally merit attention."),
            ("Warnings", "Potential problems, good-practice concerns or lower-confidence issues worth reviewing."),
            ("Review required", "Situations that need human judgement or contextual review; they are not necessarily defects."),
            ("Total findings", "All non-accessibility issues and review items found by the automated checks.")]);
        html.AppendLine("          <p class=\"group-explanation\">Accessibility observations are counted separately in Accessibility review so that they remain available without dominating the main assurance summary.</p>");
        html.AppendLine("        </section>");
        html.AppendLine("      </div>");
        html.Append("      <p class=\"summary-note\"><strong>").Append(inventory.DeveloperApparentlyUnusedSemanticObjectCount.ToString(CultureInfo.InvariantCulture))
            .Append(" model objects created as part of this model have no usage detected in this project. Review them before removing anything.</strong>");
        if (inventory.SystemGeneratedSemanticObjectCount > 0)
        {
            html.Append(" Power BI-generated objects remain analysed and are available in the semantic-model filter.");
        }
        html.AppendLine("</p>");
        html.AppendLine("      <p><a href=\"#semantic-usage\">Review semantic-model candidates</a></p>");
        AppendScope(html);
        html.AppendLine("    </section>");
    }

    /// <summary>
    /// What PBI Assure read in this project's semantic models, and what it did not.
    ///
    /// Two decisions shape this section. First, it is only rendered when something was actually left
    /// unanalysed: a panel announcing that there is nothing to report would be reassurance nobody asked
    /// for, and the standing caveats already live in the Summary disclosure. Second, the
    /// limitations that cannot affect a usage conclusion are disclosed but tucked into a details
    /// element, because a real Desktop model records six of those and one that matters — showing all
    /// seven with equal weight would bury the one worth reading.
    /// </summary>
    private static void AppendAnalysisCoverage(StringBuilder html, AnalysisCoverage coverage)
    {
        if (!coverage.HasCoverage)
        {
            return;
        }

        html.AppendLine("    <section id=\"analysis-coverage\" class=\"report-section\" data-report-section=\"analysis-coverage\" aria-labelledby=\"analysis-coverage-heading\">");
        html.AppendLine("      <h2 id=\"analysis-coverage-heading\" tabindex=\"-1\">Analysis coverage</h2>");
        html.AppendLine("      <p class=\"section-intro\">This shows report-format metadata that PBI Assure has not verified exactly, alongside anything it could not fully check in the semantic model. These notes describe PBI Assure's coverage, not a problem with your project.</p>");

        foreach (var report in coverage.Reports)
        {
            html.Append("      <section class=\"coverage-model\" id=\"").Append(Encode(report.AnchorId)).AppendLine("\">");
            html.Append("        <h3>Report: ").Append(Encode(report.ReportName)).AppendLine("</h3>");
            html.AppendLine("        <p class=\"coverage-headline\">PBI Assure recorded report-format metadata it has not verified exactly. Analysis continues normally.</p>");
            html.AppendLine("        <ul class=\"coverage-list\">");
            foreach (var group in report.Groups)
            {
                AppendReportSchemaCoverageGroup(html, group);
            }

            html.AppendLine("        </ul>");
            html.AppendLine("      </section>");
        }

        foreach (var model in coverage.Models)
        {
            html.Append("      <section class=\"coverage-model\" id=\"").Append(Encode(model.AnchorId)).AppendLine("\">");
            if (!string.IsNullOrWhiteSpace(model.ModelName))
            {
                html.Append("        <h3>").Append(Encode(model.ModelName)).AppendLine("</h3>");
            }

            AppendCoverageHeadline(html, model);

            if (model.QualifyingGroups.Count > 0)
            {
                html.AppendLine("        <ul class=\"coverage-list coverage-qualifying\">");
                foreach (var group in model.QualifyingGroups)
                {
                    AppendCoverageGroup(html, group);
                }

                html.AppendLine("        </ul>");
            }

            if (model.OtherGroups.Count > 0)
            {
                var artifacts = model.OtherGroups.Sum(group => group.ArtifactPaths.Count);
                html.Append("        <details class=\"coverage-other\"><summary>");
                if (model.QualifyingGroups.Count > 0)
                {
                    // "Other" only makes sense next to something; when nothing qualifies, the headline
                    // has already given the count and the disclosure just needs a reason to open it.
                    html.Append(artifacts.ToString(CultureInfo.InvariantCulture)).Append(' ')
                        .Append(Pluralize(artifacts, "more file", "more files"))
                        .Append(Pluralize(artifacts, " was", " were"))
                        .Append(" not fully checked, and cannot change a used or unused result");
                }
                else
                {
                    html.Append("What PBI Assure could not fully check");
                }

                html.AppendLine("</summary>");
                html.AppendLine("          <ul class=\"coverage-list\">");
                foreach (var group in model.OtherGroups)
                {
                    AppendCoverageGroup(html, group);
                }

                html.AppendLine("          </ul>");
                html.AppendLine("        </details>");
            }

            html.AppendLine("      </section>");
        }

        html.AppendLine("      <p class=\"coverage-footnote\">PBI Assure covers more Power BI metadata with each release. Anything listed here describes what this version can read — it is not a problem with your project.</p>");
        html.AppendLine("    </section>");
    }

    /// <summary>
    /// The one sentence a reader needs. Counts only: there is no evidence basis for scoring how accurate
    /// an analysis was, and a percentage would invite exactly that reading.
    /// </summary>
    private static void AppendCoverageHeadline(StringBuilder html, AnalysisCoverageModel model)
    {
        html.Append("        <p class=\"coverage-headline\">");
        if (model.QualifyingGroups.Count == 0)
        {
            html.Append("PBI Assure could not fully check ")
                .Append(model.ArtifactCount.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(model.ArtifactCount, "file", "files"))
                .Append(" in this model. <strong>None of them can change a used or unused result.</strong>");
            html.AppendLine("</p>");
            return;
        }

        html.Append("<strong>PBI Assure could not fully check ")
            .Append(model.QualifyingGroups.Count.ToString(CultureInfo.InvariantCulture))
            .Append(' ').Append(Pluralize(model.QualifyingGroups.Count, "source", "sources"))
            .Append(" of usage in this model.</strong> ");
        if (model.QualifiedObjectCount > 0)
        {
            html.Append("That could change the used or unused result for ")
                .Append(model.QualifiedObjectCount.ToString(CultureInfo.InvariantCulture)).Append(" of ")
                .Append(model.ObjectCount.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(model.ObjectCount, "model object", "model objects"))
                .Append(", so each is marked <span class=\"confidence-flag confidence-flag-sample\">")
                .Append(CoverageMarkerLabel)
                .Append("</span>. Those results are still the best answer available — they simply may miss usage PBI Assure cannot yet see.");
        }
        else
        {
            html.Append("No used or unused result in this model is affected.");
        }

        html.AppendLine("</p>");
    }

    private static void AppendCoverageGroup(StringBuilder html, AnalysisCoverageGroup group)
    {
        html.Append("            <li class=\"coverage-item")
            .Append(group.MayAffectClassification ? " coverage-item-qualifying" : string.Empty)
            .Append("\"><p class=\"coverage-item-head\"><strong>").Append(Encode(group.Label))
            .Append("</strong> <span class=\"badge badge-neutral\">").Append(Encode(group.SupportStateLabel))
            .Append("</span> <span class=\"coverage-impact\">").Append(Encode(group.ImpactLabel))
            .AppendLine("</span></p>");
        html.Append("              <p class=\"coverage-reason\">").Append(Encode(group.Reason)).AppendLine("</p>");
        html.Append("              <p class=\"coverage-artifacts\">")
            .Append(Pluralize(group.ArtifactPaths.Count, "File", "Files")).Append(": ");
        for (var index = 0; index < group.ArtifactPaths.Count; index++)
        {
            if (index > 0)
            {
                html.Append(", ");
            }

            html.Append("<code>").Append(Encode(DisplayPath(group.ArtifactPaths[index]))).Append("</code>");
        }

        html.AppendLine("</p>");
        html.AppendLine("            </li>");
    }

    private static void AppendReportSchemaCoverageGroup(StringBuilder html, ReportSchemaCoverageGroup group)
    {
        html.Append("          <li class=\"coverage-item\"><p class=\"coverage-item-head\"><strong>")
            .Append(Encode(group.Label)).Append("</strong> <span class=\"badge badge-neutral\">")
            .Append(Encode(ReportSchemaStateLabel(group.State))).AppendLine("</span></p>");
        html.Append("            <p class=\"coverage-reason\">").Append(Encode(group.Message)).AppendLine("</p>");
        html.AppendLine("            <details class=\"technical-details\"><summary>Technical details</summary><dl class=\"technical-list\">");
        AppendTechnicalDefinition(html, "Expected schema family", group.ExpectedSchemaFamily);
        if (!string.IsNullOrWhiteSpace(group.SchemaFamily))
        {
            AppendTechnicalDefinition(html, "Declared schema family", group.SchemaFamily);
        }

        if (!string.IsNullOrWhiteSpace(group.SchemaVersion))
        {
            AppendTechnicalDefinition(html, "Declared schema version", group.SchemaVersion);
        }

        if (!string.IsNullOrWhiteSpace(group.VerifiedBaselineVersion))
        {
            AppendTechnicalDefinition(html, "Verified version", group.VerifiedBaselineVersion);
        }

        if (group.RawSchemaUris.Count > 0)
        {
            html.AppendLine("              <dt>Schema declaration</dt><dd>");
            foreach (var schemaUri in group.RawSchemaUris)
            {
                html.Append("                <code>").Append(Encode(schemaUri)).AppendLine("</code>");
            }

            html.AppendLine("              </dd>");
        }

        html.AppendLine("              <dt>Source files</dt><dd>");
        foreach (var artifactPath in group.ArtifactPaths)
        {
            html.Append("                <code>").Append(Encode(DisplayPath(artifactPath))).AppendLine("</code>");
        }

        html.AppendLine("              </dd>");
        html.AppendLine("            </dl></details>");
        html.AppendLine("          </li>");
    }

    private static string ReportSchemaStateLabel(string state) => state switch
    {
        ReportSchemaObservationStates.RecognisedUnverifiedVersion => "Version not verified",
        ReportSchemaObservationStates.UnknownFamily => "Schema family not verified",
        ReportSchemaObservationStates.MetadataMissing => "Schema metadata missing",
        ReportSchemaObservationStates.MetadataMalformed => "Schema metadata could not be read",
        _ => "Schema metadata needs review",
    };

    private static void AppendTechnicalDefinition(StringBuilder html, string term, string value)
    {
        html.Append("              <dt>").Append(Encode(term)).Append("</dt><dd><code>")
            .Append(Encode(value)).AppendLine("</code></dd>");
    }

    /// <summary>
    /// The object-level half of the design: restrained, because one limitation can qualify most of a
    /// model and a warning on every affected object would train readers to ignore warnings.
    ///
    /// It is a link rather than a badge or a tooltip. A link is keyboard operable without extra
    /// scripting, carries its meaning as visible text, and takes the reader to the explanation instead
    /// of restating it here. The confidence value is read straight from the domain object, so a future
    /// impact that qualifies a positive state renders without a renderer change.
    /// </summary>
    /// <summary>
    /// The single source of the coverage vocabulary. The marker, the model headline, the summary
    /// sentence and the usage guide all render this same phrase, so they cannot drift apart and leave a
    /// reader following a word that appears nowhere in its own explanation.
    /// </summary>
    private const string CoverageMarkerLabel = "Usage check incomplete";

    private const string CoverageMarkerDescription =
        " — PBI Assure could not check every source of usage in this model.";

    private static void AppendClassificationConfidence(
        StringBuilder html,
        SemanticObjectUsage usage,
        string? coverageAnchor)
    {
        if (usage.ClassificationConfidence != ClassificationConfidences.QualifiedByLimitation)
        {
            return;
        }

        // An object is only ever qualified by a limitation in its own model, and a model with a
        // limitation always has a coverage block, so the anchor is expected. The unlinked form exists so
        // that a future qualifier which broke that assumption would still explain itself.
        if (coverageAnchor is null)
        {
            html.Append("<span class=\"confidence-flag\">").Append(CoverageMarkerLabel)
                .Append("<span class=\"visually-hidden\">").Append(CoverageMarkerDescription)
                .Append("</span></span>");
            return;
        }

        html.Append("<a class=\"confidence-flag\" href=\"#").Append(Encode(coverageAnchor))
            .Append("\">").Append(CoverageMarkerLabel)
            .Append("<span class=\"visually-hidden\">").Append(CoverageMarkerDescription)
            .Append(" See analysis coverage.</span></a>");
    }

    private static string ConfidenceSearchText(SemanticObjectUsage usage) =>
        usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation
            ? CoverageMarkerLabel + " "
            : string.Empty;

    private static void AppendScope(StringBuilder html)
    {
        html.AppendLine("    <details class=\"scope section-help\" aria-labelledby=\"scope-heading\">");
        html.AppendLine("      <summary id=\"scope-heading\">Important limits before acting on this report</summary>");
        html.AppendLine("      <p class=\"section-intro\">Keep these limits in mind before changing or removing anything.</p>");
        html.AppendLine("      <ul>");
        html.AppendLine("        <li><strong>Apparently unused</strong> means PBI Assure found no use within this project. It does not mean the object is safe to delete.</li>");
        html.AppendLine("        <li>A model table can look unused in the report while its Power Query is still needed by another query.</li>");
        html.AppendLine("        <li>Power Query dependencies built dynamically may not be detected, including column lists or query names created while the query runs.</li>");
        html.AppendLine("        <li>Uses outside this project, some bookmark state and details hidden inside a data source may not be visible to PBI Assure.</li>");
        html.AppendLine("        <li>Accessibility findings support manual WCAG and assistive-technology testing; they do not prove that the report conforms.</li>");
        html.AppendLine("        <li>PBI Assure performs read-only analysis of the selected Power BI project.</li>");
        html.AppendLine("      </ul>");
        html.AppendLine("    </details>");
    }

    private static void AppendPowerQueryLineage(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"power-query\" class=\"report-section\" data-report-section=\"power-query\" aria-labelledby=\"power-query-heading\">");
        html.AppendLine("      <h2 id=\"power-query-heading\" tabindex=\"-1\">Power Query</h2>");
        html.AppendLine("      <p class=\"section-intro\">See which queries load data into the model and which queries depend on one another. Expand a query to review its known dependencies.</p>");
        if (inventory.PowerQueryUsages.Count == 0)
        {
            AppendSectionEmptyState(html, "No Power Query queries found", "PBI Assure did not find any Power Query queries it can analyse in this project.", "unavailable");
            html.AppendLine("    </section>");
            return;
        }

        AppendDataSourceSummary(html, inventory);

        AppendInvestigationStart(html, "query", "Search queries", "Search query names, connectors, dependencies or model tables");
        AppendInvestigationFacet(html, "query", "load-state", "Load state", "All load states", inventory.PowerQueryUsages.Select(usage => usage.UsageState).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value == PowerQueryUsageStates.LoadedToModel ? "Loaded to model" : value == PowerQueryUsageStates.SupportingQuery ? "Supporting query" : "Apparently unused")));
        AppendInvestigationFacet(html, "query", "connector", "Connector type", "All connector types", inventory.DataSources.Select(source => source.ConnectorFamily).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value!, value!)));
        AppendInvestigationFacet(html, "query", "role", "How query is used", "All uses", inventory.PowerQueryUsages.Select(usage => usage.QueryRole).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value!, PowerQueryRoleLabel(inventory.PowerQueryUsages.First(usage => usage.QueryRole == value)))));
        AppendInvestigationEnd(html, "query", inventory.PowerQueryUsages.Count, "query", "queries");
        html.AppendLine("      <div id=\"query-list\">");

        foreach (var modelGroup in inventory.PowerQueryUsages.GroupBy(usage => usage.SemanticModel))
        {
            html.Append("      <h3>").Append(Encode(modelGroup.Key)).AppendLine("</h3>");
            html.AppendLine("      <div class=\"semantic-table-list\">");
            foreach (var usage in modelGroup.OrderBy(item => PowerQueryUsageOrder(item.UsageState))
                         .ThenBy(item => item.QueryName, StringComparer.OrdinalIgnoreCase))
            {
                var roleLabel = PowerQueryRoleLabel(usage);
                var targets = inventory.PowerQueryDependencies.Where(edge =>
                        edge.SemanticModel == usage.SemanticModel &&
                        edge.FromQueryName == usage.QueryName &&
                        edge.FromSourceKind == usage.SourceKind &&
                        edge.FromPartition == usage.Partition)
                    .Select(edge => edge.ToQueryName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var usedBy = usage.ReferencedBy.Select(item => item.FromQueryName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var connectors = inventory.DataSources.Where(source => source.SemanticModel == usage.SemanticModel && source.QueryName == usage.QueryName)
                    .Select(source => source.ConnectorFamily).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var searchText = string.Join(' ', new[] { usage.QueryName, usage.Table, usage.SemanticModel, roleLabel, PowerQuerySubtitle(usage, targets.Length, usedBy.Length) }
                    .Concat(targets).Concat(usedBy).Concat(connectors));
                html.Append("        <details id=\"").Append(Encode(PowerQueryAnchor(usage)))
                    .Append("\" class=\"semantic-table power-query-card\" data-investigation-item=\"query\" data-search-text=\"").Append(Encode(searchText))
                    .Append("\" data-filter-load-state=\"").Append(Encode(usage.UsageState)).Append("\" data-filter-connector=\"").Append(Encode(string.Join('\u001f', connectors)))
                    .Append("\" data-filter-role=\"").Append(Encode(usage.QueryRole ?? string.Empty)).Append("\"><summary><span class=\"summary-copy\"><strong>")
                    .Append(Encode(usage.QueryName)).Append("</strong><span>")
                    .Append(Encode(PowerQuerySubtitle(usage, targets.Length, usedBy.Length)))
                    .Append("</span></span><span class=\"badge ").Append(UsageClass(
                        usage.UsageState == PowerQueryUsageStates.ApparentlyUnused
                            ? SemanticUsageStates.ApparentlyUnused
                            : usage.UsageState == PowerQueryUsageStates.LoadedToModel
                                ? SemanticUsageStates.DirectlyUsed
                                : SemanticUsageStates.IndirectlyUsed))
                    .Append("\" title=\"").Append(Encode(roleLabel)).Append("\" aria-label=\"")
                    .Append(Encode(roleLabel)).Append("\">")
                    .Append(Encode(PowerQueryRoleBadgeLabel(usage))).AppendLine("</span></summary>");
                html.AppendLine("          <div class=\"query-card-body\">");
                if (usage.Table is not null)
                {
                    html.Append("            <p class=\"query-model-association\">Loads into model table <strong>")
                        .Append(Encode(usage.Table)).AppendLine("</strong>.</p>");
                }
                html.AppendLine("            <section class=\"query-dependencies\" aria-label=\"Query dependencies\">");
                html.AppendLine("              <h4>Dependencies</h4>");
                html.AppendLine("              <dl class=\"query-dependency-grid\">");
                html.AppendLine("                <div><dt>Uses</dt><dd>");
                AppendQueryLinksOrNone(html, inventory, usage.SemanticModel, targets);
                html.AppendLine("</dd></div>");
                html.AppendLine("                <div><dt>Used by</dt><dd>");
                AppendQueryLinksOrNone(html, inventory, usage.SemanticModel, usedBy);
                html.AppendLine("</dd></div>");
                html.AppendLine("              </dl>");
                html.AppendLine("            </section>");
                if (usage.HasDynamicReferences)
                {
                    html.AppendLine("            <p class=\"query-review\"><strong>Review:</strong> This expression constructs references dynamically, so some dependencies may not be visible here.</p>");
                }
                html.AppendLine("            <details class=\"technical-details\"><summary>View M expression</summary><pre><code>");
                html.Append(Encode(usage.Expression));
                html.AppendLine("</code></pre></details>");
                html.AppendLine("          </div>");
                html.AppendLine("        </details>");
            }
            html.AppendLine("      </div>");
        }
        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static void AppendAccessibilityReview(
        StringBuilder html,
        ProjectInventory inventory,
        AssuranceFinding[] findings)
    {
        html.AppendLine("    <section id=\"accessibility-review\" class=\"report-section\" data-report-section=\"accessibility-review\" aria-labelledby=\"accessibility-review-heading\">");
        html.AppendLine("      <h2 id=\"accessibility-review-heading\" tabindex=\"-1\">Accessibility review</h2>");
        html.AppendLine("      <p class=\"section-intro\">Supporting analysis of the existing automated accessibility checks. Review the affected visuals and pages alongside manual WCAG and assistive-technology testing.</p>");
        html.AppendLine("      <div class=\"accessibility-boundary\" role=\"note\"><strong>Review support, not a compliance verdict</strong><p>PBI Assure identifies selected metadata concerns; it does not prove WCAG conformance or replace testing with assistive technology.</p></div>");

        if (findings.Length == 0)
        {
            AppendSectionEmptyState(html, "No accessibility observations", "PBI Assure did not identify any observations from its current accessibility checks. Manual accessibility review is still recommended.", "success");
            html.AppendLine("    </section>");
            return;
        }

        var findingItems = findings.Select(finding => CreateFindingRenderItem(inventory, finding)).ToArray();
        html.AppendLine("      <section class=\"accessibility-summary\" aria-labelledby=\"accessibility-summary-heading\">");
        html.AppendLine("        <h3 id=\"accessibility-summary-heading\">Issue summary</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Observations are grouped by the existing check before their individual evidence. The counts describe affected visuals, items or pages where that is known from the rule's retained evidence.</p>");
        html.AppendLine("        <div class=\"accessibility-summary-list\">");
        foreach (var group in findingItems
                     .Select((item, index) => new IndexedFinding(item, index))
                     .GroupBy(item => item.Item.Finding.RuleId, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var first = group.First();
            var rule = AssuranceRuleCatalog.Find(group.Key);
            var count = AccessibilityAffectedItemCount(group.Select(item => item.Item.Finding));
            html.Append("          <article class=\"accessibility-summary-card\"><h4><code>")
                .Append(Encode(group.Key)).Append("</code><span aria-hidden=\"true\"> — </span>")
                .Append(Encode(rule?.FriendlyName ?? group.Key)).AppendLine("</h4>");
            html.Append("            <p><a href=\"#").Append(FindingAnchor("accessibility-finding", first.Index)).Append("\">")
                .Append(count.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Encode(AccessibilityAffectedItemLabel(group.Key, count))).AppendLine("</a></p>");
            html.AppendLine("          </article>");
        }
        html.AppendLine("        </div>");
        html.AppendLine("      </section>");
        html.AppendLine("      <section aria-labelledby=\"accessibility-details-heading\">");
        html.AppendLine("        <h3 id=\"accessibility-details-heading\">Affected items</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Expand an observation for its location, suggested action and retained technical evidence.</p>");
        AppendDetailsControls(html, "accessibility-finding-list", "accessibility observations");
        html.AppendLine("        <div id=\"accessibility-finding-list\" class=\"card-list\">");
        for (var index = 0; index < findingItems.Length; index++)
        {
            AppendFindingCard(
                html,
                inventory,
                findingItems[index],
                FindingAnchor("accessibility-finding", index),
                "accessibility-finding-card",
                "          ");
        }
        html.AppendLine("        </div>");
        html.AppendLine("      </section>");
        html.AppendLine("    </section>");
    }

    private static void AppendFindingCard(
        StringBuilder html,
        ProjectInventory inventory,
        FindingRenderItem item,
        string anchor,
        string cssClass,
        string indent)
    {
        var finding = item.Finding;
        var context = item.Context;
        html.Append(indent).Append("<details id=\"").Append(anchor).Append("\" class=\"").Append(cssClass).Append("\" data-severity=\"")
            .Append(Encode(finding.Severity)).Append("\" data-filter-severity=\"").Append(Encode(item.FilterSeverity))
            .Append("\" data-filter-rule=\"").Append(Encode(finding.RuleId))
            .Append("\" data-filter-category=\"").Append(Encode(finding.Category))
            .Append("\" data-filter-page=\"").Append(Encode(item.PageKey ?? string.Empty))
            .Append("\" data-filter-visual=\"").Append(Encode(item.VisualKey ?? string.Empty))
            .Append("\" data-filter-table=\"").Append(Encode(item.TableKey ?? string.Empty))
            .Append("\" data-filter-object-type=\"").Append(Encode(item.ObjectType ?? string.Empty))
            .Append("\" data-filter-usage-state=\"").Append(Encode(item.UsageState ?? string.Empty))
            .Append("\" data-search-text=\"").Append(Encode(item.SearchText)).AppendLine("\">");
        html.Append(indent).Append("  <summary><span class=\"badge ").Append(SeverityClass(finding.Severity))
            .Append("\">").Append(Encode(finding.Severity)).Append("</span><span class=\"summary-copy\"><strong>")
            .Append(Encode(FriendlyFindingMessage(finding, context))).Append("</strong>");
        AppendFindingLocationSummary(html, finding, context);
        html.Append(indent).AppendLine("  </span></summary>");
        html.Append(indent).AppendLine("  <div class=\"card-body\">");
        AppendFindingLocation(html, inventory, finding);
        html.Append(indent).AppendLine("    <h3>Suggested action</h3>");
        html.Append(indent).Append("    <p>").Append(Encode(finding.Recommendation)).AppendLine("</p>");
        if (finding.ReferenceUrl is not null && IsSafeHttpUrl(finding.ReferenceUrl))
        {
            html.Append(indent).Append("    <p><a href=\"").Append(Encode(finding.ReferenceUrl))
                .AppendLine("\">Open supporting guidance</a></p>");
        }

        AppendEvidence(html, finding);
        html.Append(indent).AppendLine("  </div>");
        html.Append(indent).AppendLine("</details>");
    }

    private static int AccessibilityAffectedItemCount(IEnumerable<AssuranceFinding> findings)
    {
        var items = findings.ToArray();
        return items.All(finding => string.Equals(finding.RuleId, "PBI-ACCESS-002", StringComparison.OrdinalIgnoreCase))
            ? items.Sum(finding => finding.EvidencePaths.Count)
            : items.Length;
    }

    private static string AccessibilityAffectedItemLabel(string ruleId, int count)
    {
        return ruleId switch
        {
            "PBI-ACCESS-001" or "PBI-ACCESS-003" or "PBI-ACCESS-004" => Pluralize(count, "affected visual", "affected visuals"),
            "PBI-ACCESS-002" => Pluralize(count, "affected item", "affected items"),
            "PBI-ACCESS-005" => Pluralize(count, "affected page", "affected pages"),
            _ => Pluralize(count, "affected item", "affected items"),
        };
    }

    private static void AppendDataSourceSummary(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("      <h3>Data sources</h3>");
        if (inventory.DataSources.Count == 0)
        {
            AppendSectionEmptyState(html, "No recognised data sources", "No supported connector calls were identified in the available Power Query expressions.", "neutral");
            return;
        }

        html.AppendLine("      <div class=\"semantic-table-list\">");
        foreach (var connectorGroup in inventory.DataSources.GroupBy(source => source.ConnectorFamily)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var queries = connectorGroup.Select(source => (source.SemanticModel, source.QueryName))
                .Distinct()
                .OrderBy(item => item.SemanticModel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.QueryName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var locationLabels = connectorGroup.Select(source => SourceLocationLabel(source.LocationKind))
                .Distinct(StringComparer.Ordinal).ToArray();
            html.Append("        <details class=\"semantic-table power-query-card data-source-card\"><summary><span class=\"summary-copy\"><strong>")
                .Append(Encode(connectorGroup.Key)).Append("</strong>");
            AppendSummaryMetadata(html, ("Location", string.Join(", ", locationLabels)));
            html.Append("</span><span class=\"count-pill\">")
                .Append(queries.Length.ToString(CultureInfo.InvariantCulture))
                .Append(queries.Length == 1 ? " query" : " queries")
                .AppendLine("</span></summary>");
            html.AppendLine("          <div class=\"query-card-body\">");
            html.AppendLine("            <section class=\"query-dependencies\" aria-label=\"Queries using this data source\">");
            html.AppendLine("              <h4>Used by</h4>");
            html.Append("              <p class=\"query-link-row\">");
            AppendDataSourceQueryLinks(html, inventory, queries);
            html.AppendLine("</p>");
            html.AppendLine("              <p class=\"secondary\">Raw connection arguments are not repeated in this source summary. Full M expressions remain available in the query details and can contain sensitive values.</p>");
            html.AppendLine("            </section>");
            html.AppendLine("            <details class=\"technical-details\"><summary>Connector details</summary>");
            html.AppendLine("            <ul class=\"plain-list\">");
            foreach (var function in connectorGroup.Select(source => source.ConnectorFunction)
                         .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                html.Append("              <li><code>").Append(Encode(function)).AppendLine("</code></li>");
            }
            html.AppendLine("            </ul>");
            html.AppendLine("          </details>");
            html.AppendLine("          </div>");
            html.AppendLine("        </details>");
        }
        html.AppendLine("      </div>");
    }

    private static string SourceLocationLabel(string locationKind) => locationKind switch
    {
        DataSourceLocationKinds.LocalFile => "File on a developer’s computer",
        DataSourceLocationKinds.NetworkFile => "Network file",
        DataSourceLocationKinds.RelativeFile => "Relative file path",
        DataSourceLocationKinds.WebAddress => "Web or cloud address",
        DataSourceLocationKinds.NamedServer => "Named server or database",
        _ => "Built dynamically or not available in the report metadata",
    };

    private static string PowerQueryRoleLabel(PowerQueryUsage usage) => usage.QueryRole switch
    {
        PowerQueryRoles.LoadedAndSupporting => "Loaded into model and used by other queries",
        PowerQueryRoles.LoadedOnly => "Loaded into model only",
        PowerQueryRoles.HelperOrStaging => "Helper / staging query",
        PowerQueryRoles.ApparentlyOrphaned => "No known use found",
        _ => "How this query is used needs review",
    };

    private static string PowerQueryRoleBadgeLabel(PowerQueryUsage usage) => usage.QueryRole switch
    {
        PowerQueryRoles.LoadedAndSupporting => "Loaded to model + used by other queries",
        PowerQueryRoles.LoadedOnly => "Loaded to model",
        PowerQueryRoles.HelperOrStaging => "Helper / staging",
        PowerQueryRoles.ApparentlyOrphaned => "No known consumers",
        _ => "Review",
    };

    private static string PowerQuerySubtitle(PowerQueryUsage usage, int usesCount, int usedByCount)
    {
        var usedByText = usedByCount == 1 ? "supports 1 query" : $"supports {usedByCount} queries";
        var usesText = usesCount == 1 ? "uses 1 query" : $"uses {usesCount} queries";
        return usage.QueryRole switch
        {
            PowerQueryRoles.LoadedAndSupporting => $"Loads into the model · {usedByText}",
            PowerQueryRoles.LoadedOnly when usesCount > 0 => $"Loads into the model · {usesText}",
            PowerQueryRoles.LoadedOnly => "Loads into the model",
            PowerQueryRoles.HelperOrStaging when usedByCount > 0 => $"Reusable query · {usedByText}",
            PowerQueryRoles.HelperOrStaging => "Reusable query",
            PowerQueryRoles.ApparentlyOrphaned => "Reusable query · no consumers found",
            _ when usage.UsageState == PowerQueryUsageStates.LoadedToModel => "Loads into the model · dependency review needed",
            _ => "Reusable query · dependency review needed",
        };
    }

    private static int PowerQueryUsageOrder(string state) => state switch
    {
        PowerQueryUsageStates.LoadedToModel => 0,
        PowerQueryUsageStates.SupportingQuery => 1,
        _ => 2,
    };

    private static void AppendFindings(
        StringBuilder html,
        ProjectInventory inventory,
        AssuranceFinding[] findings)
    {
        html.AppendLine("    <section id=\"findings\" class=\"report-section\" data-report-section=\"findings\" aria-labelledby=\"findings-heading\">");
        html.AppendLine("      <h2 id=\"findings-heading\" tabindex=\"-1\">Findings</h2>");
        html.AppendLine("      <p class=\"section-intro\">Non-accessibility issues and review points found by automated checks. Expand one to see where it occurs and what to do next.</p>");
        html.AppendLine("      <details class=\"section-help\"><summary>How to use findings</summary><p>A finding is an automated observation, not a verdict on the whole report. Its location shows where PBI Assure found it and Suggested action gives a practical next step. Items marked Review required can be intentional, depending on your report's context.</p></details>");
        AppendRuleCatalogue(html, findings, includeAccessibility: false);
        if (findings.Length == 0)
        {
            AppendSectionEmptyState(html, "No primary assurance findings", "PBI Assure did not identify non-accessibility issues or review items in its current checks. Accessibility observations, if any, are shown separately in Accessibility review. Manual review is still recommended.", "success");
            html.AppendLine("    </section>");
            return;
        }

        var findingItems = findings.Select(finding => CreateFindingRenderItem(inventory, finding)).ToArray();
        html.AppendLine("      <div class=\"finding-investigation\">");
        html.AppendLine("        <div class=\"finding-search\"><label for=\"finding-search\">Search findings</label><input id=\"finding-search\" type=\"search\" autocomplete=\"off\" placeholder=\"Search messages, rules, pages, visuals or model objects\"></div>");
        html.AppendLine("        <details class=\"finding-filter-panel\"><summary>More filters <span id=\"finding-active-filter-count\" class=\"active-filter-count\" hidden></span></summary>");
        html.AppendLine("          <div class=\"finding-facet-grid\" aria-label=\"Filter findings\">");
        AppendFindingFacet(html, "finding-severity", "Severity", "All severities", FindingFacetOptions(findingItems, item => (item.FilterSeverity, item.SeverityLabel)));
        AppendFindingFacet(html, "finding-rule", "Rule", "All rules", FindingFacetOptions(findingItems, item =>
        {
            var metadata = AssuranceRuleCatalog.Find(item.Finding.RuleId);
            return (item.Finding.RuleId, metadata is null ? item.Finding.RuleId : $"{item.Finding.RuleId} — {metadata.FriendlyName}");
        }));
        AppendFindingFacet(html, "finding-category", "Category", "All categories", FindingFacetOptions(findingItems, item => (item.Finding.Category, HumanizeIdentifier(item.Finding.Category))));
        AppendFindingFacet(html, "finding-page", "Page", "All pages", FindingFacetOptions(findingItems, item => (item.PageKey, item.PageLabel)));
        AppendFindingFacet(html, "finding-visual", "Visual", "All visuals", FindingFacetOptions(findingItems, item => (item.VisualKey, item.VisualLabel)));
        AppendFindingFacet(html, "finding-table", "Semantic table", "All tables", FindingFacetOptions(findingItems, item => (item.TableKey, item.TableLabel)));
        AppendFindingFacet(html, "finding-object-type", "Object type", "All object types", FindingFacetOptions(findingItems, item => (item.ObjectType, HumanizeIdentifier(item.ObjectType ?? string.Empty))));
        AppendFindingFacet(html, "finding-usage-state", "Usage state", "All usage states", FindingFacetOptions(findingItems, item => (item.UsageState, UsageLabel(item.UsageState ?? string.Empty))));
        html.AppendLine("          </div>");
        html.AppendLine("        </details>");
        html.AppendLine("      </div>");
        html.AppendLine("      <div class=\"finding-results-row\">");
        html.Append("        <p id=\"finding-filter-status\" class=\"filter-status\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\" tabindex=\"-1\">")
            .Append(findings.Length.ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" findings</p>");
        html.AppendLine("        <button id=\"finding-clear-filters\" type=\"button\" hidden>Clear search and filters</button>");
        html.AppendLine("      </div>");
        html.AppendLine("      <div id=\"finding-active-filters\" class=\"filter-chips\" aria-label=\"Active finding filters\" hidden></div>");
        html.AppendLine("      <div id=\"finding-empty-state\" class=\"finding-empty-state\" role=\"status\" aria-live=\"polite\" hidden><strong>No findings match the current search and filters.</strong><span>Try removing a filter or changing the search text.</span><button type=\"button\" data-clear-finding-filters>Clear search and filters</button></div>");
        AppendDetailsControls(html, "finding-list", "issues");
        html.AppendLine("      <div id=\"finding-list\" class=\"card-list\">");
        for (var index = 0; index < findingItems.Length; index++)
        {
            AppendFindingCard(html, inventory, findingItems[index], FindingAnchor(index), "finding-card", "        ");
        }

        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static void AppendRuleCatalogue(
        StringBuilder html,
        AssuranceFinding[] findings,
        bool includeAccessibility)
    {
        var counts = findings
            .GroupBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        html.AppendLine("      <details class=\"section-help rule-catalogue\"><summary>Checks in PBI Assure <span class=\"rule-catalogue-hint\">Rule catalogue</span></summary>");
        html.AppendLine("        <p>PBI Assure runs these automated checks. No findings from a check does not prove full compliance.</p>");
        foreach (var category in AssuranceRuleCatalog.ActiveRules
                     .Where(rule => includeAccessibility == IsAccessibilityCategory(rule.Category))
                     .GroupBy(rule => rule.Category))
        {
            var categoryId = category.Key.ToLowerInvariant();
            html.Append("        <section class=\"rule-category\" aria-labelledby=\"rule-category-")
                .Append(Encode(categoryId)).AppendLine("\">");
            html.Append("          <h3 id=\"rule-category-").Append(Encode(categoryId)).Append("\">")
                .Append(Encode(HumanizeIdentifier(category.Key))).AppendLine("</h3>");
            html.AppendLine("          <div class=\"rule-catalogue-list\">");
            foreach (var rule in category.OrderBy(rule => rule.RuleId, StringComparer.Ordinal))
            {
                var count = counts.GetValueOrDefault(rule.RuleId);
                html.Append("            <article class=\"rule-catalogue-item\"><h4><code>").Append(Encode(rule.RuleId))
                    .Append("</code><span aria-hidden=\"true\"> — </span>").Append(Encode(rule.FriendlyName)).AppendLine("</h4>");
                html.Append("              <p>").Append(Encode(rule.Description)).AppendLine("</p>");
                if (count > 0)
                {
                    html.Append("              <button type=\"button\" class=\"rule-finding-count\" data-filter-findings-by-rule=\"")
                        .Append(Encode(rule.RuleId)).Append("\" aria-label=\"Show findings for ").Append(Encode(rule.RuleId)).Append(" — ")
                        .Append(Encode(rule.FriendlyName)).Append("\">").Append(count.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                        .Append(Pluralize(count, "finding", "findings")).AppendLine(" in this report</button>");
                }
                else
                {
                    html.AppendLine("              <p class=\"rule-finding-count-empty\">No findings in this report</p>");
                }
                html.AppendLine("            </article>");
            }
            html.AppendLine("          </div>");
            html.AppendLine("        </section>");
        }
        html.AppendLine("      </details>");
    }

    private static void AppendFindingFacet(
        StringBuilder html,
        string id,
        string label,
        string allLabel,
        IReadOnlyList<FindingFacetOption> options)
    {
        if (options.Count == 0)
        {
            return;
        }

        html.Append("            <div><label for=\"").Append(id).Append("\">").Append(Encode(label))
            .Append("</label><select id=\"").Append(id).Append("\" data-finding-facet data-filter-key=\"")
            .Append(id[8..]).Append("\"><option value=\"\">").Append(Encode(allLabel)).AppendLine("</option>");
        foreach (var option in options)
        {
            html.Append("              <option value=\"").Append(Encode(option.Value)).Append("\">")
                .Append(Encode(option.Label)).AppendLine("</option>");
        }

        html.AppendLine("            </select></div>");
    }

    private static FindingFacetOption[] FindingFacetOptions(
        IEnumerable<FindingRenderItem> items,
        Func<FindingRenderItem, (string? Value, string? Label)> selector)
    {
        var options = items.Select(selector)
            .Where(option => !string.IsNullOrWhiteSpace(option.Value) && !string.IsNullOrWhiteSpace(option.Label))
            .GroupBy(option => option.Value!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FindingFacetOption(group.Key, group.First().Label!))
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var duplicateLabels in options
                     .Select((option, index) => (Option: option, Index: index))
                     .GroupBy(item => item.Option.Label, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            var ordinal = 1;
            foreach (var item in duplicateLabels.OrderBy(item => item.Option.Value, StringComparer.OrdinalIgnoreCase))
            {
                options[item.Index] = item.Option with { Label = $"{item.Option.Label} ({ordinal++})" };
            }
        }

        return options;
    }

    private static void AppendInvestigationStart(StringBuilder html, string prefix, string searchLabel, string placeholder)
    {
        html.Append("      <div class=\"finding-investigation investigation-controls\" data-investigation=\"").Append(prefix).AppendLine("\">");
        html.Append("        <div class=\"finding-search\"><label for=\"").Append(prefix).Append("-search\">").Append(Encode(searchLabel))
            .Append("</label><input id=\"").Append(prefix).Append("-search\" type=\"search\" autocomplete=\"off\" placeholder=\"")
            .Append(Encode(placeholder)).AppendLine("\"></div>");
        html.Append("        <details class=\"finding-filter-panel\"><summary>More filters <span id=\"").Append(prefix)
            .AppendLine("-active-filter-count\" class=\"active-filter-count\" hidden></span></summary>");
        html.Append("          <div class=\"finding-facet-grid\" aria-label=\"").Append(Encode($"Filter {searchLabel.ToLowerInvariant()}"))
            .AppendLine("\">");
    }

    private static void AppendInvestigationFacet(StringBuilder html, string prefix, string key, string label, string allLabel, IEnumerable<FindingFacetOption> options, string? selected = null)
    {
        var values = options.ToArray();
        if (values.Length == 0) return;
        html.Append("            <div><label for=\"").Append(prefix).Append('-').Append(key).Append("\">").Append(Encode(label))
            .Append("</label><select id=\"").Append(prefix).Append('-').Append(key).Append("\" data-investigation-facet data-filter-key=\"")
            .Append(key).Append("\"><option value=\"\">").Append(Encode(allLabel)).AppendLine("</option>");
        foreach (var option in values)
        {
            html.Append("              <option value=\"").Append(Encode(option.Value)).Append('"');
            if (string.Equals(option.Value, selected, StringComparison.OrdinalIgnoreCase)) html.Append(" selected");
            html.Append('>').Append(Encode(option.Label)).AppendLine("</option>");
        }
        html.AppendLine("            </select></div>");
    }

    private static void AppendInvestigationEnd(StringBuilder html, string prefix, int initialCount, string singular, string plural)
    {
        html.AppendLine("          </div></details></div>");
        html.AppendLine("      <div class=\"finding-results-row investigation-results-row\">");
        html.Append("        <p id=\"").Append(prefix).Append("-filter-status\" class=\"filter-status\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\">")
            .Append(initialCount.ToString("N0", CultureInfo.InvariantCulture)).Append(' ').Append(initialCount == 1 ? singular : plural).AppendLine("</p>");
        html.Append("        <button id=\"").Append(prefix).AppendLine("-clear-filters\" type=\"button\" hidden>Clear search and filters</button></div>");
        html.Append("      <div id=\"").Append(prefix).AppendLine("-active-filters\" class=\"filter-chips\" aria-label=\"Active filters\" hidden></div>");
        html.Append("      <div id=\"").Append(prefix).Append("-empty-state\" class=\"finding-empty-state investigation-empty-state\" role=\"status\" aria-live=\"polite\" hidden><strong>No ")
            .Append(Encode(plural)).Append(" match the current search and filters.</strong><span>Try removing a filter or changing the search text.</span><button type=\"button\" data-clear-investigation=\"")
            .Append(prefix).AppendLine("\">Clear search and filters</button></div>");
    }

    private static void AppendReportInventory(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"reports\" class=\"report-section\" data-report-section=\"reports\" aria-labelledby=\"reports-heading\">");
        html.AppendLine("      <h2 id=\"reports-heading\" tabindex=\"-1\">Report pages</h2>");
        html.AppendLine("      <p class=\"section-intro\">Browse the report page by page and visual by visual. Expand a visual to see the columns and measures it uses.</p>");
        if (inventory.Reports.Count == 0)
        {
            AppendSectionEmptyState(html, "No report pages available", "No supported Power BI report definition was found in the selected project.", "unavailable");
            html.AppendLine("    </section>");
            return;
        }

        AppendSummaryDefinitions(html, "What these page metrics count", [
            ("Configured visual interactions", "Saved edit-interaction settings between source and target visuals, such as filtering, highlighting or no interaction."),
            ("Model object references", "References from visuals and page-level settings to semantic-model objects. Repeated uses of the same object are counted separately.")]);

        var pages = inventory.Reports.SelectMany(report => report.Pages).ToArray();
        AppendInvestigationStart(html, "page", "Search pages and visuals", "Search page names, visual titles, types or model objects");
        AppendInvestigationFacet(html, "page", "page-type", "Page type", "All page types", pages.Select(page => PageRole(page)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value)));
        AppendInvestigationFacet(html, "page", "visibility", "Visibility", "All visibility states", pages.Select(page => PageVisibility(page)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value)));
        AppendInvestigationFacet(html, "page", "visual-type", "Contains visual type", "All visual types", pages.SelectMany(page => page.Visuals).Select(visual => visual.VisualType).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(HumanizeVisualType).Select(value => new FindingFacetOption(value!, HumanizeVisualType(value))));
        AppendInvestigationEnd(html, "page", inventory.PageCount, "page", "pages");
        AppendDetailsControls(html, "page-list", "pages");
        html.AppendLine("      <div id=\"page-list\" class=\"page-list\">");
        foreach (var report in inventory.Reports)
        {
            if (inventory.ReportCount > 1)
            {
                html.Append("        <h3>").Append(Encode(report.Name)).AppendLine("</h3>");
            }

            AppendModelConnection(html, report);
            AppendReportMeasures(html, report);

            foreach (var page in report.Pages)
            {
                AppendPageCard(html, inventory, report, page);
            }
        }

        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static void AppendModelConnection(StringBuilder html, ReportInventory report)
    {
        var connection = report.ModelConnection;
        var message = connection.ConnectionKind switch
        {
            ReportModelConnectionKinds.ByPath when connection.IsTargetAvailableLocally =>
                $"Uses semantic model {connection.TargetSemanticModelName}; its definition is available in this project.",
            ReportModelConnectionKinds.ByPath =>
                $"Uses semantic model {connection.TargetSemanticModelName ?? "at the configured path"}, but its definition was not found in this project.",
            ReportModelConnectionKinds.ByConnection =>
                "Uses a live-connected semantic model. Its definition is not stored in this project, so model usage cannot be assessed locally.",
            _ =>
                "No explicit semantic-model connection was found. Local analysis uses the report name as a compatibility fallback.",
        };
        html.Append("        <p class=\"summary-note\"><strong>Data model:</strong> ")
            .Append(Encode(message)).AppendLine("</p>");
    }

    private static void AppendReportMeasures(StringBuilder html, ReportInventory report)
    {
        if (report.ReportMeasures.Count == 0)
        {
            return;
        }

        html.Append("        <details class=\"page-card\"><summary><span class=\"summary-copy\"><span class=\"kicker\">Report calculations</span><strong>")
            .Append(report.ReportMeasureCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" measures defined only in this report</strong><span>Expand to review their formulas and dependencies.</span></span></summary>");
        html.AppendLine("          <div class=\"semantic-table-list\">");
        foreach (var measure in report.ReportMeasures.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var isUsed = report.FieldReferences
                .Concat(report.Pages.SelectMany(page => page.FieldReferences))
                .Concat(report.Pages.SelectMany(page => page.Visuals.SelectMany(visual => visual.FieldReferences)))
                .Any(reference => reference.ObjectType == SemanticObjectTypes.Measure &&
                    string.Equals(reference.Table, measure.Entity, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.ObjectName, measure.Name, StringComparison.OrdinalIgnoreCase));
            html.Append("            <details class=\"semantic-table\"><summary><span class=\"summary-copy\"><strong>")
                .Append(Encode(measure.Name)).Append("</strong><span>")
                .Append(Encode(measure.Entity)).Append(" · ")
                .Append(isUsed ? "used on the report" : "not placed directly on the report")
                .AppendLine("</span></span></summary>");
            html.AppendLine("              <dl class=\"facts\">");
            AppendFact(html, "Formula", measure.Expression, code: true);
            AppendFact(html, "Data type", measure.DataType);
            if (!string.IsNullOrWhiteSpace(measure.Description))
            {
                AppendFact(html, "Description", measure.Description);
            }
            if (!string.IsNullOrWhiteSpace(measure.FormatString))
            {
                AppendFact(html, "Display format", measure.FormatString, code: true);
            }
            var dependencies = measure.References.Select(reference =>
                $"{reference.Entity}[{reference.Name}] ({(reference.IsReportMeasureReference ? "report measure" : "model measure")})").ToArray();
            AppendFact(html, "Uses", dependencies.Length == 0 ? "No measure dependencies listed" : string.Join(", ", dependencies));
            if (measure.HasUnrecognizedReferences)
            {
                AppendFact(html, "Dependency check", "Power BI could not identify every reference in this formula; review it manually.");
            }
            html.AppendLine("              </dl>");
            html.AppendLine("            </details>");
        }
        html.AppendLine("          </div>");
        html.AppendLine("        </details>");
    }

    private static void AppendRelationships(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"relationships\" class=\"report-section\" data-report-section=\"relationships\" aria-labelledby=\"relationships-heading\">");
        html.AppendLine("      <h2 id=\"relationships-heading\" tabindex=\"-1\">Model relationships</h2>");
        html.AppendLine("      <p class=\"section-intro\">See how tables are connected, whether each connection is active and which way filtering can flow.</p>");
        if (inventory.SemanticRelationshipCount == 0)
        {
            AppendSectionEmptyState(html, "No model relationships found", "The analysed semantic model does not contain any relationships to review.", "neutral");
            html.AppendLine("    </section>");
            return;
        }

        var relationships = inventory.SemanticModels.SelectMany(model => model.Relationships).ToArray();
        AppendInvestigationStart(html, "relationship", "Search relationships", "Search table names, columns, cardinality or filter direction");
        AppendInvestigationFacet(html, "relationship", "status", "Status", "All statuses", [new("active", "Active"), new("inactive", "Inactive")]);
        AppendInvestigationFacet(html, "relationship", "cardinality", "Cardinality", "All cardinalities", relationships.Select(RelationshipCardinalityLabel).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value)));
        AppendInvestigationFacet(html, "relationship", "direction", "Cross-filter direction", "All directions", relationships.Select(item => item.CrossFilteringBehavior).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(RelationshipDirectionLabel).Select(value => new FindingFacetOption(value, RelationshipDirectionLabel(value))));
        AppendInvestigationEnd(html, "relationship", inventory.SemanticRelationshipCount, "relationship", "relationships");
        html.AppendLine("      <div id=\"relationship-filter-list\">");

        foreach (var model in inventory.SemanticModels.Where(model => model.RelationshipCount > 0))
        {
            if (inventory.SemanticModelCount > 1)
            {
                html.Append("      <h3>").Append(Encode(model.Name)).AppendLine("</h3>");
            }

            html.AppendLine("      <div class=\"relationship-list\">");
            foreach (var relationship in model.Relationships
                         .OrderBy(item => item.FromTable, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.ToTable, StringComparer.OrdinalIgnoreCase))
            {
                var cardinality = RelationshipCardinalityLabel(relationship);
                var direction = RelationshipDirectionLabel(relationship.CrossFilteringBehavior);
                var reviewTerms = string.Equals(relationship.CrossFilteringBehavior, "bothDirections", StringComparison.OrdinalIgnoreCase) ||
                                  (string.Equals(relationship.FromCardinality, "many", StringComparison.OrdinalIgnoreCase) && string.Equals(relationship.ToCardinality, "many", StringComparison.OrdinalIgnoreCase))
                    ? "Review relationship risk"
                    : string.Empty;
                var activationLabel = RelationshipActivationLabel(relationship);
                var relationshipSearch = $"{relationship.FromTable} {relationship.FromColumn} {relationship.ToTable} {relationship.ToColumn} {cardinality} {direction} {activationLabel} {reviewTerms}";
                html.Append("        <details class=\"relationship-card\" data-investigation-item=\"relationship\" data-search-text=\"").Append(Encode(relationshipSearch))
                    .Append("\" data-filter-status=\"").Append(relationship.IsActive ? "active" : "inactive")
                    .Append("\" data-filter-cardinality=\"").Append(Encode(cardinality)).Append("\" data-filter-direction=\"")
                    .Append(Encode(relationship.CrossFilteringBehavior)).AppendLine("\">");
                html.Append("          <summary><span class=\"summary-copy\"><strong>")
                    .Append(Encode($"{relationship.FromTable}[{relationship.FromColumn}]"))
                    .Append("</strong><span>").Append(Encode(cardinality)).Append(" · ")
                    .Append(Encode(activationLabel)).Append(" · ")
                    .Append(Encode(direction)).Append("</span><strong>")
                    .Append(Encode($"{relationship.ToTable}[{relationship.ToColumn}]"))
                    .AppendLine("</strong></span></summary>");
                html.AppendLine("          <div class=\"relationship-body\">");
                html.AppendLine("            <dl class=\"facts relationship-facts\">");
                AppendFact(html, "From", $"{relationship.FromTable}[{relationship.FromColumn}] ({RelationshipEndLabel(relationship.FromCardinality)})");
                AppendFact(html, "To", $"{relationship.ToTable}[{relationship.ToColumn}] ({RelationshipEndLabel(relationship.ToCardinality)})");
                AppendFact(html, "Cardinality", cardinality);
                AppendFact(html, "Status", relationship.IsActive ? "Active" : "Inactive");
                AppendFact(html, "Cross-filter direction", direction);
                if (!relationship.IsActive && relationship.Activation is not null)
                {
                    if (relationship.Activation.State == SemanticRelationshipActivationStates.ActivatedByReportUsedDax)
                    {
                        AppendFact(html, "Activated by", FormatRelationshipActivationSources(relationship.Activation.Sources));
                    }
                    else if (relationship.Activation.State == SemanticRelationshipActivationStates.ReferencedOnlyByUnusedDax)
                    {
                        AppendFact(html, "Referenced only by unused DAX", FormatRelationshipActivationSources(relationship.Activation.Sources));
                    }
                }
                html.AppendLine("            </dl>");
                if (!relationship.IsActive && relationship.Activation?.State == SemanticRelationshipActivationStates.NoDetectedActivation)
                {
                    html.AppendLine("            <p class=\"relationship-review\">No <code>USERELATIONSHIP</code> call found in the analysed DAX.</p>");
                }
                if (string.Equals(relationship.CrossFilteringBehavior, "bothDirections", StringComparison.OrdinalIgnoreCase))
                {
                    html.AppendLine("            <p class=\"relationship-review\"><strong>Review:</strong> This relationship filters in both directions. Confirm that bidirectional filtering is required.</p>");
                }
                if (string.Equals(relationship.FromCardinality, "many", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(relationship.ToCardinality, "many", StringComparison.OrdinalIgnoreCase))
                {
                    html.AppendLine("            <p class=\"relationship-review\"><strong>Review:</strong> This is a many-to-many relationship. Confirm that its filter behaviour is intentional.</p>");
                }
                html.AppendLine("            <details class=\"technical-details\"><summary>Technical details</summary><dl class=\"technical-list\">");
                AppendFact(html, "Relationship ID", relationship.Name, code: true);
                AppendFact(html, "Source file", DisplayPath(Path.Combine(model.RelativePath, "definition", "relationships.tmdl")), code: true);
                html.AppendLine("            </dl></details>");
                html.AppendLine("          </div>");
                html.AppendLine("        </details>");
            }
            html.AppendLine("      </div>");
        }

        html.AppendLine("      </div>");

        html.AppendLine("    </section>");
    }

    private static void AppendRowLevelSecurity(
        StringBuilder html,
        ProjectInventory inventory,
        AnalysisCoverage coverage)
    {
        var models = inventory.SemanticModels
            .Where(model => model.Roles.Count > 0)
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (models.Length == 0)
        {
            return;
        }

        html.AppendLine("    <section id=\"row-level-security\" class=\"report-section\" data-report-section=\"row-level-security\" aria-labelledby=\"row-level-security-heading\">");
        html.AppendLine("      <h2 id=\"row-level-security-heading\" tabindex=\"-1\">Security roles</h2>");
        html.AppendLine("      <p class=\"section-intro\">Review the security role definitions, row-level filters and object-level permissions saved in each semantic model.</p>");
        html.AppendLine("      <div class=\"rls-boundary\" role=\"note\"><strong>Project definitions only</strong><p>PBI Assure shows role definitions stored in this project. It cannot see who is assigned to roles in Power BI Service, assess effective runtime identity, confirm the overall security design, or determine whether data can be accessed through another path. It reads row-level filters, table-level metadata permissions and explicitly named column permissions; other role metadata may not be fully checked.</p></div>");
        html.AppendLine("      <div class=\"rls-model-list\">");
        foreach (var model in models)
        {
            var coverageAnchor = coverage.Models
                .FirstOrDefault(item => string.Equals(item.ModelName, model.Name, StringComparison.OrdinalIgnoreCase))
                ?.AnchorId;
            html.AppendLine("        <section class=\"rls-model\">");
            html.Append("          <h3>").Append(Encode(model.Name)).AppendLine("</h3>");
            html.Append("          <p class=\"secondary\">")
                .Append(model.RoleCount.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(model.RoleCount, "role", "roles")).AppendLine(" defined in this semantic model.</p>");
            html.AppendLine("          <div class=\"rls-role-list\">");
            foreach (var role in model.Roles
                         .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                html.Append("            <details class=\"semantic-table rls-role-card\"><summary><span class=\"summary-copy\"><span class=\"kicker\">Security role</span><strong>")
                    .Append(Encode(role.Name)).Append("</strong><span>")
                    .Append(role.TablePermissionCount.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(Pluralize(role.TablePermissionCount, "row-level filter", "row-level filters"));
                if (role.ObjectLevelPermissionCount > 0)
                {
                    html.Append(" · ").Append(role.ObjectLevelPermissionCount.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                        .Append(Pluralize(role.ObjectLevelPermissionCount, "object-level permission", "object-level permissions"));
                }

                html
                    .AppendLine("</span></span></summary>");
                html.AppendLine("              <div class=\"rls-role-body\">");
                if (!string.IsNullOrWhiteSpace(role.ModelPermission))
                {
                    html.AppendLine("                <dl class=\"fact-strip compact rls-role-facts\">");
                    AppendFact(html, "Model permission", HumanizeIdentifier(role.ModelPermission));
                    html.AppendLine("                </dl>");
                }

                html.AppendLine("                <h4>Table filters</h4>");
                var tableFilters = role.TablePermissions
                    .Where(permission => !string.IsNullOrWhiteSpace(permission.FilterExpression))
                    .OrderBy(permission => permission.Table, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(permission => permission.FilterExpression, StringComparer.Ordinal)
                    .ToArray();
                if (tableFilters.Length == 0)
                {
                    html.AppendLine("                <p class=\"secondary\">No table filters were found in this role definition.</p>");
                }
                else
                {
                    html.AppendLine("                <div class=\"rls-filter-list\">");
                    foreach (var permission in tableFilters)
                    {
                        html.AppendLine("                  <article class=\"rls-filter\">");
                        html.Append("                    <h5><span>Table</span>")
                            .Append(Encode(permission.Table)).AppendLine("</h5>");
                        html.AppendLine("                    <pre><code>");
                        html.Append(Encode(permission.FilterExpression));
                        html.AppendLine("</code></pre>");
                        html.AppendLine("                  </article>");
                    }

                    html.AppendLine("                </div>");
                }

                html.AppendLine("                <h4>Object-level permissions</h4>");
                var tablePermissions = role.TablePermissions
                    .Where(permission => !string.IsNullOrWhiteSpace(permission.MetadataPermission))
                    .OrderBy(permission => permission.Table, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var columnPermissions = role.TablePermissions
                    .SelectMany(permission => permission.ColumnPermissions.Select(column => (Table: permission.Table, Column: column)))
                    .OrderBy(permission => permission.Table, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(permission => permission.Column.Column, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (tablePermissions.Length == 0 && columnPermissions.Length == 0)
                {
                    html.AppendLine("                <p class=\"secondary\">No object-level permissions were found in this role definition.</p>");
                }
                else
                {
                    html.AppendLine("                <div class=\"rls-filter-list\">");
                    foreach (var permission in tablePermissions)
                    {
                        html.AppendLine("                  <article class=\"rls-filter\">");
                        html.Append("                    <h5><span>Table protected</span>")
                            .Append(Encode(permission.Table)).AppendLine("</h5>");
                        html.Append("                    <p class=\"secondary\">Metadata access: ")
                            .Append(Encode(HumanizeIdentifier(permission.MetadataPermission!))).AppendLine("</p>");
                        html.AppendLine("                  </article>");
                    }

                    foreach (var permission in columnPermissions)
                    {
                        html.AppendLine("                  <article class=\"rls-filter\">");
                        html.Append("                    <h5><span>Column protected</span>")
                            .Append(Encode($"{permission.Table}[{permission.Column.Column}]")).AppendLine("</h5>");
                        html.Append("                    <p class=\"secondary\">Metadata access: ")
                            .Append(Encode(HumanizeIdentifier(permission.Column.Permission))).AppendLine("</p>");
                        html.AppendLine("                  </article>");
                    }

                    html.AppendLine("                </div>");
                }

                if (role.UnanalyzedConstructs.Count > 0)
                {
                    html.AppendLine("                <p class=\"rls-coverage-note\">Some metadata in this role was not fully checked.");
                    if (coverageAnchor is not null)
                    {
                        html.Append("                  <a href=\"#").Append(Encode(coverageAnchor))
                            .AppendLine("\">Review analysis coverage</a>.");
                    }

                    html.AppendLine("                </p>");
                }

                html.AppendLine("                <details class=\"technical-details\"><summary>Technical details</summary><dl class=\"technical-list\">");
                AppendDefinition(html, "Source file", DisplayPath(role.RelativePath));
                html.AppendLine("                </dl></details>");
                html.AppendLine("              </div>");
                html.AppendLine("            </details>");
            }

            html.AppendLine("          </div>");
            html.AppendLine("        </section>");
        }

        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static string RelationshipCardinalityLabel(SemanticRelationshipInventory relationship) =>
        $"{RelationshipEndLabel(relationship.FromCardinality)}-to-{RelationshipEndLabel(relationship.ToCardinality).ToLowerInvariant()}";

    private static string RelationshipActivationLabel(SemanticRelationshipInventory relationship)
    {
        if (relationship.IsActive || relationship.Activation is null)
        {
            return relationship.IsActive ? "Active" : "Inactive";
        }

        return relationship.Activation.State switch
        {
            SemanticRelationshipActivationStates.ActivatedByReportUsedDax => "Inactive · Activated by report-used DAX",
            SemanticRelationshipActivationStates.ReferencedOnlyByUnusedDax => "Inactive · Referenced only by unused DAX",
            _ => "Inactive · No USERELATIONSHIP call found in analysed DAX",
        };
    }

    private static string FormatRelationshipActivationSources(
        IReadOnlyList<SemanticRelationshipActivationSourceInventory> sources) =>
        string.Join(", ", sources
            .OrderBy(source => source.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.ObjectName, StringComparer.OrdinalIgnoreCase)
            .Select(source => string.IsNullOrWhiteSpace(source.Table)
                ? source.ObjectName
                : $"{source.Table}[{source.ObjectName}]"));

    private static string RelationshipEndLabel(string cardinality) => cardinality.ToLowerInvariant() switch
    {
        "one" => "One",
        "many" => "Many",
        _ => HumanizeIdentifier(cardinality),
    };

    private static string RelationshipDirectionLabel(string direction) => direction.ToLowerInvariant() switch
    {
        "onedirection" => "Single direction",
        "bothdirections" => "Both directions",
        _ => HumanizeIdentifier(direction),
    };

    private static void AppendSemanticUsage(
        StringBuilder html,
        ProjectInventory inventory,
        AnalysisCoverage coverage)
    {
        html.AppendLine("    <section id=\"semantic-usage\" class=\"report-section\" data-report-section=\"semantic-usage\" aria-labelledby=\"semantic-usage-heading\">");
        html.AppendLine("      <h2 id=\"semantic-usage-heading\" tabindex=\"-1\">Semantic model</h2>");
        html.AppendLine("      <p class=\"section-intro\">Review tables, columns, measures and other model objects. Expand an object to see why it has its status, where it is used and, where available, its DAX expression.</p>");
        AppendUsageGuide(html, coverage);
        if (inventory.SemanticModels.Count == 0)
        {
            AppendSectionEmptyState(html, "No semantic model available", "No supported local semantic-model definition was found in the selected project.", "unavailable");
            html.AppendLine("    </section>");
            return;
        }

        AppendInvestigationStart(html, "usage", "Search model objects", "Search tables, columns, measures or usage reasons");
        AppendInvestigationFacet(html, "usage", "table", "Table", "All tables", inventory.SemanticObjectUsages.Select(usage => usage.Table).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value)));
        AppendInvestigationFacet(html, "usage", "object-type", "Object type", "All object types", inventory.SemanticObjectUsages.Select(usage => usage.ObjectType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, HumanizeIdentifier(value))));
        AppendInvestigationFacet(html, "usage", "usage-state", "Usage state", "All usage states", inventory.SemanticObjectUsages.Select(usage => usage.UsageState).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(UsageOrder).Select(value => new FindingFacetOption(value, UsageLabel(value))));
        AppendInvestigationFacet(html, "usage", "origin", "Created by", "All objects", [new("developer", "Created in this model"), new("system", "Created by Power BI")], "developer");
        AppendInvestigationEnd(html, "usage", inventory.DeveloperSemanticObjectCount, "model object", "model objects");
        AppendDetailsControls(html, "semantic-table-list", "tables");
        html.AppendLine("      <div id=\"semantic-table-list\" class=\"semantic-table-list\">");
        foreach (var model in inventory.SemanticModels)
        {
            AppendSemanticModel(html, inventory, model, coverage);
        }

        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static void AppendDetailsControls(StringBuilder html, string targetId, string itemLabel)
    {
        html.Append("      <div class=\"details-controls\"><button type=\"button\" data-details-action=\"expand\" data-target=\"")
            .Append(Encode(targetId)).Append("\">Expand all ").Append(Encode(itemLabel))
            .Append("</button><button type=\"button\" data-details-action=\"collapse\" data-target=\"")
            .Append(Encode(targetId)).Append("\">Collapse all ").Append(Encode(itemLabel)).AppendLine("</button></div>");
    }

    private static void AppendPageCard(
        StringBuilder html,
        ProjectInventory inventory,
        ReportInventory report,
        PageInventory page)
    {
        var hierarchyContexts = BuildVisualHierarchyContexts(page);
        int? pageNumber = page.Order is null ? null : page.Order.Value + 1;
        var pageFindings = inventory.Findings.Count(finding =>
            string.Equals(finding.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(finding.Page, page.Name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(finding.PageDisplayName, page.DisplayName, StringComparison.OrdinalIgnoreCase)));
        var isLandingPage = !string.IsNullOrWhiteSpace(report.LandingPageName) &&
            string.Equals(report.LandingPageName, page.Name, StringComparison.OrdinalIgnoreCase);

        var visualTypes = string.Join('\u001f', page.Visuals.Select(visual => visual.VisualType).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
        var pageSearchText = string.Join(' ', new[] { page.DisplayName, page.Name, PageRole(page), PageVisibility(page) }
            .Append(isLandingPage ? "Landing page" : string.Empty)
            .Concat(page.Visuals.SelectMany(visual => new[] { VisualDisplayName(visual), HumanizeVisualType(visual.VisualType), visual.VisualType }))
            .Concat(page.FieldReferences.Select(reference => $"{reference.Table} {reference.ObjectName} {reference.ObjectType}")));
        html.Append("        <details class=\"page-card\" data-investigation-item=\"page\" data-search-text=\"").Append(Encode(pageSearchText))
            .Append("\" data-filter-page-type=\"").Append(Encode(PageRole(page))).Append("\" data-filter-visibility=\"")
            .Append(Encode(PageVisibility(page))).Append("\" data-filter-visual-type=\"").Append(Encode(visualTypes)).Append("\" data-page-name=\"").Append(Encode(page.DisplayName)).Append('"');
        html.AppendLine(">");
        html.Append("          <summary><span class=\"summary-copy\"><span class=\"kicker\">")
            .Append(pageNumber is null ? "Report page" : $"Page {pageNumber}").Append("</span><strong>")
            .Append(Encode(page.DisplayName)).Append("</strong>");
        AppendSummaryMetadata(
            html,
            ("Page type", PageRole(page)),
            ("Visibility", PageVisibility(page)),
            ("Visuals", page.VisualCount.ToString(CultureInfo.InvariantCulture)));
        html.AppendLine("</span>");
        if (isLandingPage)
        {
            html.AppendLine("            <span class=\"badge badge-neutral\">Landing page</span>");
        }

        if (pageFindings > 0)
        {
            html.Append("            <span class=\"count-pill\">").Append(pageFindings.ToString(CultureInfo.InvariantCulture))
                .Append(pageFindings == 1 ? " issue" : " issues").AppendLine("</span>");
        }

        html.AppendLine("          </summary>");
        html.AppendLine("          <div class=\"page-body\">");
        html.AppendLine("            <dl class=\"fact-strip\">");
        AppendFact(html, "Visuals", page.VisualCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Page filters", page.FilterCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Configured visual interactions", page.VisualInteractionCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Model object references", page.FieldReferenceCount.ToString(CultureInfo.InvariantCulture));
        html.AppendLine("            </dl>");

        if (page.FieldReferences.Count > 0)
        {
            html.AppendLine("            <h3>Objects used at page level</h3>");
            html.AppendLine("            <p class=\"secondary\">These are used by page filters, drillthrough or other page-level settings.</p>");
            AppendGroupedFieldReferenceList(html, page.FieldReferences, visualScope: false);
        }

        html.AppendLine("            <h3>Visuals on this page</h3>");
        if (page.Visuals.Count == 0)
        {
            html.AppendLine("            <p>No visuals were found on this page.</p>");
        }
        else
        {
            html.AppendLine("            <div class=\"visual-list\">");
            foreach (var visual in page.Visuals)
            {
                AppendVisualCard(html, inventory, report, page, visual, hierarchyContexts[visual.RelativePath]);
            }

            html.AppendLine("            </div>");
        }

        html.AppendLine("          </div>");
        html.AppendLine("        </details>");
    }

    private static void AppendVisualCard(
        StringBuilder html,
        ProjectInventory inventory,
        ReportInventory report,
        PageInventory page,
        VisualInventory visual,
        VisualHierarchyContext hierarchyContext)
    {
        var relatedFindings = inventory.Findings
            .Select((finding, index) => (Finding: finding, Index: index))
            .Where(item =>
                string.Equals(item.Finding.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Finding.Page, page.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Finding.Visual, visual.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        html.Append("              <details id=\"").Append(Encode(VisualAnchor(report, page, visual)))
            .AppendLine("\" class=\"visual-card\">");
        html.AppendLine("                <summary><span class=\"summary-copy\">");
        html.Append("                  <span class=\"visual-name\">");
        AppendVisualIdentity(html, visual);
        html.Append("</span><span>").Append(Encode(DescribePosition(page, visual))).Append(" · ")
            .Append(visual.DistinctFieldCount.ToString(CultureInfo.InvariantCulture))
            .Append(visual.DistinctFieldCount == 1 ? " object used" : " objects used").AppendLine("</span></span>");
        if (relatedFindings.Length > 0)
        {
            html.Append("                  <span class=\"count-pill\">").Append(relatedFindings.Length.ToString(CultureInfo.InvariantCulture))
                .Append(relatedFindings.Length == 1 ? " issue" : " issues").AppendLine("</span>");
        }

        html.AppendLine("                </summary>");
        html.AppendLine("                <div class=\"visual-body\">");
        html.AppendLine("                  <h4>Objects used by this visual</h4>");
        if (visual.FieldReferences.Count == 0)
        {
            html.AppendLine("                  <p>No model columns or measures were detected for this visual.</p>");
        }
        else
        {
            AppendGroupedFieldReferenceList(html, visual.FieldReferences, visualScope: true);
        }

        AppendVisualBehaviour(html, report, visual);
        AppendAccessibilitySummary(
            html,
            visual,
            hierarchyContext,
            $"tab-order-help-{VisualAnchor(report, page, visual)}");

        if (relatedFindings.Length > 0)
        {
            html.AppendLine("                  <h4>Issues for this visual</h4>");
            html.AppendLine("                  <ul class=\"related-findings\">");
            foreach (var item in relatedFindings)
            {
                html.Append("                    <li><span class=\"badge ").Append(SeverityClass(item.Finding.Severity))
                    .Append("\">").Append(Encode(item.Finding.Severity)).Append("</span> <a href=\"#")
                    .Append(FindingAnchor(inventory, item.Finding, item.Index)).Append("\">")
                    .Append(Encode(FriendlyFindingMessage(item.Finding, new VisualContext(report, page, visual))))
                    .AppendLine("</a></li>");
            }

            html.AppendLine("                  </ul>");
        }

        html.AppendLine("                  <details class=\"technical-details\"><summary>Technical details</summary>");
        html.AppendLine("                    <dl class=\"technical-list\">");
        AppendFact(html, "Visual ID", visual.Name, code: true);
        AppendFact(html, "Source file", DisplayPath(visual.RelativePath), code: true);
        AppendFact(html, "Position", FormatCoordinates(visual.Position));
        AppendFact(
            html,
            "PBIR position.tabOrder value",
            visual.Position.TabOrder?.ToString(CultureInfo.InvariantCulture) ?? "Not present");
        html.AppendLine("                    </dl>");
        html.AppendLine("                  </details>");
        html.AppendLine("                </div>");
        html.AppendLine("              </details>");
    }

    private static void AppendFieldReferenceList(StringBuilder html, IReadOnlyList<VisualFieldReference> references)
    {
        var objects = references
            .DistinctBy(reference => $"{reference.Table}\u001f{reference.ObjectName}\u001f{reference.ObjectType}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        html.AppendLine("                  <ul class=\"object-list\">");
        foreach (var reference in objects)
        {
            html.Append("                    <li><code>").Append(Encode($"{reference.Table}[{reference.ObjectName}]")).Append("</code><span>")
                .Append(Encode(HumanizeIdentifier(reference.ObjectType)));
            if (!string.IsNullOrWhiteSpace(reference.Role))
            {
                html.Append(" · ").Append(Encode(HumanizeIdentifier(reference.Role)));
            }

            html.AppendLine("</span></li>");
        }

        html.AppendLine("                  </ul>");
    }

    private static void AppendGroupedFieldReferenceList(
        StringBuilder html,
        IReadOnlyList<VisualFieldReference> references,
        bool visualScope)
    {
        var objects = references
            .GroupBy(reference => $"{reference.Table}\u001f{reference.ObjectName}\u001f{reference.ObjectType}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Reference = group.First(),
                References = group.ToArray(),
            })
            .OrderBy(item => item.Reference.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Reference.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        html.AppendLine("                  <ul class=\"object-list\">");
        foreach (var item in objects)
        {
            var reference = item.Reference;
            var roleLabels = MeaningfulUsageRoleLabels(
                item.References.Select(candidate => new UsagePresentationReference(
                    candidate.UsageContext,
                    candidate.Role,
                    candidate.EvidencePath)),
                visualScope,
                pageScope: !visualScope);
            html.Append("                    <li><code>").Append(Encode($"{reference.Table}[{reference.ObjectName}]")).Append("</code><span>")
                .Append(Encode(HumanizeIdentifier(reference.ObjectType)));
            if (roleLabels.Length > 0)
            {
                html.Append(" — <span class=\"usage-label\">Used as:</span> ")
                    .Append(Encode(string.Join(" · ", roleLabels)));
            }

            html.AppendLine("</span></li>");
        }

        html.AppendLine("                  </ul>");
    }

    private static void AppendVisualBehaviour(StringBuilder html, ReportInventory report, VisualInventory visual)
    {
        if (visual.Actions.Count == 0 && visual.TooltipBindings.Count == 0)
        {
            return;
        }

        html.AppendLine("                  <h4>Behaviour</h4>");
        html.AppendLine("                  <ul class=\"plain-list\">");
        foreach (var action in visual.Actions)
        {
            html.Append("                    <li>").Append(Encode(DescribeAction(report, action))).AppendLine("</li>");
        }

        foreach (var tooltip in visual.TooltipBindings)
        {
            var state = tooltip.IsEnabled == false ? "Disabled" : "Report-page tooltip";
            var target = string.IsNullOrWhiteSpace(tooltip.TargetPage)
                ? "dynamic or unspecified page"
                : $"page “{FriendlyPageName(report, tooltip.TargetPage)}”";
            html.Append("                    <li>").Append(Encode($"{state}: {target}")).AppendLine("</li>");
        }

        html.AppendLine("                  </ul>");
    }

    private static void AppendAccessibilitySummary(
        StringBuilder html,
        VisualInventory visual,
        VisualHierarchyContext hierarchyContext,
        string tooltipId)
    {
        html.AppendLine("                  <h4>Accessibility snapshot</h4>");
        html.AppendLine("                  <dl class=\"fact-strip compact\">");
        var altText = visual.Accessibility.AltTextIsDynamic
            ? "Dynamic alt text"
            : visual.Accessibility.HasAltText
                ? visual.Accessibility.AltText ?? "Configured"
                : "Not configured";
        AppendFact(html, "Alt text", altText);
        AppendTabOrderFact(html, DescribeTabOrder(visual, hierarchyContext), tooltipId);
        AppendFact(
            html,
            "Title",
            visual.Accessibility.TitleIsVisible == false ? "Hidden" : "Visible or default");
        html.AppendLine("                  </dl>");
    }

    private static void AppendSemanticModel(
        StringBuilder html,
        ProjectInventory inventory,
        SemanticModelInventory model,
        AnalysisCoverage coverage)
    {
        var coverageAnchor = coverage.Models
            .FirstOrDefault(item => string.Equals(item.ModelName, model.Name, StringComparison.OrdinalIgnoreCase))
            ?.AnchorId;
        html.AppendLine("        <section class=\"model-block\">");
        html.Append("          <h3>").Append(Encode(model.Name)).AppendLine("</h3>");
        html.AppendLine("          <dl class=\"fact-strip\">");
        var generatedTables = model.Tables.Count(table => table.IsSystemGenerated);
        var modelUsages = inventory.SemanticObjectUsages.Where(usage =>
            string.Equals(usage.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var generatedObjects = modelUsages.Count(inventory.IsSystemGeneratedSemanticObject);
        AppendFact(html, "Developer tables", (model.TableCount - generatedTables).ToString(CultureInfo.InvariantCulture));
        if (generatedTables > 0)
        {
            AppendFact(html, "System-generated tables", generatedTables.ToString(CultureInfo.InvariantCulture));
        }
        AppendFact(html, "Columns", model.ColumnCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Measures", model.MeasureCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Your model objects", (modelUsages.Length - generatedObjects).ToString(CultureInfo.InvariantCulture));
        if (generatedObjects > 0)
        {
            AppendFact(html, "System-generated model objects", generatedObjects.ToString(CultureInfo.InvariantCulture));
        }
        AppendFact(html, "Relationships", model.RelationshipCount.ToString(CultureInfo.InvariantCulture));
        if (model.FieldParameterCount > 0)
        {
            AppendFact(html, "Field parameters", model.FieldParameterCount.ToString(CultureInfo.InvariantCulture));
        }

        if (model.CalculationGroupCount > 0)
        {
            AppendFact(html, "Calculation groups", model.CalculationGroupCount.ToString(CultureInfo.InvariantCulture));
            AppendFact(html, "Calculation items", model.CalculationItemCount.ToString(CultureInfo.InvariantCulture));
        }

        html.AppendLine("          </dl>");

        foreach (var table in model.Tables.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase))
        {
            var usages = inventory.SemanticObjectUsages
                .Where(usage =>
                    string.Equals(usage.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(usage.Table, table.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(usage => UsageOrder(usage.UsageState))
                .ThenBy(usage => usage.ObjectType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var unusedCount = usages.Count(usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);

            html.Append("          <details class=\"semantic-table");
            if (table.IsSystemGenerated)
            {
                html.Append(" system-generated-table");
            }
            html.Append("\" data-object-origin=\"").Append(table.IsSystemGenerated ? "system" : "developer")
                .Append("\"><summary><span class=\"summary-copy\"><span class=\"kicker\">")
                .Append(Encode(SemanticTableKicker(table))).Append("</span><strong>")
                .Append(Encode(table.Name)).Append("</strong><span>")
                .Append(usages.Length.ToString(CultureInfo.InvariantCulture)).Append(" objects");
            if (table.IsHidden)
            {
                html.Append(" · hidden table");
            }

            if (table.IsFieldParameter)
            {
                html.Append(" · field parameter");
            }

            if (table.IsCalculationGroup)
            {
                html.Append(" · calculation group");
            }

            if (table.IsSystemGenerated)
            {
                html.Append(" · Power BI-generated Auto Date/Time table");
            }

            html.Append("</span></span>");
            if (unusedCount > 0)
            {
                html.Append("<span class=\"count-pill\">").Append(unusedCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" apparently unused</span>");
            }

            html.AppendLine("</summary>");
            AppendSemanticTablePowerQueryContext(html, inventory, model, table, usages, unusedCount);
            AppendSemanticFeatures(html, table);
            AppendCalculatedTableExpressions(html, table);
            html.AppendLine("            <ul class=\"semantic-object-list\">");
            foreach (var usage in usages)
            {
                var usageReason = usage.DirectReportLocationCount == 0
                    ? DescribeSemanticUsageReason(inventory, usage)
                    : null;
                html.Append("              <li class=\"semantic-object\" data-investigation-item=\"usage\" data-filter-table=\"").Append(Encode(table.Name))
                    .Append("\" data-filter-object-type=\"").Append(Encode(usage.ObjectType)).Append("\" data-filter-usage-state=\"").Append(Encode(usage.UsageState))
                    .Append("\" data-filter-origin=\"").Append(table.IsSystemGenerated ? "system" : "developer").Append("\" data-usage-state=\"").Append(Encode(usage.UsageState))
                    .Append("\" data-object-type=\"").Append(Encode(usage.ObjectType))
                    .Append("\" data-object-origin=\"").Append(table.IsSystemGenerated ? "system" : "developer")
                    .Append("\" data-classification-confidence=\"").Append(Encode(usage.ClassificationConfidence))
                    // Typing "qualified" into the existing search finds every qualified classification,
                    // so discoverability does not depend on spotting the marker.
                    .Append("\" data-search-text=\"").Append(Encode(
                        $"{table.Name} {usage.ObjectName} {HumanizeIdentifier(usage.ObjectType)} {UsageLabel(usage.UsageState)} " +
                        $"{ConfidenceSearchText(usage)}{usageReason}"))
                    .Append("\"><div class=\"semantic-object-header\"><span class=\"object-name\"><strong>").Append(Encode(usage.ObjectName))
                    .Append("</strong><span>").Append(Encode(HumanizeIdentifier(usage.ObjectType)));
                if (usage.DirectReportLocationCount > 0)
                {
                    html.Append(" · used in ").Append(usage.DirectReportLocationCount.ToString(CultureInfo.InvariantCulture))
                        .Append(usage.DirectReportLocationCount == 1 ? " report location" : " report locations");
                }

                html.Append("</span></span><span class=\"badge ").Append(UsageClass(usage.UsageState)).Append("\">")
                    .Append(Encode(UsageLabel(usage.UsageState))).Append("</span>");
                AppendClassificationConfidence(html, usage, coverageAnchor);
                html.AppendLine("</div>");
                if (usageReason is not null)
                {
                    html.Append("                <p class=\"usage-reason\">").Append(Encode(usageReason)).AppendLine("</p>");
                }
                AppendPowerQueryColumnUsage(html, inventory, usage);
                AppendUsageDetails(html, inventory, usage);
                AppendSemanticObjectExpression(html, table, usage);
                html.AppendLine("              </li>");
            }

            html.AppendLine("            </ul>");
            html.AppendLine("          </details>");
        }

        html.AppendLine("        </section>");
    }

    private static string SemanticTableKicker(SemanticTableInventory table)
    {
        if (table.IsSystemGenerated)
        {
            return "Power BI-generated table";
        }

        if (table.IsFieldParameter)
        {
            return "Field parameter table";
        }

        if (table.IsCalculationGroup)
        {
            return "Calculation group table";
        }

        return "Model table";
    }

    private static void AppendUsageGuide(StringBuilder html, AnalysisCoverage coverage)
    {
        html.AppendLine("      <details class=\"usage-guide\"><summary><span>How usage classification works</span><span class=\"usage-guide-hint\">5 statuses explained</span></summary>");
        html.AppendLine("        <div class=\"usage-guide-body\"><dl class=\"usage-classification-list\">");
        AppendUsageGuideItem(html, "Directly used", "Used somewhere in the report, such as a visual, filter, tooltip or drillthrough setting.", SemanticUsageStates.DirectlyUsed);
        AppendUsageGuideItem(html, "Indirectly used", "Not used directly in the report, but needed by something that is.", SemanticUsageStates.IndirectlyUsed);
        AppendUsageGuideItem(html, "Structurally required", "Needed for the model to work, for example in a relationship, hierarchy or sort-by setting.", SemanticUsageStates.StructurallyRequired);
        AppendUsageGuideItem(html, "Only used by unused items", "Only used by other model items that themselves have no detected report usage.", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AppendUsageGuideItem(html, "Apparently unused", "PBI Assure could not find anything in this project that uses it. Check before removing it because external reports and dynamic behaviour may not be visible here.", SemanticUsageStates.ApparentlyUnused);
        html.AppendLine("        </dl>");
        if (coverage.QualifiedObjectCount > 0)
        {
            html.Append("        <p class=\"usage-guide-note\">A result can also be marked <span class=\"confidence-flag confidence-flag-sample\">")
                .Append(CoverageMarkerLabel)
                .AppendLine("</span>. <strong>That is not another status.</strong> The status above is unchanged and remains the best answer available; the marker means PBI Assure could not check every possible source of usage in this model. See <a href=\"#analysis-coverage\">Analysis coverage</a>.</p>");
        }

        html.AppendLine("        </div>");
        html.AppendLine("      </details>");
    }

    private static void AppendUsageGuideItem(StringBuilder html, string label, string description, string usageState)
    {
        html.Append("          <div class=\"usage-classification-row\"><dt><span class=\"badge ")
            .Append(UsageClass(usageState)).Append("\">").Append(Encode(label))
            .Append("</span></dt><dd>").Append(Encode(description)).AppendLine("</dd></div>");
    }

    private static void AppendUsageDetails(StringBuilder html, ProjectInventory inventory, SemanticObjectUsage usage)
    {
        if (usage.DirectReportLocations.Count == 0)
        {
            return;
        }

        html.AppendLine("                <details class=\"usage-details\"><summary>Where used</summary><div class=\"usage-location-groups\">");
        foreach (var locationGroup in usage.DirectReportLocations.GroupBy(
                     location => $"{location.Report}\u001f{location.Page}",
                     StringComparer.OrdinalIgnoreCase))
        {
            var firstLocation = locationGroup.First();
            var report = inventory.Reports.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, firstLocation.Report, StringComparison.OrdinalIgnoreCase));
            var page = report?.Pages.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, firstLocation.Page, StringComparison.OrdinalIgnoreCase));
            var pageLabel = page?.DisplayName ?? firstLocation.Page;

            html.AppendLine("                  <section class=\"usage-page-group\">");
            html.AppendLine("                    <header class=\"usage-page-heading\">");
            if (inventory.ReportCount > 1)
            {
                html.Append("                    <p class=\"usage-report\"><span class=\"usage-label\">Report:</span> ")
                    .Append(Encode(report?.Name ?? firstLocation.Report)).AppendLine("</p>");
            }

            if (!string.IsNullOrWhiteSpace(pageLabel))
            {
                html.AppendLine("                      <span class=\"usage-group-type\">Report page</span>");
                html.Append("                      <h5>").Append(Encode(pageLabel)).AppendLine("</h5>");
                if (page is not null && !string.Equals(PageRole(page), "Standard", StringComparison.OrdinalIgnoreCase))
                {
                    html.Append("                      <p class=\"usage-page-kind\">")
                        .Append(Encode($"{PageRole(page)} page")).AppendLine("</p>");
                }
            }
            else
            {
                html.AppendLine("                      <span class=\"usage-group-type\">Report</span>");
                html.AppendLine("                      <h5>Report-level use</h5>");
            }
            html.AppendLine("                    </header>");

            html.AppendLine("                    <ul class=\"usage-location-list\">");
            foreach (var location in locationGroup)
            {
                var visual = page?.Visuals.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, location.Visual, StringComparison.OrdinalIgnoreCase));
                var locationLabel = visual is not null
                    ? VisualDisplayName(visual)
                    : location.UsageContext == UsageContexts.Drillthrough
                        ? "Drillthrough field"
                        : HumanizeIdentifier(location.UsageContext ?? location.LocationKind);
                var roleLabel = UsageRoleLabel(usage, location, visual is not null);
                html.AppendLine("                      <li>");
                if (visual is not null && report is not null && page is not null)
                {
                    var visualType = HumanizeVisualType(visual.VisualType);
                    html.Append("                        <span class=\"usage-visual\"><span class=\"usage-label\">Visual:</span> <a href=\"#")
                        .Append(Encode(VisualAnchor(report, page, visual))).Append("\">")
                        .Append(Encode(locationLabel)).Append("</a>");
                    if (!string.Equals(locationLabel, visualType, StringComparison.OrdinalIgnoreCase))
                    {
                        html.Append(" · ").Append(Encode(visualType));
                    }
                    html.Append(" · ").Append(Encode(DescribePosition(page, visual))).AppendLine("</span>");
                }
                else
                {
                    html.Append("                        <span class=\"usage-context\"><span class=\"usage-label\">Used in:</span> ")
                        .Append(Encode(locationLabel)).AppendLine("</span>");
                }
                if (!string.IsNullOrWhiteSpace(roleLabel) &&
                    !string.Equals(roleLabel, locationLabel, StringComparison.OrdinalIgnoreCase))
                {
                    html.Append("                        <span class=\"usage-role\"><span class=\"usage-label\">Used as:</span> ")
                        .Append(Encode(roleLabel)).AppendLine("</span>");
                }
                html.AppendLine("                      </li>");
            }

            html.AppendLine("                    </ul>");
            html.AppendLine("                  </section>");
        }
        html.AppendLine("                </div></details>");
    }

    private static string UsageRoleLabel(SemanticObjectUsage usage, SemanticUsageLocation location, bool hasVisual)
    {
        var references = usage.DirectReportReferences
            .Where(evidence => string.Equals(evidence.Report, location.Report, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evidence.Page, location.Page, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evidence.Visual, location.Visual, StringComparison.OrdinalIgnoreCase) &&
                (hasVisual || string.Equals(
                    evidence.UsageContext,
                    location.UsageContext,
                    StringComparison.OrdinalIgnoreCase)))
            .Select(evidence => new UsagePresentationReference(
                evidence.UsageContext,
                evidence.Role,
                evidence.EvidencePath))
            .ToArray();

        return string.Join(" · ", MeaningfulUsageRoleLabels(
            references,
            hasVisual,
            pageScope: !string.IsNullOrWhiteSpace(location.Page)));
    }

    private static string[] MeaningfulUsageRoleLabels(
        IEnumerable<UsagePresentationReference> references,
        bool visualScope,
        bool pageScope)
    {
        var instances = references.ToArray();
        var hasNonFilterContext = instances.Any(instance =>
            !string.Equals(instance.UsageContext, UsageContexts.Filter, StringComparison.OrdinalIgnoreCase));
        var hasDrillthroughContext = !visualScope && instances.Any(instance =>
            string.Equals(instance.UsageContext, UsageContexts.Drillthrough, StringComparison.OrdinalIgnoreCase));
        // Desktop PBIR also stores field-only filterConfig entries for ordinary projections.
        // Keep a filter label beside another visual use only when an actual filter condition is present.
        var hasConfiguredFilterCondition = instances.Any(instance =>
            string.Equals(instance.UsageContext, UsageContexts.Filter, StringComparison.OrdinalIgnoreCase) &&
            instance.EvidencePath.Contains(".filter.", StringComparison.OrdinalIgnoreCase));

        return instances
            .Where(instance =>
                !string.Equals(instance.UsageContext, UsageContexts.Filter, StringComparison.OrdinalIgnoreCase) ||
                (!hasDrillthroughContext && (!hasNonFilterContext || hasConfiguredFilterCondition)))
            .GroupBy(
                instance => $"{instance.UsageContext}\u001f{instance.Role ?? string.Empty}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => UsageRoleLabel(group.First(), visualScope, pageScope))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string UsageRoleLabel(
        UsagePresentationReference reference,
        bool visualScope,
        bool pageScope)
    {
        if (!string.IsNullOrWhiteSpace(reference.Role))
        {
            return FieldRoleLabel(reference.Role, visualScope, pageScope);
        }

        return reference.UsageContext switch
        {
            UsageContexts.Filter => FieldRoleLabel("filter", visualScope, pageScope),
            UsageContexts.Drillthrough => "Drillthrough field",
            _ => HumanizeIdentifier(reference.UsageContext),
        };
    }

    private static string FieldRoleLabel(string role, bool visualScope, bool pageScope)
    {
        if (string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase))
        {
            return visualScope ? "Visual filter" : pageScope ? "Page filter" : "Report filter";
        }

        if (string.Equals(role, "drillthrough", StringComparison.OrdinalIgnoreCase))
        {
            return "Drillthrough field";
        }

        return HumanizeIdentifier(role);
    }

    private static void AppendSemanticTablePowerQueryContext(
        StringBuilder html,
        ProjectInventory inventory,
        SemanticModelInventory model,
        SemanticTableInventory table,
        SemanticObjectUsage[] usages,
        int unusedCount)
    {
        if (unusedCount == 0)
        {
            return;
        }

        var contexts = inventory.SemanticTablePowerQueryContexts
            .Where(context =>
                string.Equals(context.SemanticModel, model.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Table, table.Name, StringComparison.OrdinalIgnoreCase) &&
                context.IsRequiredUpstream)
            .ToArray();
        if (contexts.Length == 0)
        {
            return;
        }

        var downstreamQueries = contexts.SelectMany(context => context.UsedByQueries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allObjectsAppearUnused = usages.Length > 0 && unusedCount == usages.Length;

        html.AppendLine("            <aside class=\"power-query-context\" aria-label=\"Power Query dependency\">");
        html.AppendLine("              <h4>Still needed by Power Query</h4>");
        html.Append("              <p>");
        if (allObjectsAppearUnused)
        {
            html.Append("This table's model objects appear unused in the report and model, but ");
        }
        else
        {
            html.Append("Some model objects appear unused, but ");
        }

        var backingQueries = contexts.Select(context => context.QueryName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        AppendQueryLinks(html, inventory, model.Name, backingQueries);
        html.Append(backingQueries.Length == 1 ? " is used by " : " are used by ");
        AppendQueryLinks(html, inventory, model.Name, downstreamQueries);
        html.AppendLine(". Its Power Query is therefore still required while the data is being prepared.</p>");
        html.AppendLine("              <p class=\"secondary\">Check whether this table still needs to be loaded into the model. Keep the query while other queries depend on it.</p>");
        html.AppendLine("            </aside>");
    }

    private static void AppendPowerQueryColumnUsage(
        StringBuilder html,
        ProjectInventory inventory,
        SemanticObjectUsage semanticUsage)
    {
        if (semanticUsage.ObjectType != SemanticObjectTypes.Column)
        {
            return;
        }

        var usages = inventory.PowerQueryColumnUsages.Where(usage =>
                string.Equals(usage.SemanticModel, semanticUsage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(usage.SourceTable, semanticUsage.Table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(usage.SourceColumn, semanticUsage.ObjectName, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(usage => string.Join('\u001f', usage.ConsumerQuery, usage.UsageKind), StringComparer.OrdinalIgnoreCase)
            .OrderBy(usage => usage.ConsumerQuery, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.UsageKind, StringComparer.Ordinal)
            .ToArray();
        if (usages.Length == 0)
        {
            return;
        }

        var isApparentlyUnused = semanticUsage.UsageState == SemanticUsageStates.ApparentlyUnused;
        if (isApparentlyUnused)
        {
            html.AppendLine("                <aside class=\"power-query-column-context\" aria-label=\"Power Query column usage\">");
            html.AppendLine("                  <strong>Power Query usage</strong>");
            html.AppendLine("                  <p>Power Query usage was found even though no semantic or report usage was detected.</p>");
        }
        else
        {
            html.AppendLine("                <details class=\"power-query-column-context compact\"><summary>Power Query usage</summary>");
        }
        html.AppendLine("                  <ul class=\"plain-list\">");
        foreach (var usage in usages)
        {
            html.Append("                    <li>").Append(Encode(PowerQueryColumnUsageLabel(usage))).Append(" <a href=\"#")
                .Append(Encode(PowerQueryAnchorForName(inventory, usage.SemanticModel, usage.ConsumerQuery)))
                .Append("\">Open ").Append(Encode(usage.ConsumerQuery)).AppendLine("</a></li>");
        }
        html.AppendLine("                  </ul>");
        html.AppendLine("                  <details class=\"technical-details\"><summary>Power Query evidence</summary><ul class=\"plain-list\">");
        foreach (var usage in usages)
        {
            html.Append("                    <li><code>").Append(Encode(usage.MFunction)).Append("</code>");
            if (!string.IsNullOrWhiteSpace(usage.StepName))
            {
                html.Append(" · step <code>").Append(Encode(usage.StepName)).Append("</code>");
            }
            html.Append(" · <code>").Append(Encode(DisplayPath(usage.ArtifactPath))).AppendLine("</code></li>");
        }
        html.AppendLine("                  </ul></details>");
        html.AppendLine(isApparentlyUnused ? "                </aside>" : "                </details>");
    }

    private static string PowerQueryColumnUsageLabel(PowerQueryColumnUsage usage) => usage.UsageKind switch
    {
        PowerQueryColumnUsageKinds.MergeKey => $"Used as a merge key by Power Query {usage.ConsumerQuery}.",
        PowerQueryColumnUsageKinds.ExpandedColumn => $"Expanded into Power Query {usage.ConsumerQuery}.",
        PowerQueryColumnUsageKinds.SelectedColumn => $"Selected by Power Query {usage.ConsumerQuery} during data preparation.",
        PowerQueryColumnUsageKinds.RenamedColumn => $"Renamed by Power Query {usage.ConsumerQuery} during data preparation.",
        PowerQueryColumnUsageKinds.RemovedColumn => $"Referenced in a remove-columns step by Power Query {usage.ConsumerQuery}.",
        PowerQueryColumnUsageKinds.TransformedColumn => $"Its type is transformed by Power Query {usage.ConsumerQuery}.",
        _ => $"Used by Power Query {usage.ConsumerQuery} during data preparation.",
    };

    private static void AppendQueryLinks(
        StringBuilder html,
        ProjectInventory inventory,
        string semanticModel,
        IEnumerable<string> queryNames)
    {
        var names = queryNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        for (var index = 0; index < names.Length; index++)
        {
            if (index > 0)
            {
                html.Append(index == names.Length - 1 ? " and " : ", ");
            }

            var name = names[index];
            var usage = inventory.PowerQueryUsages.FirstOrDefault(item =>
                string.Equals(item.SemanticModel, semanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.QueryName, name, StringComparison.OrdinalIgnoreCase));
            if (usage is null)
            {
                html.Append("<strong>").Append(Encode(name)).Append("</strong>");
            }
            else
            {
                html.Append("<a href=\"#").Append(Encode(PowerQueryAnchor(usage))).Append("\">")
                    .Append(Encode(name)).Append("</a>");
            }
        }
    }

    private static void AppendQueryLinksOrNone(
        StringBuilder html,
        ProjectInventory inventory,
        string semanticModel,
        string[] queryNames)
    {
        if (queryNames.Length == 0)
        {
            html.Append("None detected");
            return;
        }

        AppendQueryLinks(html, inventory, semanticModel, queryNames);
    }

    private static void AppendDataSourceQueryLinks(
        StringBuilder html,
        ProjectInventory inventory,
        (string SemanticModel, string QueryName)[] queries)
    {
        for (var index = 0; index < queries.Length; index++)
        {
            if (index > 0)
            {
                html.Append(index == queries.Length - 1 ? " and " : ", ");
            }

            var query = queries[index];
            var usage = inventory.PowerQueryUsages.FirstOrDefault(item =>
                string.Equals(item.SemanticModel, query.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.QueryName, query.QueryName, StringComparison.OrdinalIgnoreCase));
            if (usage is null)
            {
                html.Append("<strong>").Append(Encode(query.QueryName)).Append("</strong>");
            }
            else
            {
                html.Append("<a href=\"#").Append(Encode(PowerQueryAnchor(usage))).Append("\">")
                    .Append(Encode(query.QueryName)).Append("</a>");
            }
        }
    }

    private static void AppendSemanticFeatures(StringBuilder html, SemanticTableInventory table)
    {
        if (table.RefreshPolicy is not null)
        {
            AppendRefreshPolicy(html, table.RefreshPolicy);
        }

        if (table.FieldParameter is not null)
        {
            html.AppendLine("            <section class=\"semantic-feature\">");
            html.Append("              <h4>Field parameter</h4><p>Lets report readers switch between ")
                .Append(table.FieldParameter.EntryCount.ToString(CultureInfo.InvariantCulture))
                .AppendLine(table.FieldParameter.EntryCount == 1 ? " field.</p>" : " fields.</p>");
            html.AppendLine("              <ul class=\"object-list compact\">");
            foreach (var entry in table.FieldParameter.Entries)
            {
                html.Append("                <li><code>")
                    .Append(Encode($"{entry.Table}[{entry.ObjectName}]"))
                    .AppendLine("</code></li>");
            }

            html.AppendLine("              </ul>");
            html.AppendLine("            </section>");
        }

        if (table.CalculationGroup is not null)
        {
            html.AppendLine("            <section class=\"semantic-feature\">");
            html.Append("              <h4>Calculation group</h4><p>Contains ")
                .Append(table.CalculationGroup.ItemCount.ToString(CultureInfo.InvariantCulture))
                .Append(table.CalculationGroup.ItemCount == 1 ? " reusable calculation" : " reusable calculations");
            if (table.CalculationGroup.Precedence is not null)
            {
                html.Append(" with precedence ")
                    .Append(table.CalculationGroup.Precedence.Value.ToString(CultureInfo.InvariantCulture));
            }

            html.AppendLine(".</p>");
            html.AppendLine("              <ul class=\"object-list compact\">");
            foreach (var item in table.CalculationGroup.Items
                         .OrderBy(item => item.Ordinal ?? int.MaxValue)
                         .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                html.Append("                <li class=\"calculation-item\"><span class=\"object-name\"><strong>")
                    .Append(Encode(item.Name)).Append("</strong><span>Calculation item");
                if (item.Ordinal is not null)
                {
                    html.Append(" · order ").Append(item.Ordinal.Value.ToString(CultureInfo.InvariantCulture));
                }

                html.AppendLine("</span></span>");
                AppendDaxExpression(html, item.Expression);
                html.AppendLine("                </li>");
            }

            html.AppendLine("              </ul>");
            html.AppendLine("            </section>");
        }
    }

    private static void AppendRefreshPolicy(StringBuilder html, SemanticRefreshPolicyInventory policy)
    {
        html.AppendLine("            <section class=\"semantic-feature refresh-policy\">");
        html.AppendLine("              <h4>Incremental refresh</h4>");
        html.AppendLine("              <p>A refresh policy is configured for this table. These saved settings do not confirm query folding or a successful refresh in Power BI Service.</p>");
        html.AppendLine("              <dl class=\"fact-strip compact\">");
        AppendFact(
            html,
            "Archive window",
            FormatRefreshPeriod(policy.RollingWindowPeriods, policy.RollingWindowGranularity));
        AppendFact(
            html,
            "Refresh window",
            FormatRefreshPeriod(policy.IncrementalPeriods, policy.IncrementalGranularity));
        if (policy.IncrementalPeriodsOffset == -1)
        {
            AppendFact(html, "Complete periods only", "Yes");
        }
        else if (policy.IncrementalPeriodsOffset is not null)
        {
            AppendFact(
                html,
                "Refresh period offset",
                policy.IncrementalPeriodsOffset.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(policy.PollingExpression))
        {
            AppendFact(
                html,
                "Change detection",
                string.IsNullOrWhiteSpace(policy.ChangeDetectionColumn)
                    ? "Configured"
                    : policy.ChangeDetectionColumn);
        }

        if (!string.IsNullOrWhiteSpace(policy.Mode))
        {
            AppendFact(
                html,
                "Real-time data",
                string.Equals(policy.Mode, "hybrid", StringComparison.OrdinalIgnoreCase)
                    ? "On (hybrid mode)"
                    : $"Off ({HumanizeIdentifier(policy.Mode)} mode)");
        }

        html.AppendLine("              </dl>");
        html.AppendLine("              <details class=\"technical-details\"><summary>Technical details</summary><dl class=\"technical-list\">");
        AppendTechnicalDefinition(html, "Policy type", policy.PolicyType ?? "Not specified");
        AppendTechnicalDefinition(
            html,
            "Incremental periods offset",
            policy.IncrementalPeriodsOffset?.ToString(CultureInfo.InvariantCulture) ?? "Not specified");
        if (!string.IsNullOrWhiteSpace(policy.Mode))
        {
            AppendTechnicalDefinition(html, "Policy mode", policy.Mode);
        }
        html.AppendLine("              </dl>");
        if (!string.IsNullOrWhiteSpace(policy.PollingExpression))
        {
            html.Append("                <p><strong>Change-detection expression</strong></p><pre><code>")
                .Append(Encode(policy.PollingExpression)).AppendLine("</code></pre>");
        }
        html.AppendLine("              </details>");
        html.AppendLine("            </section>");
    }

    private static string FormatRefreshPeriod(int? periods, string? granularity)
    {
        if (periods is null || string.IsNullOrWhiteSpace(granularity))
        {
            return "Not specified";
        }

        var unit = HumanizeIdentifier(granularity).ToLowerInvariant();
        return $"{periods.Value.ToString(CultureInfo.InvariantCulture)} {unit}{(periods.Value == 1 ? string.Empty : "s")}";
    }

    private static void AppendCalculatedTableExpressions(StringBuilder html, SemanticTableInventory table)
    {
        if (table.IsFieldParameter)
        {
            return;
        }

        foreach (var partition in table.Partitions.Where(partition =>
                     string.Equals(partition.SourceType, "calculated", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(partition.Expression)))
        {
            AppendDaxExpression(
                html,
                partition.Expression!,
                "View calculated-table DAX expression",
                "calculated-table-expression");
        }
    }

    private static void AppendSemanticObjectExpression(
        StringBuilder html,
        SemanticTableInventory table,
        SemanticObjectUsage usage)
    {
        var expression = usage.ObjectType switch
        {
            SemanticObjectTypes.Measure => table.Measures.FirstOrDefault(measure =>
                string.Equals(measure.Name, usage.ObjectName, StringComparison.OrdinalIgnoreCase))?.Expression,
            SemanticObjectTypes.Column => table.Columns.FirstOrDefault(column =>
                string.Equals(column.Name, usage.ObjectName, StringComparison.OrdinalIgnoreCase))?.Expression,
            _ => null,
        };

        AppendDaxExpression(html, expression);
    }

    private static void AppendDaxExpression(
        StringBuilder html,
        string? expression,
        string summary = "View DAX expression",
        string? additionalClass = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return;
        }

        html.Append("                <details class=\"technical-details semantic-expression");
        if (!string.IsNullOrWhiteSpace(additionalClass))
        {
            html.Append(' ').Append(Encode(additionalClass));
        }

        html.Append("\"><summary>");
        html.Append(Encode(summary));
        html.AppendLine("</summary><pre><code>");
        html.Append(Encode(expression));
        html.AppendLine("</code></pre></details>");
    }

    private static string? DescribeSemanticUsageReason(ProjectInventory inventory, SemanticObjectUsage usage)
    {
        var reason = SemanticUsagePresentation.DescribeReason(inventory, usage);
        return reason is null ? null : $"Why: {reason}";
    }

    private static void AppendFact(StringBuilder html, string label, string value, bool code = false)
    {
        html.Append("              <div><dt>").Append(Encode(label)).Append("</dt><dd>");
        if (code)
        {
            html.Append("<code>");
        }

        html.Append(Encode(value));
        if (code)
        {
            html.Append("</code>");
        }

        html.AppendLine("</dd></div>");
    }

    private static void AppendTabOrderFact(
        StringBuilder html,
        TabOrderPresentation presentation,
        string tooltipId)
    {
        html.Append("              <div><dt><span>Tab order</span><button type=\"button\" class=\"info-tooltip\" aria-label=\"About tab-order positions\" aria-describedby=\"")
            .Append(Encode(tooltipId)).Append("\">i<span id=\"").Append(Encode(tooltipId))
            .Append("\" role=\"tooltip\">").Append(Encode(presentation.Tooltip)).Append("</span></button></dt><dd><span class=\"fact-primary\">")
            .Append(Encode(presentation.State)).Append("</span>");
        if (!string.IsNullOrWhiteSpace(presentation.Detail))
        {
            html.Append("<span class=\"fact-supporting\">").Append(Encode(presentation.Detail)).Append("</span>");
        }

        html.AppendLine("</dd></div>");
    }

    private static bool IsAccessibilityFinding(AssuranceFinding finding) =>
        IsAccessibilityCategory(finding.Category);

    private static bool IsMainFinding(AssuranceFinding finding) =>
        !IsAccessibilityFinding(finding);

    private static bool IsAccessibilityCategory(string category) =>
        string.Equals(category, AssuranceCategories.Accessibility, StringComparison.OrdinalIgnoreCase);

    private static string FindingAnchor(int index) => FindingAnchor("finding", index);

    private static string FindingAnchor(string prefix, int index)
    {
        return $"{prefix}-{index + 1}";
    }

    private static string FindingAnchor(ProjectInventory inventory, AssuranceFinding finding, int originalIndex)
    {
        Func<AssuranceFinding, bool> predicate = IsAccessibilityFinding(finding)
            ? IsAccessibilityFinding
            : IsMainFinding;
        var sectionPrefix = IsAccessibilityFinding(finding) ? "accessibility-finding" : "finding";
        var sectionIndex = inventory.Findings.Take(originalIndex + 1).Count(predicate) - 1;
        return FindingAnchor(sectionPrefix, sectionIndex);
    }

    private static string FriendlyFindingMessage(AssuranceFinding finding, VisualContext? context)
    {
        var conciseMessage = finding.RuleId switch
        {
            "PBI-NAV-001" when finding.AssessmentType != AssessmentTypes.ReviewRequired => "This visual links to a bookmark that no longer exists.",
            "PBI-NAV-004" => "A bookmark contains a reference to a visual that is no longer on this page.",
            "PBI-NAV-013" => "This visual's header tooltip links to a report page that no longer exists.",
            _ => finding.Message,
        };

        if (context?.Visual.VisualType is not { Length: > 0 } visualType)
        {
            return conciseMessage;
        }

        var friendlyType = HumanizeVisualType(visualType).ToLowerInvariant();
        var replacement = friendlyType.EndsWith("visual", StringComparison.Ordinal)
            ? friendlyType
            : $"{friendlyType} visual";
        return conciseMessage.Replace(
            $"{visualType} visual",
            replacement,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendFindingLocationSummary(
        StringBuilder html,
        AssuranceFinding finding,
        VisualContext? context)
    {
        if (context is not null)
        {
            AppendSummaryMetadata(
                html,
                ("Page", context.Page.DisplayName),
                ("Visual", VisualDisplayName(context.Visual)),
                ("Position", DescribePosition(context.Page, context.Visual)));
            return;
        }

        if (!string.IsNullOrWhiteSpace(finding.PageDisplayName ?? finding.Page))
        {
            AppendSummaryMetadata(html, ("Page", finding.PageDisplayName ?? finding.Page));
            return;
        }

        if (!string.IsNullOrWhiteSpace(finding.Table) || !string.IsNullOrWhiteSpace(finding.ObjectName))
        {
            AppendSummaryMetadata(html, ("Table", finding.Table), ("Object", finding.ObjectName));
            return;
        }

        if (!string.IsNullOrWhiteSpace(finding.SemanticModel))
        {
            AppendSummaryMetadata(html, ("Semantic model", finding.SemanticModel));
            return;
        }

        AppendSummaryMetadata(html, ("Scope", "Project-wide"));
    }

    private static void AppendSummaryMetadata(
        StringBuilder html,
        params (string Label, string? Value)[] items)
    {
        html.Append("<span class=\"summary-metadata\">");
        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
        {
            html.Append("<span><strong>").Append(Encode(item.Label)).Append(":</strong> ")
                .Append(Encode(item.Value!)).Append("</span>");
        }

        html.Append("</span>");
    }

    private static string VisualDisplayName(VisualInventory visual)
    {
        return VisualFriendlyName(visual) ?? HumanizeVisualType(visual.VisualType);
    }

    private static string? VisualFriendlyName(VisualInventory visual)
    {
        if (visual.Accessibility.TitleIsVisible != false &&
            !visual.Accessibility.TitleTextIsDynamic &&
            !string.IsNullOrWhiteSpace(visual.Accessibility.TitleText))
        {
            return $"“{visual.Accessibility.TitleText}”";
        }

        if (!visual.OnCanvasTextIsDynamic && IsUsefulVisualText(visual.OnCanvasText))
        {
            return $"“{visual.OnCanvasText}”";
        }

        return null;
    }

    private static string DescribeAction(ReportInventory report, VisualActionInventory action)
    {
        var state = action.IsEnabled == false ? "Disabled action" : "Action";
        if (action.HasDynamicConfiguration)
        {
            return $"{state}: destination is set dynamically";
        }

        if (!string.IsNullOrWhiteSpace(action.PageTarget))
        {
            return $"{state}: opens report page “{FriendlyPageName(report, action.PageTarget)}”";
        }

        if (!string.IsNullOrWhiteSpace(action.BookmarkTarget))
        {
            var bookmark = report.Bookmarks.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, action.BookmarkTarget, StringComparison.OrdinalIgnoreCase));
            return bookmark is null
                ? $"{state}: applies a bookmark that is no longer present"
                : $"{state}: applies bookmark “{bookmark.DisplayName}”";
        }

        if (!string.IsNullOrWhiteSpace(action.WebUrl))
        {
            return $"{state}: opens a web link";
        }

        return string.IsNullOrWhiteSpace(action.ActionType)
            ? $"{state}: destination is not specified"
            : $"{state}: {HumanizeIdentifier(action.ActionType)}";
    }

    private static string FriendlyPageName(ReportInventory report, string pageName)
    {
        var page = report.Pages.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, pageName, StringComparison.OrdinalIgnoreCase));
        return page?.DisplayName ?? "missing page";
    }

    private static string FormatCoordinates(VisualPosition position)
    {
        static string Number(double? value) => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?";
        return $"x {Number(position.X)}, y {Number(position.Y)}, width {Number(position.Width)}, height {Number(position.Height)}";
    }

    private static Dictionary<string, VisualHierarchyContext> BuildVisualHierarchyContexts(PageInventory page)
    {
        var scopes = VisualGroupHierarchyResolver.Resolve(page)
            .Where(scope => scope.IsComparable)
            .ToArray();
        var friendlyRanks = new Dictionary<string, string>(StringComparer.Ordinal);
        AssignScopeRanks(parentGroupName: null, prefix: null);

        return page.Visuals.ToDictionary(
            visual => visual.RelativePath,
            visual =>
            {
                friendlyRanks.TryGetValue(visual.RelativePath, out var friendlyRank);
                return new VisualHierarchyContext(friendlyRank);
            },
            StringComparer.Ordinal);

        void AssignScopeRanks(string? parentGroupName, string? prefix)
        {
            var siblings = scopes
                .Where(scope => string.Equals(scope.ParentGroup?.Name, parentGroupName, StringComparison.Ordinal))
                .Where(scope => scope.Position.TabOrder is >= 0)
                .OrderByDescending(scope => scope.Position.TabOrder)
                .ThenBy(scope => scope.RelativePath, StringComparer.Ordinal)
                .ToArray();

            for (var index = 0; index < siblings.Length; index++)
            {
                var sibling = siblings[index];
                var rank = prefix is null
                    ? (index + 1).ToString(CultureInfo.InvariantCulture)
                    : $"{prefix}.{index + 1}";
                friendlyRanks[sibling.RelativePath] = rank;
                if (sibling.IsGroup)
                {
                    AssignScopeRanks(sibling.Name, rank);
                }
            }
        }
    }

    private static TabOrderPresentation DescribeTabOrder(
        VisualInventory visual,
        VisualHierarchyContext hierarchyContext)
    {
        if (visual.Position.TabOrder is < 0)
        {
            return new TabOrderPresentation("Excluded", null, "Excluded from tab order.");
        }

        if (visual.Position.TabOrder is null)
        {
            return new TabOrderPresentation(
                "Included",
                "Power BI default order",
                "Included in tab order using Power BI's default order. No explicit tab-order position is stored in PBIR.");
        }

        var friendlyPosition = hierarchyContext.FriendlyTabOrder;
        return new TabOrderPresentation(
            "Included",
            friendlyPosition is null
                ? "Explicit order"
                : $"Position {friendlyPosition}",
            friendlyPosition is null
                ? "Included in tab order with an explicit position."
                : DescribeTabOrderTooltip(friendlyPosition));
    }

    private static string DescribeTabOrderTooltip(string friendlyPosition)
    {
        var segments = friendlyPosition.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var introduction = $"Included in tab order at position {friendlyPosition}.";
        if (segments.Length <= 1)
        {
            return introduction;
        }

        var parentPosition = string.Join('.', segments[..^1]);
        var groupDescription = segments.Length == 2 ? "group" : "nested group";
        return $"{introduction} This means it is item {segments[^1]} inside the {groupDescription} at position {parentPosition}.";
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var words = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '_' or '-')
            {
                if (words.Length > 0 && words[^1] != ' ')
                {
                    words.Append(' ');
                }

                continue;
            }

            if (index > 0 && char.IsUpper(character) && char.IsLower(value[index - 1]))
            {
                words.Append(' ');
            }

            words.Append(words.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return words.ToString();
    }

    private static void AppendDocumentEnd(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("  </main>");
        html.AppendLine("  </div>");
        html.AppendLine("  <footer class=\"site-footer\"><div class=\"content\">");
        html.Append("    <p>PBI Assure inventory schema ").Append(Encode(inventory.SchemaVersion))
            .AppendLine(". Generated locally from Power BI project metadata.</p>");
        html.AppendLine("  </div></footer>");
        html.AppendLine("  <script>");
        html.AppendLine(FilterScript);
        html.AppendLine("  </script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
    }

    private static void AppendMetric(StringBuilder html, string label, int value, string? cssClass = null)
    {
        html.Append("        <div class=\"metric");
        if (cssClass is not null)
        {
            html.Append(' ').Append(cssClass);
        }

        html.Append("\"><dt>").Append(Encode(label)).Append("</dt><dd>")
            .Append(value.ToString("N0", CultureInfo.InvariantCulture)).AppendLine("</dd></div>");
    }

    private static void AppendSummaryDefinitions(
        StringBuilder html,
        string summary,
        IReadOnlyList<(string Label, string Description)> definitions)
    {
        html.Append("      <details class=\"summary-definitions\"><summary>").Append(Encode(summary))
            .AppendLine("</summary><dl>");
        foreach (var definition in definitions)
        {
            html.Append("        <div><dt>").Append(Encode(definition.Label)).Append("</dt><dd>")
                .Append(Encode(definition.Description)).AppendLine("</dd></div>");
        }
        html.AppendLine("      </dl></details>");
    }

    /// <summary>
    /// System / Light / Dark, as three mutually exclusive toggle buttons.
    ///
    /// Toggle buttons rather than an ARIA radiogroup: a radiogroup owes the reader arrow-key
    /// navigation and roving focus, and a three-item appearance switch does not earn that
    /// machinery. Every option keeps a visible label for screen readers behind its glyph.
    /// </summary>
    private static void AppendAppearanceControl(StringBuilder html, string indent)
    {
        html.Append(indent).AppendLine("<div class=\"appearance-control\" role=\"group\" aria-label=\"Appearance\">");
        foreach (var (value, label) in AppearanceOptions)
        {
            html.Append(indent).Append("  <button type=\"button\" class=\"appearance-option\" data-appearance=\"")
                .Append(value).Append("\" aria-pressed=\"").Append(value == "system" ? "true" : "false")
                .Append("\" title=\"").Append(label).Append("\"><span class=\"visually-hidden\">").Append(label)
                .AppendLine("</span></button>");
        }

        html.Append(indent).AppendLine("</div>");
    }

    private static readonly (string Value, string Label)[] AppearanceOptions =
    [
        ("system", "Match system appearance"),
        ("light", "Light appearance"),
        ("dark", "Dark appearance"),
    ];

    private static void AppendSectionNavigationItem(StringBuilder html, string target, string label, string? context)
    {
        html.Append("          <li><a href=\"#").Append(Encode(target)).Append("\" data-section-target=\"")
            .Append(Encode(target)).Append("\"><span>").Append(Encode(label)).Append("</span>");
        if (!string.IsNullOrWhiteSpace(context))
        {
            html.Append("<small>").Append(Encode(context)).Append("</small>");
        }

        html.AppendLine("</a></li>");
    }

    private static void AppendSectionEmptyState(
        StringBuilder html,
        string heading,
        string explanation,
        string kind)
    {
        html.Append("      <div class=\"section-empty-state section-empty-").Append(Encode(kind))
            .Append("\" role=\"note\"><strong>").Append(Encode(heading)).Append("</strong><span>")
            .Append(Encode(explanation)).AppendLine("</span></div>");
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }

    private static string ProjectName(ProjectInventory inventory)
    {
        if (inventory.Reports.Count == 1)
        {
            return inventory.Reports[0].Name;
        }

        var directoryName = Path.GetFileName(inventory.RootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(directoryName) ? "Power BI project" : directoryName;
    }

    private static void AppendDefinition(StringBuilder html, string term, string value)
    {
        html.Append("        <div><dt>").Append(Encode(term)).Append("</dt><dd>")
            .Append(Encode(value)).AppendLine("</dd></div>");
    }

    private static void AppendScanTimestamp(StringBuilder html, DateTimeOffset scannedAtUtc)
    {
        var utc = scannedAtUtc.UtcDateTime;
        html.Append("        <div><dt>Scanned</dt><dd><time id=\"scan-timestamp\" datetime=\"")
            .Append(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture))
            .Append("\">").Append(utc.ToString("dd MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture))
            .AppendLine("</time></dd></div>");
    }

    private static void AppendEvidence(StringBuilder html, AssuranceFinding finding)
    {
        html.Append("            <details class=\"technical-details\"><summary>Technical details and evidence (")
            .Append(finding.EvidencePaths.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(")</summary>");
        html.AppendLine("              <dl class=\"technical-list\">");
        AppendFact(html, "Rule", finding.RuleId, code: true);
        AppendFact(html, "Category", HumanizeIdentifier(finding.Category));
        AppendFact(html, "Assessment", HumanizeIdentifier(finding.AssessmentType));
        if (!string.IsNullOrWhiteSpace(finding.Visual))
        {
            AppendFact(html, "Visual ID", finding.Visual, code: true);
        }

        if (!string.IsNullOrWhiteSpace(finding.Table))
        {
            AppendFact(html, "Table", finding.Table, code: true);
        }

        if (!string.IsNullOrWhiteSpace(finding.ObjectName))
        {
            AppendFact(html, "Object or target", finding.ObjectName, code: true);
        }

        AppendFact(html, "Source file", DisplayPath(finding.ArtifactPath), code: true);
        html.AppendLine("              </dl>");
        if (finding.EvidencePaths.Count > 0)
        {
            html.AppendLine("              <ul>");
            foreach (var evidencePath in finding.EvidencePaths)
            {
                html.Append("                <li><code>").Append(Encode(evidencePath)).AppendLine("</code></li>");
            }

            html.AppendLine("              </ul>");
        }

        html.AppendLine("            </details>");
    }

    private static void AppendFindingLocation(StringBuilder html, ProjectInventory inventory, AssuranceFinding finding)
    {
        var visualContext = ResolveVisualContext(inventory, finding);
        var referenceContext = FindingReferenceContextLabel(finding, visualContext is not null);
        html.AppendLine("                <dl class=\"finding-location\">");
        if (inventory.ReportCount > 1 && !string.IsNullOrWhiteSpace(finding.Report))
        {
            AppendLocationItem(html, "Report", finding.Report);
        }

        if (visualContext is not null)
        {
            var pageNumber = visualContext.Page.Order is null
                ? null
                : (visualContext.Page.Order.Value + 1).ToString(CultureInfo.InvariantCulture);
            var pageLabel = pageNumber is null
                ? visualContext.Page.DisplayName
                : $"{visualContext.Page.DisplayName} (page {pageNumber})";
            AppendLocationItem(html, "Page", pageLabel, emphasize: true);

            html.AppendLine("                  <div><dt>Visual</dt><dd>");
            AppendVisualIdentity(html, visualContext.Visual);
            html.AppendLine("                  </dd></div>");
            AppendLocationItem(html, "Position", DescribePosition(visualContext.Page, visualContext.Visual));
            if (visualContext.Visual.FieldReferenceCount > 0)
            {
                html.AppendLine("                  <div><dt>Uses</dt><dd>");
                AppendFieldSummary(html, visualContext.Visual);
                html.AppendLine("                  </dd></div>");
            }

            AppendLocationItem(html, "Reference context", referenceContext);

            html.AppendLine("                </dl>");
            html.Append("                <a class=\"inventory-link\" href=\"#")
                .Append(Encode(VisualAnchor(visualContext.Report, visualContext.Page, visualContext.Visual)))
                .AppendLine("\">Open this visual under its report page</a>");
            return;
        }

        AppendLocationItem(html, "Page", finding.PageDisplayName ?? finding.Page);
        if (!string.IsNullOrWhiteSpace(finding.Visual))
        {
            AppendLocationItem(html, "Visual", "Not present in the current page definition");
        }

        AppendLocationItem(html, "Semantic model", finding.SemanticModel);
        AppendLocationItem(html, "Table", finding.Table);
        AppendLocationItem(html, "Object", finding.ObjectName);
        AppendLocationItem(html, "Reference context", referenceContext);
        if (string.IsNullOrWhiteSpace(finding.PageDisplayName) &&
            string.IsNullOrWhiteSpace(finding.Page) &&
            string.IsNullOrWhiteSpace(finding.SemanticModel) &&
            string.IsNullOrWhiteSpace(finding.Table) &&
            string.IsNullOrWhiteSpace(finding.ObjectName))
        {
            AppendLocationItem(html, "Scope", "Project");
        }

        html.AppendLine("                </dl>");
    }

    private static string? FindingReferenceContextLabel(AssuranceFinding finding, bool visualScope)
    {
        if (finding.ReferenceContexts.Count == 0)
        {
            return null;
        }

        var pageScope = !string.IsNullOrWhiteSpace(finding.Page);
        return string.Join(" · ", finding.ReferenceContexts
            .Select(context => !string.IsNullOrWhiteSpace(context.Role)
                ? FieldRoleLabel(context.Role, visualScope, pageScope)
                : context.UsageContext == UsageContexts.Drillthrough
                    ? "Drillthrough field"
                    : HumanizeIdentifier(context.UsageContext))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void AppendLocationItem(StringBuilder html, string label, string? value, bool emphasize = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        html.Append("                  <div><dt>").Append(Encode(label)).Append("</dt><dd>");
        if (emphasize)
        {
            html.Append("<strong>");
        }

        html.Append(Encode(value));
        if (emphasize)
        {
            html.Append("</strong>");
        }

        html.AppendLine("</dd></div>");
    }

    private static void AppendVisualIdentity(StringBuilder html, VisualInventory visual)
    {
        var visualType = HumanizeVisualType(visual.VisualType);
        var hasVisibleStaticTitle = visual.Accessibility.TitleIsVisible != false &&
                                    !visual.Accessibility.TitleTextIsDynamic &&
                                    !string.IsNullOrWhiteSpace(visual.Accessibility.TitleText);
        var hasUsefulCanvasText = !visual.OnCanvasTextIsDynamic && IsUsefulVisualText(visual.OnCanvasText);

        if (hasVisibleStaticTitle)
        {
            html.Append("<strong>“").Append(Encode(visual.Accessibility.TitleText!)).Append("”</strong><br>")
                .Append("<span class=\"secondary\">").Append(Encode(visualType)).Append("</span>");
        }
        else if (hasUsefulCanvasText)
        {
            html.Append("<strong>“").Append(Encode(visual.OnCanvasText!)).Append("”</strong><br>")
                .Append("<span class=\"secondary\">").Append(Encode(visualType)).Append("</span>");
        }
        else
        {
            html.Append("<strong>").Append(Encode(visualType)).Append("</strong>");
            if (visual.Accessibility.TitleTextIsDynamic || visual.OnCanvasTextIsDynamic)
            {
                html.Append("<br><span class=\"secondary\">Uses dynamic display text</span>");
            }
        }

        if (visual.Accessibility.TitleIsVisible == false &&
            !visual.Accessibility.TitleTextIsDynamic &&
            !string.IsNullOrWhiteSpace(visual.Accessibility.TitleText))
        {
            html.Append("<br><span class=\"secondary\">Configured title is hidden: “")
                .Append(Encode(visual.Accessibility.TitleText)).Append("”</span>");
        }

        if (visual.IsHidden)
        {
            html.Append("<br><span class=\"badge badge-neutral\">Hidden in saved report state</span>");
        }

    }

    private static void AppendFieldSummary(StringBuilder html, VisualInventory visual)
    {
        var fields = visual.FieldReferences
            .DistinctBy(reference => $"{reference.Table}\u001f{reference.ObjectName}", StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        if (fields.Length == 0)
        {
            html.Append("None detected");
            return;
        }

        for (var index = 0; index < fields.Length && index < 3; index++)
        {
            if (index > 0)
            {
                html.Append(", ");
            }

            html.Append("<code>").Append(Encode($"{fields[index].Table}[{fields[index].ObjectName}]")).Append("</code>");
        }

        if (fields.Length > 3 || visual.DistinctFieldCount > 3)
        {
            html.Append(" <span class=\"secondary\">+")
                .Append((visual.DistinctFieldCount - 3).ToString(CultureInfo.InvariantCulture))
                .Append(" more</span>");
        }
    }

    private static VisualContext? ResolveVisualContext(ProjectInventory inventory, AssuranceFinding finding)
    {
        if (string.IsNullOrWhiteSpace(finding.Visual))
        {
            return null;
        }

        foreach (var report in inventory.Reports)
        {
            if (!string.IsNullOrWhiteSpace(finding.Report) &&
                !string.Equals(report.Name, finding.Report, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var page = report.Pages.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, finding.Page, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, finding.PageDisplayName, StringComparison.OrdinalIgnoreCase));
            var visual = page?.Visuals.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, finding.Visual, StringComparison.OrdinalIgnoreCase));
            if (page is not null && visual is not null)
            {
                return new VisualContext(report, page, visual);
            }
        }

        return null;
    }

    private static FindingRenderItem CreateFindingRenderItem(ProjectInventory inventory, AssuranceFinding finding)
    {
        var context = ResolveVisualContext(inventory, finding);
        var semanticUsage = inventory.SemanticObjectUsages
            .Where(usage =>
                (string.IsNullOrWhiteSpace(finding.SemanticModel) || string.Equals(usage.SemanticModel, finding.SemanticModel, StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(usage.Table, finding.Table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(usage.ObjectName, finding.ObjectName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        var usage = semanticUsage.Length == 1 ? semanticUsage[0] : null;
        var filterSeverity = finding.AssessmentType == AssessmentTypes.ReviewRequired
            ? AssessmentTypes.ReviewRequired
            : finding.Severity;
        var severityLabel = filterSeverity == AssessmentTypes.ReviewRequired
            ? "Review required"
            : finding.Severity;
        var pageKey = context is null
            ? CompositeFindingKey(finding.Report, finding.Page ?? finding.PageDisplayName)
            : CompositeFindingKey(context.Report.Name, context.Page.Name);
        var pageLabel = context?.Page.DisplayName ?? finding.PageDisplayName ?? finding.Page;
        if (!string.IsNullOrWhiteSpace(pageLabel) && inventory.ReportCount > 1 && !string.IsNullOrWhiteSpace(finding.Report))
        {
            pageLabel = $"{pageLabel} — {finding.Report}";
        }

        string? visualKey = null;
        string? visualLabel = null;
        if (context is not null)
        {
            visualKey = CompositeFindingKey(context.Report.Name, context.Page.Name, context.Visual.Name);
            var visualName = VisualDisplayName(context.Visual);
            var visualType = HumanizeVisualType(context.Visual.VisualType);
            var identity = string.Equals(visualName, visualType, StringComparison.OrdinalIgnoreCase)
                ? visualType
                : $"{visualName} — {visualType}";
            visualLabel = $"{identity} · {context.Page.DisplayName} · {DescribePosition(context.Page, context.Visual)}";
        }

        var tableKey = string.IsNullOrWhiteSpace(finding.Table)
            ? null
            : CompositeFindingKey(finding.SemanticModel, finding.Table);
        var tableLabel = finding.Table;
        if (!string.IsNullOrWhiteSpace(tableLabel) && inventory.SemanticModelCount > 1 && !string.IsNullOrWhiteSpace(finding.SemanticModel))
        {
            tableLabel = $"{tableLabel} — {finding.SemanticModel}";
        }

        var searchText = string.Join(' ', new[]
        {
            finding.RuleId, finding.RuleVersion, finding.Category, HumanizeIdentifier(finding.Category),
            finding.Severity, severityLabel, finding.AssessmentType, finding.Message,
            FriendlyFindingMessage(finding, context), finding.Recommendation, finding.Report,
            finding.Page, finding.PageDisplayName, pageLabel, finding.Visual, visualLabel,
            finding.SemanticModel, finding.Table, finding.ObjectName, usage?.ObjectType,
            usage is null ? null : UsageLabel(usage.UsageState), finding.ArtifactPath,
            string.Join(' ', finding.EvidencePaths),
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new FindingRenderItem(
            finding, context, searchText, filterSeverity, severityLabel, pageKey, pageLabel,
            visualKey, visualLabel, tableKey, tableLabel, usage?.ObjectType, usage?.UsageState);
    }

    private static string? CompositeFindingKey(params string?[] parts)
    {
        var populated = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return populated.Length == 0 ? null : string.Join('\u001f', populated);
    }

    private static string DescribePosition(PageInventory page, VisualInventory visual)
    {
        if (page.Width is null or <= 0 || page.Height is null or <= 0 ||
            visual.Position.X is null || visual.Position.Y is null)
        {
            return "Position unavailable";
        }

        var centreX = visual.Position.X.Value + (visual.Position.Width ?? 0) / 2;
        var centreY = visual.Position.Y.Value + (visual.Position.Height ?? 0) / 2;
        var horizontal = (centreX / page.Width.Value) switch
        {
            < 1d / 3d => "left",
            > 2d / 3d => "right",
            _ => "centre",
        };
        var vertical = (centreY / page.Height.Value) switch
        {
            < 1d / 3d => "upper",
            > 2d / 3d => "lower",
            _ => "centre",
        };

        var description = (vertical, horizontal) switch
        {
            ("centre", "centre") => "centre",
            ("centre", _) => $"centre-{horizontal}",
            _ => $"{vertical}-{horizontal}",
        };
        return $"{char.ToUpperInvariant(description[0])}{description[1..]} of page";
    }

    private sealed record VisualHierarchyContext(string? FriendlyTabOrder);

    private sealed record TabOrderPresentation(string State, string? Detail, string Tooltip);

    private static string HumanizeVisualType(string? visualType)
    {
        if (string.IsNullOrWhiteSpace(visualType))
        {
            return "Unknown visual type";
        }

        if (visualType.StartsWith("PBI_CV_", StringComparison.OrdinalIgnoreCase))
        {
            return "Custom visual";
        }

        var knownType = visualType switch
        {
            "actionButton" => "Button",
            "basicShape" => "Shape",
            "barChart" => "Bar chart",
            "card" => "Card",
            "columnChart" => "Column chart",
            "donutChart" => "Donut chart",
            "image" => "Image",
            "keyDriversVisual" => "Key influencers",
            "lineChart" => "Line chart",
            "multiRowCard" => "Multi-row card",
            "pageNavigator" => "Page navigator",
            "pieChart" => "Pie chart",
            "pivotTable" => "Matrix",
            "qnaVisual" => "Q&A visual",
            "slicer" => "Slicer",
            "tableEx" => "Table",
            "textbox" => "Text box",
            _ => null,
        };
        if (knownType is not null)
        {
            return knownType;
        }

        var words = new StringBuilder(visualType.Length + 8);
        for (var index = 0; index < visualType.Length; index++)
        {
            var character = visualType[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(visualType[index - 1]))
            {
                words.Append(' ');
            }

            words.Append(index == 0 ? char.ToUpperInvariant(character) : character);
        }

        return words.ToString();
    }

    private static bool IsUsefulVisualText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Count(char.IsLetterOrDigit) >= 2;
    }

    private static string VisualAnchor(ReportInventory report, PageInventory page, VisualInventory visual)
    {
        return $"visual-{DomToken(report.Name)}-{DomToken(page.Name)}-{DomToken(visual.Name)}";
    }

    private static string PowerQueryAnchor(PowerQueryUsage usage)
    {
        return $"power-query-{DomToken(usage.SemanticModel)}-{DomToken(usage.QueryName)}-{DomToken(usage.SourceKind)}-{DomToken(usage.Partition ?? "expression")}";
    }

    private static string PowerQueryAnchorForName(
        ProjectInventory inventory,
        string semanticModel,
        string queryName)
    {
        var usage = inventory.PowerQueryUsages.First(item =>
            string.Equals(item.SemanticModel, semanticModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.QueryName, queryName, StringComparison.OrdinalIgnoreCase));
        return PowerQueryAnchor(usage);
    }

    private static string DomToken(string value)
    {
        var result = new StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                result.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                result.Append('-');
                previousWasSeparator = true;
            }
        }

        return result.ToString().Trim('-');
    }

    private sealed record VisualContext(ReportInventory Report, PageInventory Page, VisualInventory Visual);

    private sealed record UsagePresentationReference(string UsageContext, string? Role, string EvidencePath);

    private sealed record FindingFacetOption(string Value, string Label);

    private sealed record IndexedFinding(FindingRenderItem Item, int Index);

    private sealed record FindingRenderItem(
        AssuranceFinding Finding,
        VisualContext? Context,
        string SearchText,
        string FilterSeverity,
        string SeverityLabel,
        string? PageKey,
        string? PageLabel,
        string? VisualKey,
        string? VisualLabel,
        string? TableKey,
        string? TableLabel,
        string? ObjectType,
        string? UsageState);

    private static string PageRole(PageInventory page)
    {
        if (string.Equals(page.PageBinding?.Type, "Drillthrough", StringComparison.OrdinalIgnoreCase))
        {
            return "Drillthrough";
        }

        return page.PageType ?? "Standard";
    }

    private static string PageVisibility(PageInventory page)
    {
        return string.Equals(page.Visibility, "HiddenInViewMode", StringComparison.OrdinalIgnoreCase)
            ? "Hidden in reading view"
            : "Visible";
    }

    private static string SeverityClass(string severity)
    {
        return severity switch
        {
            FindingSeverities.Error => "badge-error",
            FindingSeverities.Warning => "badge-warning",
            _ => "badge-information",
        };
    }

    private static string UsageClass(string usageState)
    {
        return usageState switch
        {
            SemanticUsageStates.DirectlyUsed => "badge-used",
            SemanticUsageStates.IndirectlyUsed => "badge-indirect",
            SemanticUsageStates.StructurallyRequired => "badge-structural",
            SemanticUsageStates.UsedOnlyByUnusedBranch => "badge-unused-branch",
            _ => "badge-unused",
        };
    }

    private static string UsageLabel(string usageState)
    {
        return usageState switch
        {
            SemanticUsageStates.DirectlyUsed => "Directly used",
            SemanticUsageStates.IndirectlyUsed => "Indirectly used",
            SemanticUsageStates.StructurallyRequired => "Structurally required",
            SemanticUsageStates.UsedOnlyByUnusedBranch => "Only used by unused items",
            SemanticUsageStates.ApparentlyUnused => "Apparently unused",
            _ => usageState,
        };
    }

    private static int UsageOrder(string usageState)
    {
        return usageState switch
        {
            SemanticUsageStates.DirectlyUsed => 0,
            SemanticUsageStates.IndirectlyUsed => 1,
            SemanticUsageStates.StructurallyRequired => 2,
            SemanticUsageStates.UsedOnlyByUnusedBranch => 3,
            _ => 4,
        };
    }

    private static bool IsSafeHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    private static string Encode(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    private static string DisplayPath(string value) => value.Replace('\\', '/');


    private const string FilterScript = """
    (() => {
      const normalise = value => (value || '').toLocaleLowerCase();
      const scanTimestamp = document.getElementById('scan-timestamp');
      if (scanTimestamp) {
        try {
          const instant = new Date(scanTimestamp.dateTime);
          if (!Number.isNaN(instant.getTime())) {
            const formatter = new Intl.DateTimeFormat('en-GB', {
              day: 'numeric', month: 'long', year: 'numeric',
              hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZoneName: 'short'
            });
            const parts = Object.fromEntries(formatter.formatToParts(instant)
              .filter(part => part.type !== 'literal').map(part => [part.type, part.value]));
            if ([parts.day, parts.month, parts.year, parts.hour, parts.minute, parts.timeZoneName].every(Boolean)) {
              scanTimestamp.textContent = `${parts.day} ${parts.month} ${parts.year}, ${parts.hour}:${parts.minute} ${parts.timeZoneName}`;
            }
          }
        } catch {
          // Keep the rendered UTC fallback if browser-local formatting is unavailable.
        }
      }
      const reportSections = [...document.querySelectorAll('[data-report-section]')];
      const sectionLinks = [...document.querySelectorAll('[data-section-target]')];

      const revealDetails = target => {
        if (target instanceof HTMLDetailsElement) target.open = true;
        let parent = target.parentElement?.closest('details');
        while (parent) {
          parent.open = true;
          parent = parent.parentElement?.closest('details');
        }
      };

      const sectionForTarget = target => target?.closest?.('[data-report-section]')?.dataset.reportSection;

      const activateSection = (sectionName, options = {}) => {
        const { focus = false, updateFragment = false } = options;
        if (!reportSections.some(section => section.dataset.reportSection === sectionName)) return false;
        reportSections.forEach(section => { section.hidden = section.dataset.reportSection !== sectionName; });
        sectionLinks.forEach(link => {
          const selected = link.dataset.sectionTarget === sectionName;
          if (selected) link.setAttribute('aria-current', 'page');
          else link.removeAttribute('aria-current');
        });
        if (updateFragment) history.pushState(null, '', `#${sectionName}`);
        if (focus) {
          const heading = document.querySelector(`[data-report-section="${sectionName}"] h2`);
          heading?.focus({ preventScroll: true });
          window.scrollTo({ top: 0, left: 0, behavior: 'instant' });
        }
        return true;
      };

      const revealFragmentTarget = (fragment, options = {}) => {
        const target = document.getElementById(fragment);
        if (!target) return false;
        const sectionName = sectionForTarget(target);
        if (sectionName) activateSection(sectionName);
        revealDetails(target);
        if (options.focus) {
          const focusTarget = target instanceof HTMLDetailsElement ? target.querySelector('summary') : target;
          if (focusTarget) {
            focusTarget.setAttribute('tabindex', '-1');
            focusTarget.focus({ preventScroll: true });
          }
          requestAnimationFrame(() => target.scrollIntoView({ block: 'start' }));
        }
        return true;
      };

      const findingSearch = document.getElementById('finding-search');
      const findingList = document.getElementById('finding-list');
      const findingStatus = document.getElementById('finding-filter-status');
      const findingFacets = [...document.querySelectorAll('[data-finding-facet]')];
      const findingClear = document.getElementById('finding-clear-filters');
      const findingChips = document.getElementById('finding-active-filters');
      const findingEmpty = document.getElementById('finding-empty-state');
      const findingActiveCount = document.getElementById('finding-active-filter-count');
      const findingRule = document.getElementById('finding-rule');
      const findingCards = findingList ? [...findingList.querySelectorAll('.finding-card')] : [];
      findingCards.forEach(card => { card.findingSearchText = normalise(card.dataset.searchText); });

      const clearFindingFilters = () => {
        if (findingSearch) findingSearch.value = '';
        findingFacets.forEach(control => { control.value = ''; });
        filterFindings();
        findingSearch?.focus();
      };

      const addFindingChip = (label, clear) => {
        if (!findingChips) return;
        const chip = document.createElement('button');
        chip.type = 'button';
        chip.className = 'filter-chip';
        chip.textContent = label;
        chip.setAttribute('aria-label', `Remove filter: ${label}`);
        chip.addEventListener('click', clear);
        findingChips.append(chip);
      };

      function filterFindings() {
        if (!findingList) return;
        const query = normalise(findingSearch?.value.trim());
        const activeFacets = findingFacets.filter(control => control.value);
        let visible = 0;
        findingCards.forEach(card => {
          const show = (!query || card.findingSearchText.includes(query)) && activeFacets.every(control =>
            card.dataset[`filter${control.dataset.filterKey.replace(/(^|-)([a-z])/g, (_, __, character) => character.toUpperCase())}`] === control.value);
          card.hidden = !show;
          if (show) visible += 1;
        });

        const total = findingCards.length;
        const activeCount = activeFacets.length + (query ? 1 : 0);
        const findingWord = total === 1 ? 'finding' : 'findings';
        findingStatus.textContent = activeCount ? `${visible.toLocaleString()} of ${total.toLocaleString()} ${findingWord}` : `${total.toLocaleString()} ${findingWord}`;
        if (findingClear) findingClear.hidden = activeCount === 0;
        if (findingEmpty) findingEmpty.hidden = visible !== 0;
        if (findingActiveCount) {
          findingActiveCount.hidden = activeCount === 0;
          findingActiveCount.textContent = `${activeCount} active`;
        }

        if (findingChips) {
          findingChips.replaceChildren();
          if (query) addFindingChip(`Search: ${findingSearch.value.trim()}`, () => {
            findingSearch.value = '';
            filterFindings();
            findingSearch.focus();
          });
          activeFacets.forEach(control => {
            const label = `${control.previousElementSibling?.textContent}: ${control.selectedOptions[0]?.textContent}`;
            addFindingChip(label, () => {
              control.value = '';
              filterFindings();
              control.focus();
            });
          });
          findingChips.hidden = activeCount === 0;
        }
      }

      findingSearch?.addEventListener('input', filterFindings);
      findingFacets.forEach(control => control.addEventListener('change', filterFindings));
      findingClear?.addEventListener('click', clearFindingFilters);
      document.querySelectorAll('[data-clear-finding-filters]').forEach(button => button.addEventListener('click', clearFindingFilters));
      document.querySelectorAll('[data-filter-findings-by-rule]').forEach(button => button.addEventListener('click', () => {
        if (!findingRule) return;
        if (findingSearch) findingSearch.value = '';
        findingFacets.forEach(control => { control.value = ''; });
        findingRule.value = button.dataset.filterFindingsByRule;
        filterFindings();
        activateSection('findings');
        requestAnimationFrame(() => {
          findingStatus?.scrollIntoView({ block: 'start' });
          findingStatus?.focus({ preventScroll: true });
        });
      }));
      filterFindings();

      const investigationConfigs = [
        { prefix: 'page', singular: 'page', plural: 'pages' },
        { prefix: 'query', singular: 'query', plural: 'queries' },
        { prefix: 'relationship', singular: 'relationship', plural: 'relationships' },
        { prefix: 'usage', singular: 'model object', plural: 'model objects' },
        { prefix: 'theme', singular: 'visual', plural: 'visuals' },
        { prefix: 'theme-governance', singular: 'review item', plural: 'review items' }
      ];

      const setupInvestigation = ({ prefix, singular, plural }) => {
        const search = document.getElementById(`${prefix}-search`);
        if (!search) return;
        const items = [...document.querySelectorAll(`[data-investigation-item="${prefix}"]`)];
        const facets = [...document.querySelectorAll(`[data-investigation="${prefix}"] [data-investigation-facet]`)];
        const status = document.getElementById(`${prefix}-filter-status`);
        const clear = document.getElementById(`${prefix}-clear-filters`);
        const chips = document.getElementById(`${prefix}-active-filters`);
        const empty = document.getElementById(`${prefix}-empty-state`);
        const activeBadge = document.getElementById(`${prefix}-active-filter-count`);
        items.forEach(item => { item.investigationSearchText = normalise(item.dataset.searchText); });

        const addChip = (label, remove) => {
          const chip = document.createElement('button');
          chip.type = 'button';
          chip.className = 'filter-chip';
          chip.textContent = label;
          chip.setAttribute('aria-label', `Remove filter: ${label}`);
          chip.addEventListener('click', remove);
          chips.append(chip);
        };
        const run = () => {
          const query = normalise(search.value.trim());
          const activeFacets = facets.filter(control => control.value);
          let visible = 0;
          items.forEach(item => {
            const facetMatch = activeFacets.every(control => {
              const key = `filter${control.dataset.filterKey.replace(/(^|-)([a-z])/g, (_, __, character) => character.toUpperCase())}`;
              return (item.dataset[key] || '').split('\u001f').includes(control.value);
            });
            const show = (!query || item.investigationSearchText.includes(query)) && facetMatch;
            item.hidden = !show;
            if (show) visible += 1;
          });
          if (prefix === 'usage') {
            document.querySelectorAll('#semantic-table-list .semantic-table').forEach(table => {
              const shown = [...table.querySelectorAll('.semantic-object')].some(item => !item.hidden);
              table.hidden = !shown;
            });
          }
          const activeCount = activeFacets.length + (query ? 1 : 0);
          status.textContent = activeCount ? `${visible.toLocaleString()} of ${items.length.toLocaleString()} ${items.length === 1 ? singular : plural}` : `${items.length.toLocaleString()} ${items.length === 1 ? singular : plural}`;
          clear.hidden = activeCount === 0;
          empty.hidden = visible !== 0;
          activeBadge.hidden = activeCount === 0;
          activeBadge.textContent = `${activeCount} active`;
          chips.replaceChildren();
          if (query) addChip(`Search: ${search.value.trim()}`, () => { search.value = ''; run(); search.focus(); });
          activeFacets.forEach(control => addChip(`${control.previousElementSibling?.textContent}: ${control.selectedOptions[0]?.textContent}`, () => { control.value = ''; run(); control.focus(); }));
          chips.hidden = activeCount === 0;
        };
        const clearAll = () => { search.value = ''; facets.forEach(control => { control.value = ''; }); run(); search.focus(); };
        search.addEventListener('input', run);
        facets.forEach(control => control.addEventListener('change', run));
        clear.addEventListener('click', clearAll);
        document.querySelector(`[data-clear-investigation="${prefix}"]`)?.addEventListener('click', clearAll);
        run();
      };
      investigationConfigs.forEach(setupInvestigation);

      document.querySelectorAll('[data-details-action]').forEach(button => {
        button.addEventListener('click', () => {
          const target = document.getElementById(button.dataset.target);
          if (!target) return;
          const details = button.dataset.target === 'semantic-table-list'
            ? target.querySelectorAll('.semantic-table')
            : target.querySelectorAll(':scope > details');
          details.forEach(item => { item.open = button.dataset.detailsAction === 'expand'; });
        });
      });

      sectionLinks.forEach(link => {
        link.addEventListener('click', event => {
          event.preventDefault();
          activateSection(link.dataset.sectionTarget, { focus: true, updateFragment: true });
        });
      });

      document.querySelectorAll('a[href^="#"]').forEach(link => {
        if (link.classList.contains('skip-link') || link.dataset.sectionTarget) return;
        link.addEventListener('click', event => {
          const fragment = link.getAttribute('href').slice(1);
          if (!revealFragmentTarget(fragment, { focus: true })) return;
          event.preventDefault();
          history.pushState(null, '', `#${fragment}`);
        });
      });

      const appearanceButtons = [...document.querySelectorAll('[data-appearance]')];
      if (appearanceButtons.length > 0) {
        const showAppearance = choice => {
          if (choice === 'light' || choice === 'dark') document.documentElement.dataset.theme = choice;
          else delete document.documentElement.dataset.theme;
          appearanceButtons.forEach(button =>
            button.setAttribute('aria-pressed', String(button.dataset.appearance === choice)));
        };

        let stored = null;
        try { stored = localStorage.getItem('pbiassure-appearance'); } catch { stored = null; }
        showAppearance(stored === 'light' || stored === 'dark' ? stored : 'system');
        appearanceButtons.forEach(button => button.addEventListener('click', () => {
          const choice = button.dataset.appearance;
          showAppearance(choice);
          try {
            if (choice === 'system') localStorage.removeItem('pbiassure-appearance');
            else localStorage.setItem('pbiassure-appearance', choice);
          } catch {
            // A downloaded report opened from the file system has no usable storage; the choice
            // still applies to this document, it simply does not survive a reload.
          }
        }));
      }

      const initialFragment = decodeURIComponent(window.location.hash.slice(1));
      if (!initialFragment || !revealFragmentTarget(initialFragment)) activateSection('summary');
      window.addEventListener('hashchange', () => {
        const fragment = decodeURIComponent(window.location.hash.slice(1));
        if (fragment) revealFragmentTarget(fragment, { focus: true });
      });
    })();
    """;

    /// <summary>
    /// Applies a stored appearance choice before the first paint, so a reader who has chosen
    /// Light or Dark never sees the other one flash past. Readers on System need nothing here:
    /// the stylesheet already follows <c>prefers-color-scheme</c>.
    /// </summary>
    private const string AppearanceBootstrapScript = """
    (() => {
      try {
        const stored = localStorage.getItem('pbiassure-appearance');
        if (stored === 'light' || stored === 'dark') document.documentElement.dataset.theme = stored;
      } catch {
        // Storage is unavailable for a report opened from the file system. System appearance applies.
      }
    })();
    """;
}
