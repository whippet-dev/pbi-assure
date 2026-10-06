using System.Globalization;
using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

/// <summary>
/// Object-focused lineage cards. Each card is ordinary semantic HTML: the focus, with the card's
/// heading; then one titled list per relationship group, in a container for each side of the focus;
/// then the path to report and the supporting context. That text is the lineage view, not a fallback
/// for one. On a wide card the stylesheet arranges the same elements as a diagram — what the focus
/// depends on to its left, what uses it to its right — and draws the connectors as decoration. No
/// second representation of the relationships is emitted.
/// </summary>
public static partial class HtmlReportRenderer
{
    internal const string LineageScopeNote =
        "Lineage shows the dependencies PBI Assure found in this project's files. Some usage can't be checked, and anything outside this project isn't visible.";

    /// <summary>A path with more steps than this, the report location included, shows its middle collapsed.</summary>
    internal const int LineagePathStepLimit = 10;

    /// <summary>How many steps a collapsed path keeps after the focus.</summary>
    internal const int LineagePathHeadSteps = 3;

    /// <summary>How many model steps a collapsed path keeps before the report location.</summary>
    internal const int LineagePathTailSteps = 2;

    private static void AppendLineage(
        StringBuilder html,
        ProjectInventory inventory,
        SemanticLineageProjection lineage,
        AnalysisCoverage coverage)
    {
        html.AppendLine("    <section id=\"lineage\" class=\"report-section lineage-section\" data-report-section=\"lineage\" aria-labelledby=\"lineage-heading\">");
        html.AppendLine("      <div class=\"lineage-index\" data-lineage-index>");
        html.AppendLine("        <h2 id=\"lineage-heading\" tabindex=\"-1\">Lineage</h2>");
        html.AppendLine("        <p class=\"section-intro\">Lineage shows what a model object depends on, what uses it and where the report uses it. Open an object in Semantic model and choose its Lineage view, or follow an object listed on a visual in Report pages.</p>");
        html.AppendLine("        <p><a href=\"#semantic-usage\">Go to Semantic model</a></p>");
        html.AppendLine("      </div>");
        foreach (var card in lineage.Cards)
        {
            AppendLineageCard(html, inventory, card, coverage);
        }

        // One scope note for the whole view, below whichever card is shown.
        AppendTableContexts(html, inventory, coverage);
        html.Append("      <p class=\"lineage-scope\">").Append(Encode(LineageScopeNote)).AppendLine("</p>");
        html.AppendLine("    </section>");
    }

    private static void AppendLineageCard(
        StringBuilder html,
        ProjectInventory inventory,
        LineageCard card,
        AnalysisCoverage coverage)
    {
        var titleId = card.Id + "-title";
        html.Append("    <article id=\"").Append(Encode(card.Id)).Append("\" class=\"lineage-card\" data-lineage-card=\"")
            .Append(card.Kind switch
            {
                LineageFocusKind.Function => "function",
                LineageFocusKind.ReportMeasure => "report-measure",
                LineageFocusKind.Visual => "visual",
                _ => "semantic",
            })
            .Append("\" aria-labelledby=\"").Append(Encode(titleId)).AppendLine("\">");
        var objectContext = card.Kind != LineageFocusKind.Visual;
        if (objectContext) AppendObjectContextStart(html, inventory, card, coverage);
        html.AppendLine("<div class=\"lineage-diagram\">");
        html.AppendLine("<header class=\"lineage-focus\">");
        if (objectContext)
        {
            html.AppendLine("<p class=\"kicker\">Selected object</p><p class=\"lineage-focus-reference\">Dependencies into this object · consumers out</p>");
        }
        else
        {
            html.Append("<p class=\"kicker\">Lineage</p>");
            html.Append("<h2 id=\"").Append(Encode(titleId)).Append("\" class=\"lineage-title\" tabindex=\"-1\">")
                .Append(Encode(LineageTitle(card))).AppendLine("</h2>");
            AppendLineageFacts(html, inventory, card);
            AppendLineageStatus(html, card, coverage);
            if (!string.IsNullOrWhiteSpace(card.Reason))
            {
                html.Append("<p class=\"lineage-why\">Why: ").Append(Encode(card.Reason)).AppendLine("</p>");
            }

            AppendLineageActions(html, card);
        }
        html.AppendLine("</header>");
        html.AppendLine("<div class=\"lineage-side\" data-lineage-side=\"upstream\">");
        if (card.Kind == LineageFocusKind.Visual)
        {
            AppendLineageGroup(html, "uses", "Uses", card.Uses, "None found",
                use => AppendLineageVisualUse(html, use));
            AppendLineageGroup(html, "not-resolved", "Not resolved", card.UnresolvedReportReferences, emptyText: null,
                reference => AppendLineageReportReferenceNote(html, reference), unresolved: true);
            html.AppendLine("</div>");
        }
        else
        {
            AppendLineageGroup(html, "depends-on", "Depends on", card.DependsOn, "None found",
                neighbour => AppendLineageNeighbour(html, neighbour));
            AppendLineageGroup(html, "not-resolved", "Not resolved", card.NotResolved, emptyText: null,
                reference => AppendLineageUnresolved(html, reference), unresolved: true);
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"lineage-side\" data-lineage-side=\"downstream\">");
            AppendLineageGroup(html, "used-by", "Used by", card.UsedBy, "None found",
                neighbour => AppendLineageNeighbour(html, neighbour));
            if (card.Kind is LineageFocusKind.SemanticObject or LineageFocusKind.ReportMeasure)
            {
                AppendLineageReportUse(html, inventory, card.UsedInReport);
            }

            AppendLineageGroup(html, "required", "Required by model", card.RequiredByModel, emptyText: null,
                neighbour => AppendLineageNeighbour(html, neighbour));
            AppendLineageGroup(html, "possible", "Possible use", card.PossibleUse, emptyText: null,
                possible => AppendLineagePossibleUse(html, possible), unresolved: true);
            html.AppendLine("</div>");
        }

        html.AppendLine("</div>");
        if (card.Path is not null)
        {
            AppendLineagePath(html, inventory, card.Path);
        }

        if (objectContext)
        {
            html.AppendLine("</section>");
            AppendObjectDefinition(html, inventory, card);
            AppendObjectDetails(html, inventory, card, coverage);
        }
        else if (card.PowerQuery is not null)
        {
            AppendLineagePowerQuery(html, inventory, card);
        }

        if (!objectContext) AppendLineageEvidence(html, card);
        html.AppendLine("</article>");
    }

    private static string LineageTitle(LineageCard card) =>
        card.Kind == LineageFocusKind.Visual && card.Visual is not null
            ? VisualDisplayName(card.Visual)
            : card.Title;

    private static void AppendLineageFacts(StringBuilder html, ProjectInventory inventory, LineageCard card)
    {
        var facts = new List<string>();
        if (card.Kind == LineageFocusKind.Visual)
        {
            facts.Add(HumanizeVisualType(card.Visual?.VisualType));
            if (card.Page is not null)
            {
                facts.Add($"Page {card.Page.DisplayName}");
                if (card.Visual is not null && VisualFriendlyName(card.Visual) is null &&
                    DescribePosition(card.Page, card.Visual) is var position && position != "Position unavailable")
                {
                    facts.Add(position);
                }
            }

            if (card.Report is not null)
            {
                facts.Add($"Report {card.Report.Name}");
            }
        }
        else
        {
            facts.Add(SemanticLineageProjection.ObjectTypeLabel(card.ObjectType));
            if (!string.IsNullOrWhiteSpace(card.Table))
            {
                facts.Add($"Table {card.Table}");
            }

            if (card.ReportName is not null)
            {
                facts.Add($"Report {card.ReportName}");
            }

            if (inventory.SemanticModels.Count > 1 && card.SemanticModel is not null)
            {
                facts.Add($"Model {card.SemanticModel}");
            }
        }

        html.Append("<p class=\"lineage-facts\">").Append(Encode(string.Join(" · ", facts))).AppendLine("</p>");
    }

    private static void AppendLineageStatus(StringBuilder html, LineageCard card, AnalysisCoverage coverage)
    {
        if (card.Usage is { } usage)
        {
            html.Append("<p class=\"lineage-status\"><span class=\"badge ").Append(UsageClass(usage.UsageState)).Append("\">")
                .Append(Encode(UsageLabel(usage.UsageState))).Append("</span>");
            var coverageAnchor = coverage.Models
                .FirstOrDefault(item => string.Equals(item.ModelName, usage.SemanticModel, StringComparison.OrdinalIgnoreCase))
                ?.AnchorId;
            AppendClassificationConfidence(html, usage, coverageAnchor);
            html.AppendLine("</p>");
            return;
        }

        // Functions and report measures have no usage state of their own; the scanner's reachability for
        // the node is the fact, and a report measure's node is its own report's. Where none was published
        // nothing is claimed.
        if (card.Kind is LineageFocusKind.Function or LineageFocusKind.ReportMeasure &&
            card.ReachedFromReport is { } reached)
        {
            html.Append("<p class=\"lineage-status\"><span class=\"lineage-reach\">")
                .Append(reached ? "Reached from a report" : "Not reached from a report")
                .AppendLine("</span></p>");
        }
    }

    private static void AppendLineageActions(StringBuilder html, LineageCard card)
    {
        html.Append("<p class=\"lineage-actions\">");
        if (card.Kind is LineageFocusKind.Visual or LineageFocusKind.ReportMeasure)
        {
            if (card.DetailsAnchor is not null)
            {
                html.Append("<a href=\"#").Append(Encode(card.DetailsAnchor)).Append("\">")
                    .Append(card.Kind == LineageFocusKind.Visual ? "Visual details" : "Object details").Append("</a>");
            }

            html.AppendLine("<a href=\"#reports\">Open Report pages</a></p>");
            return;
        }

        if (card.DetailsAnchor is not null)
        {
            html.Append("<a href=\"#").Append(Encode(card.DetailsAnchor)).Append("\">Object details</a>");
        }

        html.AppendLine("<a href=\"#semantic-usage\">Open Semantic model</a></p>");
    }

    /// <summary>
    /// One relationship group: its heading and count, the first <see cref="SemanticLineageProjection.PreviewLimit"/>
    /// items, and the rest of the listed items behind a "+N more" disclosure. An empty group that is
    /// worth stating is one quiet line; any other empty group is left out.
    /// </summary>
    private static void AppendLineageGroup<T>(
        StringBuilder html,
        string group,
        string heading,
        LineageGroup<T> items,
        string? emptyText,
        Action<T> appendItem,
        bool unresolved = false)
    {
        if (items.TotalCount == 0)
        {
            if (emptyText is not null)
            {
                html.Append("<section class=\"lineage-group\" data-lineage-group=\"").Append(group).Append("\"><h3>")
                    .Append(Encode(heading)).Append("</h3><p class=\"lineage-empty\">").Append(Encode(emptyText)).AppendLine("</p></section>");
            }

            return;
        }

        var itemClass = unresolved ? "lineage-item lineage-item-unresolved" : "lineage-item";
        html.Append("<section class=\"lineage-group\" data-lineage-group=\"").Append(group).Append("\"><h3>").Append(Encode(heading)).Append(" (")
            .Append(items.TotalCount.ToString(CultureInfo.InvariantCulture)).AppendLine(")</h3>");
        var shown = Math.Min(items.Items.Count, SemanticLineageProjection.PreviewLimit);
        AppendLineageItems(html, items.Items, 0, shown, appendItem, itemClass);
        AppendLineageOverflow(html, heading, items, shown, appendItem, itemClass);
        html.AppendLine("</section>");
    }

    private static void AppendLineageItems<T>(
        StringBuilder html,
        IReadOnlyList<T> items,
        int start,
        int end,
        Action<T> appendItem,
        string itemClass)
    {
        html.AppendLine("<ul class=\"lineage-list\">");
        for (var index = start; index < end; index++)
        {
            html.Append("<li class=\"").Append(itemClass).Append('"');
            var state = items[index] switch
            {
                LineageNeighbour neighbour => neighbour.UsageState,
                LineageVisualUse use => use.Object.UsageState,
                LineagePossibleUse possible => possible.Source.UsageState,
                _ => null,
            };
            if (state is not null)
            {
                html.Append(" data-lineage-state=\"").Append(Encode(state)).Append('"');
            }

            html.Append('>');
            appendItem(items[index]);
            html.AppendLine("</li>");
        }

        html.Append("</ul>");
    }

    /// <summary>
    /// The listed items after the first <paramref name="shown"/>, behind a native disclosure that says
    /// how many more there are. Where the group itself was capped, the cap is stated inside.
    /// </summary>
    private static void AppendLineageOverflow<T>(
        StringBuilder html,
        string heading,
        LineageGroup<T> items,
        int shown,
        Action<T> appendItem,
        string itemClass)
    {
        if (items.TotalCount <= shown)
        {
            return;
        }

        html.Append("<details class=\"lineage-overflow\"><summary>+")
            .Append((items.TotalCount - shown).ToString(CultureInfo.InvariantCulture))
            .Append(" more<span class=\"visually-hidden\"> in ").Append(Encode(heading)).Append("</span></summary>");
        if (items.Items.Count > shown)
        {
            AppendLineageItems(html, items.Items, shown, items.Items.Count, appendItem, itemClass);
        }

        if (items.HiddenCount > 0)
        {
            html.Append("<p class=\"lineage-more\">Showing ").Append(items.ShownCount.ToString(CultureInfo.InvariantCulture))
                .Append(" of ").Append(items.TotalCount.ToString(CultureInfo.InvariantCulture)).Append(". ")
                .Append(items.HiddenCount.ToString(CultureInfo.InvariantCulture))
                .Append(" more are not shown in this view.</p>");
        }

        html.AppendLine("</details>");
    }

    /// <summary>
    /// Where the focus is used in the report. Only direct use has locations, and the first of them is the
    /// endpoint of the path to report, shown there in full; here it is a compact node, and the other
    /// locations are behind "+N more".
    /// </summary>
    private static void AppendLineageReportUse(StringBuilder html, ProjectInventory inventory, LineageGroup<LineageReportLocation> locations)
    {
        if (locations.TotalCount == 0)
        {
            html.AppendLine("<section class=\"lineage-group\" data-lineage-group=\"report\"><h3>Used in the report</h3><p class=\"lineage-empty\">None directly</p></section>");
            return;
        }

        html.Append("<section class=\"lineage-group\" data-lineage-group=\"report\"><h3>Used in the report (")
            .Append(locations.TotalCount.ToString(CultureInfo.InvariantCulture)).AppendLine(")</h3>");
        html.Append("<ul class=\"lineage-list\"><li class=\"lineage-item lineage-endpoint\">");
        AppendLineageLocation(html, inventory, locations.Items[0], compact: true);
        html.AppendLine("</li></ul>");
        AppendLineageOverflow(html, "Used in the report", locations, 1,
            location => AppendLineageLocation(html, inventory, location), "lineage-item lineage-endpoint");
        html.AppendLine("</section>");
    }

    private static void AppendLineageNode(StringBuilder html, string? cardId, string name)
    {
        if (cardId is null)
        {
            html.Append("<span class=\"lineage-node\">").Append(Encode(name)).Append("</span>");
            return;
        }

        html.Append("<a class=\"lineage-node\" href=\"#").Append(Encode(cardId)).Append("\">").Append(Encode(name)).Append("</a>");
    }

    private static void AppendLineageNeighbour(StringBuilder html, LineageNeighbour neighbour)
    {
        AppendLineageNode(html, neighbour.CardId, neighbour.Name);
        html.Append("<span class=\"lineage-meta\">").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(neighbour.ObjectType)));
        if (neighbour.HasOnlyDefaultRelationship)
        {
            // The ordinary case is left unlabelled on screen; assistive technology still hears it.
            html.Append("<span class=\"visually-hidden\"> · via DAX</span>");
        }
        else if (neighbour.RelationshipLabels.Count > 0)
        {
            html.Append(" · <span class=\"lineage-relationship\">")
                .Append(Encode(string.Join(" · ", neighbour.RelationshipLabels))).Append("</span>");
        }

        AppendLineageSharedName(html, neighbour.IsSharedReportMeasure, neighbour.ReportCount, neighbour.Report);
        AppendLineageNeighbourState(html, neighbour);
        html.Append("</span>");
    }

    /// <summary>
    /// For a report measure whose name other reports bound to the model also define: the report it
    /// belongs to, or where no report could be identified, that the name alone is ambiguous.
    /// </summary>
    private static void AppendLineageSharedName(StringBuilder html, bool isShared, int reportCount, string? report)
    {
        if (!isShared)
        {
            return;
        }

        if (report is not null)
        {
            html.Append(" · <span class=\"lineage-shared\">Report ").Append(Encode(report)).Append("</span>");
            return;
        }

        html.Append(" · <span class=\"lineage-shared\">same name in ")
            .Append(reportCount.ToString(CultureInfo.InvariantCulture)).Append(" reports</span>");
    }

    /// <summary>
    /// Path to report: from the focus, each item that uses the one before it, then the report location
    /// the last of them is used in. The focus is the card's centre, so here it is only the start mark,
    /// named for assistive technology. A long path keeps its start and end and collapses its middle,
    /// saying how many steps are behind the disclosure.
    /// </summary>
    private static void AppendLineagePath(StringBuilder html, ProjectInventory inventory, LineagePath path)
    {
        html.Append("<section class=\"lineage-path\" data-lineage-path=\"")
            .Append(path.Status switch
            {
                LineagePathStatus.DirectlyUsed => "direct",
                LineagePathStatus.ReachedThroughModel => "model",
                _ => "none",
            })
            .Append("\"><h3>Path to report</h3>");
        if (path.Status == LineagePathStatus.NotFound)
        {
            html.Append("<p class=\"lineage-path-none\">No report path found in this project.</p>");
            if (path.OnlyReachedFrom.TotalCount > 0)
            {
                html.Append("<p class=\"lineage-path-note\">Only reached from: ");
                AppendLineagePathNames(html, path.OnlyReachedFrom, neighbour => neighbour.UsageState is not null
                    ? UsageLabel(neighbour.UsageState)
                    : neighbour.IsSharedReportMeasure && neighbour.Report is not null
                        ? $"Report {neighbour.Report}"
                        : null);
                html.Append("</p>");
            }

            if (path.StructuralSources.TotalCount > 0)
            {
                html.Append("<p class=\"lineage-path-note\">Required by model structure: ");
                AppendLineagePathNames(html, path.StructuralSources, neighbour => string.Join(" · ", neighbour.RelationshipLabels));
                html.Append("</p>");
            }
            else if (!string.IsNullOrWhiteSpace(path.StructuralReason))
            {
                html.Append("<p class=\"lineage-path-note\">Required by model structure: ").Append(Encode(path.StructuralReason)).Append("</p>");
            }
            else if (path.RequiredByModelStructure)
            {
                html.Append("<p class=\"lineage-path-note\">Required by model structure.</p>");
            }

            if (path.ChecksLimited)
            {
                html.Append("<p class=\"lineage-path-note\"><span class=\"confidence-flag\">").Append(CoverageMarkerLabel)
                    .Append("<span class=\"visually-hidden\">").Append(CoverageMarkerDescription).Append("</span></span></p>");
            }

            html.AppendLine("</section>");
            return;
        }

        html.Append("<ol class=\"lineage-path-steps\"><li class=\"lineage-path-step lineage-path-start\" data-lineage-path-step=\"focus\"><span class=\"visually-hidden\">")
            .Append(Encode(path.Steps[0].Name)).AppendLine("</span></li>");
        var model = path.Steps.Count - 1;
        var collapse = model + 1 > LineagePathStepLimit;
        var head = collapse ? LineagePathHeadSteps : model;
        for (var index = 1; index <= head; index++)
        {
            AppendLineagePathStep(html, path.Steps[index]);
        }

        if (collapse)
        {
            var hidden = model - LineagePathHeadSteps - LineagePathTailSteps;
            html.Append("<li class=\"lineage-path-gap\" data-lineage-path-step=\"hidden\"><details><summary>")
                .Append(hidden.ToString(CultureInfo.InvariantCulture)).Append(" more steps</summary><ol class=\"lineage-path-hidden\">");
            for (var index = LineagePathHeadSteps + 1; index <= model - LineagePathTailSteps; index++)
            {
                html.Append("<li>");
                AppendLineageNode(html, path.Steps[index].CardId, path.Steps[index].Name);
                html.Append("</li>");
            }

            html.AppendLine("</ol></details></li>");
            for (var index = model - LineagePathTailSteps + 1; index <= model; index++)
            {
                AppendLineagePathStep(html, path.Steps[index]);
            }
        }

        html.Append("<li class=\"lineage-path-step lineage-endpoint\" data-lineage-path-step=\"report\">");
        AppendLineageLocation(html, inventory, path.Endpoint!);
        html.AppendLine("</li></ol>");
        if (path.Status == LineagePathStatus.DirectlyUsed)
        {
            html.Append("<p class=\"lineage-path-note\">Used directly in ")
                .Append(path.EndpointLocationCount.ToString(CultureInfo.InvariantCulture))
                .Append(path.EndpointLocationCount == 1 ? " report location.</p>" : " report locations.</p>");
        }
        else if (path.EndpointLocationCount > 1)
        {
            var otherLocations = path.EndpointLocationCount - 1;
            html.Append("<p class=\"lineage-path-note\">").Append(Encode($"{path.Steps[^1].Name} is also used"))
                .Append(" in ").Append(otherLocations.ToString(CultureInfo.InvariantCulture))
                .Append(otherLocations == 1 ? " other report location.</p>" : " other report locations.</p>");
        }

        // The count is of the focus's other immediate consumers that are themselves reached from a report,
        // not of routes: it never enumerates paths.
        if (path.OtherReportReachingConsumers > 0)
        {
            html.Append("<p class=\"lineage-path-note\">")
                .Append(path.OtherReportReachingConsumers.ToString(CultureInfo.InvariantCulture))
                .Append(path.OtherReportReachingConsumers == 1
                    ? " other item that uses this is also reached from a report.</p>"
                    : " other items that use this are also reached from a report.</p>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendLineagePathStep(StringBuilder html, LineagePathStep step)
    {
        html.Append("<li class=\"lineage-path-step\" data-lineage-path-step=\"model\"");
        if (step.UsageState is not null)
        {
            html.Append(" data-lineage-state=\"").Append(Encode(step.UsageState)).Append('"');
        }

        html.Append('>');
        AppendLineageNode(html, step.CardId, step.Name);
        html.Append("<span class=\"lineage-meta\">").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(step.ObjectType)));
        // An ordinary DAX use is what a path step means, so only another relationship is named.
        if (!step.HasOnlyDefaultRelationship && step.RelationshipLabels.Count > 0)
        {
            html.Append(" · <span class=\"lineage-relationship\">")
                .Append(Encode(string.Join(" · ", step.RelationshipLabels))).Append("</span>");
        }

        AppendLineageSharedName(html, step.IsSharedReportMeasure, step.ReportCount, step.Report);
        html.AppendLine("</span></li>");
    }

    private static void AppendLineagePathNames(
        StringBuilder html,
        LineageGroup<LineageNeighbour> neighbours,
        Func<LineageNeighbour, string?> detail)
    {
        var shown = neighbours.Items.Take(SemanticLineageProjection.PathNoteLimit).ToArray();
        for (var index = 0; index < shown.Length; index++)
        {
            if (index > 0)
            {
                html.Append(", ");
            }

            AppendLineageNode(html, shown[index].CardId, shown[index].Name);
            var text = detail(shown[index]);
            if (!string.IsNullOrWhiteSpace(text))
            {
                html.Append(" (").Append(Encode(text)).Append(')');
            }
        }

        var more = neighbours.TotalCount - shown.Length;
        if (more > 0)
        {
            html.Append(" and ").Append(more.ToString(CultureInfo.InvariantCulture)).Append(" more");
        }
    }

    private static void AppendLineageNeighbourState(StringBuilder html, LineageNeighbour neighbour)
    {
        if (neighbour.UsageState is not null)
        {
            html.Append(" · <span class=\"lineage-state\">").Append(Encode(UsageLabel(neighbour.UsageState))).Append("</span>");
        }
        else if (neighbour.ObjectType is SemanticObjectTypes.Function or SemanticObjectTypes.ReportMeasure &&
                 neighbour.ReachableFromReport is { } reachable)
        {
            html.Append(" · <span class=\"lineage-state\">")
                .Append(reachable ? "Reached from a report" : "Not reached from a report").Append("</span>");
        }

        if (neighbour.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation)
        {
            html.Append(" <span class=\"confidence-flag\">").Append(CoverageMarkerLabel)
                .Append("<span class=\"visually-hidden\">").Append(CoverageMarkerDescription).Append("</span></span>");
        }
    }

    /// <summary>
    /// A report location as an endpoint. An untitled visual is named by its type, so its position on the
    /// page tells it apart from the page's other visuals of that type. A compact endpoint is the name and
    /// page only, for a location shown in full elsewhere on the card.
    /// </summary>
    private static void AppendLineageLocation(StringBuilder html, ProjectInventory inventory, LineageReportLocation location, bool compact = false)
    {
        var hasVisual = location.Visual is not null;
        string label;
        if (location.Visual is not null)
        {
            label = VisualDisplayName(location.Visual);
        }
        else if (location.Location.UsageContext == UsageContexts.Drillthrough)
        {
            label = "Drillthrough field";
        }
        else if (location.Location.Page is not null)
        {
            label = "Page-level use";
        }
        else
        {
            label = "Report-level use";
        }

        AppendLineageNode(html, location.VisualCardId, label);
        var facts = new List<string>();
        if (location.Visual is not null && !compact)
        {
            var visualType = HumanizeVisualType(location.Visual.VisualType);
            if (!string.Equals(visualType, label, StringComparison.OrdinalIgnoreCase))
            {
                facts.Add(visualType);
            }
        }

        if (location.Location.Page is not null)
        {
            facts.Add($"Page {location.Page?.DisplayName ?? location.Location.Page}");
            if (!compact && location.Visual is not null && location.Page is not null && VisualFriendlyName(location.Visual) is null &&
                DescribePosition(location.Page, location.Visual) is var position && position != "Position unavailable")
            {
                facts.Add(position);
            }
        }

        if (inventory.ReportCount > 1 || location.Location.Page is null)
        {
            facts.Add($"Report {location.Report?.Name ?? location.Location.Report}");
        }

        var role = compact ? null : UsageRoleLabel(location.OwnerReferences, location.Location, hasVisual);
        if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, label, StringComparison.OrdinalIgnoreCase))
        {
            facts.Add(role);
        }

        html.Append("<span class=\"lineage-meta\">").Append(Encode(string.Join(" · ", facts))).Append("</span>");
    }

    private static void AppendLineageVisualUse(StringBuilder html, LineageVisualUse use)
    {
        AppendLineageNode(html, use.Object.CardId, use.Object.Name);
        html.Append("<span class=\"lineage-meta\">").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(use.Object.ObjectType)));
        var role = UsageRoleLabel(use.Location.OwnerReferences, use.Location.Location, hasVisual: true);
        if (!string.IsNullOrWhiteSpace(role))
        {
            html.Append(" · <span class=\"lineage-relationship\">").Append(Encode(role)).Append("</span>");
        }

        AppendLineageNeighbourState(html, use.Object);
        html.Append("</span>");
    }

    private static void AppendLineageUnresolved(StringBuilder html, LineageUnresolvedReference reference)
    {
        html.Append("<span class=\"lineage-node lineage-unresolved\">").Append(Encode(reference.Dependency.ReferenceText)).Append("</span>");
        html.Append("<span class=\"lineage-meta\">");
        if (reference.RelationshipLabel != SemanticLineageProjection.DaxLabel)
        {
            html.Append(Encode(reference.RelationshipLabel)).Append(" · ");
        }

        if (reference.Dependency.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous)
        {
            html.Append(reference.CandidateNames.Count > 0
                ? Encode($"Not resolved: may be {JoinAlternatives(reference.CandidateNames)}")
                : "Not resolved: could match more than one object");
        }
        else
        {
            html.Append("Not resolved: not found in this model");
        }

        html.Append("</span>");
    }

    private static void AppendLineagePossibleUse(StringBuilder html, LineagePossibleUse possible)
    {
        AppendLineageNode(html, possible.Source.CardId, possible.Source.Name);
        html.Append("<span class=\"lineage-meta\">").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(possible.Source.ObjectType)))
            .Append(" · ").Append(Encode($"Not resolved: {possible.Dependency.ReferenceText} may mean this"));
        if (possible.OtherCandidateNames.Count > 0)
        {
            html.Append(Encode($" or {JoinAlternatives(possible.OtherCandidateNames)}"));
        }

        AppendLineageNeighbourState(html, possible.Source);
        html.Append("</span>");
    }

    private static void AppendLineageReportReferenceNote(StringBuilder html, UnresolvedSemanticReference reference)
    {
        html.Append("<span class=\"lineage-node lineage-unresolved\">").Append(Encode($"{reference.Table}[{reference.ObjectName}]")).Append("</span>");
        html.Append("<span class=\"lineage-meta\">").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(reference.ObjectType)))
            .Append(" · Not resolved: not found in the model</span>");
    }

    /// <summary>
    /// Power Query context for a column: the queries that load its table and the bounded column
    /// evidence. It is context beside the card, never a node of the diagram: the query loads the table,
    /// and the column's own route through the query is not claimed.
    /// </summary>
    private static void AppendLineagePowerQuery(StringBuilder html, ProjectInventory inventory, LineageCard card)
    {
        var context = card.PowerQuery!;
        html.AppendLine("<section class=\"lineage-context\" data-lineage-group=\"power-query\"><h3>Power Query context</h3><ul class=\"lineage-context-list\">");
        foreach (var query in context.TableQueries)
        {
            html.Append("<li>");
            var usage = inventory.PowerQueryUsages.FirstOrDefault(item =>
                string.Equals(item.SemanticModel, query.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.QueryName, query.QueryName, StringComparison.OrdinalIgnoreCase));
            html.Append("<span class=\"lineage-node\">Power Query: ");
            if (usage is null)
            {
                html.Append(Encode(query.QueryName));
            }
            else
            {
                html.Append("<a href=\"#").Append(Encode(PowerQueryAnchor(usage))).Append("\">").Append(Encode(query.QueryName)).Append("</a>");
            }

            html.Append("</span> <span class=\"lineage-meta\">").Append(Encode($"Loads table {query.Table}"));
            if (query.HasDynamicReferences)
            {
                html.Append(" · uses dynamic references");
            }

            html.AppendLine("</span></li>");
        }

        if (!string.IsNullOrWhiteSpace(context.SourceColumn))
        {
            html.Append("<li><span class=\"lineage-meta\">")
                .Append(Encode($"Source column: {context.SourceColumn}")).AppendLine("</span></li>");
        }

        foreach (var evidence in context.ColumnEvidence)
        {
            html.Append("<li><span class=\"lineage-meta\">")
                .Append(Encode($"Power Query evidence: {PowerQueryColumnUsageLabel(evidence)}")).AppendLine("</span></li>");
        }

        html.AppendLine("</ul></section>");
    }

    /// <summary>
    /// The files and references behind every listed relationship, as plain text. It carries no links, so
    /// it adds evidence without adding a second route through the same relationships.
    /// </summary>
    private static void AppendLineageEvidence(StringBuilder html, LineageCard card)
    {
        var lines = new List<string>();
        foreach (var neighbour in card.DependsOn.Items.Concat(card.UsedBy.Items).Concat(card.RequiredByModel.Items))
        {
            foreach (var dependency in neighbour.Dependencies)
            {
                lines.Add($"{EdgeEndName(dependency, source: true)} → {EdgeEndName(dependency, source: false)} · " +
                          $"{SemanticLineageProjection.RelationshipLabel(dependency.DependencyKind, dependency.FromObjectType)} · " +
                          $"{DisplayPath(dependency.EvidencePath)} · {dependency.EvidenceText}");
            }
        }

        foreach (var location in card.UsedInReport.Items)
        {
            foreach (var evidence in location.Evidence)
            {
                lines.Add($"{DisplayPath(evidence.ArtifactPath)} · {evidence.EvidencePath}");
            }
        }

        foreach (var use in card.Uses.Items)
        {
            foreach (var evidence in use.Location.Evidence)
            {
                lines.Add($"{use.Object.Name} · {DisplayPath(evidence.ArtifactPath)} · {evidence.EvidencePath}");
            }
        }

        foreach (var reference in card.NotResolved.Items)
        {
            lines.Add($"{reference.Dependency.ReferenceText} · {reference.Dependency.Reason} · {DisplayPath(reference.Dependency.EvidencePath)}");
        }

        foreach (var possible in card.PossibleUse.Items)
        {
            lines.Add($"{possible.Source.Name} · {possible.Dependency.Reason} · {DisplayPath(possible.Dependency.EvidencePath)}");
        }

        foreach (var reference in card.UnresolvedReportReferences.Items)
        {
            lines.Add($"{reference.Table}[{reference.ObjectName}] · {DisplayPath(reference.ArtifactPath)} · {reference.EvidencePath}");
        }

        if (lines.Count == 0)
        {
            return;
        }

        html.Append("<details class=\"technical-details lineage-evidence\"><summary>Evidence (")
            .Append(lines.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(")</summary><ul class=\"plain-list\">");
        foreach (var line in lines)
        {
            html.Append("<li>").Append(Encode(line)).AppendLine("</li>");
        }

        html.AppendLine("</ul></details>");
    }

    private static string EdgeEndName(SemanticDependencyEdge dependency, bool source)
    {
        var (table, name) = source
            ? (dependency.FromTable, dependency.FromObjectName)
            : (dependency.ToTable, dependency.ToObjectName);
        var type = source ? dependency.FromObjectType : dependency.ToObjectType;
        return type is SemanticObjectTypes.Table or SemanticObjectTypes.RefreshPolicy
            ? table
            : string.IsNullOrEmpty(table) || type is SemanticObjectTypes.Relationship or SemanticObjectTypes.Role or SemanticObjectTypes.Perspective
                ? name
                : $"{table}[{name}]";
    }

    private static string JoinAlternatives(IReadOnlyList<string> names) => names.Count switch
    {
        0 => string.Empty,
        1 => names[0],
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}",
    };

}
