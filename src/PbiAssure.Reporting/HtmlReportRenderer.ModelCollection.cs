using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    private const int ModelInspectPreviewLimit = 3;

    private static void AppendModelInspect(StringBuilder html, LineageCard card, SemanticTableInventory table, bool multipleReports)
    {
        // Read the same saved primary expressions as Definition. No new parsing or usage analysis.
        var name = card.Usage!.ObjectName;
        var expression = card.ObjectType switch
        {
            SemanticObjectTypes.Measure => table.Measures.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression,
            SemanticObjectTypes.Column => table.Columns.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression,
            SemanticObjectTypes.CalculationItem => table.CalculationGroup?.Items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression,
            _ => null,
        };
        if (card.UsedBy.TotalCount == 0 && card.UsedInReport.TotalCount == 0 && string.IsNullOrWhiteSpace(expression)) return;

        // The existing history mechanism keys anonymous disclosures by section and DOM position.
        html.Append("<details class=\"model-inspect\"><summary class=\"disclosure-marker\" aria-label=\"More about this object: ")
            .Append(Encode(card.Title)).AppendLine("\">More about this object</summary>");
        if (card.UsedBy.TotalCount > 0)
        {
            html.AppendLine("<section><h4>Used by</h4><ul>");
            foreach (var consumer in card.UsedBy.Items.Take(ModelInspectPreviewLimit))
            {
                html.Append("<li>");
                if (consumer.CardId is { } id) ContextLink(html, "sum-" + id[4..], consumer.Name);
                else html.Append("<span>").Append(Encode(consumer.Name)).Append("</span>");
                html.Append("<small>")
                    .Append(Encode(SemanticLineageProjection.ObjectTypeLabel(consumer.ObjectType)));
                if (!consumer.HasOnlyDefaultRelationship && consumer.RelationshipLabels.Count > 0)
                    html.Append(" · ").Append(Encode(string.Join(" · ", consumer.RelationshipLabels)));
                if (consumer.IsSharedReportMeasure && consumer.Report is { } report)
                    html.Append(" · Report ").Append(Encode(report));
                html.AppendLine("</small></li>");
            }
            html.AppendLine("</ul>");
            if (card.UsedBy.TotalCount > ModelInspectPreviewLimit)
            {
                ContextLink(html, card.Id, card.UsedBy.HiddenCount > 0
                    ? $"Explore in Lineage ({card.UsedBy.TotalCount} objects)" : $"See all {card.UsedBy.TotalCount} in Lineage");
            }
            html.AppendLine("</section>");
        }
        if (card.UsedInReport.TotalCount > 0)
        {
            html.AppendLine("<section><h4>Used in the report</h4><ul>");
            foreach (var location in card.UsedInReport.Items.Take(ModelInspectPreviewLimit))
                AppendModelInspectLocation(html, location, multipleReports);
            html.AppendLine("</ul>");
            if (card.UsedInReport.TotalCount > ModelInspectPreviewLimit)
                ContextLink(html, card.Id, card.UsedInReport.HiddenCount > 0
                    ? $"Report usage in Lineage ({card.UsedInReport.TotalCount} locations)" : $"See all {card.UsedInReport.TotalCount} report locations");
            html.AppendLine("</section>");
        }
        if (!string.IsNullOrWhiteSpace(expression))
        {
            var saved = expression.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd();
            var length = Math.Min(600, saved.Length);
            var lines = 1;
            for (var index = 0; index < length; index++)
                if (saved[index] == '\n' && ++lines > 6) { length = index; break; }
            if (length > 0 && char.IsHighSurrogate(saved[length - 1])) length--;
            html.Append("<section><h4>DAX</h4><pre><code>")
                .Append(Encode(saved[..length])).AppendLine("</code></pre>");
            if (length < saved.Length) html.AppendLine("<p>Preview — expression shortened.</p>");
            ContextLink(html, ObjectDefinitionId(card), "Full definition");
            html.AppendLine("</section>");
        }
        html.AppendLine("</details>");
    }

    private static void AppendModelInspectLocation(StringBuilder html, LineageReportLocation location, bool multipleReports)
    {
        // Page/report references remain explicitly distinct from visual placements. Only the
        // already policy-filtered, direct relation is used; indirect paths never enter this preview.
        var visual = location.Visual;
        var label = visual is not null ? VisualDisplayName(visual)
            : location.Location.Page is not null ? "Page-level reference" : "Report-level reference";
        html.Append("<li>");
        if (visual is not null && location.VisualCardId is { } id) ContextLink(html, VisualViewId(id, "summary"), label);
        else html.Append("<span>").Append(Encode(label)).Append("</span>");
        var context = new List<string>();
        if (visual is not null && !string.Equals(label, HumanizeVisualType(visual.VisualType), StringComparison.OrdinalIgnoreCase))
            context.Add(HumanizeVisualType(visual.VisualType));
        if (location.Location.Page is not null) context.Add("Page " + (location.Page?.DisplayName ?? location.Location.Page));
        if (multipleReports || location.Location.Page is null) context.Add("Report " + (location.Report?.Name ?? location.Location.Report));
        if (visual is not null && location.Page is not null && VisualFriendlyName(visual) is null &&
            DescribePosition(location.Page, visual) is var position && position != "Position unavailable" &&
            location.Page.Visuals.Any(other => other.Name != visual.Name && VisualDisplayName(other) == label &&
                DescribePosition(location.Page, other) != position)) context.Add(position);
        var roles = MeaningfulUsageRoleLabels(location.Evidence.Select(evidence => new UsagePresentationReference(
            evidence.UsageContext, evidence.Role, evidence.EvidencePath)), visualScope: visual is not null, pageScope: location.Location.Page is not null)
            .Where(role => CollectionRoleLabels.Contains(role) || role is "Page filter" or "Report filter" or "Drillthrough field").ToArray();
        if (roles.Length > 0) context.Add("Used as: " + string.Join(" · ", roles));
        html.Append("<small>").Append(Encode(string.Join(" · ", context))).AppendLine("</small></li>");
    }
}
