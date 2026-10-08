using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    internal const int VisualCollectionPreviewLimit = 3;

    private static readonly HashSet<string> CollectionRoleLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Values", "Rows", "Columns", "Axis", "Category", "Categories", "Legend", "Series", "Tooltips", "Tooltip",
        "Details", "Size", "Latitude", "Longitude", "Small multiples", "Color saturation", "Colour saturation",
        "Visual filter", "Conditional formatting", "Formatting", "Dynamic text", "Sort",
    };

    private static string CollectionUsageRole(LineageVisualUse use) => string.Join(" · ",
        MeaningfulUsageRoleLabels(use.Location.Evidence.Select(evidence => new UsagePresentationReference(
            evidence.UsageContext, evidence.Role, evidence.EvidencePath)), visualScope: true, pageScope: true)
        .Where(CollectionRoleLabels.Contains));

    private static void AppendCollectionVisualList(StringBuilder html, ProjectInventory inventory, SemanticLineageProjection lineage,
        ReportInventory report, PageInventory page)
    {
        if (page.Visuals.Count == 0) { html.AppendLine("<p>No visuals on this page.</p>"); return; }
        html.AppendLine("<ul class=\"visual-preview-list\">");
        foreach (var visual in page.Visuals)
        {
            var card = VisualContextCard(lineage, report, page, visual);
            var name = VisualDisplayName(visual);
            var type = HumanizeVisualType(visual.VisualType);
            // A saved title equal to the type (with the existing title quotes) adds no second identity.
            var sameAsType = string.Equals(name.Trim('“', '”'), type, StringComparison.OrdinalIgnoreCase);
            html.Append("<li class=\"visual-preview\" data-preview-visual=\"").Append(Encode(card.Id)).AppendLine("\">");
            html.Append("<h4>"); ContextLink(html, VisualViewId(card.Id, "summary"), name); html.AppendLine("</h4>");
            html.Append("<p class=\"visual-preview-meta\">").Append(Encode((sameAsType ? "" : type + " · ") + DescribePosition(page, visual))).AppendLine("</p>");
            if (card.Uses.TotalCount == 0)
                html.AppendLine("<p class=\"visual-preview-empty\">No model objects used</p>");
            else
            {
                html.AppendLine("<p class=\"visual-preview-label\">Uses</p><ul class=\"visual-use-preview\">");
                foreach (var use in card.Uses.Items.Take(VisualCollectionPreviewLimit))
                {
                    html.Append("<li><span>");
                    if (use.Object.CardId is { } objectId) ContextLink(html, "sum-" + objectId[4..], use.Object.Name);
                    else html.Append(Encode(use.Object.Name));
                    html.Append("</span>");
                    var role = CollectionUsageRole(use);
                    if (role.Length > 0) html.Append("<span class=\"visual-use-role\">").Append(Encode(role)).Append("</span>");
                    html.AppendLine("</li>");
                }
                html.AppendLine("</ul>");
                var remaining = card.Uses.TotalCount - Math.Min(card.Uses.Items.Count, VisualCollectionPreviewLimit);
                if (remaining > 0)
                    html.Append("<a class=\"visual-preview-more\" href=\"#").Append(Encode(VisualViewId(card.Id, "objects")))
                        .Append("\" aria-label=\"").Append(Encode($"{remaining} more model objects used by {name}"))
                        .Append("\">+").Append(remaining).AppendLine(" more</a>");
            }
            var findings = inventory.Findings.Where(item =>
                string.Equals(item.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Page, page.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Visual, visual.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var cues = new List<string>();
            var count = findings.Count(item => item.Category != AssuranceCategories.Accessibility);
            if (count > 0) cues.Add($"{count} {Pluralize(count, "finding", "findings")}");
            count = findings.Length - count;
            if (count > 0) cues.Add($"{count} {Pluralize(count, "accessibility observation", "accessibility observations")}");
            var theme = report.ThemeReview;
            count = theme.Deviations.Count(item => item.PageName == page.Name && item.VisualName == visual.Name)
                + theme.ConsistencyObservations.Count(item => item.PageName == page.Name && item.VisualName == visual.Name)
                + theme.AccessibilityObservations.Count(item => item.PageName == page.Name && item.VisualName == visual.Name);
            if (count > 0) cues.Add($"{count} {Pluralize(count, "theme review item", "theme review items")}");
            if (cues.Count > 0)
            {
                html.Append("<p class=\"visual-review-preview\">"); ContextLink(html, VisualViewId(card.Id, "reviews"), string.Join(" · ", cues)); html.AppendLine("</p>");
            }
            html.AppendLine("</li>");
        }
        html.AppendLine("</ul>");
    }

    private static void AppendCollectionPageReferences(StringBuilder html, SemanticLineageProjection lineage, ReportInventory report, PageInventory page)
    {
        var references = page.FieldReferences.DistinctBy(reference =>
            string.Join('\u001f', reference.Table, reference.ObjectName, reference.ObjectType, reference.HierarchyName), StringComparer.OrdinalIgnoreCase).ToArray();
        if (references.Length == 0) return;
        html.AppendLine("<div class=\"page-reference-preview\"><p class=\"visual-preview-label\">Page-level references</p><ul>");
        foreach (var reference in references.Take(VisualCollectionPreviewLimit))
        {
            var card = lineage.CardForReference(report, reference);
            html.Append("<li>");
            if (card is not null) ContextLink(html, ObjectSummaryId(card), card.Title);
            else html.Append(Encode($"{reference.Table}[{reference.ObjectName}] (couldn't be matched)"));
            html.AppendLine("</li>");
        }
        html.AppendLine("</ul>");
        if (references.Length > VisualCollectionPreviewLimit)
        {
            html.Append("<p>"); ContextLink(html, PageContextId(report, page) + "-summary", $"+{references.Length - VisualCollectionPreviewLimit} more page-level references"); html.AppendLine("</p>");
        }
        html.AppendLine("</div>");
    }
}
