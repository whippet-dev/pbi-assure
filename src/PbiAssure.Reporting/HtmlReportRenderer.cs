using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static class HtmlReportRenderer
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
        AppendSectionNavigationItem(html, "semantic-usage", "Semantic model", $"{inventory.DeveloperSemanticObjectCount} developer {Pluralize(inventory.DeveloperSemanticObjectCount, "object", "objects")}");
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
        html.AppendLine("          <p id=\"summary-assurance-help\" class=\"group-explanation\">Automated checks that may need attention. Start with errors, then warnings and items that need a person to review them.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Errors", inventory.ErrorFindingCount, "metric-error");
        AppendMetric(html, "Warnings", inventory.WarningFindingCount, "metric-warning");
        AppendMetric(html, "Review required", inventory.ReviewRequiredCount, "metric-review");
        AppendMetric(html, "Total findings", inventory.FindingCount);
        html.AppendLine("      </dl>");
        html.AppendLine("        </section>");
        html.AppendLine("        <section class=\"summary-group summary-group-project\" aria-labelledby=\"summary-project-heading\" aria-describedby=\"summary-project-help\">");
        html.AppendLine("          <h3 id=\"summary-project-heading\">Project</h3>");
        html.AppendLine("          <p id=\"summary-project-help\" class=\"group-explanation\">The amount of report, model and data-preparation content included in this scan.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Reports", inventory.ReportCount);
        AppendMetric(html, "Pages", inventory.PageCount);
        AppendMetric(html, "Visuals", inventory.VisualCount);
        if (inventory.ReportMeasureCount > 0)
        {
            AppendMetric(html, "Report measures", inventory.ReportMeasureCount);
        }
        AppendMetric(html, "Developer objects", inventory.DeveloperSemanticObjectCount);
        if (inventory.SystemGeneratedSemanticObjectCount > 0)
        {
            AppendMetric(html, "System-generated", inventory.SystemGeneratedSemanticObjectCount);
        }
        if (inventory.PowerQueryCount > 0)
        {
            AppendMetric(html, "Power Query sources", inventory.PowerQueryCount);
        }
        if (inventory.DataSourceCount > 0)
        {
            AppendMetric(html, "Connector types", inventory.DistinctConnectorFamilyCount);
        }
        html.AppendLine("      </dl>");
        html.AppendLine("        </section>");
        html.AppendLine("        <section class=\"summary-group summary-group-semantic\" aria-labelledby=\"summary-semantic-heading\" aria-describedby=\"summary-semantic-help\">");
        html.AppendLine("          <h3 id=\"summary-semantic-heading\">Semantic usage</h3>");
        html.AppendLine("          <p id=\"summary-semantic-help\" class=\"group-explanation\">How the report uses model objects such as columns and measures. If no use was found, review the object rather than deleting it automatically.</p>");
        html.AppendLine("      <dl class=\"metrics\">");
        AppendMetric(html, "Directly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.DirectlyUsed));
        AppendMetric(html, "Indirectly used", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.IndirectlyUsed));
        AppendMetric(html, "Structurally required", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.StructurallyRequired));
        AppendMetric(html, "Unused branch", inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.UsedOnlyByUnusedBranch));
        AppendMetric(html, "Apparently unused", inventory.DeveloperApparentlyUnusedSemanticObjectCount, "metric-unused");
        html.AppendLine("      </dl>");
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

        foreach (var modelGroup in inventory.PowerQueryUsages.GroupBy(usage => usage.SemanticModel))
        {
            html.Append("      <h3>").Append(Encode(modelGroup.Key)).AppendLine("</h3>");
            html.AppendLine("      <div class=\"semantic-table-list\">");
            foreach (var usage in modelGroup.OrderBy(item => PowerQueryUsageOrder(item.UsageState))
                         .ThenBy(item => item.QueryName, StringComparer.OrdinalIgnoreCase))
            {
                var label = usage.UsageState switch
                {
                    PowerQueryUsageStates.LoadedToModel => "Loads into the model",
                    PowerQueryUsageStates.SupportingQuery => "Supports a loaded query",
                    _ => "No use found",
                };
                html.Append("        <details class=\"semantic-table\"><summary><span class=\"summary-copy\"><strong>")
                    .Append(Encode(usage.QueryName)).Append("</strong><span>").Append(Encode(label));
                if (usage.Table is not null)
                {
                    html.Append(" · table ").Append(Encode(usage.Table));
                }
                html.Append("</span></span><span class=\"badge ").Append(UsageClass(
                        usage.UsageState == PowerQueryUsageStates.ApparentlyUnused
                            ? SemanticUsageStates.ApparentlyUnused
                            : usage.UsageState == PowerQueryUsageStates.LoadedToModel
                                ? SemanticUsageStates.DirectlyUsed
                                : SemanticUsageStates.IndirectlyUsed))
                    .Append("\">").Append(Encode(label)).AppendLine("</span></summary>");
                html.AppendLine("          <dl class=\"facts\">");
                AppendFact(html, "Type", usage.SourceKind == PowerQuerySourceKinds.TablePartition
                    ? "Table load" : "Reusable query");
                var targets = inventory.PowerQueryDependencies.Where(edge =>
                        edge.SemanticModel == usage.SemanticModel &&
                        edge.FromQueryName == usage.QueryName &&
                        edge.FromSourceKind == usage.SourceKind &&
                        edge.FromPartition == usage.Partition)
                    .Select(edge => edge.ToQueryName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                AppendFact(html, "Uses", targets.Length == 0 ? "No known query dependencies" : string.Join(", ", targets));
                AppendFact(html, "Used by", usage.ReferencedBy.Count == 0
                    ? "No known queries" : string.Join(", ", usage.ReferencedBy.Select(item => item.FromQueryName).Distinct()));
                if (usage.HasDynamicReferences)
                {
                    AppendFact(html, "Manual review", "This expression constructs references dynamically, so some dependencies may not be visible here.");
                }
                html.AppendLine("          </dl>");
                html.AppendLine("          <details class=\"technical-details\"><summary>View M expression</summary><pre><code>");
                html.Append(Encode(usage.Expression));
                html.AppendLine("</code></pre></details>");
                html.AppendLine("        </details>");
            }
            html.AppendLine("      </div>");
        }
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
            var queries = connectorGroup.Select(source => source.QueryName)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            html.Append("        <details class=\"semantic-table\"><summary><span class=\"summary-copy\"><strong>")
                .Append(Encode(connectorGroup.Key)).Append("</strong><span>Used by ")
                .Append(queries.Length.ToString(CultureInfo.InvariantCulture))
                .Append(queries.Length == 1 ? " query" : " queries")
                .AppendLine(" · connection details withheld</span></span></summary>");
            html.AppendLine("          <dl class=\"facts\">");
            AppendFact(html, "Queries", string.Join(", ", queries));
            var locationLabels = connectorGroup.Select(source => SourceLocationLabel(source.LocationKind))
                .Distinct(StringComparer.Ordinal).ToArray();
            AppendFact(html, "Location type", string.Join(", ", locationLabels));
            html.AppendLine("          </dl>");
            html.AppendLine("          <details class=\"technical-details\"><summary>Connector details</summary>");
            html.AppendLine("            <ul class=\"plain-list\">");
            foreach (var function in connectorGroup.Select(source => source.ConnectorFunction)
                         .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                html.Append("              <li><code>").Append(Encode(function)).AppendLine("</code></li>");
            }
            html.AppendLine("            </ul>");
            html.AppendLine("          </details>");
            html.AppendLine("        </details>");
        }
        html.AppendLine("      </div>");
    }

    private static string SourceLocationLabel(string locationKind) => locationKind switch
    {
        DataSourceLocationKinds.LocalFile => "File on a developer computer",
        DataSourceLocationKinds.NetworkFile => "Network file",
        DataSourceLocationKinds.RelativeFile => "Relative file path",
        DataSourceLocationKinds.WebAddress => "Web or cloud address",
        DataSourceLocationKinds.NamedServer => "Named server or database",
        _ => "Dynamic or not exposed",
    };

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
        if (inventory.Findings.Count == 0)
        {
            html.AppendLine("      <p>No automated findings were produced. Manual review is still required.</p>");
            html.AppendLine("    </section>");
            return;
        }

        html.AppendLine("      <div class=\"filters\" aria-label=\"Filter findings\">");
        html.AppendLine("        <div><label for=\"finding-search\">Search findings</label><input id=\"finding-search\" type=\"search\" autocomplete=\"off\"></div>");
        html.AppendLine("        <div><label for=\"finding-severity\">Severity</label><select id=\"finding-severity\"><option value=\"\">All severities</option><option>Error</option><option>Warning</option><option>Information</option></select></div>");
        html.AppendLine("        <div><label for=\"finding-category\">Category</label><select id=\"finding-category\"><option value=\"\">All categories</option>");
        foreach (var category in inventory.Findings
                     .Select(finding => finding.Category)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
        {
            html.Append("          <option value=\"").Append(Encode(category)).Append("\">")
                .Append(Encode(HumanizeIdentifier(category))).AppendLine("</option>");
        }

        html.AppendLine("        </select></div>");
        html.AppendLine("      </div>");
        html.Append("      <p id=\"finding-filter-status\" class=\"filter-status\" role=\"status\">")
            .Append(inventory.Findings.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(" findings shown.</p>");
        AppendDetailsControls(html, "finding-list", "issues");
        html.AppendLine("      <div id=\"finding-list\" class=\"card-list\">");
        for (var index = 0; index < inventory.Findings.Count; index++)
        {
            var finding = inventory.Findings[index];
            var context = ResolveVisualContext(inventory, finding);
            html.Append("        <details id=\"").Append(FindingAnchor(index)).Append("\" class=\"finding-card\" data-severity=\"")
                .Append(Encode(finding.Severity)).Append("\" data-category=\"").Append(Encode(finding.Category)).AppendLine("\">");
            html.Append("          <summary><span class=\"badge ").Append(SeverityClass(finding.Severity))
                .Append("\">").Append(Encode(finding.Severity)).Append("</span><span class=\"summary-copy\"><strong>")
                .Append(Encode(FriendlyFindingMessage(finding, context))).Append("</strong><span>")
                .Append(Encode(FindingLocationSummary(finding, context))).AppendLine("</span></span></summary>");
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

        html.AppendLine("      <div class=\"filters page-tools\" aria-label=\"Find report pages and visuals\">");
        html.AppendLine("        <div><label for=\"page-search\">Find a page, visual, column or measure</label><input id=\"page-search\" type=\"search\" autocomplete=\"off\"></div>");
        html.AppendLine("      </div>");
        html.Append("      <p id=\"page-filter-status\" class=\"filter-status\" role=\"status\">")
            .Append(inventory.PageCount.ToString(CultureInfo.InvariantCulture)).Append(" pages and ")
            .Append(inventory.VisualCount.ToString(CultureInfo.InvariantCulture)).AppendLine(" visuals shown.</p>");
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
                html.AppendLine("        <details class=\"relationship-card\">");
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
        html.AppendLine("      <p class=\"section-intro\">Review columns, measures and other model objects by table. Expand an object to see why it has its status and exactly where it is used.</p>");
        AppendUsageGuide(html);
        if (inventory.SemanticModels.Count == 0)
        {
            html.AppendLine("      <p>No supported semantic model definition was found.</p>");
            html.AppendLine("    </section>");
            return;
        }

        html.AppendLine("      <div class=\"filters\" aria-label=\"Filter semantic objects\">");
        html.AppendLine("        <div><label for=\"usage-search\">Search tables and objects</label><input id=\"usage-search\" type=\"search\" autocomplete=\"off\"></div>");
        html.AppendLine("        <div><label for=\"usage-state\">Usage state</label><select id=\"usage-state\"><option value=\"\">All usage states</option>");
        foreach (var state in inventory.SemanticObjectUsages
                     .Select(usage => usage.UsageState)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(UsageOrder))
        {
            html.Append("          <option value=\"").Append(Encode(state)).Append("\">")
                .Append(Encode(UsageLabel(state))).AppendLine("</option>");
        }

        html.AppendLine("        </select></div>");
        html.AppendLine("        <div><label for=\"usage-type\">Object type</label><select id=\"usage-type\"><option value=\"\">All object types</option>");
        foreach (var type in inventory.SemanticObjectUsages.Select(usage => usage.ObjectType).Distinct(StringComparer.Ordinal).OrderBy(type => type, StringComparer.Ordinal))
        {
            html.Append("          <option value=\"").Append(Encode(type)).Append("\">").Append(Encode(HumanizeIdentifier(type))).AppendLine("</option>");
        }
        html.AppendLine("        </select></div>");
        html.AppendLine("        <div><label for=\"usage-origin\">Object origin</label><select id=\"usage-origin\"><option value=\"developer\" selected>Developer objects</option><option value=\"\">All objects</option><option value=\"system\">Power BI-generated objects</option></select></div>");
        html.AppendLine("      </div>");
        html.Append("      <p id=\"usage-filter-status\" class=\"filter-status\" role=\"status\">")
            .Append(inventory.DeveloperSemanticObjectCount.ToString(CultureInfo.InvariantCulture)).AppendLine(" developer semantic objects shown.</p>");
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

        html.Append("        <details class=\"page-card\" data-page-name=\"").Append(Encode(page.DisplayName)).Append('"');
        if (page.IsActive)
        {
            html.Append(" open");
        }

        html.AppendLine(">");
        html.Append("          <summary><span class=\"summary-copy\"><span class=\"kicker\">")
            .Append(pageNumber is null ? "Report page" : $"Page {pageNumber}").Append("</span><strong>")
            .Append(Encode(page.DisplayName)).Append("</strong><span>")
            .Append(Encode($"{PageRole(page)} · {PageVisibility(page)} · {page.VisualCount} visuals"))
            .AppendLine("</span></span>");
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
        AppendFact(html, "Interactions", page.VisualInteractionCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Object uses", page.FieldReferenceCount.ToString(CultureInfo.InvariantCulture));
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
                    string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)
                        ? visualScope ? "Visual filter" : "Page filter"
                        : HumanizeIdentifier(role!)));
                html.Append(" - ").Append(Encode(roleLabel));
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
        AppendFact(html, "Developer objects", (modelUsages.Length - generatedObjects).ToString(CultureInfo.InvariantCulture));
        if (generatedObjects > 0)
        {
            AppendFact(html, "System-generated objects", generatedObjects.ToString(CultureInfo.InvariantCulture));
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
            AppendSemanticFeatures(html, table);
            html.AppendLine("            <ul class=\"semantic-object-list\">");
            foreach (var usage in usages)
            {
                var usageReason = usage.DirectReportLocationCount == 0
                    ? DescribeSemanticUsageReason(inventory, usage)
                    : null;
                html.Append("              <li class=\"semantic-object\" data-usage-state=\"").Append(Encode(usage.UsageState))
                    .Append("\" data-object-type=\"").Append(Encode(usage.ObjectType))
                    .Append("\" data-object-origin=\"").Append(table.IsSystemGenerated ? "system" : "developer")
                    .Append("\" data-search-text=\"").Append(Encode($"{table.Name} {usage.ObjectName} {HumanizeIdentifier(usage.ObjectType)}"))
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
                AppendUsageDetails(html, inventory, usage);
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
        AppendUsageGuideItem(html, "Directly used", "Used in a visual, filter, tooltip or drillthrough setting.", SemanticUsageStates.DirectlyUsed);
        AppendUsageGuideItem(html, "Indirectly used", "Needed by something used directly, such as a DAX measure.", SemanticUsageStates.IndirectlyUsed);
        AppendUsageGuideItem(html, "Structurally required", "Needed by the model structure, such as a relationship key.", SemanticUsageStates.StructurallyRequired);
        AppendUsageGuideItem(html, "Used only by unused branch", "Only referenced by an object with no detected report usage.", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AppendUsageGuideItem(html, "Apparently unused", "No usage was found here. This does not prove it is safe to remove.", SemanticUsageStates.ApparentlyUnused);
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
            if (inventory.ReportCount > 1)
            {
                html.Append("                    <p class=\"usage-report\"><span class=\"usage-label\">Report:</span> ")
                    .Append(Encode(report?.Name ?? firstLocation.Report)).AppendLine("</p>");
            }

            if (!string.IsNullOrWhiteSpace(pageLabel))
            {
                html.Append("                    <h5><span class=\"usage-label\">Page:</span> ")
                    .Append(Encode(pageLabel));
                if (page is not null && !string.Equals(PageRole(page), "Standard", StringComparison.OrdinalIgnoreCase))
                {
                    html.Append(" <span class=\"usage-page-kind\">· ")
                        .Append(Encode($"{PageRole(page)} page")).Append("</span>");
                }
                html.AppendLine("</h5>");
            }
            else
            {
                html.AppendLine("                    <h5>Report-level use</h5>");
            }

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
                if (!string.IsNullOrWhiteSpace(roleLabel))
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
            string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase) && hasVisual
                ? "Visual filter"
                : HumanizeIdentifier(role!)));
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
                html.Append("                <li><span class=\"object-name\"><strong>")
                    .Append(Encode(item.Name)).Append("</strong><span>Calculation item");
                if (item.Ordinal is not null)
                {
                    html.Append(" · order ").Append(item.Ordinal.Value.ToString(CultureInfo.InvariantCulture));
                }

                html.AppendLine("</span></span></li>");
            }

            html.AppendLine("              </ul>");
            html.AppendLine("            </section>");
        }
    }

    private static string? DescribeSemanticUsageReason(ProjectInventory inventory, SemanticObjectUsage usage)
    {
        if (usage.UsageState is SemanticUsageStates.DirectlyUsed or SemanticUsageStates.ApparentlyUnused)
        {
            return null;
        }

        var incoming = inventory.SemanticDependencies.Where(dependency =>
            string.Equals(dependency.SemanticModel, usage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dependency.ToTable, usage.Table, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dependency.ToObjectName, usage.ObjectName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dependency.ToObjectType, usage.ObjectType, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var relationship = incoming.FirstOrDefault(dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint);
        if (relationship is not null)
        {
            var otherEndpoint = inventory.SemanticDependencies.FirstOrDefault(dependency =>
                dependency.DependencyKind == SemanticDependencyKinds.RelationshipEndpoint &&
                string.Equals(dependency.SemanticModel, relationship.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dependency.FromObjectName, relationship.FromObjectName, StringComparison.OrdinalIgnoreCase) &&
                (!string.Equals(dependency.ToTable, usage.Table, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(dependency.ToObjectName, usage.ObjectName, StringComparison.OrdinalIgnoreCase)));
            return otherEndpoint is null
                ? "Why: Used as a relationship key"
                : $"Why: Relationship key between {usage.Table}[{usage.ObjectName}] and {otherEndpoint.ToTable}[{otherEndpoint.ToObjectName}]";
        }

        var sortBy = incoming.FirstOrDefault(dependency => dependency.DependencyKind == SemanticDependencyKinds.SortBy);
        if (sortBy is not null)
        {
            return $"Why: Sorts {sortBy.FromTable}[{sortBy.FromObjectName}]";
        }

        var fieldParameter = incoming.FirstOrDefault(dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.FieldParameter);
        if (fieldParameter is not null)
        {
            return $"Why: Available through field parameter {fieldParameter.FromTable}";
        }

        var calculationGroupItem = incoming.FirstOrDefault(dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.CalculationGroupItem);
        if (calculationGroupItem is not null)
        {
            return $"Why: Available through calculation group {calculationGroupItem.FromTable}";
        }

        var dax = incoming.FirstOrDefault(dependency => dependency.DependencyKind is
            SemanticDependencyKinds.Dax or SemanticDependencyKinds.ReportMeasure);
        if (dax is not null)
        {
            var prefix = usage.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch
                ? "Referenced only by unused object"
                : "Referenced by";
            return $"Why: {prefix} {dax.FromTable}[{dax.FromObjectName}]";
        }

        return null;
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
            "PBI-NAV-001" => "This visual links to a bookmark that no longer exists.",
            "PBI-NAV-004" => "A bookmark refers to a visual that is no longer on this page.",
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

    private static string FindingLocationSummary(AssuranceFinding finding, VisualContext? context)
    {
        if (context is not null)
        {
            var identity = VisualDisplayName(context.Visual);
            return $"Page {context.Page.DisplayName} · {identity} · {DescribePosition(context.Page, context.Visual)}";
        }

        if (!string.IsNullOrWhiteSpace(finding.PageDisplayName ?? finding.Page))
        {
            return $"Page {finding.PageDisplayName ?? finding.Page}";
        }

        if (!string.IsNullOrWhiteSpace(finding.Table) || !string.IsNullOrWhiteSpace(finding.ObjectName))
        {
            return string.Join(" · ", new[] { finding.Table, finding.ObjectName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (!string.IsNullOrWhiteSpace(finding.SemanticModel))
        {
            return $"Semantic model {finding.SemanticModel}";
        }

        return "Project-wide";
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
    .scope { border-left: .55rem solid var(--warning) !important; }
    .filters { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; margin: 1rem 0 .5rem; padding: 1rem; background: #eef2f6; border-radius: .3rem; }
    .filters div { min-width: min(100%, 14rem); flex: 1; }
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
    .usage-reason { min-width: 0; max-width: 100%; margin: .5rem 0 0; color: var(--muted); font-size: .86rem; overflow-wrap: anywhere; }
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
    .usage-location-groups { display: grid; min-width: 0; max-width: 100%; gap: .8rem; margin-top: .55rem; }
    .usage-page-group { min-width: 0; max-width: 100%; }
    .usage-page-group + .usage-page-group { padding-top: .75rem; border-top: 1px solid #d7dee5; }
    .usage-page-group h5, .usage-report { margin: 0 0 .3rem; color: var(--text); font-size: .92rem; }
    .usage-label { font-weight: 750; }
    .usage-page-kind { color: var(--muted); font-weight: 450; }
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
    .semantic-table[hidden], .semantic-object[hidden], .finding-card[hidden], .page-card[hidden], .visual-card[hidden] { display: none; }
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
      .finding-card > summary, .page-card > summary, .relationship-card > summary, .visual-card > summary, .semantic-table > summary { gap: .6rem; padding: .75rem; }
      .card-body, .page-body, .visual-body { padding: .75rem; }
      .object-list, .semantic-object-list { grid-template-columns: 1fr; }
      .usage-classification-row { grid-template-columns: 1fr; gap: .35rem; align-items: start; }
      .count-pill { white-space: normal; }
      .technical-list div, .finding-location div { grid-template-columns: 1fr; gap: .15rem; }
    }
    @media print {
      body { background: #fff; }
      .skip-link, .section-navigator, .filters, .filter-status, .details-controls { display: none; }
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
      const findingSeverity = document.getElementById('finding-severity');
      const findingCategory = document.getElementById('finding-category');
      const findingList = document.getElementById('finding-list');
      const findingStatus = document.getElementById('finding-filter-status');

      const filterFindings = () => {
        if (!findingList) return;
        const query = normalise(findingSearch.value.trim());
        const severity = findingSeverity.value;
        const category = findingCategory.value;
        let visible = 0;
        findingList.querySelectorAll('.finding-card').forEach(card => {
          const show = (!query || normalise(card.textContent).includes(query)) &&
            (!severity || card.dataset.severity === severity) &&
            (!category || card.dataset.category === category);
          card.hidden = !show;
          if (show) visible += 1;
        });
        findingStatus.textContent = `${visible} ${visible === 1 ? 'finding' : 'findings'} shown.`;
      };

      [findingSearch, findingSeverity, findingCategory].forEach(control => {
        if (control) control.addEventListener('input', filterFindings);
      });

      const pageSearch = document.getElementById('page-search');
      const pageList = document.getElementById('page-list');
      const pageStatus = document.getElementById('page-filter-status');

      const filterPages = () => {
        if (!pageList) return;
        const query = normalise(pageSearch.value.trim());
        let visiblePages = 0;
        let visibleVisuals = 0;
        pageList.querySelectorAll('.page-card').forEach(page => {
          const pageSummary = page.querySelector(':scope > summary');
          const pageMatches = !query || normalise(pageSummary?.textContent).includes(query);
          let pageVisuals = 0;
          page.querySelectorAll('.visual-card').forEach(visual => {
            const show = pageMatches || !query || normalise(visual.textContent).includes(query);
            visual.hidden = !show;
            if (show) pageVisuals += 1;
          });
          const showPage = pageMatches || pageVisuals > 0;
          page.hidden = !showPage;
          if (showPage) {
            visiblePages += 1;
            visibleVisuals += pageVisuals;
            if (query) page.open = true;
          }
        });
        pageStatus.textContent = `${visiblePages} ${visiblePages === 1 ? 'page' : 'pages'} and ${visibleVisuals} ${visibleVisuals === 1 ? 'visual' : 'visuals'} shown.`;
      };

      if (pageSearch) pageSearch.addEventListener('input', filterPages);

      const usageSearch = document.getElementById('usage-search');
      const usageState = document.getElementById('usage-state');
      const usageType = document.getElementById('usage-type');
      const usageOrigin = document.getElementById('usage-origin');
      const semanticTableList = document.getElementById('semantic-table-list');
      const usageStatus = document.getElementById('usage-filter-status');

      const filterUsage = () => {
        if (!semanticTableList) return;
        const query = normalise(usageSearch.value.trim());
        const state = usageState.value;
        const type = usageType.value;
        const origin = usageOrigin.value;
        let visible = 0;
        semanticTableList.querySelectorAll('.semantic-table').forEach(table => {
          let tableVisible = 0;
          table.querySelectorAll('.semantic-object').forEach(item => {
            const show = (!query || normalise(item.dataset.searchText).includes(query)) &&
              (!state || item.dataset.usageState === state) &&
              (!type || item.dataset.objectType === type) &&
              (!origin || item.dataset.objectOrigin === origin);
            item.hidden = !show;
            if (show) {
              tableVisible += 1;
              visible += 1;
            }
          });
          table.hidden = tableVisible === 0;
          if (tableVisible > 0 && (query || state || type || origin === 'system')) table.open = true;
        });
        const originLabel = origin === 'developer' ? ' developer' : origin === 'system' ? ' Power BI-generated' : ' semantic';
        usageStatus.textContent = `${visible}${originLabel} ${visible === 1 ? 'object' : 'objects'} shown.`;
      };

      [usageSearch, usageState, usageType, usageOrigin].forEach(control => {
        if (control) control.addEventListener('input', filterUsage);
      });
      filterUsage();

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
