using System.Globalization;
using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    private static string ReportContextId(ReportInventory report) => LineageIds.Create("report", "context", report.RelativePath);
    private static string PageContextId(ReportInventory report, PageInventory page) => LineageIds.Create("page", "context", report.RelativePath + "\u001f" + page.Name);
    private static string VisualViewId(string cardId, string view) => "visual-" + cardId[4..] + "-" + view;
    private static string VisualViewId(ReportInventory report, PageInventory page, VisualInventory visual, string view) =>
        VisualViewId(LineageIds.Create("lin", VisualAnchor(report, page, visual), report.Name + "\u001f" + page.Name + "\u001f" + visual.Name, maximumTokenLength: 96), view);

    private static LineageCard VisualContextCard(SemanticLineageProjection lineage, ReportInventory report, PageInventory page, VisualInventory visual) =>
        lineage.CardForVisual(report.Name, page.Name, visual.Name) ?? new LineageCard(
            LineageIds.Create("lin", VisualAnchor(report, page, visual), report.Name + "\u001f" + page.Name + "\u001f" + visual.Name, maximumTokenLength: 96),
            LineageFocusKind.Visual, visual.Name, "Visual") { Report = report, Page = page, Visual = visual };

    private static void ContextStart(StringBuilder html, string id, string kind, string name, Action<StringBuilder>? context, params string[] views)
    {
        html.Append("<article id=\"").Append(Encode(id)).Append("\" class=\"report-context lineage-card\" data-report-context=\"").Append(kind)
            .Append("\" aria-labelledby=\"").Append(Encode(id + "-title")).AppendLine("\">");
        html.Append("<header class=\"object-context-header\"><p class=\"kicker\">").Append(kind).Append("</p><h2 tabindex=\"-1\" class=\"object-context-title\" id=\"")
            .Append(Encode(id + "-title")).Append("\">").Append(Encode(name)).AppendLine("</h2>");
        context?.Invoke(html);
        html.AppendLine("</header>");
        html.Append("<nav class=\"object-view-nav\" aria-label=\"").Append(kind).AppendLine(" views\">");
        foreach (var view in views)
            html.Append("<a data-context-view-link=\"").Append(view.ToLowerInvariant()).Append("\" href=\"#").Append(Encode(ContextViewId(id, kind, view.ToLowerInvariant()))).Append("\">").Append(view).AppendLine("</a>");
        html.AppendLine("</nav>");
    }

    private static string ContextViewId(string id, string kind, string view) => kind == "Visual" ? VisualViewId(id, view) : id + "-" + view;
    private static void ContextView(StringBuilder html, string id, string kind, string view)
    {
        html.Append("<section class=\"object-local-view\" data-context-view=\"").Append(view.ToLowerInvariant()).Append("\" id=\"")
            .Append(Encode(ContextViewId(id, kind, view.ToLowerInvariant()))).Append("\"><h3 tabindex=\"-1\">").Append(view).AppendLine("</h3>");
    }
    private static void ContextLink(StringBuilder html, string id, string name) => html.Append("<a href=\"#").Append(Encode(id)).Append("\">").Append(Encode(name)).Append("</a>");
    private static void ContextDetails(StringBuilder html, string id) => html.Append("<details class=\"object-context-details\" id=\"").Append(Encode(id + "-details")).AppendLine("\"><summary>Details</summary><dl class=\"technical-list\">");

    private static void AppendReportContexts(StringBuilder html, ProjectInventory inventory, SemanticLineageProjection lineage)
    {
        html.AppendLine("<section id=\"report-contexts\" class=\"report-section\" data-report-section=\"report-contexts\" aria-label=\"Report investigation\">");
        foreach (var report in inventory.Reports)
        {
            var id = ReportContextId(report);
            ContextStart(html, id, "Report", report.Name, null, "Summary", "Pages");
            ContextView(html, id, "Report", "Summary");
            AppendModelConnection(html, report);
            html.AppendLine("<dl class=\"object-summary-facts\">");
            AppendFact(html, "Pages", report.PageCount.ToString(CultureInfo.InvariantCulture));
            AppendFact(html, "Visuals", report.VisualCount.ToString(CultureInfo.InvariantCulture));
            AppendFact(html, "Report filters", report.Filters.Count.ToString(CultureInfo.InvariantCulture));
            html.AppendLine("</dl>");
            AppendOwnedReviewCues(html, inventory, report, null);
            if (report.FieldReferences.Count > 0)
            {
                html.AppendLine("<h4>Report-level references</h4>");
                AppendGroupedFieldReferenceList(html, lineage, report, report.FieldReferences, false, pageScope: false);
            }
            AppendReportMeasures(html, lineage, report);
            html.AppendLine("</section>");
            ContextView(html, id, "Report", "Pages");
            html.AppendLine("<ul class=\"plain-list\">");
            foreach (var page in report.Pages)
            {
                html.Append("<li>"); ContextLink(html, PageContextId(report, page) + "-summary", page.DisplayName);
                html.Append(" · ").Append(page.VisualCount).Append(' ').Append(Pluralize(page.VisualCount, "visual", "visuals")).AppendLine("</li>");
            }
            html.AppendLine("</ul></section>");
            ContextDetails(html, id);
            AppendFact(html, "Report folder", report.RelativePath, true);
            AppendFact(html, "Definition", report.DefinitionPath ?? "Not set", true);
            AppendFact(html, "Schema", report.SchemaUri ?? "Not set", true);
            html.AppendLine("</dl></details></article>");
            foreach (var page in report.Pages)
            {
                var pageId = PageContextId(report, page);
                ContextStart(html, pageId, "Page", page.DisplayName, h => { h.Append("<p>Report: "); ContextLink(h, id + "-summary", report.Name); h.AppendLine("</p>"); }, "Summary", "Visuals");
                ContextView(html, pageId, "Page", "Summary");
                html.AppendLine("<dl class=\"object-summary-facts\">");
                AppendFact(html, "Visuals", page.VisualCount.ToString(CultureInfo.InvariantCulture));
                AppendFact(html, "Page type", PageRole(page)); AppendFact(html, "Visibility", PageVisibility(page));
                AppendFact(html, "Page filters", page.FilterCount.ToString(CultureInfo.InvariantCulture));
                AppendFact(html, "Configured visual interactions", page.VisualInteractionCount.ToString(CultureInfo.InvariantCulture));
                html.AppendLine("</dl>");
                AppendOwnedReviewCues(html, inventory, report, page);
                if (page.FieldReferences.Count > 0)
                {
                    html.AppendLine("<h4>Objects used at page level</h4><p class=\"secondary\">These are used by page filters, drillthrough or other page-level settings.</p>");
                    AppendGroupedFieldReferenceList(html, lineage, report, page.FieldReferences, false);
                }
                html.AppendLine("</section>");
                ContextView(html, pageId, "Page", "Visuals");
                AppendContextVisualList(html, lineage, report, page);
                html.AppendLine("</section>");
                ContextDetails(html, pageId);
                AppendFact(html, "Model object references", page.FieldReferenceCount.ToString(CultureInfo.InvariantCulture));
                AppendFact(html, "Page ID", page.Name, true); AppendFact(html, "Source file", page.RelativePath, true);
                AppendFact(html, "Schema", page.SchemaUri ?? "Not set", true); AppendFact(html, "Display option", page.DisplayOption ?? "Not set");
                html.AppendLine("</dl></details></article>");
                var hierarchy = BuildVisualHierarchyContexts(page);
                foreach (var visual in page.Visuals)
                    if (VisualContextCard(lineage, report, page, visual) is { } card)
                        AppendVisualContext(html, inventory, lineage, report, page, visual, card, hierarchy[visual.RelativePath]);
            }
        }
        html.AppendLine("</section>");
    }

    private static string ReviewCue(string message) => message.Length <= 160 ? message : message[..157] + "...";

    private static void AppendOwnedReviewCues(StringBuilder html, ProjectInventory inventory, ReportInventory report, PageInventory? page)
    {
        var findings = inventory.Findings.Where(finding => finding.Report == report.Name && finding.Visual is null &&
            (page is null ? finding.Page is null : finding.Page == page.Name)).ToArray();
        var count = findings.Count(item => item.Category != AssuranceCategories.Accessibility);
        var accessibility = findings.Count(item => item.Category == AssuranceCategories.Accessibility);
        var scope = page is null ? "report itself" : "page";
        html.Append("<p>");
        if (count == 0 && accessibility == 0)
            html.Append(CultureInfo.InvariantCulture, $"No findings or accessibility observations for this {scope}.");
        else
            html.Append(CultureInfo.InvariantCulture, $"{count} {Pluralize(count, "finding", "findings")} · {accessibility} {Pluralize(accessibility, "accessibility observation", "accessibility observations")} for this {scope}.");
        html.AppendLine(" <a href=\"#findings\">Review findings</a> · <a href=\"#theme-review\">Review report theme</a></p>");
    }

    private static void AppendContextVisualList(StringBuilder html, SemanticLineageProjection lineage, ReportInventory report, PageInventory page)
    {
        if (page.Visuals.Count == 0) { html.AppendLine("<p>No visuals were found on this page.</p>"); return; }
        html.AppendLine("<ul class=\"plain-list visual-list\">");
        foreach (var visual in page.Visuals)
        {
            var card = VisualContextCard(lineage, report, page, visual);
            html.Append("<li>"); ContextLink(html, card is null ? "reports" : VisualViewId(card.Id, "summary"), VisualDisplayName(visual));
            html.Append(" <span class=\"secondary\">· ").Append(Encode(HumanizeVisualType(visual.VisualType) + " · " + DescribePosition(page, visual))).AppendLine("</span></li>");
        }
        html.AppendLine("</ul>");
    }

    private static void AppendVisualContext(StringBuilder html, ProjectInventory inventory, SemanticLineageProjection lineage, ReportInventory report,
        PageInventory page, VisualInventory visual, LineageCard card, VisualHierarchyContext hierarchy)
    {
        var findings = inventory.Findings.Select((finding, index) => (Finding: finding, Index: index)).Where(item =>
            string.Equals(item.Finding.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Finding.Page, page.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Finding.Visual, visual.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var retainedUnresolved = inventory.UnresolvedSemanticReferences.Where(item =>
            string.Equals(item.Report, report.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Page, page.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Visual, visual.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var theme = report.ThemeReview;
        var themeItems = theme.Deviations.Where(item => item.PageName == page.Name && item.VisualName == visual.Name).Select(item => (Label: "Deviation · " + item.PropertyLabel, Route: "theme-deviations-heading"))
            .Concat(theme.ConsistencyObservations.Where(item => item.PageName == page.Name && item.VisualName == visual.Name).Select(item => ("Consistency · " + item.PropertyLabel, "theme-consistency-heading")))
            .Concat(theme.AccessibilityObservations.Where(item => item.PageName == page.Name && item.VisualName == visual.Name).Select(item => ("Theme accessibility · " + item.Property, "theme-accessibility-heading"))).ToArray();
        ContextStart(html, card.Id, "Visual", VisualDisplayName(visual), h =>
        {
            h.Append("<p class=\"lineage-facts\">").Append(Encode(HumanizeVisualType(visual.VisualType) + " · " + DescribePosition(page, visual))).Append(" · ");
            ContextLink(h, ReportContextId(report) + "-summary", report.Name); h.Append(" / ");
            ContextLink(h, PageContextId(report, page) + "-summary", page.DisplayName); h.AppendLine("</p>");
        }, "Summary", "Objects", "Reviews");
        html.Append("<span id=\"").Append(Encode(VisualAnchor(report, page, visual))).Append("\" data-context-route=\"").Append(Encode(VisualViewId(card.Id, "summary"))).AppendLine("\"></span>");
        ContextView(html, card.Id, "Visual", "Summary");
        html.AppendLine("<dl class=\"object-summary-facts\">");
        AppendFact(html, "Model objects used", card.Uses.TotalCount.ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Findings", findings.Count(item => item.Finding.Category != AssuranceCategories.Accessibility).ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Accessibility observations", findings.Count(item => item.Finding.Category == AssuranceCategories.Accessibility).ToString(CultureInfo.InvariantCulture));
        AppendFact(html, "Theme review items", themeItems.Length.ToString(CultureInfo.InvariantCulture)); html.AppendLine("</dl>");
        AppendVisualBehaviour(html, report, visual);
        if (card.UnresolvedReportReferences.TotalCount > 0) html.Append("<p>").Append(card.UnresolvedReportReferences.TotalCount).Append(' ').Append(Pluralize(card.UnresolvedReportReferences.TotalCount, "field reference not found in the model", "field references not found in the model")).AppendLine(". See Details.</p>");
        if (card.UnresolvedReportReferences.TotalCount == 0 && retainedUnresolved.Length > 0)
            html.Append("<p>").Append(retainedUnresolved.Length).Append(' ').Append(Pluralize(retainedUnresolved.Length, "field reference not found in the model", "field references not found in the model")).AppendLine(". See Details.</p>");
        var limitations = inventory.AnalysisLimitations.Where(item => (string.Equals(item.ArtifactPath, visual.RelativePath, StringComparison.OrdinalIgnoreCase) || string.Equals(item.ArtifactPath, page.DefinitionPath, StringComparison.OrdinalIgnoreCase) || string.Equals(item.ArtifactPath, report.DefinitionPath, StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach (var limitation in limitations) html.Append("<p class=\"group-explanation\">Checks limited: ").Append(Encode(AnalysisCoveragePresentation.DisplayReason(limitation))).AppendLine("</p>");
        html.AppendLine("<p class=\"group-explanation\">Saved project evidence; runtime behaviour and external usage are outside this view.</p></section>");
        ContextView(html, card.Id, "Visual", "Objects");
        html.Append("<div class=\"lineage-diagram\"><header class=\"lineage-focus\"><p class=\"lineage-focus-reference\">")
            .Append(Encode(VisualDisplayName(visual))).Append("</p><p>")
            .Append(Encode(HumanizeVisualType(visual.VisualType) + " · " + page.DisplayName + " · " + DescribePosition(page, visual)))
            .AppendLine("</p></header><div class=\"lineage-side\" data-lineage-side=\"upstream\">");
        AppendLineageGroup(html, "uses", "Uses", card.Uses, "No model objects found for this visual.", use => AppendLineageVisualUse(html, use));
        html.AppendLine("</div></div><p class=\"group-explanation\">The model objects this visual uses. Other saved field references are in Details.</p></section>");
        ContextView(html, card.Id, "Visual", "Reviews");
        foreach (var (label, accessibility) in new[] { ("Findings", false), ("Accessibility", true) })
        {
            html.Append("<h4>").Append(label).AppendLine("</h4><ul class=\"plain-list\">");
            var family = findings.Where(item => (item.Finding.Category == AssuranceCategories.Accessibility) == accessibility).ToArray();
            foreach (var item in family)
            {
                html.Append("<li>"); ContextLink(html, FindingAnchor(inventory, item.Finding, item.Index), item.Finding.Severity + " · " + ReviewCue(FriendlyFindingMessage(item.Finding, new VisualContext(report, page, visual)))); html.AppendLine("</li>");
            }
            html.AppendLine("</ul>");
            if (family.Length == 0) html.Append("<p>No ").Append(accessibility ? "accessibility observations" : "findings").AppendLine(" for this visual.</p>");
        }
        html.AppendLine("<h4>Theme</h4><ul class=\"plain-list\">");
        foreach (var item in themeItems) { html.Append("<li>"); ContextLink(html, item.Item2, item.Item1); html.AppendLine("</li>"); }
        html.AppendLine("</ul>");
        if (DisplayedFormattingValues(new ThemeVisualContext(report, page, visual)).Any())
        {
            html.Append("<p>");
            ContextLink(html, VisualViewId(card.Id, "formatting"), "Review saved formatting evidence");
            html.AppendLine(" (saved settings, not a count of issues).</p>");
        }
        if (themeItems.Length == 0) html.AppendLine("<p>No theme review items for this visual.</p>");
        html.AppendLine("</section>");
        ContextDetails(html, card.Id);
        AppendFact(html, "Visual ID", visual.Name, true);
        AppendFact(html, "Saved title", visual.Accessibility.TitleText ?? "Not set");
        AppendFact(html, "Saved alternative text", visual.Accessibility.AltText ?? "Not set");
        AppendFact(html, "On-canvas text", visual.OnCanvasText ?? "Not set");
        AppendFact(html, "Hidden", visual.IsHidden ? "Yes" : "No");
        AppendFact(html, "Parent group", visual.ParentGroupName ?? "Not set");
        AppendFact(html, "Schema", visual.SchemaUri ?? "Not set", true); AppendFact(html, "Source file", DisplayPath(visual.RelativePath), true);
        AppendFact(html, "Position", FormatCoordinates(visual.Position)); AppendFact(html, "Saved tab order", visual.Position.TabOrder?.ToString(CultureInfo.InvariantCulture) ?? "Not set");
        html.AppendLine("</dl><h4>All saved field references</h4><p class=\"secondary\">Includes references PBI Assure doesn't count as use.</p>");
        AppendGroupedFieldReferenceList(html, lineage, report, visual.FieldReferences, true);
        AppendAccessibilitySummary(html, visual, hierarchy, "tab-order-help-" + card.Id);
        AppendLineageGroup(html, "not-resolved", "Couldn't be matched", card.UnresolvedReportReferences, null, reference => AppendLineageReportReferenceNote(html, reference), unresolved: true);
        if (card.UnresolvedReportReferences.TotalCount == 0 && retainedUnresolved.Length > 0)
            AppendLineageGroup(html, "retained-unresolved", "Field references not found in the model",
                new LineageGroup<UnresolvedSemanticReference>(retainedUnresolved.Take(SemanticLineageProjection.GroupLimit).ToArray(), retainedUnresolved.Length),
                null, reference => AppendLineageReportReferenceNote(html, reference), unresolved: true);
        AppendLineageEvidence(html, card);
        html.AppendLine("</details></article>");
    }
}
