using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting;

/// <summary>
/// A short working list of the developer-authored model items for which the scan found no usage
/// evidence: the answer to "what columns and measures don't appear to be used?". The page says
/// "item" where the inventory says "object"; the semantic types themselves keep their names.
///
/// It is a separate, lighter document rather than a filtered full report. It reads the scan's own
/// classifications and recalculates nothing: an object is listed exactly when its state is
/// <c>ApparentlyUnused</c> and its table is not Power BI-generated. <c>UsedOnlyByUnusedBranch</c>
/// stays out — something in the model still references those objects — as do Power Query queries
/// and every finding. Each object is presented as a row inside a card for its table; the only status
/// the page draws attention to is a limited check, because an ordinary row needs nothing beyond its
/// name and type. Nothing here is a deletion recommendation.
/// </summary>
public static class ApparentlyUnusedReportRenderer
{
    internal const string Lede =
        "PBI Assure found no report or semantic-model usage for these items. Review them before removing anything.";

    internal const string Caution =
        "PBI Assure checks the project you selected. An item could still be used by something outside this project, so this is a review list rather than a deletion recommendation.";

    internal const string ZeroStateMessage =
        "No developer-authored model items were classified as apparently unused in this analysis.";

    internal const string CompleteLabel = "Checks complete";
    internal const string LimitedLabel = "Checks limited";

    public static string Render(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var objects = Select(inventory);
        var qualifying = objects
            .SelectMany(item => SemanticUsageConfidenceQualifier.Qualifying(item.Usage, inventory.AnalysisLimitations))
            .DistinctBy(LimitationKey, StringComparer.Ordinal)
            .OrderBy(limitation => limitation.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(limitation => limitation.LimitationId, StringComparer.Ordinal)
            .ThenBy(limitation => limitation.Reason, StringComparer.Ordinal)
            .ToArray();
        var limitationAnchors = qualifying
            .Select((limitation, index) => (Key: LimitationKey(limitation), Anchor: $"limitation-{index + 1}"))
            .ToDictionary(item => item.Key, item => item.Anchor, StringComparer.Ordinal);

        var html = new StringBuilder(capacity: 32_000);
        AppendDocumentStart(html, inventory, objects);
        if (objects.Length == 0)
        {
            AppendZeroState(html);
        }
        else
        {
            AppendLimitations(html, qualifying, limitationAnchors);
            AppendTools(html, objects);
            html.AppendLine("    <div class=\"review-grid\">");
            foreach (var group in objects.GroupBy(item => (item.Usage.SemanticModel, item.Usage.Table)))
            {
                AppendCard(html, inventory, group.Key.SemanticModel, group.Key.Table, group.ToArray(), limitationAnchors);
            }

            html.AppendLine("    </div>");
        }

        AppendDocumentEnd(html, inventory, objects.Length > 0);
        return html.ToString();
    }

    /// <summary>The inclusion rule, in one place: apparently unused, and not Power BI-generated.</summary>
    internal static ReviewObject[] Select(ProjectInventory inventory)
    {
        return inventory.SemanticObjectUsages
            .Where(usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused)
            .Where(usage => !inventory.IsSystemGeneratedSemanticObject(usage))
            .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => ObjectTypeOrder(usage.ObjectType))
            .ThenBy(usage => usage.HierarchyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
            .Select(usage => new ReviewObject(
                usage,
                usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation,
                FindDescription(inventory, usage)))
            .ToArray();
    }

    // ---- Document ----------------------------------------------------------------------------

    private static void AppendDocumentStart(StringBuilder html, ProjectInventory inventory, ReviewObject[] objects)
    {
        var projectName = HtmlReportRenderer.ProjectName(inventory);
        var limited = objects.Count(item => item.IsQualified);

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en-GB\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("  <title>Apparently unused — ").Append(Encode(projectName)).AppendLine("</title>");
        html.Append("  <link rel=\"icon\" href=\"").Append(BrandIdentity.FaviconDataUri).AppendLine("\">");
        html.AppendLine("  <style>");
        html.AppendLine(DesignSystem.Core);
        html.AppendLine(DesignSystem.UnusedReview);
        html.AppendLine("  </style>");
        html.AppendLine("  <script>");
        html.AppendLine(HtmlReportRenderer.AppearanceBootstrapScript);
        html.AppendLine("  </script>");
        html.AppendLine("</head>");
        html.AppendLine("<body class=\"unused-review\">");
        html.AppendLine("  <header class=\"review-header\">");
        html.AppendLine("    <div class=\"content\">");
        html.AppendLine("      <div class=\"review-bar\">");
        html.Append("        <span class=\"brand\">").Append(BrandIdentity.MarkSvg)
            .AppendLine("PBI Assure<span class=\"brand-qualifier\">Apparently unused</span></span>");
        HtmlReportRenderer.AppendAppearanceControl(html, "        ");
        html.AppendLine("      </div>");
        html.Append("      <p class=\"eyebrow\">").Append(Encode(projectName)).AppendLine("</p>");
        html.Append("      <h1>").Append(Pluralise(objects.Length, "item")).AppendLine(" to review</h1>");
        html.Append("      <p class=\"review-lede\">").Append(Lede).AppendLine("</p>");
        if (limited > 0)
        {
            // The headline already counts the list; the only summary worth a line is the part of it
            // whose checks were limited, and only when that part exists.
            html.Append("      <p class=\"review-limited-summary\"><a href=\"#review-limitations\">")
                .Append(limited == 1 ? "1 item has" : limited.ToString(CultureInfo.InvariantCulture) + " items have")
                .AppendLine(" limited usage checks</a>.</p>");
        }

        html.Append("      <p class=\"review-caution\">").Append(Caution).AppendLine("</p>");
        html.Append("      <p class=\"review-meta\">Scanned ")
            .Append(Encode(inventory.ScannedAtUtc.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.GetCultureInfo("en-GB"))))
            .AppendLine("</p>");
        html.AppendLine("    </div>");
        html.AppendLine("  </header>");
        html.AppendLine("  <main class=\"content\" id=\"main-content\">");
    }

    private static void AppendZeroState(StringBuilder html)
    {
        html.AppendLine("    <section class=\"review-zero\" aria-labelledby=\"review-zero-title\">");
        html.AppendLine("      <h2 id=\"review-zero-title\">Nothing to review</h2>");
        html.Append("      <p>").Append(ZeroStateMessage).AppendLine("</p>");
        html.AppendLine("      <p>This is not a statement that the model contains no unused items: items used only by other " +
                        "unused items, Power BI-generated date tables and anything outside the analysed project files are " +
                        "not part of this list. The full report shows every classification.</p>");
        html.AppendLine("    </section>");
    }

    private static void AppendLimitations(
        StringBuilder html,
        AnalysisLimitation[] limitations,
        Dictionary<string, string> anchors)
    {
        if (limitations.Length == 0)
        {
            return;
        }

        html.AppendLine("    <section class=\"review-limitations\" id=\"review-limitations\" aria-labelledby=\"review-limitations-title\">");
        html.AppendLine("      <h2 id=\"review-limitations-title\">Why some checks are limited</h2>");
        html.AppendLine("      <p>Items marked “" + LimitedLabel + "” keep their classification, but PBI Assure could not fully " +
                        "analyse the metadata below, and it could bear on them.</p>");
        html.AppendLine("      <ul>");
        foreach (var limitation in limitations)
        {
            html.Append("        <li id=\"").Append(anchors[LimitationKey(limitation)]).Append("\"><code>")
                .Append(Encode(limitation.LimitationId)).Append("</code>").Append(Encode(limitation.Reason)).AppendLine("</li>");
        }

        html.AppendLine("      </ul>");
        html.AppendLine("    </section>");
    }

    private static void AppendTools(StringBuilder html, ReviewObject[] objects)
    {
        var types = objects.Select(item => item.Usage.ObjectType).Distinct(StringComparer.Ordinal)
            .OrderBy(ObjectTypeOrder).ToArray();
        html.AppendLine("    <div class=\"review-tools\" role=\"search\" aria-label=\"Filter the list\">");
        html.AppendLine("      <label><span>Find</span><input type=\"search\" id=\"review-search\" placeholder=\"Table or item name\" autocomplete=\"off\"></label>");
        if (types.Length > 1)
        {
            html.AppendLine("      <label><span>Type</span><select id=\"review-type\"><option value=\"\">All types</option>");
            foreach (var type in types)
            {
                html.Append("        <option value=\"").Append(Encode(type)).Append("\">").Append(Encode(ObjectTypeLabel(type))).AppendLine("</option>");
            }

            html.AppendLine("      </select></label>");
        }

        if (objects.Any(item => item.IsQualified) && objects.Any(item => !item.IsQualified))
        {
            html.AppendLine("      <label><span>Checks</span><select id=\"review-confidence\"><option value=\"\">All items</option>" +
                            "<option value=\"" + ClassificationConfidences.Established + "\">" + CompleteLabel + "</option>" +
                            "<option value=\"" + ClassificationConfidences.QualifiedByLimitation + "\">" + LimitedLabel + "</option></select></label>");
        }

        html.Append("      <p class=\"review-showing\" id=\"review-showing\" aria-live=\"polite\">Showing all ")
            .Append(Pluralise(objects.Length, "item")).AppendLine(".</p>");
        html.AppendLine("    </div>");
    }

    private static void AppendCard(
        StringBuilder html,
        ProjectInventory inventory,
        string semanticModel,
        string tableName,
        ReviewObject[] objects,
        Dictionary<string, string> anchors)
    {
        var table = inventory.SemanticModels
            .FirstOrDefault(model => string.Equals(model.Name, semanticModel, StringComparison.OrdinalIgnoreCase))?
            .Tables.FirstOrDefault(candidate => string.Equals(candidate.Name, tableName, StringComparison.OrdinalIgnoreCase));
        var wholeTableUnused = inventory.SemanticTableUsages.Any(usage =>
            string.Equals(usage.SemanticModel, semanticModel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(usage.Table, tableName, StringComparison.OrdinalIgnoreCase) &&
            usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        var downstreamQueries = inventory.SemanticTablePowerQueryContexts
            .Where(context => context.IsRequiredUpstream &&
                              string.Equals(context.SemanticModel, semanticModel, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(context.Table, tableName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(context => context.UsedByQueries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        html.Append("      <article class=\"review-card\" data-table=\"").Append(Encode(tableName)).AppendLine("\">");
        html.AppendLine("        <header class=\"review-card-head\">");
        html.Append("          <p class=\"eyebrow\">Table");
        if (inventory.SemanticModels.Count > 1)
        {
            html.Append(" · ").Append(Encode(semanticModel));
        }

        html.AppendLine("</p>");
        html.Append("          <h2>").Append(Encode(tableName)).AppendLine("</h2>");
        html.Append("          <p class=\"review-card-count\">").Append(Pluralise(objects.Length, "item")).AppendLine(" to review</p>");
        html.AppendLine("        </header>");
        if (!string.IsNullOrWhiteSpace(table?.Description))
        {
            html.Append("        <p class=\"review-card-note review-description\">").Append(Encode(table!.Description!)).AppendLine("</p>");
        }

        if (wholeTableUnused)
        {
            html.AppendLine("        <p class=\"review-card-note\">No report or semantic-model usage was found for any item in this table.</p>");
        }

        if (downstreamQueries.Length > 0)
        {
            html.Append("        <p class=\"review-card-note review-card-preparation\"><strong>Used in Power Query.</strong> This table\u2019s query helps prepare ")
                .Append(Encode(JoinNames(downstreamQueries))).AppendLine(".</p>");
        }

        html.AppendLine("        <ul class=\"review-rows\">");
        foreach (var item in objects)
        {
            AppendRow(html, inventory, item, anchors);
        }

        html.AppendLine("        </ul>");
        html.AppendLine("      </article>");
    }

    private static void AppendRow(
        StringBuilder html,
        ProjectInventory inventory,
        ReviewObject item,
        Dictionary<string, string> anchors)
    {
        var usage = item.Usage;
        var confidence = item.IsQualified ? ClassificationConfidences.QualifiedByLimitation : ClassificationConfidences.Established;
        var searchText = string.Join(' ', new[] { usage.Table, usage.HierarchyName, usage.ObjectName, ObjectTypeLabel(usage.ObjectType) }
            .Where(part => !string.IsNullOrWhiteSpace(part))).ToLowerInvariant();

        html.Append("          <li class=\"review-row\" data-search=\"").Append(Encode(searchText))
            .Append("\" data-type=\"").Append(Encode(usage.ObjectType))
            .Append("\" data-confidence=\"").Append(confidence).AppendLine("\">");
        html.Append("            <div class=\"review-row-head\"><span class=\"review-row-name\">");
        if (!string.IsNullOrWhiteSpace(usage.HierarchyName))
        {
            html.Append("<span class=\"review-row-parent\">").Append(Encode(usage.HierarchyName)).Append(" › </span>");
        }

        html.Append(Encode(usage.ObjectName)).Append("</span>");
        html.Append("<span class=\"review-row-type\">").Append(Encode(ObjectTypeLabel(usage.ObjectType))).Append("</span>");
        if (item.IsQualified)
        {
            html.Append("<span class=\"badge badge-review\">").Append(LimitedLabel).Append("</span>");
        }

        html.AppendLine("</div>");

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            html.Append("            <p class=\"review-row-note review-description\">").Append(Encode(item.Description!)).AppendLine("</p>");
        }

        var evidence = PowerQueryEvidence(inventory, usage);
        if (evidence.Length > 0)
        {
            html.Append("            <p class=\"review-row-note review-row-evidence\">Power Query use: ")
                .Append(Encode(string.Join("; ", evidence))).AppendLine("</p>");
        }

        if (item.IsQualified)
        {
            var references = SemanticUsageConfidenceQualifier.Qualifying(usage, inventory.AnalysisLimitations)
                .Select(limitation => (limitation.LimitationId, Anchor: anchors.GetValueOrDefault(LimitationKey(limitation))))
                .DistinctBy(reference => reference.LimitationId + reference.Anchor, StringComparer.Ordinal)
                .OrderBy(reference => reference.LimitationId, StringComparer.Ordinal)
                .ToArray();
            html.Append("            <p class=\"review-row-note review-row-limitations\">Limited by ");
            for (var index = 0; index < references.Length; index++)
            {
                if (index > 0)
                {
                    html.Append(", ");
                }

                var (limitationId, anchor) = references[index];
                if (anchor is null)
                {
                    html.Append(Encode(limitationId));
                }
                else
                {
                    html.Append("<a href=\"#").Append(anchor).Append("\">").Append(Encode(limitationId)).Append("</a>");
                }
            }

            html.AppendLine(" — see why above.</p>");
        }

        html.AppendLine("          </li>");
    }

    private static void AppendDocumentEnd(StringBuilder html, ProjectInventory inventory, bool interactive)
    {
        html.Append("    <p class=\"review-footer\">Generated locally by PBI Assure · inventory schema ")
            .Append(Encode(inventory.SchemaVersion)).AppendLine(" · the full report holds every classification and the analysis coverage.</p>");
        html.AppendLine("  </main>");
        html.AppendLine("  <script>");
        html.AppendLine(HtmlReportRenderer.AppearanceControlScript);
        if (interactive)
        {
            html.AppendLine(FilterScript);
        }

        html.AppendLine("  </script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
    }

    // ---- Evidence ----------------------------------------------------------------------------

    /// <summary>
    /// Power Query column lineage the scan already recorded for this column, said briefly: it can be
    /// why a column no report uses still matters while data is prepared. The absence of a line here is
    /// not evidence of anything, because lineage is recorded only for supported static steps, and its
    /// presence does not change the object's classification.
    /// </summary>
    private static string[] PowerQueryEvidence(ProjectInventory inventory, SemanticObjectUsage usage)
    {
        if (usage.ObjectType != SemanticObjectTypes.Column)
        {
            return [];
        }

        return inventory.PowerQueryColumnUsages
            .Where(columnUsage =>
                string.Equals(columnUsage.SemanticModel, usage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(columnUsage.SourceTable, usage.Table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(columnUsage.SourceColumn, usage.ObjectName, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(columnUsage => string.Join('', columnUsage.ConsumerQuery, columnUsage.UsageKind), StringComparer.OrdinalIgnoreCase)
            .OrderBy(columnUsage => columnUsage.ConsumerQuery, StringComparer.OrdinalIgnoreCase)
            .ThenBy(columnUsage => columnUsage.UsageKind, StringComparer.Ordinal)
            .Select(columnUsage => $"{PowerQueryUseLabel(columnUsage.UsageKind)} in {columnUsage.ConsumerQuery}")
            .ToArray();
    }

    private static string PowerQueryUseLabel(string usageKind) => usageKind switch
    {
        PowerQueryColumnUsageKinds.MergeKey => "Merge key",
        PowerQueryColumnUsageKinds.ExpandedColumn => "Expanded",
        PowerQueryColumnUsageKinds.SelectedColumn => "Selected",
        PowerQueryColumnUsageKinds.RenamedColumn => "Renamed",
        PowerQueryColumnUsageKinds.RemovedColumn => "Removed",
        PowerQueryColumnUsageKinds.TransformedColumn => "Type changed",
        PowerQueryColumnUsageKinds.AddedColumnExpression => "Used in an added column",
        PowerQueryColumnUsageKinds.GroupingKey => "Grouping key",
        PowerQueryColumnUsageKinds.AggregationExpression => "Aggregated",
        PowerQueryColumnUsageKinds.CombinedColumn => "Combined",
        PowerQueryColumnUsageKinds.UnpivotRetainedColumn => "Kept when unpivoting",
        _ => "Referenced",
    };

    private static string? FindDescription(ProjectInventory inventory, SemanticObjectUsage usage)
    {
        var table = inventory.SemanticModels
            .FirstOrDefault(model => string.Equals(model.Name, usage.SemanticModel, StringComparison.OrdinalIgnoreCase))?
            .Tables.FirstOrDefault(candidate => string.Equals(candidate.Name, usage.Table, StringComparison.OrdinalIgnoreCase));
        return usage.ObjectType switch
        {
            SemanticObjectTypes.Column => table?.Columns.FirstOrDefault(column =>
                string.Equals(column.Name, usage.ObjectName, StringComparison.OrdinalIgnoreCase))?.Description,
            SemanticObjectTypes.Measure => table?.Measures.FirstOrDefault(measure =>
                string.Equals(measure.Name, usage.ObjectName, StringComparison.OrdinalIgnoreCase))?.Description,
            _ => null,
        };
    }

    private static string LimitationKey(AnalysisLimitation limitation) =>
        string.Join('', limitation.SemanticModel, limitation.LimitationId, limitation.Reason);

    private static string ObjectTypeLabel(string objectType) => objectType switch
    {
        SemanticObjectTypes.Column => "Column",
        SemanticObjectTypes.Measure => "Measure",
        SemanticObjectTypes.HierarchyLevel => "Hierarchy level",
        SemanticObjectTypes.CalculationItem => "Calculation item",
        _ => objectType,
    };

    private static int ObjectTypeOrder(string objectType) => objectType switch
    {
        SemanticObjectTypes.Measure => 0,
        SemanticObjectTypes.Column => 1,
        SemanticObjectTypes.CalculationItem => 2,
        SemanticObjectTypes.HierarchyLevel => 3,
        _ => 4,
    };

    private static string JoinNames(string[] names) => names.Length switch
    {
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => string.Join(", ", names[..^1]) + " and " + names[^1],
    };

    private static string Pluralise(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString(CultureInfo.InvariantCulture)} {noun}s";

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    internal sealed record ReviewObject(SemanticObjectUsage Usage, bool IsQualified, string? Description);

    /// <summary>
    /// Plain filtering over the already-rendered list: nothing is hidden until someone types, and the
    /// document is complete without it. A card disappears when none of its rows match.
    /// </summary>
    private const string FilterScript = """
    (() => {
      const search = document.getElementById('review-search');
      const type = document.getElementById('review-type');
      const confidence = document.getElementById('review-confidence');
      const showing = document.getElementById('review-showing');
      const rows = Array.from(document.querySelectorAll('.review-row'));
      const cards = Array.from(document.querySelectorAll('.review-card'));
      const plural = count => count === 1 ? '1 item' : count + ' items';
      const apply = () => {
        const query = (search?.value ?? '').trim().toLowerCase();
        const wantedType = type?.value ?? '';
        const wantedConfidence = confidence?.value ?? '';
        let visible = 0;
        for (const row of rows) {
          const match = (query === '' || row.dataset.search.includes(query)) &&
            (wantedType === '' || row.dataset.type === wantedType) &&
            (wantedConfidence === '' || row.dataset.confidence === wantedConfidence);
          row.hidden = !match;
          if (match) visible++;
        }
        for (const card of cards) {
          card.hidden = !card.querySelector('.review-row:not([hidden])');
        }
        if (showing) {
          showing.textContent = visible === rows.length
            ? 'Showing all ' + plural(rows.length) + '.'
            : 'Showing ' + plural(visible) + ' of ' + rows.length + '.';
        }
      };
      for (const control of [search, type, confidence]) {
        control?.addEventListener('input', apply);
      }
    })();
    """;
}
