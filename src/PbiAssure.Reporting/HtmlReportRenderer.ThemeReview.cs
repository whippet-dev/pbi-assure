using System.Globalization;
using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    private static void AppendThemeReview(StringBuilder html, ProjectInventory inventory)
    {
        var contexts = inventory.Reports
            .SelectMany(report => report.Pages.SelectMany(page => page.Visuals.Select(visual => new ThemeVisualContext(report, page, visual))))
            .ToArray();
        var all = contexts.SelectMany(context => context.Visual.PersistedFormatting).ToArray();
        var headline = all.Where(item => item.IncludeInHeadline).ToArray();

        html.AppendLine("    <section id=\"theme-review\" class=\"report-section\" data-report-section=\"theme-review\" aria-labelledby=\"theme-review-heading\">");
        html.AppendLine("      <h2 id=\"theme-review-heading\" tabindex=\"-1\">Theme Review</h2>");
        html.AppendLine("      <p class=\"section-intro\">See which theme is applied, which formatting differs from it and whether similar visuals use noticeably different formatting.</p>");
        html.AppendLine("      <div class=\"theme-early-access\" role=\"note\"><strong>Beta coverage</strong><p>PBI Assure compares only the theme settings it can assess confidently. A report with no flagged differences may still contain properties that were not checked. Coverage will expand over time; use Theme Review to support, not replace, human design and governance review.</p></div>");
        html.AppendLine("      <div class=\"theme-boundary\"><strong>Interpret differences in context</strong><p>Intentional design exceptions can be valid. Theme Review does not grade the report or reproduce Power BI’s full formatting engine.</p></div>");
        AppendThemeSummary(html, inventory);
        AppendThemeReviewFilters(html, inventory);
        AppendThemeDeviations(html, inventory, contexts);
        AppendThemeConsistency(html, inventory, contexts);
        AppendThemeAccessibility(html, inventory);
        html.AppendLine("      <details class=\"theme-supporting-details\"><summary>Supporting theme details</summary><div class=\"theme-supporting-body\">");
        AppendThemeContentsSection(html, inventory);
        AppendPersistedFormatting(html, contexts, all, headline);
        html.AppendLine("      </div></details>");
        html.AppendLine("    </section>");
    }

    private static void AppendThemeSummary(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"theme-summary-heading\">");
        html.AppendLine("        <h3 id=\"theme-summary-heading\">Theme status</h3>");
        html.AppendLine("        <p class=\"group-explanation\">The theme currently associated with each report, including whether a custom theme is applied over the built-in base theme.</p>");
        html.AppendLine("        <div class=\"theme-report-list\">");
        foreach (var report in inventory.Reports)
        {
            html.Append("          <article class=\"theme-report-card\"><h4>").Append(Encode(report.Name)).AppendLine("</h4>");
            html.Append("            <p class=\"theme-state\">").Append(Encode(ThemeStatusLabel(report.ThemeReview.Status.State))).AppendLine("</p>");
            AppendThemeSourceSummary(html, "Base theme", report.Theme.BaseSource);
            if (report.Theme.CustomSource is { } custom) AppendThemeSourceSummary(html, "Custom theme", custom);
            if (report.Theme.ResolutionIssues.Count > 0)
            {
                html.AppendLine("            <div class=\"theme-resolution-issues\"><strong>Some theme details could not be read</strong><ul>");
                foreach (var issue in report.Theme.ResolutionIssues) html.Append("              <li>").Append(Encode(issue)).AppendLine("</li>");
                html.AppendLine("            </ul></div>");
            }
            html.AppendLine("          </article>");
        }
        if (inventory.Reports.Count == 0) html.AppendLine("          <p>No report definition was found.</p>");
        html.AppendLine("        </div>");
        html.AppendLine("      </section>");
    }

    private static void AppendThemeReviewFilters(StringBuilder html, ProjectInventory inventory)
    {
        var items = ReviewItems(inventory).ToArray();
        if (items.Length == 0) return;

        AppendInvestigationStart(html, "theme-governance", "Search items needing review", "Search page, visual, property or value");
        AppendInvestigationFacet(html, "theme-governance", "page", "Page", "All pages", items
            .Select(item => new FindingFacetOption(item.PageDisplayName, item.PageDisplayName)).Distinct());
        AppendInvestigationFacet(html, "theme-governance", "visual-type", "Visual type", "All visual types", items
            .Select(item => new FindingFacetOption(item.VisualType ?? "Unknown", HumanizeVisualType(item.VisualType))).Distinct());
        AppendInvestigationFacet(html, "theme-governance", "review-type", "Review type", "All review types", items
            .Select(item => new FindingFacetOption(item.ReviewType, item.ReviewType)).Distinct());
        AppendInvestigationFacet(html, "theme-governance", "property", "Property", "All properties", items
            .Select(item => new FindingFacetOption(item.PropertyKey, item.PropertyLabel)).Distinct());
        AppendInvestigationEnd(html, "theme-governance", items.Length, "review item", "review items");
    }

    private static void AppendThemeDeviations(StringBuilder html, ProjectInventory inventory, ThemeVisualContext[] contexts)
    {
        var deviations = inventory.Reports.SelectMany(report => report.ThemeReview.Deviations.Select(item => (report, item))).ToArray();
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"theme-deviations-heading\">");
        html.AppendLine("        <h3 id=\"theme-deviations-heading\">Significant theme deviations</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Formatting that differs from the report's theme and may need review. A difference is not automatically a problem; check whether it is intentional.</p>");
        var checkedCount = inventory.Reports.SelectMany(report => report.Pages).SelectMany(page => page.Visuals)
            .SelectMany(visual => visual.PersistedFormatting).Count(item => item.ThemeComparison is not null);
        if (checkedCount == 0)
        {
            html.AppendLine("        <p class=\"theme-check-summary\"><strong>No theme settings could be compared automatically in this report.</strong></p>");
        }
        else
        {
            html.Append("        <p class=\"theme-check-summary\"><strong>").Append(checkedCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append(' ').Append(Pluralize(checkedCount, "theme setting", "theme settings")).Append(" checked</strong> · ")
                .Append(deviations.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(Pluralize(deviations.Length, "difference", "differences")).AppendLine(" found</p>");
        }
        if (deviations.Length == 0)
        {
            html.AppendLine("        <p class=\"theme-empty-state\">No differences were found in the theme settings PBI Assure could compare.</p>");
        }
        else
        {
            html.AppendLine("        <div class=\"theme-governance-list\">");
            foreach (var group in deviations.GroupBy(value => new
                     {
                         ReportName = value.report.Name,
                         value.item.PageDisplayName,
                         value.item.PropertyKey,
                         value.item.PropertyLabel,
                         value.item.SavedValue,
                         value.item.ThemeValue,
                         value.item.VisualType,
                     }))
            {
                var examples = group.ToArray();
                var first = examples[0].item;
                AppendGovernanceCardStart(html, "Theme deviation", first.PageDisplayName, first.VisualType,
                    first.PropertyKey, first.PropertyLabel, $"{first.SavedValue} {first.ThemeValue}");
                html.Append("            <h4>").Append(Encode(first.PropertyLabel)).AppendLine(" differs from the theme</h4>");
                html.Append("            <p><strong>Saved value:</strong> ").Append(Encode(FormattingValue(first.PropertyKey, first.SavedValue)))
                    .Append(" · <strong>Theme setting:</strong> ").Append(Encode(FormattingValue(first.PropertyKey, first.ThemeValue))).AppendLine("</p>");
                html.Append("            <p class=\"secondary\">").Append(examples.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(Pluralize(examples.Length, "visual is", "visuals are")).AppendLine(" affected. Review whether this difference is intentional.</p>");
                AppendGovernanceExamples(html, examples.Select(value => ContextFor(contexts, value.report, value.item.PageName, value.item.VisualName)));
                html.AppendLine("          </article>");
            }
            html.AppendLine("        </div>");
        }
        html.AppendLine("      </section>");
    }

    private static void AppendThemeConsistency(StringBuilder html, ProjectInventory inventory, ThemeVisualContext[] contexts)
    {
        var observations = inventory.Reports.SelectMany(report => report.ThemeReview.ConsistencyObservations.Select(item => (report, item))).ToArray();
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"theme-consistency-heading\">");
        html.AppendLine("        <h3 id=\"theme-consistency-heading\">Consistency review</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Looks for visuals whose saved title formatting is noticeably different from similar visuals elsewhere in the report. Only strong, like-for-like patterns are shown.</p>");
        if (observations.Length == 0)
        {
            html.AppendLine("        <p class=\"theme-empty-state\">No clear formatting differences were found among comparable visuals.</p>");
        }
        else
        {
            html.AppendLine("        <div class=\"theme-governance-list\">");
            foreach (var group in observations.GroupBy(value => new
                     {
                         ReportName = value.report.Name,
                         value.item.PageDisplayName,
                         value.item.VisualType,
                         value.item.PropertyKey,
                         value.item.PropertyLabel,
                         value.item.ObservedValue,
                         value.item.DominantValue,
                         value.item.PeerCount,
                         value.item.DominantCount,
                     }))
            {
                var examples = group.ToArray();
                var first = examples[0].item;
                AppendGovernanceCardStart(html, "Consistency review", first.PageDisplayName, first.VisualType,
                    first.PropertyKey, first.PropertyLabel, $"{first.ObservedValue} {first.DominantValue}");
                html.Append("            <h4>").Append(Encode(first.PropertyLabel)).AppendLine(" differs from comparable visuals</h4>");
                var affectedVisuals = examples.Select(value => new AffectedVisual(
                    ContextFor(contexts, value.report, value.item.PageName, value.item.VisualName),
                    value.item.PageDisplayName,
                    value.item.VisualType)).ToArray();
                AppendAffectedVisuals(html, affectedVisuals);
                html.Append("            <p><strong>Most comparable visuals use:</strong> ").Append(Encode(FormattingValue(first.PropertyKey, first.DominantValue)))
                    .Append(" (" ).Append(first.DominantCount).Append(" of ").Append(first.PeerCount).AppendLine(")</p>");
                html.Append("            <p><strong>This ").Append(Pluralize(examples.Length, "visual uses", "group uses")).Append(":</strong> ")
                    .Append(Encode(FormattingValue(first.PropertyKey, first.ObservedValue))).AppendLine("</p>");
                html.AppendLine("            <p class=\"secondary\">Review whether this difference is intentional.</p>");
                AppendGovernanceExamples(html, affectedVisuals.Select(value => value.Context));
                html.AppendLine("          </article>");
            }
            html.AppendLine("        </div>");
        }
        html.AppendLine("      </section>");
    }

    private static void AppendThemeAccessibility(StringBuilder html, ProjectInventory inventory)
    {
        var observations = inventory.Reports.Sum(report => report.ThemeReview.AccessibilityObservations.Count);
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"theme-accessibility-heading\">");
        html.AppendLine("        <h3 id=\"theme-accessibility-heading\">Theme accessibility coverage</h3>");
        if (observations == 0)
            html.AppendLine("        <p class=\"theme-empty-state\">Automated theme accessibility checks are not available yet. Contrast will be reported only when PBI Assure can determine the colours used by Power BI reliably.</p>");
        html.AppendLine("      </section>");
    }

    private static void AppendGovernanceCardStart(StringBuilder html, string reviewType, string page, string? visualType,
        string propertyKey, string propertyLabel, string values)
    {
        html.Append("          <article class=\"theme-governance-card\" data-investigation-item=\"theme-governance\" data-search-text=\"")
            .Append(Encode($"{reviewType} {page} {visualType} {propertyLabel} {values}"))
            .Append("\" data-filter-page=\"").Append(Encode(page)).Append("\" data-filter-visual-type=\"").Append(Encode(visualType ?? "Unknown"))
            .Append("\" data-filter-review-type=\"").Append(Encode(reviewType)).Append("\" data-filter-property=\"").Append(Encode(propertyKey)).AppendLine("\">");
        html.Append("            <span class=\"theme-review-kind\">").Append(Encode(reviewType)).AppendLine("</span>");
    }

    private static void AppendGovernanceExamples(StringBuilder html, IEnumerable<ThemeVisualContext?> contexts)
    {
        var examples = contexts.Where(context => context is not null).Cast<ThemeVisualContext>().ToArray();
        if (examples.Length == 0) return;
        html.Append("            <details class=\"technical-details\"><summary>Show affected ").Append(Pluralize(examples.Length, "visual", "visuals")).AppendLine("</summary><ul class=\"theme-example-list\">");
        foreach (var context in examples.Take(20))
        {
            html.Append("              <li><strong>Page:</strong> ").Append(Encode(context.Page.DisplayName)).Append(" · ")
                .Append(Encode(VisualDisplayName(context.Visual))).Append(" · ").Append(Encode(HumanizeVisualType(context.Visual.VisualType))).AppendLine("</li>");
        }
        if (examples.Length > 20) html.Append("              <li>").Append((examples.Length - 20).ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" more affected visuals</li>");
        html.AppendLine("            </ul></details>");
    }

    private static void AppendAffectedVisuals(StringBuilder html, AffectedVisual[] affectedVisuals)
    {
        if (affectedVisuals.Length == 0) return;
        var duplicateCounts = affectedVisuals
            .GroupBy(AffectedVisualBaseIdentity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        if (affectedVisuals.Length == 1)
        {
            html.Append("            <p class=\"theme-affected-visual\"><strong>Affected visual:</strong> ")
                .Append(Encode(AffectedVisualIdentity(affectedVisuals[0], duplicateCounts))).AppendLine("</p>");
            return;
        }

        html.AppendLine("            <div class=\"theme-affected-visual\"><strong>Affected visuals:</strong><ul>");
        foreach (var affectedVisual in affectedVisuals)
        {
            html.Append("              <li>").Append(Encode(AffectedVisualIdentity(affectedVisual, duplicateCounts))).AppendLine("</li>");
        }
        html.AppendLine("            </ul></div>");
    }

    private static string AffectedVisualBaseIdentity(AffectedVisual affectedVisual)
    {
        var context = affectedVisual.Context;
        var visualType = HumanizeVisualType(context?.Visual.VisualType ?? affectedVisual.VisualType);
        var page = context?.Page.DisplayName ?? affectedVisual.PageDisplayName;
        var friendlyName = context is null ? null : VisualFriendlyName(context.Visual);
        return $"{visualType}\u001f{friendlyName}\u001f{page}";
    }

    private static string AffectedVisualIdentity(AffectedVisual affectedVisual, IReadOnlyDictionary<string, int> duplicateCounts)
    {
        var context = affectedVisual.Context;
        var visualType = HumanizeVisualType(context?.Visual.VisualType ?? affectedVisual.VisualType);
        var page = context?.Page.DisplayName ?? affectedVisual.PageDisplayName;
        var friendlyName = context is null ? null : VisualFriendlyName(context.Visual);
        var identity = friendlyName is null
            ? $"{visualType} · {page}"
            : $"{visualType} — {friendlyName} · {page}";
        var needsPosition = context is not null &&
            (friendlyName is null || duplicateCounts.GetValueOrDefault(AffectedVisualBaseIdentity(affectedVisual)) > 1);
        if (!needsPosition) return identity;

        var position = DescribePosition(context!.Page, context.Visual);
        return position == "Position unavailable" ? identity : $"{identity} · {position}";
    }

    private static ThemeVisualContext? ContextFor(ThemeVisualContext[] contexts, ReportInventory report, string pageName, string visualName) =>
        contexts.FirstOrDefault(context => ReferenceEquals(context.Report, report) &&
            string.Equals(context.Page.Name, pageName, StringComparison.Ordinal) &&
            string.Equals(context.Visual.Name, visualName, StringComparison.Ordinal));

    private static IEnumerable<ThemeReviewItem> ReviewItems(ProjectInventory inventory) =>
        inventory.Reports.SelectMany(report =>
            report.ThemeReview.Deviations.Select(item => new ThemeReviewItem(
                "Theme deviation", item.PageDisplayName, item.VisualType, item.PropertyKey, item.PropertyLabel))
            .Concat(report.ThemeReview.ConsistencyObservations.Select(item => new ThemeReviewItem(
                "Consistency review", item.PageDisplayName, item.VisualType, item.PropertyKey, item.PropertyLabel))));

    private static string FormattingValue(string propertyKey, string value) =>
        propertyKey.EndsWith("fontSize", StringComparison.Ordinal) ? $"{value} pt" : value;

    private static string ThemeStatusLabel(string state) => state switch
    {
        ThemeReviewStatusStates.CustomThemeAppliedOverBase => "Custom theme applied",
        ThemeReviewStatusStates.BaseThemeOnly => "Built-in theme applied",
        _ => "Theme details unavailable",
    };

    private static void AppendThemeSourceSummary(StringBuilder html, string label, ThemeSourceInventory source)
    {
        html.AppendLine("            <dl class=\"theme-source-summary\">");
        AppendDefinition(html, label, source.ThemeName ?? source.ReferenceName ?? "Name unavailable");
        AppendDefinition(html, "Theme type", source.Kind switch
        {
            ThemeSourceKinds.RegisteredCustom => "Custom theme saved with the report",
            ThemeSourceKinds.SharedBase => "Built-in Power BI base theme",
            _ => "Base theme details unavailable",
        });
        AppendDefinition(html, "Status", FriendlyAvailability(source.AvailabilityState));
        html.AppendLine("            </dl>");
        if (source.ResourcePath is null && source.ReportVersionAtImport is null) return;
        html.AppendLine("            <details class=\"technical-details\"><summary>Technical theme details</summary><dl class=\"technical-list\">");
        if (source.ResourcePath is not null) AppendDefinition(html, "Resource path", DisplayPath(source.ResourcePath));
        AppendDefinition(html, "Reference", source.ReferenceName ?? "Unavailable");
        if (source.ReportVersionAtImport is { } version)
        {
            AppendDefinition(html, "Imported visual/report/page versions",
                string.Join(" / ", new[] { version.Visual, version.Report, version.Page }.Select(value => value ?? "unknown")));
        }
        html.AppendLine("            </dl></details>");
    }

    private static void AppendThemeContentsSection(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"theme-contents-heading\">");
        html.AppendLine("        <h3 id=\"theme-contents-heading\">Theme contents</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Colours, text styles and visual settings found in the available theme files. Settings not defined by a custom theme may still come from the base theme.</p>");
        foreach (var report in inventory.Reports)
        {
            html.Append("        <div class=\"theme-content-report\"><h4>").Append(Encode(report.Name)).AppendLine("</h4><div class=\"theme-content-grid\">");
            AppendThemeContents(html, "Base theme", report.Theme.BaseSource);
            if (report.Theme.CustomSource is { } custom) AppendThemeContents(html, "Custom theme", custom);
            html.AppendLine("        </div></div>");
        }
        html.AppendLine("      </section>");
    }

    private static void AppendThemeContents(StringBuilder html, string layerLabel, ThemeSourceInventory source)
    {
        html.Append("          <article class=\"theme-content-card\"><h5>").Append(Encode(layerLabel)).AppendLine("</h5>");
        if (source.Metadata is not { } metadata)
        {
            html.Append("            <p>").Append(Encode(FriendlyAvailability(source.AvailabilityState))).AppendLine("</p></article>");
            return;
        }

        html.AppendLine("            <dl class=\"theme-content-facts\">");
        AppendDefinition(html, "Theme name", metadata.Name ?? source.ReferenceName ?? "Not supplied");
        AppendDefinition(html, "Palette", $"{metadata.DataColors.Count:N0} colours · {metadata.DistinctDataColorCount:N0} distinct");
        AppendDefinition(html, "Text classes", metadata.TextClasses.Count.ToString("N0", CultureInfo.InvariantCulture));
        AppendDefinition(html, "Visual style rules", metadata.VisualStyleRuleCount.ToString("N0", CultureInfo.InvariantCulture));
        AppendDefinition(html, "Visual types represented", metadata.VisualTypes.Count == 0
            ? "None directly listed"
            : string.Join(", ", metadata.VisualTypes.Select(HumanizeVisualType)));
        html.AppendLine("            </dl>");
        if (metadata.DataColors.Count > 0)
        {
            var preview = metadata.DataColors.Where(IsSafeSwatchColor).Take(24).ToArray();
            html.Append("            <ul class=\"theme-palette\" aria-label=\"Palette preview: ").Append(preview.Length).Append(" of ").Append(metadata.DataColors.Count).AppendLine(" colours\">");
            foreach (var color in preview)
            {
                html.Append("              <li class=\"theme-swatch\" style=\"--swatch:").Append(Encode(color)).Append("\" title=\"").Append(Encode(color)).Append("\"><span class=\"visually-hidden\">Palette colour ").Append(Encode(color)).AppendLine("</span></li>");
            }
            html.AppendLine("            </ul>");
            if (metadata.DataColors.Count > preview.Length) html.Append("            <p class=\"secondary\">Showing ").Append(preview.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(" swatches from ").Append(metadata.DataColors.Count.ToString("N0", CultureInfo.InvariantCulture)).AppendLine(" palette colours.</p>");
        }
        AppendThemeMetadataLists(html, metadata);
        html.AppendLine("          </article>");
    }

    private static void AppendThemeMetadataLists(StringBuilder html, ThemeMetadataInventory metadata)
    {
        if (metadata.TextClasses.Count > 0)
        {
            html.AppendLine("            <div class=\"theme-metadata-grid\"><h6>Text classes</h6><dl>");
            foreach (var textClass in metadata.TextClasses)
            {
                var parts = new[] { textClass.FontFamily, textClass.FontSize is null ? null : $"{textClass.FontSize:0.##} pt" }
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                AppendThemeColourDefinition(html, HumanizeIdentifier(textClass.Name), string.Join(" · ", parts), textClass.Color);
            }
            html.AppendLine("            </dl></div>");
        }
        if (metadata.NamedColors.Count > 0)
        {
            html.AppendLine("            <div class=\"theme-metadata-grid\"><h6>Named colours</h6><dl>");
            foreach (var color in metadata.NamedColors) AppendThemeColourDefinition(html, HumanizeIdentifier(color.Name), null, color.Value);
            html.AppendLine("            </dl></div>");
        }
    }

    private static void AppendThemeColourDefinition(StringBuilder html, string term, string? prefix, string? color)
    {
        html.Append("              <div><dt>").Append(Encode(term)).Append("</dt><dd>");
        if (!string.IsNullOrWhiteSpace(prefix)) html.Append(Encode(prefix)).Append(" · ");
        if (IsSafeSwatchColor(color ?? string.Empty))
        {
            html.Append("<span class=\"theme-colour-value\"><span class=\"theme-colour-chip\" style=\"--colour-chip:")
                .Append(Encode(color!)).Append("\" aria-hidden=\"true\"></span>").Append(Encode(color!)).Append("</span>");
        }
        else html.Append(Encode(color ?? "Not supplied"));
        html.AppendLine("</dd></div>");
    }

    private static void AppendPersistedFormatting(
        StringBuilder html,
        ThemeVisualContext[] contexts,
        PersistedFormattingObservation[] all,
        PersistedFormattingObservation[] headline)
    {
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"persisted-formatting-heading\">");
        html.AppendLine("        <h3 id=\"persisted-formatting-heading\">Formatting details</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Formatting values PBI Assure could read from visuals. Counts represent visual and property combinations, not visuals. When no value is saved in a visual, Power BI or the theme may still provide it.</p>");
        html.AppendLine("        <dl class=\"metrics theme-metrics\">");
        AppendMetric(html, "Formatting values reviewed", headline.Length);
        AppendMetric(html, "No value saved in visual", Count(headline, PersistedFormattingClassifications.NoPersistedValue));
        AppendMetric(html, "Saved values", Count(headline, PersistedFormattingClassifications.PersistedLiteral));
        AppendMetric(html, "Colours linked to the theme", Count(headline, PersistedFormattingClassifications.ThemeReference));
        AppendMetric(html, "Dynamic or conditional values", Count(headline, PersistedFormattingClassifications.DynamicExpression));
        AppendMetric(html, "Specific series/category values", headline.Count(item => item.IsSelectorScoped));
        AppendMetric(html, "Could not interpret confidently", headline.Count(item => item.Classification == PersistedFormattingClassifications.Unsupported || item.IsAmbiguous));
        html.AppendLine("        </dl>");
        var staleCount = all.Count(item => !item.IncludeInHeadline);
        if (staleCount > 0)
        {
            html.Append("        <p class=\"secondary\">").Append(staleCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" saved formatting ").Append(Pluralize(staleCount, "entry was", "entries were"))
                .AppendLine(" linked to items no longer used by the visual. These are excluded from the summary and retained in technical details.</p>");
        }

        var details = contexts.Where(context => DisplayedFormattingValues(context).Any()).ToArray();
        if (details.Length == 0)
        {
            html.AppendLine("        <p>No supported saved formatting values were found. Properties without a locally saved value are included in the summary above.</p>");
            html.AppendLine("      </section>");
            return;
        }

        AppendThemeFilters(html, details);
        html.AppendLine("        <div class=\"theme-visual-list\">");
        foreach (var context in details) AppendThemeVisual(html, context);
        html.AppendLine("        </div>");
        html.AppendLine("      </section>");
    }

    private static void AppendThemeFilters(StringBuilder html, ThemeVisualContext[] details)
    {
        AppendInvestigationStart(html, "theme", "Search saved formatting", "Search page, visual, property or value");
        AppendInvestigationFacet(html, "theme", "page", "Page", "All pages", details
            .Select(context => new FindingFacetOption(context.Page.DisplayName, context.Page.DisplayName)).Distinct());
        AppendInvestigationFacet(html, "theme", "visual-type", "Visual type", "All visual types", details
            .Select(context => new FindingFacetOption(context.Visual.VisualType ?? "Unknown", HumanizeVisualType(context.Visual.VisualType))).Distinct());
        AppendInvestigationFacet(html, "theme", "classification", "Saved value type", "All saved value types", details
            .SelectMany(DisplayedFormattingValues).Select(item => new FindingFacetOption(item.Classification, FormattingClassificationLabel(item.Classification))).Distinct());
        AppendInvestigationFacet(html, "theme", "scope", "Where formatting applies", "All formatting locations", details
            .SelectMany(DisplayedFormattingValues).Select(item => new FindingFacetOption(
                item.IsSelectorScoped ? "Scoped" : "VisualWide",
                item.IsSelectorScoped ? "Specific series or category" : "Whole visual")).Distinct());
        AppendInvestigationFacet(html, "theme", "property", "Property", "All properties", details
            .SelectMany(DisplayedFormattingValues).Select(item => new FindingFacetOption(item.PropertyKey, item.PropertyLabel)).Distinct());
        var comparisonOptions = details.SelectMany(DisplayedFormattingValues).Where(item => item.ThemeComparison is not null)
            .Select(item => new FindingFacetOption(item.ThemeComparison!.State, ThemeComparisonLabel(item.ThemeComparison.State))).Distinct().ToArray();
        if (comparisonOptions.Length > 0)
            AppendInvestigationFacet(html, "theme", "comparison", "Comparison state", "All comparison states", comparisonOptions);
        AppendInvestigationEnd(html, "theme", details.Length, "visual", "visuals");
    }

    private static void AppendThemeVisual(StringBuilder html, ThemeVisualContext context)
    {
        var observations = DisplayedFormattingValues(context).ToArray();
        var classifications = string.Join('\u001f', observations.Select(item => item.Classification).Distinct(StringComparer.Ordinal));
        var scopes = string.Join('\u001f', observations.Select(item => item.IsSelectorScoped ? "Scoped" : "VisualWide").Distinct(StringComparer.Ordinal));
        var properties = string.Join('\u001f', observations.Select(item => item.PropertyKey).Distinct(StringComparer.Ordinal));
        var comparisons = string.Join('\u001f', observations.Where(item => item.ThemeComparison is not null).Select(item => item.ThemeComparison!.State).Distinct(StringComparer.Ordinal));
        var search = string.Join(' ', context.Report.Name, context.Page.DisplayName, VisualDisplayName(context.Visual), context.Visual.VisualType,
            string.Join(' ', observations.Select(item => $"{item.PropertyLabel} {item.NormalizedValue} {item.SelectorScope} {item.ExpressionSource} {item.ThemeComparison?.State} {item.ThemeComparison?.ThemeRuleValue}")));
        html.Append("          <details class=\"theme-visual-card\" data-investigation-item=\"theme\" data-search-text=\"").Append(Encode(search))
            .Append("\" data-filter-page=\"").Append(Encode(context.Page.DisplayName)).Append("\" data-filter-visual-type=\"").Append(Encode(context.Visual.VisualType ?? "Unknown"))
            .Append("\" data-filter-classification=\"").Append(Encode(classifications)).Append("\" data-filter-scope=\"").Append(Encode(scopes)).Append("\" data-filter-property=\"").Append(Encode(properties)).Append("\" data-filter-comparison=\"").Append(Encode(comparisons)).AppendLine("\">");
        html.Append("            <summary><span class=\"summary-copy\"><strong>").Append(Encode(VisualDisplayName(context.Visual))).Append("</strong><span><strong>Page:</strong> ")
            .Append(Encode(context.Page.DisplayName)).Append(" · ").Append(Encode(HumanizeVisualType(context.Visual.VisualType))).Append(" · ")
            .Append(observations.Length).Append(' ').Append(Pluralize(observations.Length, "formatting property", "formatting properties")).AppendLine("</span></span></summary>");
        html.AppendLine("            <div class=\"theme-visual-body\"><div class=\"theme-observation-list\">");
        foreach (var observation in observations) AppendThemeObservation(html, context, observation);
        html.AppendLine("            </div></div></details>");
    }

    private static void AppendThemeObservation(StringBuilder html, ThemeVisualContext context, PersistedFormattingObservation observation)
    {
        html.Append("              <article class=\"theme-observation\"><div class=\"semantic-object-header\"><span class=\"object-name\"><strong>")
            .Append(Encode(observation.PropertyLabel)).Append("</strong><span>").Append(Encode(FormattingClassificationLabel(observation))).AppendLine("</span></span></div>");
        if (observation.ThemeComparison is { } comparison) AppendThemeComparison(html, comparison);
        else if (!string.IsNullOrWhiteSpace(observation.NormalizedValue)) html.Append("                <p><strong>Saved evidence:</strong> ").Append(Encode(observation.NormalizedValue)).AppendLine("</p>");
        if (observation.IsSelectorScoped) html.Append("                <p><strong>Applies to:</strong> ").Append(Encode(observation.SelectorScope ?? "A specific series or category")).AppendLine("</p>");
        if (!string.IsNullOrWhiteSpace(observation.ExpressionSource)) html.Append("                <p><strong>Value comes from:</strong> ").Append(Encode(observation.ExpressionSource)).AppendLine("</p>");
        if (observation.IsAmbiguous) html.AppendLine("                <p class=\"secondary\">PBI Assure could not determine exactly which series or category this setting applies to, so it is shown for review only.</p>");
        if (!observation.IncludeInHeadline) html.AppendLine("                <p class=\"secondary\">This setting appears to belong to an item no longer used by the visual, so it is excluded from the summary.</p>");
        html.AppendLine("                <details class=\"technical-details\"><summary>Technical details</summary><dl class=\"technical-list\">");
        AppendDefinition(html, "Visual definition", DisplayPath(context.Visual.RelativePath));
        AppendDefinition(html, "Evidence path", observation.EvidencePath);
        AppendDefinition(html, "Expression kind", observation.ExpressionKind ?? "None");
        AppendDefinition(html, "Selector kind", observation.SelectorKind ?? "None");
        AppendDefinition(html, "Selector relevance", observation.SelectorRelevance ?? "Not applicable");
        if (!string.IsNullOrWhiteSpace(observation.RawValue)) AppendDefinition(html, "Raw saved value", observation.RawValue);
        if (observation.ThemeComparison?.ThemeRuleEvidencePath is { } rulePath) AppendDefinition(html, "Theme rule evidence", rulePath);
        if (observation.ThemeComparison?.ThemeSourcePath is { } sourcePath) AppendDefinition(html, "Theme source", DisplayPath(sourcePath));
        html.AppendLine("                </dl></details></article>");
    }

    private static void AppendThemeComparison(StringBuilder html, ThemeFormattingComparison comparison)
    {
        html.Append("                <p class=\"theme-comparison-state\"><strong>").Append(Encode(ThemeComparisonLabel(comparison.State))).AppendLine("</strong></p>");
        if (comparison.SavedValue is not null) html.Append("                <p><strong>Saved value:</strong> ").Append(Encode(comparison.SavedValue)).AppendLine(" pt</p>");
        if (comparison.ThemeRuleValue is not null)
        {
            html.Append("                <p><strong>Theme setting:</strong> ").Append(Encode(comparison.ThemeRuleValue)).AppendLine(" pt</p>");
        }
    }

    private static IEnumerable<PersistedFormattingObservation> DisplayedFormattingValues(ThemeVisualContext context) =>
        context.Visual.PersistedFormatting.Where(item =>
            item.Classification != PersistedFormattingClassifications.NoPersistedValue || item.ThemeComparison is not null);

    private static int Count(IEnumerable<PersistedFormattingObservation> values, string classification) =>
        values.Count(item => item.Classification == classification);

    private static string FormattingClassificationLabel(PersistedFormattingObservation observation)
    {
        var label = FormattingClassificationLabel(observation.Classification);
        return observation.IsSelectorScoped && observation.Classification is not PersistedFormattingClassifications.NoPersistedValue and not PersistedFormattingClassifications.Unsupported
            ? $"{label} · specific series or category"
            : label;
    }

    private static string FormattingClassificationLabel(string classification) => classification switch
    {
        PersistedFormattingClassifications.NoPersistedValue => "No value saved in visual",
        PersistedFormattingClassifications.PersistedLiteral => "Saved value",
        PersistedFormattingClassifications.ThemeReference => "Colour linked to the theme",
        PersistedFormattingClassifications.DynamicExpression => "Dynamic or conditional value",
        _ => "Could not be interpreted confidently",
    };

    private static string ThemeComparisonLabel(string state) => state switch
    {
        ThemeFormattingComparisonStates.NoSavedLocalValue => "No formatting value saved in the visual",
        ThemeFormattingComparisonStates.SavedValueMatchesTheme => "Saved value matches the theme",
        ThemeFormattingComparisonStates.SavedValueDiffersFromTheme => "Saved value differs from the theme",
        ThemeFormattingComparisonStates.ThemeCandidateUnavailable => "No comparable theme setting found",
        ThemeFormattingComparisonStates.ComparisonAmbiguous => "Could not compare confidently",
        _ => "Not available for automatic comparison",
    };

    private static string FriendlyAvailability(string state) => state switch
    {
        ThemeAvailabilityStates.Available => "Available",
        ThemeAvailabilityStates.ReferencedButUnavailable => "Theme file unavailable",
        ThemeAvailabilityStates.Malformed => "Theme file could not be read",
        _ => "Theme details unavailable",
    };

    private static bool IsSafeSwatchColor(string value) =>
        value.Length is 4 or 7 or 9 && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private sealed record ThemeReviewItem(
        string ReviewType,
        string PageDisplayName,
        string? VisualType,
        string PropertyKey,
        string PropertyLabel);

    private sealed record ThemeVisualContext(ReportInventory Report, PageInventory Page, VisualInventory Visual);
    private sealed record AffectedVisual(ThemeVisualContext? Context, string PageDisplayName, string? VisualType);
}
