using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    public static string Render(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var html = new StringBuilder(capacity: 256_000);
        AppendDocumentStart(html, inventory);
        AppendSummary(html, inventory);
        AppendScope(html);
        AppendFindings(html, inventory);
        AppendReportInventory(html, inventory);
        AppendThemeReview(html, inventory);
        AppendPowerQueryLineage(html, inventory);
        AppendRelationships(html, inventory);
        AppendSemanticUsage(html, inventory);
        AppendDocumentEnd(html, inventory);
        return html.ToString();
    }

    private static void AppendDocumentStart(StringBuilder html, ProjectInventory inventory)
    {
        var projectName = ProjectName(inventory);

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en-GB\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("  <title>PBI Assure report — ").Append(Encode(projectName)).AppendLine("</title>");
        html.AppendLine("  <style>");
        html.AppendLine(Styles);
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <a class=\"skip-link\" href=\"#main-content\">Skip to main content</a>");
        html.AppendLine("  <header class=\"site-header\">");
        html.AppendLine("    <div class=\"content\">");
        html.AppendLine("      <p class=\"eyebrow\">Power BI metadata assurance</p>");
        html.Append("      <h1>").Append(Encode(projectName)).AppendLine("</h1>");
        html.AppendLine("      <p class=\"lede\">A read-only review of this Power BI project.</p>");
        html.AppendLine("      <dl class=\"report-meta\">");
        AppendDefinition(html, "Scanned", inventory.ScannedAtUtc.UtcDateTime.ToString(
            "dd MMMM yyyy, HH:mm 'UTC'",
            CultureInfo.InvariantCulture));
        AppendDefinition(html, "Inventory schema", inventory.SchemaVersion);
        AppendDefinition(html, "Source project", inventory.RootPath);
        html.AppendLine("      </dl>");
        html.AppendLine("      <nav class=\"section-navigator\" aria-label=\"Report sections\">");
        html.AppendLine("        <ul class=\"section-nav\">");
        AppendSectionNavigationItem(html, "summary", "Summary", null);
        AppendSectionNavigationItem(html, "findings", "Findings", FindingNavigationSummary(inventory));
        AppendSectionNavigationItem(html, "reports", "Report pages", $"{inventory.PageCount} {Pluralize(inventory.PageCount, "page", "pages")} · {inventory.VisualCount} {Pluralize(inventory.VisualCount, "visual", "visuals")}");
        AppendSectionNavigationItem(html, "power-query", "Power Query", inventory.PowerQueryCount == 0 ? null : $"{inventory.PowerQueryCount} {Pluralize(inventory.PowerQueryCount, "query", "queries")}");
        AppendSectionNavigationItem(html, "relationships", "Model relationships", $"{inventory.SemanticRelationshipCount} {Pluralize(inventory.SemanticRelationshipCount, "relationship", "relationships")}");
        AppendSectionNavigationItem(html, "semantic-usage", "Semantic model", $"{inventory.DeveloperSemanticObjectCount} developer-authored {Pluralize(inventory.DeveloperSemanticObjectCount, "object", "objects")}");
        AppendSectionNavigationItem(html, "theme-review", "Theme Review", inventory.Reports.Count == 0 ? null : $"{inventory.Reports.Count} {Pluralize(inventory.Reports.Count, "report", "reports")}");
        html.AppendLine("        </ul>");
        html.AppendLine("      </nav>");
        html.AppendLine("    </div>");
        html.AppendLine("  </header>");
        html.AppendLine("  <main id=\"main-content\" class=\"content\" tabindex=\"-1\">");
    }

    private static void AppendSummary(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"summary\" class=\"report-section\" data-report-section=\"summary\" aria-labelledby=\"summary-heading\">");
        html.AppendLine("      <h2 id=\"summary-heading\" tabindex=\"-1\">Assurance summary</h2>");
        html.AppendLine("      <p class=\"section-intro\">Start here for the overall assurance result, the size of the project and how its model objects are being used.</p>");
        html.AppendLine("      <div class=\"summary-groups\">");
        html.AppendLine("        <section class=\"summary-group summary-group-assurance\" aria-labelledby=\"summary-assurance-heading\" aria-describedby=\"summary-assurance-help\">");
        html.AppendLine("          <h3 id=\"summary-assurance-heading\">Assurance</h3>");
        html.AppendLine("          <p id=\"summary-assurance-help\" class=\"group-explanation\">Findings from automated checks across the report, semantic model and Power Query. Start with errors, then warnings and items that need a person to review them.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Errors", inventory.ErrorFindingCount, "metric-error");
        AppendMetric(html, "Warnings", inventory.WarningFindingCount, "metric-warning");
        AppendMetric(html, "Review required", inventory.ReviewRequiredCount, "metric-review");
        AppendMetric(html, "Total findings", inventory.FindingCount);
        html.AppendLine("      </dl>");
        AppendSummaryDefinitions(html, "What these finding numbers mean", [
            ("Errors", "Higher-confidence issues that would normally merit attention."),
            ("Warnings", "Potential problems, good-practice concerns or lower-confidence issues worth reviewing."),
            ("Review required", "Situations that need human judgement or contextual review; they are not necessarily defects."),
            ("Total findings", "All findings from the automated checks, across every severity and assessment type.")]);
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
        AppendMetric(html, "Developer-authored model objects", inventory.DeveloperSemanticObjectCount);
        if (inventory.SystemGeneratedSemanticObjectCount > 0)
        {
            AppendMetric(html, "System-generated model objects", inventory.SystemGeneratedSemanticObjectCount);
        }
        html.AppendLine("      </dl>");
        AppendSummaryDefinitions(html, "What these project numbers count", [
            ("Reports", "PBIR report definitions found in the project."),
            ("Pages", "Report pages found across those report definitions."),
            ("Visuals", "Visuals placed across all report pages."),
            ("Report measures", "DAX measures defined in the report itself, rather than in its semantic model."),
            ("Developer-authored model objects", "Columns, measures, hierarchy levels and calculation items in tables not identified as Power BI-generated."),
            ("System-generated model objects", "The same model-object types in tables PBI Assure identifies as generated by Power BI, such as local date-table artefacts.")]);
        html.AppendLine("        </section>");
        if (inventory.PowerQueryCount > 0 || inventory.DataSourceCount > 0)
        {
            html.AppendLine("        <section class=\"summary-group summary-group-power-query\" aria-labelledby=\"summary-power-query-heading\" aria-describedby=\"summary-power-query-help\">");
            html.AppendLine("          <h3 id=\"summary-power-query-heading\">Power Query</h3>");
            html.AppendLine("          <p id=\"summary-power-query-help\" class=\"group-explanation\">Queries, recognised connector types and lineage information detected from the project's Power Query definitions.</p>");
            html.AppendLine("      <dl class=\"metrics\">");
            if (inventory.PowerQueryCount > 0)
            {
                AppendMetric(html, "Power Query queries", inventory.PowerQueryCount);
            }
            if (inventory.DataSourceCount > 0)
            {
                AppendMetric(html, "Connector types", inventory.DistinctConnectorFamilyCount);
            }
            html.AppendLine("      </dl>");
            AppendSummaryDefinitions(html, "What these Power Query numbers count", [
                ("Power Query queries", "M-backed table partitions and named expressions found in the semantic model."),
                ("Connector types", "Distinct recognised connector families used by those Power Query expressions; this is not a count of connection instances.")]);
            html.AppendLine("        </section>");
        }
        html.AppendLine("        <section class=\"summary-group summary-group-semantic\" aria-labelledby=\"summary-semantic-heading\" aria-describedby=\"summary-semantic-help\">");
        html.AppendLine("          <h3 id=\"summary-semantic-heading\">Semantic usage</h3>");
        html.AppendLine("          <p id=\"summary-semantic-help\" class=\"group-explanation\">How PBI Assure classified developer-authored model objects according to where and how they are referenced.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Directly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.DirectlyUsed));
        AppendMetric(html, "Indirectly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.IndirectlyUsed));
        AppendMetric(html, "Structurally required", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.StructurallyRequired));
        AppendMetric(html, "Unused branch", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.UsedOnlyByUnusedBranch));
        AppendMetric(html, "Apparently unused", inventory.DeveloperApparentlyUnusedSemanticObjectCount, "metric-unused");
        html.AppendLine("      </dl>");
        html.AppendLine("          <p class=\"summary-caution\"><strong>Apparently unused needs care:</strong> no usage was found within this analysed project. It is not proof that an object is safe to delete or unused by external reports, models or processes.</p>");
        AppendSummaryDefinitions(html, "What these usage states mean", [
            ("Directly used", "Referenced directly by the report, for example in a visual, filter, tooltip or drillthrough setting."),
            ("Indirectly used", "Needed by another model object that has direct report usage, such as a DAX measure."),
            ("Structurally required", "Needed by model structure detected by PBI Assure, such as relationships, sort-by configuration or hierarchy levels."),
            ("Unused branch", "Referenced only through an object or dependency branch with no identified report usage."),
            ("Apparently unused", "No usage was found within the analysed project scope. Review it before removal because external consumers and dynamic behaviour are outside that scope.")]);
        html.AppendLine("        </section>");
        html.AppendLine("      </div>");
        html.Append("      <p class=\"summary-note\"><strong>").Append(inventory.DeveloperApparentlyUnusedSemanticObjectCount.ToString(CultureInfo.InvariantCulture))
            .Append(" developer-authored semantic objects have no usage detected in this project. Review them before removing anything.</strong>");
        if (inventory.SystemGeneratedSemanticObjectCount > 0)
        {
            html.Append(" Power BI-generated objects remain analysed and are available in the semantic-model filter.");
        }
        html.AppendLine("</p>");
        html.AppendLine("      <p><a href=\"#semantic-usage\">Review semantic-model candidates</a></p>");
        html.AppendLine("    </section>");
    }

    private static void AppendScope(StringBuilder html)
    {
        html.AppendLine("    <section class=\"scope report-section\" data-report-section=\"summary\" aria-labelledby=\"scope-heading\">");
        html.AppendLine("      <h2 id=\"scope-heading\">Important interpretation boundaries</h2>");
        html.AppendLine("      <p class=\"section-intro\">Keep these limits in mind when using the report to make development decisions.</p>");
        html.AppendLine("      <ul>");
        html.AppendLine("        <li><strong>Apparently unused</strong> means no usage was found in the analysed scope; it is not permission to delete an object.</li>");
        html.AppendLine("        <li>Semantic usage and Power Query dependency are separate: a model table can appear unused while its Power Query is still required by another query.</li>");
        html.AppendLine("        <li>Power Query column usage is based on explicit static M references; dynamically constructed column lists and custom transformations may remain unresolved.</li>");
        html.AppendLine("        <li>Power Query lineage follows static references between known table queries and named expressions. Dynamically constructed references, data-source internals, bookmark-captured semantic state, and external consumers remain analysis boundaries.</li>");
        html.AppendLine("        <li>Accessibility findings support manual WCAG and assistive-technology testing; they do not certify conformance.</li>");
        html.AppendLine("        <li>PBI Assure performs read-only analysis of the selected Power BI project.</li>");
        html.AppendLine("      </ul>");
        html.AppendLine("    </section>");
    }

    private static void AppendPowerQueryLineage(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"power-query\" class=\"report-section\" data-report-section=\"power-query\" aria-labelledby=\"power-query-heading\">");
        html.AppendLine("      <h2 id=\"power-query-heading\" tabindex=\"-1\">Power Query lineage</h2>");
        html.AppendLine("      <p class=\"section-intro\">See how data-preparation queries feed the model and support one another. Expand a query for the known steps it depends on.</p>");
        if (inventory.PowerQueryUsages.Count == 0)
        {
            html.AppendLine("      <p>No Power Query M partitions or named expressions were found.</p>");
            html.AppendLine("    </section>");
            return;
        }

        AppendDataSourceSummary(html, inventory);

        AppendInvestigationStart(html, "query", "Search queries", "Search query names, connectors, dependencies or model tables");
        AppendInvestigationFacet(html, "query", "load-state", "Load state", "All load states", inventory.PowerQueryUsages.Select(usage => usage.UsageState).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value == PowerQueryUsageStates.LoadedToModel ? "Loaded to model" : value == PowerQueryUsageStates.SupportingQuery ? "Supporting query" : "Apparently unused")));
        AppendInvestigationFacet(html, "query", "connector", "Connector type", "All connector types", inventory.DataSources.Select(source => source.ConnectorFamily).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value!, value!)));
        AppendInvestigationFacet(html, "query", "role", "Query role", "All query roles", inventory.PowerQueryUsages.Select(usage => usage.QueryRole).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value!, PowerQueryRoleLabel(inventory.PowerQueryUsages.First(usage => usage.QueryRole == value)))));
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

    private static void AppendDataSourceSummary(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("      <h3>Data sources</h3>");
        if (inventory.DataSources.Count == 0)
        {
            html.AppendLine("      <p>No recognised connector calls were found in the available M expressions.</p>");
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
        PowerQueryRoles.ApparentlyOrphaned => "Apparently orphaned query",
        _ => "Dependency role needs review",
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

    private static void AppendFindings(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"findings\" class=\"report-section\" data-report-section=\"findings\" aria-labelledby=\"findings-heading\">");
        html.AppendLine("      <h2 id=\"findings-heading\" tabindex=\"-1\">Findings</h2>");
        html.AppendLine("      <p class=\"section-intro\">Issues and review points found by automated checks. Expand one to see where it occurs and what to do next.</p>");
        html.AppendLine("      <details class=\"section-help\"><summary>How to use findings</summary><p>A finding is an automated observation, not a verdict on the whole report. Its location shows where PBI Assure found it and Suggested action gives a practical next step. Items marked Review required can be intentional, depending on your report's context.</p></details>");
        if (inventory.Findings.Count == 0)
        {
            html.AppendLine("      <p>No automated findings were produced. Manual review is still required.</p>");
            html.AppendLine("    </section>");
            return;
        }

        var findingItems = inventory.Findings.Select(finding => CreateFindingRenderItem(inventory, finding)).ToArray();
        html.AppendLine("      <div class=\"finding-investigation\">");
        html.AppendLine("        <div class=\"finding-search\"><label for=\"finding-search\">Search findings</label><input id=\"finding-search\" type=\"search\" autocomplete=\"off\" placeholder=\"Search messages, rules, pages, visuals or model objects\"></div>");
        html.AppendLine("        <details class=\"finding-filter-panel\"><summary>More filters <span id=\"finding-active-filter-count\" class=\"active-filter-count\" hidden></span></summary>");
        html.AppendLine("          <div class=\"finding-facet-grid\" aria-label=\"Filter findings\">");
        AppendFindingFacet(html, "finding-severity", "Severity", "All severities", FindingFacetOptions(findingItems, item => (item.FilterSeverity, item.SeverityLabel)));
        AppendFindingFacet(html, "finding-rule", "Rule", "All rules", FindingFacetOptions(findingItems, item => (item.Finding.RuleId, item.Finding.RuleId)));
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
        html.Append("        <p id=\"finding-filter-status\" class=\"filter-status\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\">")
            .Append(inventory.Findings.Count.ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" findings</p>");
        html.AppendLine("        <button id=\"finding-clear-filters\" type=\"button\" hidden>Clear filters</button>");
        html.AppendLine("      </div>");
        html.AppendLine("      <div id=\"finding-active-filters\" class=\"filter-chips\" aria-label=\"Active finding filters\" hidden></div>");
        html.AppendLine("      <div id=\"finding-empty-state\" class=\"finding-empty-state\" hidden><strong>No findings match these filters.</strong><span>Try removing a filter or changing the search text.</span><button type=\"button\" data-clear-finding-filters>Clear search and filters</button></div>");
        AppendDetailsControls(html, "finding-list", "issues");
        html.AppendLine("      <div id=\"finding-list\" class=\"card-list\">");
        for (var index = 0; index < findingItems.Length; index++)
        {
            var item = findingItems[index];
            var finding = item.Finding;
            var context = item.Context;
            html.Append("        <details id=\"").Append(FindingAnchor(index)).Append("\" class=\"finding-card\" data-severity=\"")
                .Append(Encode(finding.Severity)).Append("\" data-filter-severity=\"").Append(Encode(item.FilterSeverity))
                .Append("\" data-filter-rule=\"").Append(Encode(finding.RuleId))
                .Append("\" data-filter-category=\"").Append(Encode(finding.Category))
                .Append("\" data-filter-page=\"").Append(Encode(item.PageKey ?? string.Empty))
                .Append("\" data-filter-visual=\"").Append(Encode(item.VisualKey ?? string.Empty))
                .Append("\" data-filter-table=\"").Append(Encode(item.TableKey ?? string.Empty))
                .Append("\" data-filter-object-type=\"").Append(Encode(item.ObjectType ?? string.Empty))
                .Append("\" data-filter-usage-state=\"").Append(Encode(item.UsageState ?? string.Empty))
                .Append("\" data-search-text=\"").Append(Encode(item.SearchText)).AppendLine("\">");
            html.Append("          <summary><span class=\"badge ").Append(SeverityClass(finding.Severity))
                .Append("\">").Append(Encode(finding.Severity)).Append("</span><span class=\"summary-copy\"><strong>")
                .Append(Encode(FriendlyFindingMessage(finding, context))).Append("</strong>");
            AppendFindingLocationSummary(html, finding, context);
            html.AppendLine("</span></summary>");
            html.AppendLine("          <div class=\"card-body\">");
            AppendFindingLocation(html, inventory, finding);
            html.AppendLine("            <h3>Suggested action</h3>");
            html.Append("            <p>").Append(Encode(finding.Recommendation)).AppendLine("</p>");
            if (finding.ReferenceUrl is not null && IsSafeHttpUrl(finding.ReferenceUrl))
            {
                html.Append("            <p><a href=\"").Append(Encode(finding.ReferenceUrl))
                    .AppendLine("\">Open supporting guidance</a></p>");
            }

            AppendEvidence(html, finding);
            html.AppendLine("          </div>");
            html.AppendLine("        </details>");
        }

        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
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
        html.Append("        <button id=\"").Append(prefix).AppendLine("-clear-filters\" type=\"button\" hidden>Clear filters</button></div>");
        html.Append("      <div id=\"").Append(prefix).AppendLine("-active-filters\" class=\"filter-chips\" aria-label=\"Active filters\" hidden></div>");
        html.Append("      <div id=\"").Append(prefix).Append("-empty-state\" class=\"finding-empty-state investigation-empty-state\" hidden><strong>No ")
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
            html.AppendLine("      <p>No PBIR report definitions were found.</p>");
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
            html.AppendLine("      <p>No model relationships were found.</p>");
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
                var relationshipSearch = $"{relationship.FromTable} {relationship.FromColumn} {relationship.ToTable} {relationship.ToColumn} {cardinality} {direction} {(relationship.IsActive ? "Active" : "Inactive")} {reviewTerms}";
                html.Append("        <details class=\"relationship-card\" data-investigation-item=\"relationship\" data-search-text=\"").Append(Encode(relationshipSearch))
                    .Append("\" data-filter-status=\"").Append(relationship.IsActive ? "active" : "inactive")
                    .Append("\" data-filter-cardinality=\"").Append(Encode(cardinality)).Append("\" data-filter-direction=\"")
                    .Append(Encode(relationship.CrossFilteringBehavior)).AppendLine("\">");
                html.Append("          <summary><span class=\"summary-copy\"><strong>")
                    .Append(Encode($"{relationship.FromTable}[{relationship.FromColumn}]"))
                    .Append("</strong><span>").Append(Encode(cardinality)).Append(" · ")
                    .Append(relationship.IsActive ? "Active" : "Inactive").Append(" · ")
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
                html.AppendLine("            </dl>");
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
                AppendFact(html, "Source file", Path.Combine(model.RelativePath, "definition", "relationships.tmdl"), code: true);
                html.AppendLine("            </dl></details>");
                html.AppendLine("          </div>");
                html.AppendLine("        </details>");
            }
            html.AppendLine("      </div>");
        }

        html.AppendLine("      </div>");

        html.AppendLine("    </section>");
    }

    private static string RelationshipCardinalityLabel(SemanticRelationshipInventory relationship) =>
        $"{RelationshipEndLabel(relationship.FromCardinality)}-to-{RelationshipEndLabel(relationship.ToCardinality).ToLowerInvariant()}";

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

    private static void AppendSemanticUsage(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("    <section id=\"semantic-usage\" class=\"report-section\" data-report-section=\"semantic-usage\" aria-labelledby=\"semantic-usage-heading\">");
        html.AppendLine("      <h2 id=\"semantic-usage-heading\" tabindex=\"-1\">Semantic model</h2>");
        html.AppendLine("      <p class=\"section-intro\">Review columns, measures and other model objects by table. Expand an object to see why it has its status, exactly where it is used and, where available, its DAX expression.</p>");
        AppendUsageGuide(html);
        if (inventory.SemanticModels.Count == 0)
        {
            html.AppendLine("      <p>No supported semantic model definition was found.</p>");
            html.AppendLine("    </section>");
            return;
        }

        AppendInvestigationStart(html, "usage", "Search model objects", "Search tables, columns, measures or usage reasons");
        AppendInvestigationFacet(html, "usage", "table", "Table", "All tables", inventory.SemanticObjectUsages.Select(usage => usage.Table).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, value)));
        AppendInvestigationFacet(html, "usage", "object-type", "Object type", "All object types", inventory.SemanticObjectUsages.Select(usage => usage.ObjectType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(value => new FindingFacetOption(value, HumanizeIdentifier(value))));
        AppendInvestigationFacet(html, "usage", "usage-state", "Usage state", "All usage states", inventory.SemanticObjectUsages.Select(usage => usage.UsageState).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(UsageOrder).Select(value => new FindingFacetOption(value, UsageLabel(value))));
        AppendInvestigationFacet(html, "usage", "origin", "Object origin", "All objects", [new("developer", "Developer-authored objects"), new("system", "Power BI-generated objects")], "developer");
        AppendInvestigationEnd(html, "usage", inventory.DeveloperSemanticObjectCount, "semantic object", "semantic objects");
        AppendDetailsControls(html, "semantic-table-list", "tables");
        html.AppendLine("      <div id=\"semantic-table-list\" class=\"semantic-table-list\">");
        foreach (var model in inventory.SemanticModels)
        {
            AppendSemanticModel(html, inventory, model);
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
        int? pageNumber = page.Order is null ? null : page.Order.Value + 1;
        var pageFindings = inventory.Findings.Count(finding =>
            string.Equals(finding.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(finding.Page, page.Name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(finding.PageDisplayName, page.DisplayName, StringComparison.OrdinalIgnoreCase)));

        var visualTypes = string.Join('\u001f', page.Visuals.Select(visual => visual.VisualType).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
        var pageSearchText = string.Join(' ', new[] { page.DisplayName, page.Name, PageRole(page), PageVisibility(page) }
            .Concat(page.Visuals.SelectMany(visual => new[] { VisualDisplayName(visual), HumanizeVisualType(visual.VisualType), visual.VisualType }))
            .Concat(page.FieldReferences.Select(reference => $"{reference.Table} {reference.ObjectName} {reference.ObjectType}")));
        html.Append("        <details class=\"page-card\" data-investigation-item=\"page\" data-search-text=\"").Append(Encode(pageSearchText))
            .Append("\" data-filter-page-type=\"").Append(Encode(PageRole(page))).Append("\" data-filter-visibility=\"")
            .Append(Encode(PageVisibility(page))).Append("\" data-filter-visual-type=\"").Append(Encode(visualTypes)).Append("\" data-page-name=\"").Append(Encode(page.DisplayName)).Append('"');
        if (page.IsActive)
        {
            html.Append(" open");
        }

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
                AppendVisualCard(html, inventory, report, page, visual);
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
        VisualInventory visual)
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
        AppendAccessibilitySummary(html, visual);

        if (relatedFindings.Length > 0)
        {
            html.AppendLine("                  <h4>Issues for this visual</h4>");
            html.AppendLine("                  <ul class=\"related-findings\">");
            foreach (var item in relatedFindings)
            {
                html.Append("                    <li><span class=\"badge ").Append(SeverityClass(item.Finding.Severity))
                    .Append("\">").Append(Encode(item.Finding.Severity)).Append("</span> <a href=\"#")
                    .Append(FindingAnchor(item.Index)).Append("\">")
                    .Append(Encode(FriendlyFindingMessage(item.Finding, new VisualContext(report, page, visual))))
                    .AppendLine("</a></li>");
            }

            html.AppendLine("                  </ul>");
        }

        html.AppendLine("                  <details class=\"technical-details\"><summary>Technical details</summary>");
        html.AppendLine("                    <dl class=\"technical-list\">");
        AppendFact(html, "Visual ID", visual.Name, code: true);
        AppendFact(html, "Source file", visual.RelativePath, code: true);
        AppendFact(html, "Position", FormatCoordinates(visual.Position));
        AppendFact(
            html,
            "Tab order value",
            visual.Position.TabOrder?.ToString(CultureInfo.InvariantCulture) ?? "Not included");
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
                Roles = group.Select(reference => reference.Role)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .OrderBy(item => item.Reference.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Reference.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        html.AppendLine("                  <ul class=\"object-list\">");
        foreach (var item in objects)
        {
            var roles = item.Roles;
            if (roles.Any(role => !string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)))
            {
                roles = roles.Where(role => !string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            var reference = item.Reference;
            html.Append("                    <li><code>").Append(Encode($"{reference.Table}[{reference.ObjectName}]")).Append("</code><span>")
                .Append(Encode(HumanizeIdentifier(reference.ObjectType)));
            if (roles.Length > 0)
            {
                var roleLabel = string.Join(", ", roles.Select(role =>
                    FieldRoleLabel(role!, visualScope, pageScope: !visualScope)));
                html.Append(" · ").Append(Encode(roleLabel));
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

    private static void AppendAccessibilitySummary(StringBuilder html, VisualInventory visual)
    {
        html.AppendLine("                  <h4>Accessibility snapshot</h4>");
        html.AppendLine("                  <dl class=\"fact-strip compact\">");
        var altText = visual.Accessibility.AltTextIsDynamic
            ? "Dynamic alt text"
            : visual.Accessibility.HasAltText
                ? visual.Accessibility.AltText ?? "Configured"
                : "Not configured";
        AppendFact(html, "Alt text", altText);
        AppendFact(
            html,
            "Tab order",
            visual.Position.TabOrder is null ? "Not included in tab order" : "Included in tab order");
        AppendFact(
            html,
            "Title",
            visual.Accessibility.TitleIsVisible == false ? "Hidden" : "Visible or default");
        html.AppendLine("                  </dl>");
    }

    private static void AppendSemanticModel(
        StringBuilder html,
        ProjectInventory inventory,
        SemanticModelInventory model)
    {
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
        AppendFact(html, "Developer-authored model objects", (modelUsages.Length - generatedObjects).ToString(CultureInfo.InvariantCulture));
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
                    .Append("\" data-search-text=\"").Append(Encode($"{table.Name} {usage.ObjectName} {HumanizeIdentifier(usage.ObjectType)} {UsageLabel(usage.UsageState)} {usageReason}"))
                    .Append("\"><div class=\"semantic-object-header\"><span class=\"object-name\"><strong>").Append(Encode(usage.ObjectName))
                    .Append("</strong><span>").Append(Encode(HumanizeIdentifier(usage.ObjectType)));
                if (usage.DirectReportLocationCount > 0)
                {
                    html.Append(" · used in ").Append(usage.DirectReportLocationCount.ToString(CultureInfo.InvariantCulture))
                        .Append(usage.DirectReportLocationCount == 1 ? " report location" : " report locations");
                }

                html.Append("</span></span><span class=\"badge ").Append(UsageClass(usage.UsageState)).Append("\">")
                    .Append(Encode(UsageLabel(usage.UsageState))).AppendLine("</span></div>");
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

    private static void AppendUsageGuide(StringBuilder html)
    {
        html.AppendLine("      <details class=\"usage-guide\"><summary><span>How usage classification works</span><span class=\"usage-guide-hint\">5 statuses explained</span></summary>");
        html.AppendLine("        <div class=\"usage-guide-body\"><dl class=\"usage-classification-list\">");
        AppendUsageGuideItem(html, "Directly used", "Referenced directly by the report, for example in a visual, filter, tooltip or drillthrough setting.", SemanticUsageStates.DirectlyUsed);
        AppendUsageGuideItem(html, "Indirectly used", "Needed by another model object that has direct report usage, such as a DAX measure.", SemanticUsageStates.IndirectlyUsed);
        AppendUsageGuideItem(html, "Structurally required", "Needed by model structure detected by PBI Assure, such as relationships, sort-by configuration or hierarchy levels.", SemanticUsageStates.StructurallyRequired);
        AppendUsageGuideItem(html, "Used only by unused branch", "Referenced only through an object or dependency branch with no identified report usage.", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AppendUsageGuideItem(html, "Apparently unused", "No usage was found within the analysed project scope. This is not proof it is safe to delete or unused by external reports, models or processes.", SemanticUsageStates.ApparentlyUnused);
        html.AppendLine("        </dl></div>");
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
        var roles = usage.DirectReportReferences
            .Where(evidence => string.Equals(evidence.Report, location.Report, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evidence.Page, location.Page, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evidence.Visual, location.Visual, StringComparison.OrdinalIgnoreCase))
            .Select(evidence => evidence.Role)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roles.Any(role => !string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)))
        {
            roles = roles.Where(role => !string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        return string.Join(", ", roles.Select(role =>
            FieldRoleLabel(role!, hasVisual, pageScope: !string.IsNullOrWhiteSpace(location.Page))));
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
        html.AppendLine("              <h4>Power Query dependency</h4>");
        html.Append("              <p>");
        if (allObjectsAppearUnused)
        {
            html.Append("This table&#x27;s model objects appear unused in the semantic and report layers, but ");
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
        html.AppendLine(". The backing Power Query is still required during data preparation.</p>");
        html.AppendLine("              <p class=\"secondary\">Review whether loading this table into the semantic model is still required. Keep the query while known downstream queries depend on it.</p>");
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
            html.Append(" · <code>").Append(Encode(usage.ArtifactPath)).AppendLine("</code></li>");
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

    private static string FindingAnchor(int index)
    {
        return $"finding-{index + 1}";
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

        return HumanizeVisualType(visual.VisualType);
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

    private static string? FindingNavigationSummary(ProjectInventory inventory)
    {
        var parts = new List<string>();
        if (inventory.ErrorFindingCount > 0)
        {
            parts.Add($"{inventory.ErrorFindingCount} {Pluralize(inventory.ErrorFindingCount, "error", "errors")}");
        }

        parts.Add($"{inventory.WarningFindingCount} {Pluralize(inventory.WarningFindingCount, "warning", "warnings")}");
        parts.Add($"{inventory.ReviewRequiredCount} {Pluralize(inventory.ReviewRequiredCount, "review", "reviews")}");
        return string.Join(" · ", parts);
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

        AppendFact(html, "Source file", finding.ArtifactPath, code: true);
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

    private sealed record FindingFacetOption(string Value, string Label);

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
            SemanticUsageStates.UsedOnlyByUnusedBranch => "Used only by unused branch",
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

    private const string Styles = """
    :root {
      color-scheme: light;
      --background: #f4f6f8;
      --surface: #ffffff;
      --text: #182230;
      --muted: #52606d;
      --border: #b8c2cc;
      --link: #004b87;
      --focus: #ffbf47;
      --error: #8b1e2d;
      --error-bg: #fdecef;
      --warning: #6b4600;
      --warning-bg: #fff3cd;
      --info: #174a73;
      --info-bg: #e8f3fb;
      --used: #145a32;
      --used-bg: #e8f6ee;
    }
    .visually-hidden { position: absolute !important; width: 1px !important; height: 1px !important; padding: 0 !important; margin: -1px !important; overflow: hidden !important; clip: rect(0, 0, 0, 0) !important; white-space: nowrap !important; border: 0 !important; }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body { margin: 0; background: var(--background); color: var(--text); font-family: "Segoe UI", Arial, sans-serif; line-height: 1.5; }
    a { color: var(--link); text-decoration-thickness: .12em; text-underline-offset: .15em; }
    a:hover { text-decoration-thickness: .2em; }
    a:focus-visible, button:focus-visible, input:focus-visible, select:focus-visible, summary:focus-visible { outline: 4px solid var(--focus); outline-offset: 2px; }
    code { overflow-wrap: anywhere; font-size: .92em; }
    .skip-link { position: absolute; left: .75rem; top: -5rem; z-index: 10; padding: .75rem 1rem; background: #111827; color: #fff; font-weight: 700; }
    .skip-link:focus { top: .75rem; }
    .content { width: min(82rem, calc(100% - 2rem)); margin-inline: auto; }
    .site-header { background: #15324b; color: #fff; padding: 2.5rem 0 1.5rem; }
    .site-header code { color: #fff; }
    .site-header a { color: #fff; }
    .eyebrow { margin: 0 0 .25rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
    h1 { margin: 0; font-size: clamp(2rem, 5vw, 3.25rem); line-height: 1.1; }
    h2 { margin-top: 0; font-size: 1.7rem; }
    h3 { margin-top: 2rem; }
    .lede { max-width: 70rem; }
    .report-meta { display: flex; flex-wrap: wrap; gap: .75rem 1.5rem; margin: 1rem 0; padding: 0; }
    .report-meta div { display: flex; min-width: 0; max-width: 100%; flex-wrap: wrap; gap: .4rem; }
    .report-meta dt { font-weight: 700; }
    .report-meta dd { min-width: 0; margin: 0; overflow-wrap: anywhere; }
    .section-nav { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 11rem), 1fr)); gap: .6rem; margin: 1.25rem 0 0; padding: 0; list-style: none; }
    .section-nav a { display: grid; min-width: 0; min-height: 4.25rem; align-content: center; gap: .15rem; padding: .65rem .75rem; border: 1px solid rgb(255 255 255 / 55%); border-radius: .35rem; color: #fff; text-decoration: none; }
    .section-nav a:hover, .section-nav a[aria-current="page"] { background: rgb(255 255 255 / 16%); border-color: #fff; }
    .section-nav a > span { font-weight: 750; }
    .section-nav small { color: inherit; font-size: .82rem; opacity: .9; overflow-wrap: anywhere; }
    main > section { min-width: 0; margin: 1.5rem 0; padding: 1.5rem; background: var(--surface); border: 1px solid var(--border); border-radius: .4rem; }
    .summary-groups { display: grid; min-width: 0; gap: 1.2rem; }
    .summary-group { min-width: 0; }
    .summary-group h3 { display: flex; align-items: center; gap: .75rem; margin: 0 0 .65rem; color: var(--muted); font-size: .88rem; letter-spacing: .06em; text-transform: uppercase; }
    .summary-group h3::after { height: 1px; flex: 1; background: #d7dee5; content: ""; }
    .summary-group-assurance { padding: .85rem; border: 2px solid #91a2b3; border-radius: .35rem; background: #f7fafc; }
    .summary-group-assurance h3 { color: #15324b; }
    .section-intro { max-width: 68rem; margin: -.35rem 0 1rem; color: var(--muted); }
    .group-explanation { max-width: 64rem; margin: -.2rem 0 .7rem; color: var(--muted); font-size: .92rem; }
    .metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .7rem; margin: 0; }
    .metric { padding: 1rem; border: 1px solid var(--border); border-left: .5rem solid #66788a; border-radius: .25rem; }
    .summary-group-project .metric, .summary-group-semantic .metric { padding: .75rem .85rem; }
    .summary-group-project .metric dd, .summary-group-semantic .metric dd { font-size: 1.65rem; }
    .metric dt { color: var(--muted); font-weight: 700; }
    .metric dd { margin: .15rem 0 0; font-size: 2rem; font-weight: 750; }
    .metric-error { border-left-color: var(--error); }
    .metric-warning { border-left-color: #a66b00; }
    .metric-review { border-left-color: var(--info); }
    .metric-unused { border-left-color: #5b3f88; }
    .summary-note, .secondary, .filter-status { color: var(--muted); }
    .summary-caution { max-width: 64rem; margin: .7rem 0 0; padding: .65rem .75rem; border-left: .3rem solid #5b3f88; background: #f6f2fb; color: #3f2c5b; overflow-wrap: anywhere; }
    .theme-boundary { max-width: 68rem; margin: 0 0 1.25rem; padding: .75rem .85rem; border-left: .35rem solid var(--info); background: var(--info-bg); }
    .theme-boundary p { margin: .25rem 0 0; }
    .theme-review-group + .theme-review-group { margin-top: 2rem; padding-top: 1.25rem; border-top: 1px solid #d7dee5; }
    .theme-review-group > h3 { margin: 0 0 .65rem; }
    .theme-report-list, .theme-content-grid, .theme-visual-list { display: grid; min-width: 0; gap: .75rem; }
    .theme-report-list, .theme-content-grid { grid-template-columns: repeat(auto-fit, minmax(min(100%, 25rem), 1fr)); }
    .theme-report-card, .theme-content-card { min-width: 0; padding: .9rem 1rem; border: 1px solid var(--border); border-radius: .4rem; background: #f9fbfc; }
    .theme-report-card h4, .theme-content-report > h4, .theme-content-card h5 { margin: 0 0 .45rem; }
    .theme-state { margin: 0 0 .65rem; color: var(--link); font-weight: 750; }
    .theme-source-summary, .theme-content-facts, .theme-metadata-grid dl { display: grid; gap: .4rem; margin: .5rem 0; }
    .theme-source-summary div, .theme-content-facts div, .theme-metadata-grid dl div { display: grid; min-width: 0; grid-template-columns: minmax(7rem, 10rem) minmax(0, 1fr); gap: .65rem; }
    .theme-source-summary dt, .theme-content-facts dt, .theme-metadata-grid dt { color: var(--muted); font-weight: 700; }
    .theme-source-summary dd, .theme-content-facts dd, .theme-metadata-grid dd { min-width: 0; margin: 0; overflow-wrap: anywhere; }
    .theme-resolution-issues { margin-top: .75rem; padding: .65rem .75rem; border-left: .3rem solid var(--warning); background: var(--warning-bg); }
    .theme-resolution-issues ul { margin-bottom: 0; }
    .theme-content-report { min-width: 0; margin-top: 1rem; }
    .theme-palette { display: grid; grid-template-columns: repeat(auto-fill, minmax(1.65rem, 1.65rem)); min-width: 0; max-width: 100%; gap: .4rem; margin: .75rem 0 .35rem; padding: 0; list-style: none; }
    .theme-swatch { display: grid; width: 1.65rem; height: 1.65rem; overflow: hidden; border: 1px solid #637282; border-radius: .2rem; background: var(--swatch); }
    .theme-colour-value { display: inline-flex; min-width: 0; align-items: center; gap: .35rem; vertical-align: middle; }
    .theme-colour-chip { flex: 0 0 auto; width: 1.05rem; height: 1.05rem; border: 1px solid #637282; border-radius: .15rem; background: var(--colour-chip); }
    .theme-metadata-grid { min-width: 0; margin-top: .8rem; padding-top: .65rem; border-top: 1px solid #d7dee5; }
    .theme-metadata-grid h6 { margin: 0; font-size: .92rem; }
    .theme-metrics { margin-bottom: 1rem; }
    .theme-visual-card { min-width: 0; max-width: 100%; margin: 0; border: 1px solid var(--border); border-radius: .4rem; background: #fff; overflow: clip; }
    .theme-visual-card > summary { display: flex; min-width: 0; justify-content: space-between; gap: .75rem; padding: .8rem .9rem; color: var(--text); }
    .theme-visual-card > summary::after { align-self: center; content: "+"; color: var(--link); font-size: 1.3rem; font-weight: 800; }
    .theme-visual-card[open] > summary { border-bottom: 1px solid var(--border); }
    .theme-visual-card[open] > summary::after { content: "−"; }
    .theme-visual-body { min-width: 0; padding: .75rem .9rem; }
    .theme-observation-list { display: grid; min-width: 0; grid-template-columns: repeat(auto-fit, minmax(min(100%, 22rem), 1fr)); gap: .65rem; }
    .theme-observation { min-width: 0; padding: .75rem; border: 1px solid #d7dee5; border-radius: .35rem; overflow-wrap: anywhere; }
    .theme-observation p { margin: .45rem 0 0; }
    .summary-definitions, .section-help { margin-top: .75rem; padding: .55rem .7rem; border: 1px solid #c8d2dc; border-radius: .3rem; background: #f8fafc; }
    .summary-definitions > summary, .section-help > summary { color: var(--link); font-weight: 700; }
    .summary-definitions dl { display: grid; gap: .55rem; margin: .75rem 0 .15rem; }
    .summary-definitions dl div { display: grid; gap: .1rem; }
    .summary-definitions dt { color: var(--text); font-weight: 700; }
    .summary-definitions dd { margin: 0; color: var(--muted); overflow-wrap: anywhere; }
    .section-help > p { max-width: 64rem; margin: .65rem 0 .15rem; color: var(--muted); }
    .scope { border-left: .55rem solid var(--warning) !important; }
    .filters { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; margin: 1rem 0 .5rem; padding: 1rem; background: #eef2f6; border-radius: .3rem; }
    .filters div { min-width: min(100%, 14rem); flex: 1; }
    .finding-investigation { min-width: 0; margin: 1rem 0 .5rem; padding: 1rem; border-radius: .3rem; background: #eef2f6; }
    .finding-search { max-width: 48rem; }
    .finding-filter-panel { min-width: 0; margin-top: .75rem; border-top: 1px solid #c8d2dc; }
    .finding-filter-panel > summary { display: flex; width: fit-content; align-items: center; gap: .5rem; padding-top: .65rem; }
    .active-filter-count { padding: .08rem .4rem; border-radius: 999px; background: var(--link); color: #fff; font-size: .78rem; }
    .finding-facet-grid { display: grid; min-width: 0; grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr)); gap: .75rem 1rem; padding-top: .8rem; }
    .finding-facet-grid > div { min-width: 0; }
    .finding-results-row { display: flex; min-width: 0; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: .5rem 1rem; margin: .65rem 0 .35rem; }
    .finding-results-row .filter-status { margin: 0; font-weight: 650; }
    .finding-results-row button { min-height: 2.15rem; padding: .25rem .6rem; }
    .filter-chips { display: flex; min-width: 0; flex-wrap: wrap; gap: .4rem; margin: .35rem 0 .75rem; }
    .filter-chips[hidden], .finding-empty-state[hidden] { display: none; }
    .filter-chip { min-height: 2rem; max-width: 100%; padding: .2rem .55rem; border-width: 1px; border-radius: 999px; font-size: .86rem; overflow-wrap: anywhere; }
    .filter-chip::after { margin-left: .35rem; content: "×"; font-size: 1.05em; }
    .finding-empty-state { display: grid; gap: .3rem; justify-items: start; margin: .75rem 0; padding: 1rem; border: 1px dashed #91a2b3; border-radius: .35rem; background: #f8fafc; }
    .finding-empty-state span { color: var(--muted); }
    .finding-empty-state button { margin-top: .35rem; }
    label { display: block; margin-bottom: .3rem; font-weight: 700; }
    input, select { width: 100%; min-height: 2.75rem; padding: .5rem; border: 2px solid #5f6c79; border-radius: .2rem; background: #fff; color: var(--text); font: inherit; }
    button { min-height: 2.5rem; padding: .45rem .8rem; border: 2px solid var(--link); border-radius: .25rem; background: #fff; color: var(--link); font: inherit; font-weight: 700; cursor: pointer; }
    button:hover { background: var(--info-bg); }
    .details-controls { display: flex; flex-wrap: wrap; gap: .5rem; margin: .75rem 0; }
    .card-list, .page-list, .relationship-list, .semantic-table-list, .visual-list { display: grid; min-width: 0; grid-template-columns: minmax(0, 1fr); gap: .75rem; }
    .finding-card, .page-card, .relationship-card, .visual-card, .semantic-table { min-width: 0; max-width: 100%; margin: 0; border: 1px solid var(--border); border-radius: .45rem; background: #fff; overflow: clip; }
    .page-card { border-color: #91a2b3; box-shadow: 0 1px 3px rgb(24 34 48 / 10%); }
    .visual-card { background: #f9fbfc; }
    .finding-card[data-severity="Error"] { border-left: .45rem solid var(--error); }
    .finding-card[data-severity="Warning"] { border-left: .45rem solid #a66b00; }
    .finding-card[data-severity="Information"] { border-left: .45rem solid var(--info); }
    .finding-card > summary, .page-card > summary, .relationship-card > summary, .visual-card > summary, .semantic-table > summary { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; padding: .9rem 1rem; color: var(--text); }
    .finding-card > summary::after, .page-card > summary::after, .relationship-card > summary::after, .visual-card > summary::after, .semantic-table > summary::after { align-self: center; content: "+"; color: var(--link); font-size: 1.35rem; font-weight: 800; line-height: 1; }
    .finding-card[open] > summary::after, .page-card[open] > summary::after, .relationship-card[open] > summary::after, .visual-card[open] > summary::after, .semantic-table[open] > summary::after { content: "−"; }
    .finding-card > summary { justify-content: flex-start; }
    .page-card > summary { padding: 1rem 1.1rem; background: #f4f7fa; }
    .visual-card > summary { padding: .8rem .9rem; }
    .finding-card[open] > summary, .page-card[open] > summary, .relationship-card[open] > summary, .visual-card[open] > summary, .semantic-table[open] > summary { border-bottom: 1px solid var(--border); }
    .summary-copy { display: flex; min-width: 0; flex: 1; flex-direction: column; gap: .15rem; overflow-wrap: anywhere; }
    .summary-copy > strong, .visual-name > strong { font-size: 1.05rem; color: var(--text); }
    .summary-copy > span:not(.kicker), .visual-name .secondary { color: var(--muted); font-weight: 450; }
    .summary-metadata { display: flex; min-width: 0; flex-wrap: wrap; gap: .15rem .35rem; }
    .summary-metadata > span { min-width: 0; overflow-wrap: anywhere; }
    .summary-metadata > span:not(:last-child)::after { content: " ·"; color: var(--muted); font-weight: 450; }
    .summary-metadata strong { color: var(--text); font-weight: 700; }
    .finding-card .summary-metadata strong, .page-card .summary-metadata strong { color: inherit; font-weight: 600; }
    .kicker { color: var(--link); font-size: .78rem; font-weight: 800; letter-spacing: .06em; text-transform: uppercase; }
    .count-pill { align-self: center; padding: .2rem .5rem; border-radius: 999px; background: #e4eaf0; color: #344054; font-size: .84rem; font-weight: 750; white-space: nowrap; }
    .card-body, .page-body, .visual-body { min-width: 0; max-width: 100%; padding: 1rem; }
    .card-body > :first-child, .page-body > :first-child, .visual-body > :first-child { margin-top: 0; }
    .card-body h3, .page-body h3, .visual-body h4 { margin: 1.25rem 0 .4rem; }
    .fact-strip { display: grid; grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr)); gap: .65rem; margin: .25rem 0 1rem; padding: 0; }
    .fact-strip div { padding: .65rem .75rem; border-radius: .3rem; background: #eef2f6; }
    .fact-strip dt { color: var(--muted); font-size: .83rem; font-weight: 700; }
    .fact-strip dd { margin: .15rem 0 0; font-weight: 650; }
    .fact-strip.compact { grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); }
    .semantic-feature { min-width: 0; max-width: 100%; margin: .75rem 1rem; padding: .8rem 1rem; border-left: .3rem solid #6b879d; background: #f5f8fa; overflow-wrap: anywhere; }
    .semantic-feature h4, .semantic-feature p { margin: 0 0 .35rem; }
    .calculation-item { display: block !important; }
    .semantic-expression { margin-top: .65rem; }
    .calculated-table-expression { width: calc(100% - 2rem); margin: .75rem 1rem 0; box-sizing: border-box; }
    .relationship-body { min-width: 0; max-width: 100%; padding: .85rem 1rem; }
    .relationship-facts { display: grid; min-width: 0; grid-template-columns: repeat(auto-fit, minmax(min(100%, 12rem), 1fr)); gap: .65rem; margin: 0; }
    .relationship-facts div { min-width: 0; padding: .55rem .65rem; border-radius: .3rem; background: #f4f7fa; }
    .relationship-facts dt { color: var(--muted); font-size: .83rem; font-weight: 700; }
    .relationship-review { margin: .75rem 0 0; padding: .65rem .75rem; border-left: .3rem solid var(--info); background: var(--info-bg); overflow-wrap: anywhere; }
    .object-list, .semantic-object-list, .related-findings { display: grid; min-width: 0; grid-template-columns: repeat(auto-fit, minmax(min(100%, 16rem), 1fr)); gap: .5rem; margin: .6rem 0 1rem; padding: 0; list-style: none; }
    .object-list.compact { margin-bottom: 0; }
    .object-list li { display: flex; min-width: 0; justify-content: space-between; gap: .75rem; padding: .65rem .75rem; border: 1px solid #d7dee5; border-radius: .35rem; background: #fff; overflow-wrap: anywhere; }
    .semantic-object { min-width: 0; max-width: 100%; padding: .65rem .75rem; border: 1px solid #d7dee5; border-radius: .35rem; background: #fff; }
    .semantic-object-header { display: flex; min-width: 0; max-width: 100%; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: .5rem .75rem; }
    .object-list li > span, .object-name > span { color: var(--muted); font-size: .86rem; }
    .object-name { display: flex; min-width: 0; flex: 1 1 12rem; flex-direction: column; overflow-wrap: anywhere; }
    .semantic-object[data-usage-state="StructurallyRequired"] .object-name { flex-basis: 8rem; }
    .usage-reason { min-width: 0; max-width: 100%; margin: .5rem 0 0; color: var(--muted); font-size: .86rem; overflow-wrap: anywhere; }
    .power-query-context { min-width: 0; max-width: 100%; margin: .8rem 1rem 1rem; padding: .8rem .9rem; border-left: .25rem solid var(--link); border-radius: .3rem; background: #eef6fc; overflow-wrap: anywhere; }
    .power-query-context h4 { margin: 0 0 .35rem; color: var(--text); }
    .power-query-context p { margin: .25rem 0; }
    .power-query-card > summary { align-items: center; padding: .75rem .9rem; }
    .power-query-card > summary .badge, .power-query-card > summary .count-pill { align-self: center; }
    .query-card-body { min-width: 0; max-width: 100%; padding: .75rem .9rem .85rem; }
    .query-model-association { margin: 0 0 .65rem; color: var(--muted); overflow-wrap: anywhere; }
    .query-dependencies { min-width: 0; max-width: 100%; padding: .65rem .75rem; border-radius: .3rem; background: #f4f7fa; }
    .query-dependencies h4 { margin: 0 0 .45rem; color: var(--text); font-size: .9rem; }
    .query-dependency-grid { display: grid; min-width: 0; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .55rem 1rem; margin: 0; }
    .query-dependency-grid div { min-width: 0; }
    .query-dependency-grid dt { color: var(--muted); font-size: .8rem; font-weight: 750; }
    .query-dependency-grid dd { min-width: 0; margin: .12rem 0 0; overflow-wrap: anywhere; }
    .query-link-row { min-width: 0; margin: .15rem 0 0; overflow-wrap: anywhere; }
    .query-dependencies .secondary { margin: .45rem 0 0; font-size: .86rem; }
    .query-review { margin: .65rem 0 0; padding: .6rem .7rem; border-left: .3rem solid var(--info); background: var(--info-bg); overflow-wrap: anywhere; }
    .power-query-card .technical-details { margin-top: .65rem; }
    .power-query-column-context { min-width: 0; max-width: 100%; margin: .55rem 0 0; padding: .65rem .75rem; border-radius: .3rem; background: #eef6fc; overflow-wrap: anywhere; }
    .power-query-column-context p { margin: .3rem 0; color: var(--muted); font-size: .86rem; }
    .power-query-column-context .plain-list { margin: .4rem 0 0; }
    .power-query-column-context.compact { background: #f7f9fb; }
    .power-query-column-context.compact > summary { color: var(--link); font-weight: 700; }
    .usage-guide { min-width: 0; max-width: 100%; margin: 1rem 0; border: 1px solid #91a2b3; border-radius: .4rem; background: #f7fafc; overflow: clip; }
    .usage-guide > summary { display: flex; min-width: 0; align-items: center; justify-content: space-between; gap: .75rem; padding: .75rem .9rem; color: var(--link); }
    .usage-guide > summary::after { flex: 0 0 auto; content: "+"; font-size: 1.25rem; font-weight: 800; line-height: 1; }
    .usage-guide[open] > summary { border-bottom: 1px solid #c8d2dc; background: #eef4f8; }
    .usage-guide[open] > summary::after { content: "−"; }
    .usage-guide-hint { margin-left: auto; color: var(--muted); font-size: .84rem; font-weight: 450; }
    .usage-guide-body { padding: .35rem .9rem .55rem; }
    .usage-classification-list { margin: 0; padding: 0; }
    .usage-classification-row { display: grid; min-width: 0; grid-template-columns: minmax(12rem, 14rem) minmax(0, 1fr); gap: 1rem; align-items: center; padding: .65rem 0; }
    .usage-classification-row + .usage-classification-row { border-top: 1px solid #d7dee5; }
    .usage-classification-row dt, .usage-classification-row dd { min-width: 0; margin: 0; }
    .usage-classification-row dd { color: var(--text); overflow-wrap: anywhere; }
    .usage-details { min-width: 0; max-width: 100%; margin-top: .65rem; padding-top: .45rem; border-top: 1px solid #d7dee5; }
    .usage-location-groups { display: grid; min-width: 0; max-width: 100%; gap: .9rem; margin-top: .55rem; }
    .usage-page-group { min-width: 0; max-width: 100%; padding-left: .75rem; border-left: .2rem solid #9bb3c7; }
    .usage-page-group + .usage-page-group { padding-top: .8rem; border-top: 1px solid #d7dee5; }
    .usage-page-heading { min-width: 0; margin: 0 0 .35rem; }
    .usage-group-type { display: block; color: var(--link); font-size: .72rem; font-weight: 800; letter-spacing: .06em; text-transform: uppercase; }
    .usage-page-group h5 { margin: .08rem 0 0; color: var(--text); font-size: 1rem; overflow-wrap: anywhere; }
    .usage-report { margin: 0 0 .3rem; color: var(--text); font-size: .88rem; }
    .usage-label { font-weight: 750; }
    .usage-page-kind { margin: .08rem 0 0; color: var(--muted); font-size: .86rem; font-weight: 450; }
    .usage-location-list { display: grid; min-width: 0; max-width: 100%; gap: .45rem; margin: 0; padding: 0; list-style: none; }
    .usage-location-list li { display: grid; min-width: 0; max-width: 100%; gap: .08rem; padding: .4rem 0; border: 0; border-top: 1px solid #e4e9ee; border-radius: 0; background: transparent; overflow-wrap: anywhere; }
    .usage-location-list li:first-child { border-top: 0; }
    .usage-visual, .usage-context, .usage-role { min-width: 0; }
    .usage-role { color: var(--muted); }
    .plain-list { margin: .5rem 0 1rem; }
    .related-findings li { display: flex; align-items: flex-start; gap: .45rem; }
    .model-block + .model-block { margin-top: 2rem; }
    .model-block h3 { margin-bottom: .65rem; }
    .system-generated-table { border-style: dashed; background: #fafbfc; }
    .semantic-table[hidden], .semantic-object[hidden], .finding-card[hidden], .page-card[hidden], .visual-card[hidden], .theme-visual-card[hidden] { display: none; }
    .badge { display: inline-block; max-width: 100%; flex: 0 0 auto; padding: .2rem .45rem; border: 1px solid currentColor; border-radius: .2rem; font-weight: 700; white-space: nowrap; }
    .badge-error { color: var(--error); background: var(--error-bg); }
    .badge-warning { color: var(--warning); background: var(--warning-bg); }
    .badge-information { color: var(--info); background: var(--info-bg); }
    .badge-neutral { color: #364152; background: #eef2f6; }
    .badge-used { color: var(--used); background: var(--used-bg); }
    .badge-indirect, .badge-structural { color: var(--info); background: var(--info-bg); }
    .badge-unused-branch, .badge-unused { color: #553080; background: #f2ecfa; }
    .finding-location { display: grid; gap: .45rem; margin: 0; }
    .finding-location div { display: grid; min-width: 0; grid-template-columns: minmax(4.5rem, auto) minmax(0, 1fr); gap: .65rem; }
    .finding-location dt { color: var(--muted); font-weight: 700; }
    .finding-location dd { margin: 0; }
    .inventory-link { display: inline-block; margin-top: .65rem; font-weight: 650; }
    .technical-details { color: var(--muted); font-size: .92em; }
    details:target { outline: 4px solid var(--focus); outline-offset: 2px; }
    details { margin-top: .5rem; }
    summary { cursor: pointer; color: var(--link); font-weight: 650; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    .technical-details { margin-top: 1rem; padding-top: .4rem; border-top: 1px solid #d7dee5; }
    .technical-list { display: grid; gap: .4rem; padding: 0; }
    .technical-list div { display: grid; min-width: 0; grid-template-columns: minmax(6rem, auto) minmax(0, 1fr); gap: .75rem; }
    .technical-list dt { font-weight: 700; }
    .technical-list dd, .facts dd { min-width: 0; margin: 0; overflow-wrap: anywhere; }
    .technical-details { min-width: 0; max-width: 100%; overflow-wrap: anywhere; }
    .technical-details pre { max-width: 100%; overflow-x: auto; }
    .site-footer { padding: 1.5rem 0; color: var(--muted); }
    @media (max-width: 45rem) {
      .content { width: min(100% - 1rem, 82rem); }
      main > section { padding: 1rem .65rem; }
      .filters { display: block; }
      .filters div + div { margin-top: .8rem; }
      .finding-investigation { padding: .75rem; }
      .finding-facet-grid { grid-template-columns: 1fr; }
      .finding-card > summary, .page-card > summary, .relationship-card > summary, .visual-card > summary, .semantic-table > summary { gap: .6rem; padding: .75rem; }
      .card-body, .page-body, .visual-body { padding: .75rem; }
      .power-query-card > summary { flex-wrap: wrap; }
      .query-card-body { padding: .7rem .75rem .75rem; }
      .query-dependency-grid { grid-template-columns: 1fr; }
      .object-list, .semantic-object-list { grid-template-columns: 1fr; }
      .usage-classification-row { grid-template-columns: 1fr; gap: .35rem; align-items: start; }
      .count-pill { white-space: normal; }
      .technical-list div, .finding-location div { grid-template-columns: 1fr; gap: .15rem; }
      .theme-source-summary div, .theme-content-facts div, .theme-metadata-grid dl div { grid-template-columns: 1fr; gap: .1rem; }
    }
    @media print {
      body { background: #fff; }
      .skip-link, .section-navigator, .filters, .filter-status, .details-controls, .finding-investigation, .finding-results-row, .filter-chips, .finding-empty-state { display: none; }
      .report-section[hidden] { display: block !important; }
      main > section { break-inside: avoid; border-color: #777; }
      details { break-inside: avoid; }
      a { color: #000; }
    }
    """;

    private const string FilterScript = """
    (() => {
      const normalise = value => (value || '').toLocaleLowerCase();
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
          heading?.scrollIntoView({ block: 'start' });
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
      filterFindings();

      const investigationConfigs = [
        { prefix: 'page', singular: 'page', plural: 'pages' },
        { prefix: 'query', singular: 'query', plural: 'queries' },
        { prefix: 'relationship', singular: 'relationship', plural: 'relationships' },
        { prefix: 'usage', singular: 'semantic object', plural: 'semantic objects' },
        { prefix: 'theme', singular: 'visual', plural: 'visuals' }
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
              if (shown && (query || activeFacets.length)) table.open = true;
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

      const initialFragment = decodeURIComponent(window.location.hash.slice(1));
      if (!initialFragment || !revealFragmentTarget(initialFragment)) activateSection('summary');
      window.addEventListener('hashchange', () => {
        const fragment = decodeURIComponent(window.location.hash.slice(1));
        if (fragment) revealFragmentTarget(fragment, { focus: true });
      });
    })();
    """;
}
