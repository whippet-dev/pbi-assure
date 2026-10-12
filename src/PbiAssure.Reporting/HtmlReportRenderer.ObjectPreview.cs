using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    // Emit the bounded text itself, never a complete expression hidden by presentation.
    private static void AppendBoundedDax(StringBuilder html, LineageCard card, string expression)
    {
        var saved = expression.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd();
        var length = Math.Min(600, saved.Length);
        var lines = 1;
        for (var index = 0; index < length; index++)
            if (saved[index] == '\n' && ++lines > 6) { length = index; break; }
        if (length > 0 && char.IsHighSurrogate(saved[length - 1])) length--;
        html.Append("<pre><code>").Append(Encode(saved[..length])).AppendLine("</code></pre>");
        if (length < saved.Length) html.AppendLine("<p>Preview — expression shortened.</p>");
        ContextLink(html, ObjectDefinitionId(card), "Full definition");
    }

    private static void AppendObjectSummaryPreview(StringBuilder html, ProjectInventory inventory, LineageCard card)
    {
        // The projection already applies the report-evidence policy and provides accurate totals.
        // Share a three-location budget across visual placements and explicitly separate references.
        var locations = card.UsedInReport.Items.Take(3).ToArray();
        // Keep a mixed set understandable without spending a second location budget.
        if (locations.Length == 3 && locations.All(location => location.Visual is not null) &&
            card.UsedInReport.Items.FirstOrDefault(location => location.Visual is null) is { } reference)
            locations[^1] = reference;
        else if (locations.Length == 3 && locations.All(location => location.Visual is null) &&
            card.UsedInReport.Items.FirstOrDefault(location => location.Visual is not null) is { } placement)
            locations[^1] = placement;
        foreach (var visualLocations in new[] { true, false })
        {
            var shown = locations.Where(location => (location.Visual is not null) == visualLocations).ToArray();
            if (shown.Length == 0) continue;
            html.Append("<section class=\"object-summary-group\"><h4>")
                .Append(visualLocations ? "Used in the report" : "Page and report references")
                .AppendLine("</h4><ul class=\"object-summary-preview\">");
            foreach (var location in shown)
            {
                html.Append("<li>");
                var visual = location.Visual;
                var label = visual is not null ? VisualDisplayName(visual)
                    : location.Location.Page is not null ? "Page-level reference" : "Report-level reference";
                if (visual is not null && location.VisualCardId is { } id) ContextLink(html, VisualViewId(id, "summary"), label);
                else html.Append("<span>").Append(Encode(label)).Append("</span>");
                if (visual is not null)
                    html.Append("<small>").Append(Encode(HumanizeVisualType(visual.VisualType))).Append("</small>");
                html.Append("<small>Report ").Append(Encode(location.Report?.Name ?? location.Location.Report));
                if (location.Location.Page is not null)
                    html.Append(" · Page ").Append(Encode(location.Page?.DisplayName ?? location.Location.Page));
                if (visual is not null && location.Page is not null && VisualFriendlyName(visual) is null &&
                    DescribePosition(location.Page, visual) is var position && position != "Position unavailable" &&
                    location.Page.Visuals.Any(other => other.Name != visual.Name && VisualDisplayName(other) == label &&
                        DescribePosition(location.Page, other) != position)) html.Append(" · ").Append(Encode(position));
                html.Append("</small>");
                var roles = ObjectLocationRoles(location);
                if (roles.Length > 0) html.Append("<small>Used as: ").Append(Encode(string.Join(" · ", roles))).Append("</small>");
                html.AppendLine("</li>");
            }
            html.AppendLine("</ul></section>");
        }
        if (card.UsedInReport.TotalCount > locations.Length)
            ContextLink(html, card.Id, card.UsedInReport.HiddenCount > 0
                ? $"Report usage in Lineage ({card.UsedInReport.TotalCount} locations)" : $"See all {card.UsedInReport.TotalCount} report locations");

        if (card.UsedBy.Items.Count > 0)
        {
            html.AppendLine("<section class=\"object-summary-group\"><h4>Used by</h4><ul class=\"object-summary-preview\">");
            foreach (var consumer in card.UsedBy.Items.Take(3))
            {
                html.Append("<li>");
                if (consumer.CardId is { } id) ContextLink(html, "sum-" + id[4..], consumer.Name);
                else html.Append("<span>").Append(Encode(consumer.Name)).Append("</span>");
                html.Append("<small>").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(consumer.ObjectType)));
                if (!consumer.HasOnlyDefaultRelationship && consumer.RelationshipLabels.Count > 0)
                    html.Append(" · ").Append(Encode(string.Join(" · ", consumer.RelationshipLabels)));
                if (consumer.ObjectType == SemanticObjectTypes.ReportMeasure && consumer.Report is { } report)
                    html.Append(" · Report ").Append(Encode(report));
                html.AppendLine("</small></li>");
            }
            html.AppendLine("</ul>");
            if (card.UsedBy.TotalCount > 3)
                ContextLink(html, card.Id, card.UsedBy.HiddenCount > 0
                    ? $"Explore in Lineage ({card.UsedBy.TotalCount} objects)" : $"See all {card.UsedBy.TotalCount} in Lineage");
            html.AppendLine("</section>");
        }

        var name = card.Usage?.ObjectName ?? card.Reachability?.ObjectName;
        var table = ObjectTable(inventory, card);
        var expression = card.ObjectType switch
        {
            SemanticObjectTypes.Measure => table?.Measures.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression,
            SemanticObjectTypes.Column => table?.Columns.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression,
            SemanticObjectTypes.CalculationItem => table?.CalculationGroup?.Items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression,
            SemanticObjectTypes.Table => table?.Partitions.FirstOrDefault(partition => string.Equals(partition.SourceType, "calculated", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(partition.Expression))?.Expression,
            SemanticObjectTypes.Function => inventory.SemanticModels.FirstOrDefault(model => model.Name == card.SemanticModel)?.Functions.FirstOrDefault(function => function.Name == name)?.Expression,
            SemanticObjectTypes.ReportMeasure => ObjectReportMeasure(card)?.Expression,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(expression))
        {
            html.AppendLine("<section class=\"object-summary-group\"><h4>DAX</h4>");
            AppendBoundedDax(html, card, expression);
            html.AppendLine("</section>");
        }
    }
}
