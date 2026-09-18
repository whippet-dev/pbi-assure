using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting;

/// <summary>
/// A short working list of the developer-authored model objects for which the scan found no usage
/// evidence: the "Measure Killer" view of a PBI Assure analysis.
///
/// It is a separate, lighter document rather than a filtered full report. It reads the scan's own
/// classifications and recalculates nothing: an object is listed exactly when its state is
/// <c>ApparentlyUnused</c> and its table is not Power BI-generated. <c>UsedOnlyByUnusedBranch</c>
/// stays out — something in the model still references those objects — as do Power Query queries
/// and every finding. Confidence is the one distinction the page insists on: an absence PBI Assure
/// is sure of within the analysed scope, and one that metadata it could not fully check may bear on.
/// Neither is presented as permission to delete.
/// </summary>
public static class ApparentlyUnusedReportRenderer
{
    internal const string Lede =
        "PBI Assure found no evidence that these objects are used in the analysed project. Review them before making changes.";

    internal const string ZeroStateMessage =
        "No developer-authored semantic objects were classified as apparently unused in this analysis.";

    private const string EstablishedLabel = "Established";
    private const string QualifiedLabel = "Usage check incomplete";

    public static string Render(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var objects = Select(inventory);
        var qualifying = objects
            .SelectMany(item => SemanticUsageConfidenceQualifier.Qualifying(item.Usage, inventory.AnalysisLimitations))
            .DistinctBy(limitation => LimitationKey(limitation), StringComparer.Ordinal)
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
            foreach (var group in objects.GroupBy(item => (item.Usage.SemanticModel, item.Usage.Table)))
            {
                AppendGroup(html, inventory, group.Key.SemanticModel, group.Key.Table, group.ToArray(), limitationAnchors);
            }
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
        var established = objects.Count(item => !item.IsQualified);
        var qualified = objects.Length - established;

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
        html.Append("      <span class=\"brand\">").Append(BrandIdentity.MarkSvg)
            .AppendLine("PBI Assure<span class=\"brand-qualifier\">Review list</span></span>");
        html.AppendLine("      <p class=\"eyebrow\">Semantic model</p>");
        html.Append("      <h1>").Append(Encode(projectName)).AppendLine("</h1>");
        html.AppendLine("      <p class=\"review-title\">Apparently unused</p>");
        html.Append("      <p class=\"review-lede\">").Append(Lede).AppendLine("</p>");
        html.AppendLine("      <dl class=\"review-counts\">");
        AppendCount(html, "review-count-total", "Apparently unused", objects.Length,
            "developer-authored model objects with no usage evidence found");
        AppendCount(html, "review-count-established", EstablishedLabel, established,
            "no limitation in this analysis qualifies the result");
        AppendCount(html, "review-count-qualified", QualifiedLabel, qualified,
            "metadata PBI Assure could not fully check may bear on the result");
        html.AppendLine("      </dl>");
        html.AppendLine("      <p class=\"review-caution\"><strong>Neither count means an object is safe to delete.</strong> " +
                        "“Apparently unused” is what this analysis found within the project files; it is not proof that " +
                        "nothing outside them, such as another report, a composite model or an external tool, depends on the object.</p>");
        html.AppendLine("      <dl class=\"review-meta\">");
        html.Append("        <div><dt>Scanned</dt><dd>")
            .Append(Encode(inventory.ScannedAtUtc.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.GetCultureInfo("en-GB"))))
            .AppendLine("</dd></div>");
        html.Append("        <div><dt>Source project</dt><dd>").Append(Encode(inventory.RootPath)).AppendLine("</dd></div>");
        html.AppendLine("      </dl>");
        html.AppendLine("    </div>");
        html.AppendLine("  </header>");
        html.AppendLine("  <main class=\"content\" id=\"main-content\">");
    }

    private static void AppendCount(StringBuilder html, string cssClass, string label, int value, string note)
    {
        html.Append("        <div class=\"review-count ").Append(cssClass).Append("\"><dt>").Append(Encode(label))
            .Append("</dt><dd class=\"review-count-value\">").Append(value.ToString(CultureInfo.InvariantCulture))
            .Append("</dd><dd class=\"review-count-note\">").Append(Encode(note)).AppendLine("</dd></div>");
    }

    private static void AppendZeroState(StringBuilder html)
    {
        html.AppendLine("    <section class=\"review-zero\" aria-labelledby=\"review-zero-title\">");
        html.AppendLine("      <h2 id=\"review-zero-title\">Nothing to review here</h2>");
        html.Append("      <p>").Append(ZeroStateMessage).AppendLine("</p>");
        html.AppendLine("      <p>This is not a statement that the model contains no unused objects: objects used only by other " +
                        "unused objects, Power BI-generated date tables and anything outside the analysed project files are " +
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

        html.AppendLine("    <section class=\"review-limitations\" aria-labelledby=\"review-limitations-title\">");
        html.Append("      <h2 id=\"review-limitations-title\">Why some usage checks are incomplete</h2>").AppendLine();
        html.AppendLine("      <p>Objects marked “Usage check incomplete” keep their classification, but the metadata below was not fully analysed and could bear on it.</p>");
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
        html.AppendLine("      <label>Search<input type=\"search\" id=\"review-search\" placeholder=\"Table or object name\" autocomplete=\"off\"></label>");
        if (types.Length > 1)
        {
            html.AppendLine("      <label>Type<select id=\"review-type\"><option value=\"\">All types</option>");
            foreach (var type in types)
            {
                html.Append("        <option value=\"").Append(Encode(type)).Append("\">").Append(Encode(ObjectTypeLabel(type))).AppendLine("</option>");
            }

            html.AppendLine("      </select></label>");
        }

        if (objects.Any(item => item.IsQualified) && objects.Any(item => !item.IsQualified))
        {
            html.AppendLine("      <label>Confidence<select id=\"review-confidence\"><option value=\"\">All</option>" +
                            "<option value=\"" + ClassificationConfidences.Established + "\">" + EstablishedLabel + "</option>" +
                            "<option value=\"" + ClassificationConfidences.QualifiedByLimitation + "\">" + QualifiedLabel + "</option></select></label>");
        }

        html.Append("      <p class=\"review-showing\" id=\"review-showing\" aria-live=\"polite\">Showing all ")
            .Append(Pluralise(objects.Length, "object")).AppendLine(".</p>");
        html.AppendLine("    </div>");
    }

    private static void AppendGroup(
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

        html.Append("    <section class=\"review-group\" data-table=\"").Append(Encode(tableName)).AppendLine("\">");
        html.AppendLine("      <div class=\"review-group-head\">");
        if (inventory.SemanticModels.Count > 1)
        {
            html.Append("        <p class=\"eyebrow\">").Append(Encode(semanticModel)).AppendLine("</p>");
        }

        html.Append("        <h2>").Append(Encode(tableName)).AppendLine("</h2>");
        html.Append("        <span class=\"review-group-count\">").Append(Pluralise(objects.Length, "object")).AppendLine("</span>");
        html.AppendLine("      </div>");
        if (!string.IsNullOrWhiteSpace(table?.Description))
        {
            html.Append("      <p class=\"review-group-note review-object-description\">").Append(Encode(table!.Description!)).AppendLine("</p>");
        }

        if (wholeTableUnused)
        {
            html.AppendLine("      <p class=\"review-group-note\"><strong>Whole table.</strong> No object in this table has any usage evidence, so the table itself is apparently unused.</p>");
        }

        if (downstreamQueries.Length > 0)
        {
            html.Append("      <p class=\"review-group-note\"><strong>Still needed by Power Query.</strong> The query that loads this table is used by ")
                .Append(Encode(string.Join(", ", downstreamQueries)))
                .AppendLine(", so it is still required while the data is being prepared.</p>");
        }

        html.AppendLine("      <ul class=\"review-objects\">");
        foreach (var item in objects)
        {
            AppendObject(html, inventory, item, anchors);
        }

        html.AppendLine("      </ul>");
        html.AppendLine("    </section>");
    }

    private static void AppendObject(
        StringBuilder html,
        ProjectInventory inventory,
        ReviewObject item,
        Dictionary<string, string> anchors)
    {
        var usage = item.Usage;
        var confidence = item.IsQualified ? ClassificationConfidences.QualifiedByLimitation : ClassificationConfidences.Established;
        var searchText = string.Join(' ', new[] { usage.Table, usage.HierarchyName, usage.ObjectName, ObjectTypeLabel(usage.ObjectType) }
            .Where(part => !string.IsNullOrWhiteSpace(part))).ToLowerInvariant();

        html.Append("        <li class=\"review-object\" data-search=\"").Append(Encode(searchText))
            .Append("\" data-type=\"").Append(Encode(usage.ObjectType))
            .Append("\" data-confidence=\"").Append(confidence).AppendLine("\">");
        html.Append("          <div class=\"review-object-head\"><span class=\"review-object-name\">");
        if (!string.IsNullOrWhiteSpace(usage.HierarchyName))
        {
            html.Append("<span class=\"review-object-parent\">").Append(Encode(usage.HierarchyName)).Append(" › </span>");
        }

        html.Append(Encode(usage.ObjectName)).Append("</span>");
        html.Append("<span class=\"review-object-type\">").Append(Encode(ObjectTypeLabel(usage.ObjectType))).Append("</span>");
        html.Append(item.IsQualified
                ? "<span class=\"badge badge-review\">" + QualifiedLabel + "</span>"
                : "<span class=\"badge badge-neutral\">" + EstablishedLabel + "</span>")
            .AppendLine("</div>");

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            html.Append("          <p class=\"review-object-description\">").Append(Encode(item.Description!)).AppendLine("</p>");
        }

        foreach (var label in PowerQueryEvidence(inventory, usage))
        {
            html.Append("          <p class=\"review-object-evidence\">").Append(Encode(label)).AppendLine("</p>");
        }

        if (item.IsQualified)
        {
            var references = SemanticUsageConfidenceQualifier.Qualifying(usage, inventory.AnalysisLimitations)
                .Select(limitation => (limitation.LimitationId, Anchor: anchors.GetValueOrDefault(LimitationKey(limitation))))
                .DistinctBy(reference => reference.LimitationId + reference.Anchor, StringComparer.Ordinal)
                .OrderBy(reference => reference.LimitationId, StringComparer.Ordinal)
                .ToArray();
            html.Append("          <p class=\"review-object-limitations\">Limited by ");
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

            html.AppendLine(" (see why above).</p>");
        }

        html.AppendLine("        </li>");
    }

    private static void AppendDocumentEnd(StringBuilder html, ProjectInventory inventory, bool interactive)
    {
        html.Append("    <p class=\"review-footer\">Generated locally by PBI Assure · inventory schema ")
            .Append(Encode(inventory.SchemaVersion)).AppendLine(" · the full report holds every classification and the analysis coverage.</p>");
        html.AppendLine("  </main>");
        if (interactive)
        {
            html.AppendLine("  <script>");
            html.AppendLine(FilterScript);
            html.AppendLine("  </script>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");
    }

    // ---- Evidence ----------------------------------------------------------------------------

    /// <summary>
    /// Power Query column lineage the scan already recorded for this column. It is shown because a
    /// column no report uses can still be needed while data is prepared; the absence of a line here
    /// is not evidence of anything, because lineage is recorded only for supported static steps.
    /// </summary>
    private static IEnumerable<string> PowerQueryEvidence(ProjectInventory inventory, SemanticObjectUsage usage)
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
            .Select(HtmlReportRenderer.PowerQueryColumnUsageLabel);
    }

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

    private static string Pluralise(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString(CultureInfo.InvariantCulture)} {noun}s";

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    internal sealed record ReviewObject(SemanticObjectUsage Usage, bool IsQualified, string? Description);

    /// <summary>
    /// Plain filtering over the already-rendered list: nothing is hidden until someone types, and the
    /// document is complete without it. Groups disappear when none of their objects match.
    /// </summary>
    private const string FilterScript = """
    (() => {
      const search = document.getElementById('review-search');
      const type = document.getElementById('review-type');
      const confidence = document.getElementById('review-confidence');
      const showing = document.getElementById('review-showing');
      const objects = Array.from(document.querySelectorAll('.review-object'));
      const groups = Array.from(document.querySelectorAll('.review-group'));
      const plural = count => count === 1 ? '1 object' : count + ' objects';
      const apply = () => {
        const query = (search?.value ?? '').trim().toLowerCase();
        const wantedType = type?.value ?? '';
        const wantedConfidence = confidence?.value ?? '';
        let visible = 0;
        for (const object of objects) {
          const match = (query === '' || object.dataset.search.includes(query)) &&
            (wantedType === '' || object.dataset.type === wantedType) &&
            (wantedConfidence === '' || object.dataset.confidence === wantedConfidence);
          object.hidden = !match;
          if (match) visible++;
        }
        for (const group of groups) {
          group.hidden = !group.querySelector('.review-object:not([hidden])');
        }
        if (showing) {
          showing.textContent = visible === objects.length
            ? 'Showing all ' + plural(objects.length) + '.'
            : 'Showing ' + plural(visible) + ' of ' + objects.length + '.';
        }
      };
      for (const control of [search, type, confidence]) {
        control?.addEventListener('input', apply);
      }
    })();
    """;
}
