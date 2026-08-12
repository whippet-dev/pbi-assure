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
        html.AppendLine("      <p class=\"section-intro\">See which theme resources are active, what metadata they contain and which supported formatting values are saved in the report.</p>");
        html.AppendLine("      <div class=\"theme-early-access\" role=\"note\"><strong>Early access</strong><p>Theme Review is under active development. It currently inventories theme resources and saved formatting, and compares only one fixture-validated property mapping: clustered column chart title font size. Other theme properties may be shown as evidence but are not yet compared.</p></div>");
        html.AppendLine("      <div class=\"theme-boundary\"><strong>What this review means</strong><p>Theme Review compares only a small set of explicitly supported saved formatting properties against supported active-theme rules. It does not reproduce Power BI’s full formatting engine, infer authoring intent, determine final rendered formatting or assess accessibility compliance.</p></div>");
        AppendThemeSummary(html, inventory);
        AppendThemeContentsSection(html, inventory);
        AppendPersistedFormatting(html, contexts, all, headline);
        html.AppendLine("    </section>");
    }

    private static void AppendThemeSummary(StringBuilder html, ProjectInventory inventory)
    {
        html.AppendLine("      <section class=\"theme-review-group\" aria-labelledby=\"theme-summary-heading\">");
        html.AppendLine("        <h3 id=\"theme-summary-heading\">Theme summary</h3>");
        html.AppendLine("        <p class=\"group-explanation\">The base and custom theme layers selected by each report. A custom theme, when present, is shown separately from the base theme.</p>");
        html.AppendLine("        <div class=\"theme-report-list\">");
        foreach (var report in inventory.Reports)
        {
            html.Append("          <article class=\"theme-report-card\"><h4>").Append(Encode(report.Name)).AppendLine("</h4>");
            html.Append("            <p class=\"theme-state\">").Append(Encode(report.Theme.ActiveState)).AppendLine("</p>");
            AppendThemeSourceSummary(html, "Base theme", report.Theme.BaseSource);
            if (report.Theme.CustomSource is { } custom) AppendThemeSourceSummary(html, "Active custom theme", custom);
            if (report.Theme.ResolutionIssues.Count > 0)
            {
                html.AppendLine("            <div class=\"theme-resolution-issues\"><strong>Resource resolution needs attention</strong><ul>");
                foreach (var issue in report.Theme.ResolutionIssues) html.Append("              <li>").Append(Encode(issue)).AppendLine("</li>");
                html.AppendLine("            </ul></div>");
            }
            html.AppendLine("          </article>");
        }
        if (inventory.Reports.Count == 0) html.AppendLine("          <p>No report definition was found.</p>");
        html.AppendLine("        </div>");
        html.AppendLine("      </section>");
    }

    private static void AppendThemeSourceSummary(StringBuilder html, string label, ThemeSourceInventory source)
    {
        html.AppendLine("            <dl class=\"theme-source-summary\">");
        AppendDefinition(html, label, source.ThemeName ?? source.ReferenceName ?? "Metadata unavailable");
        AppendDefinition(html, "Source", source.Kind switch
        {
            ThemeSourceKinds.RegisteredCustom => "Registered report resource",
            ThemeSourceKinds.SharedBase => "Shared base resource",
            _ => "Base theme metadata unavailable",
        });
        AppendDefinition(html, "Resource", FriendlyAvailability(source.AvailabilityState));
        html.AppendLine("            </dl>");
        if (source.ResourcePath is null && source.ReportVersionAtImport is null) return;
        html.AppendLine("            <details class=\"technical-details\"><summary>Technical theme details</summary><dl class=\"technical-list\">");
        if (source.ResourcePath is not null) AppendDefinition(html, "Resource path", source.ResourcePath);
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
        html.AppendLine("        <p class=\"group-explanation\">A compact view of metadata directly present in resolved theme files. Missing custom values may be supplied by the base theme, but that merge is not assessed here.</p>");
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
        html.AppendLine("        <h3 id=\"persisted-formatting-heading\">Persisted formatting</h3>");
        html.AppendLine("        <p class=\"group-explanation\">Theme Review currently checks four supported formatting property paths. The eligible count is across applicable visual/property combinations, so it is not a visual count and not every property applies to every visual. No saved local value is a storage state only; it does not prove what Power BI finally renders.</p>");
        html.AppendLine("        <dl class=\"metrics theme-metrics\">");
        AppendMetric(html, "Eligible supported properties", headline.Length);
        AppendMetric(html, "No saved local value", Count(headline, PersistedFormattingClassifications.NoPersistedValue));
        AppendMetric(html, "Persisted literals", Count(headline, PersistedFormattingClassifications.PersistedLiteral));
        AppendMetric(html, "Theme-linked references", Count(headline, PersistedFormattingClassifications.ThemeReference));
        AppendMetric(html, "Dynamic values", Count(headline, PersistedFormattingClassifications.DynamicExpression));
        AppendMetric(html, "Selector-scoped values", headline.Count(item => item.IsSelectorScoped));
        AppendMetric(html, "Unsupported or ambiguous", headline.Count(item => item.Classification == PersistedFormattingClassifications.Unsupported || item.IsAmbiguous));
        html.AppendLine("        </dl>");
        var staleCount = all.Count(item => !item.IncludeInHeadline);
        if (staleCount > 0)
        {
            html.Append("        <p class=\"secondary\">").Append(staleCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" high-confidence stale selector ").Append(Pluralize(staleCount, "observation was", "observations were"))
                .AppendLine(" excluded from these headline counts and retained in technical detail.</p>");
        }

        var details = contexts.Where(context => DisplayedFormattingValues(context).Any()).ToArray();
        if (details.Length == 0)
        {
            html.AppendLine("        <p>No supported persisted values were found. Supported properties without a saved value are included in the aggregate above.</p>");
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
        AppendInvestigationStart(html, "theme", "Search persisted formatting", "Search page, visual, property or value");
        AppendInvestigationFacet(html, "theme", "page", "Page", "All pages", details
            .Select(context => new FindingFacetOption(context.Page.DisplayName, context.Page.DisplayName)).Distinct());
        AppendInvestigationFacet(html, "theme", "visual-type", "Visual type", "All visual types", details
            .Select(context => new FindingFacetOption(context.Visual.VisualType ?? "Unknown", HumanizeVisualType(context.Visual.VisualType))).Distinct());
        AppendInvestigationFacet(html, "theme", "classification", "Classification", "All classifications", details
            .SelectMany(DisplayedFormattingValues).Select(item => new FindingFacetOption(item.Classification, FormattingClassificationLabel(item.Classification))).Distinct());
        AppendInvestigationFacet(html, "theme", "scope", "Scope", "All scopes", details
            .SelectMany(DisplayedFormattingValues).Select(item => new FindingFacetOption(
                item.IsSelectorScoped ? "Scoped" : "VisualWide",
                item.IsSelectorScoped ? "Scoped to series/category" : "Not selector-scoped")).Distinct());
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
        if (observation.IsSelectorScoped) html.Append("                <p><strong>Scope:</strong> ").Append(Encode(observation.SelectorScope ?? observation.SelectorKind ?? "Selector-scoped")).AppendLine("</p>");
        if (!string.IsNullOrWhiteSpace(observation.ExpressionSource)) html.Append("                <p><strong>Expression source:</strong> ").Append(Encode(observation.ExpressionSource)).AppendLine("</p>");
        if (observation.IsAmbiguous) html.AppendLine("                <p class=\"secondary\">Selector mapping is ambiguous; this evidence is shown conservatively.</p>");
        if (!observation.IncludeInHeadline) html.AppendLine("                <p class=\"secondary\">High-confidence stale selector evidence; excluded from headline counts.</p>");
        html.AppendLine("                <details class=\"technical-details\"><summary>Technical details</summary><dl class=\"technical-list\">");
        AppendDefinition(html, "Visual definition", context.Visual.RelativePath);
        AppendDefinition(html, "Evidence path", observation.EvidencePath);
        AppendDefinition(html, "Expression kind", observation.ExpressionKind ?? "None");
        AppendDefinition(html, "Selector kind", observation.SelectorKind ?? "None");
        AppendDefinition(html, "Selector relevance", observation.SelectorRelevance ?? "Not applicable");
        if (!string.IsNullOrWhiteSpace(observation.RawValue)) AppendDefinition(html, "Raw saved value", observation.RawValue);
        if (observation.ThemeComparison?.ThemeRuleEvidencePath is { } rulePath) AppendDefinition(html, "Theme rule evidence", rulePath);
        if (observation.ThemeComparison?.ThemeSourcePath is { } sourcePath) AppendDefinition(html, "Theme source", sourcePath);
        html.AppendLine("                </dl></details></article>");
    }

    private static void AppendThemeComparison(StringBuilder html, ThemeFormattingComparison comparison)
    {
        html.Append("                <p class=\"theme-comparison-state\"><strong>").Append(Encode(ThemeComparisonLabel(comparison.State))).AppendLine("</strong></p>");
        if (comparison.SavedValue is not null) html.Append("                <p><strong>Saved value:</strong> ").Append(Encode(comparison.SavedValue)).AppendLine(" pt</p>");
        if (comparison.ThemeRuleValue is not null)
        {
            var label = comparison.State == ThemeFormattingComparisonStates.NoSavedLocalValue
                ? "Supported active-theme rule"
                : "Theme rule";
            html.Append("                <p><strong>").Append(label).Append(":</strong> ").Append(Encode(comparison.ThemeRuleValue)).AppendLine(" pt</p>");
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
            ? $"{label} · scoped to series/category"
            : label;
    }

    private static string FormattingClassificationLabel(string classification) => classification switch
    {
        PersistedFormattingClassifications.NoPersistedValue => "No saved local value",
        PersistedFormattingClassifications.PersistedLiteral => "Persisted literal value",
        PersistedFormattingClassifications.ThemeReference => "Theme-linked colour reference",
        PersistedFormattingClassifications.DynamicExpression => "Dynamic or conditional value",
        _ => "Unsupported or ambiguous mapping",
    };

    private static string ThemeComparisonLabel(string state) => state switch
    {
        ThemeFormattingComparisonStates.NoSavedLocalValue => "No saved local value",
        ThemeFormattingComparisonStates.SavedValueMatchesTheme => "Saved value matches supported active-theme rule",
        ThemeFormattingComparisonStates.SavedValueDiffersFromTheme => "Saved value differs from supported active-theme rule",
        ThemeFormattingComparisonStates.ThemeCandidateUnavailable => "Supported theme rule unavailable",
        ThemeFormattingComparisonStates.ComparisonAmbiguous => "Comparison ambiguous",
        _ => "Unsupported for comparison",
    };

    private static string FriendlyAvailability(string state) => state switch
    {
        ThemeAvailabilityStates.Available => "Theme resource resolved",
        ThemeAvailabilityStates.ReferencedButUnavailable => "Referenced theme resource unavailable",
        ThemeAvailabilityStates.Malformed => "Theme resource could not be parsed",
        _ => "Base theme metadata unavailable",
    };

    private static bool IsSafeSwatchColor(string value) =>
        value.Length is 4 or 7 or 9 && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private sealed record ThemeVisualContext(ReportInventory Report, PageInventory Page, VisualInventory Visual);
}
