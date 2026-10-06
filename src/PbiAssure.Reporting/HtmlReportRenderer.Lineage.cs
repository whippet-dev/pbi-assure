using System.Globalization;
using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

/// <summary>
/// Object-focused lineage cards. Each card is ordinary semantic HTML — a heading, then one titled list
/// per relationship group — so the text is the lineage view rather than a fallback for one. The groups
/// carry the side of the focus they sit on, so a later layout can arrange them without restructuring.
/// </summary>
public static partial class HtmlReportRenderer
{
    internal const string LineageScopeNote =
        "Lineage shows the dependencies PBI Assure found in this project's files. Some usage can't be checked, and anything outside this project isn't visible.";

    private static void AppendLineage(
        StringBuilder html,
        ProjectInventory inventory,
        SemanticLineageProjection lineage,
        AnalysisCoverage coverage)
    {
        html.AppendLine("    <section id=\"lineage\" class=\"report-section lineage-section\" data-report-section=\"lineage\" aria-labelledby=\"lineage-heading\">");
        html.AppendLine("      <div class=\"lineage-index\" data-lineage-index>");
        html.AppendLine("        <h2 id=\"lineage-heading\" tabindex=\"-1\">Lineage</h2>");
        html.AppendLine("        <p class=\"section-intro\">Lineage shows what a model object depends on, what uses it and where the report uses it. Choose View lineage beside an object in Semantic model, or an object listed on a visual in Report pages.</p>");
        html.AppendLine("        <p><a href=\"#semantic-usage\">Go to Semantic model</a></p>");
        html.AppendLine("      </div>");
        foreach (var card in lineage.Cards)
        {
            AppendLineageCard(html, inventory, card, coverage);
        }

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
        html.AppendLine("<header class=\"lineage-header\">");
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
        html.AppendLine("</header>");
        html.Append("<p class=\"lineage-scope\">").Append(Encode(LineageScopeNote)).AppendLine("</p>");
        html.AppendLine("<div class=\"lineage-content\">");
        if (card.Kind == LineageFocusKind.Visual)
        {
            AppendLineageGroup(html, "uses", "upstream", "Uses", card.Uses,
                "No model objects were found for this visual.",
                use => AppendLineageVisualUse(html, use));
            AppendLineageGroup(html, "not-resolved", "upstream", "Not resolved", card.UnresolvedReportReferences,
                emptyText: null,
                reference => AppendLineageReportReferenceNote(html, reference));
        }
        else
        {
            if (card.PowerQuery is not null)
            {
                AppendLineagePowerQuery(html, inventory, card);
            }

            AppendLineageGroup(html, "depends-on", "upstream", "Depends on", card.DependsOn,
                "No dependencies found.",
                neighbour => AppendLineageNeighbour(html, neighbour));
            AppendLineageGroup(html, "not-resolved", "upstream", "Not resolved", card.NotResolved,
                emptyText: null,
                reference => AppendLineageUnresolved(html, reference));
            AppendLineageGroup(html, "used-by", "downstream", "Used by", card.UsedBy,
                "No model object found in this project uses this.",
                neighbour => AppendLineageNeighbour(html, neighbour));
            if (card.Kind is LineageFocusKind.SemanticObject or LineageFocusKind.ReportMeasure)
            {
                AppendLineageGroup(html, "report", "downstream", "Used in the report", card.UsedInReport,
                    "No direct report use found in this project.",
                    location => AppendLineageLocation(html, inventory, location));
            }

            AppendLineageGroup(html, "required", "downstream", "Required by model", card.RequiredByModel,
                emptyText: null,
                neighbour => AppendLineageNeighbour(html, neighbour));
            AppendLineageGroup(html, "possible", "downstream", "Possible use", card.PossibleUse,
                emptyText: null,
                possible => AppendLineagePossibleUse(html, possible));
        }

        html.AppendLine("</div>");
        if (card.Path is not null)
        {
            AppendLineagePath(html, inventory, card.Path);
        }

        AppendLineageEvidence(html, card);
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

            html.AppendLine("<a href=\"#reports\">Back to Report pages</a></p>");
            return;
        }

        if (card.DetailsAnchor is not null)
        {
            html.Append("<a href=\"#").Append(Encode(card.DetailsAnchor)).Append("\">Object details</a>");
        }

        html.AppendLine("<a href=\"#semantic-usage\">Back to Semantic model</a></p>");
    }

    private static void AppendLineageGroup<T>(
        StringBuilder html,
        string group,
        string side,
        string heading,
        LineageGroup<T> items,
        string? emptyText,
        Action<T> appendItem)
    {
        if (items.TotalCount == 0 && emptyText is null)
        {
            return;
        }

        html.Append("<section class=\"lineage-group\" data-lineage-group=\"").Append(group)
            .Append("\" data-lineage-side=\"").Append(side).Append("\"><h3>").Append(Encode(heading)).Append(" (")
            .Append(items.TotalCount.ToString(CultureInfo.InvariantCulture)).Append(")</h3>");
        if (items.TotalCount == 0)
        {
            html.Append("<p class=\"lineage-empty\">").Append(Encode(emptyText!)).AppendLine("</p></section>");
            return;
        }

        html.AppendLine("<ul class=\"lineage-list\">");
        for (var index = 0; index < items.Items.Count; index++)
        {
            html.Append("<li class=\"lineage-item\"");
            if (index >= SemanticLineageProjection.PreviewLimit)
            {
                html.Append(" data-lineage-overflow");
            }

            var state = items.Items[index] switch
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
            appendItem(items.Items[index]);
            html.AppendLine("</li>");
        }

        html.Append("</ul>");
        if (items.HiddenCount > 0)
        {
            html.Append("<p class=\"lineage-more\">Showing ").Append(items.ShownCount.ToString(CultureInfo.InvariantCulture))
                .Append(" of ").Append(items.TotalCount.ToString(CultureInfo.InvariantCulture)).Append(". ")
                .Append(items.HiddenCount.ToString(CultureInfo.InvariantCulture))
                .Append(" more are not shown in this view.</p>");
        }

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
    /// Path to report: the focus, each item that uses the one before it, and the report location the
    /// last of them is used in. Steps are list items so a later layout can draw them as a spine; the
    /// notes are what the path does not show, counted for what they are.
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

        html.AppendLine("<ol class=\"lineage-path-steps\">");
        for (var index = 0; index < path.Steps.Count; index++)
        {
            var step = path.Steps[index];
            html.Append("<li class=\"lineage-path-step\" data-lineage-path-step=\"").Append(index == 0 ? "focus" : "model").Append('"');
            if (step.UsageState is not null)
            {
                html.Append(" data-lineage-state=\"").Append(Encode(step.UsageState)).Append('"');
            }

            html.Append('>');
            AppendLineageNode(html, step.CardId, step.Name);
            html.Append("<span class=\"lineage-meta\">").Append(Encode(SemanticLineageProjection.ObjectTypeLabel(step.ObjectType)));
            if (step.HasOnlyDefaultRelationship)
            {
                html.Append("<span class=\"visually-hidden\"> · via DAX</span>");
            }
            else if (step.RelationshipLabels.Count > 0)
            {
                html.Append(" · <span class=\"lineage-relationship\">")
                    .Append(Encode(string.Join(" · ", step.RelationshipLabels))).Append("</span>");
            }

            AppendLineageSharedName(html, step.IsSharedReportMeasure, step.ReportCount, step.Report);
            html.AppendLine("</span></li>");
        }

        html.Append("<li class=\"lineage-path-step\" data-lineage-path-step=\"report\">");
        AppendLineageLocation(html, inventory, path.Endpoint!);
        html.AppendLine("</li></ol>");
        var otherLocations = path.EndpointLocationCount - 1;
        if (otherLocations > 0)
        {
            html.Append("<p class=\"lineage-path-note\">")
                .Append(Encode(path.Status == LineagePathStatus.DirectlyUsed ? "Also used" : $"{path.Steps[^1].Name} is also used"))
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

    private static void AppendLineageLocation(StringBuilder html, ProjectInventory inventory, LineageReportLocation location)
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
        if (location.Visual is not null)
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
        }

        if (inventory.ReportCount > 1 || location.Location.Page is null)
        {
            facts.Add($"Report {location.Report?.Name ?? location.Location.Report}");
        }

        var role = UsageRoleLabel(location.OwnerReferences, location.Location, hasVisual);
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

    private static void AppendLineagePowerQuery(StringBuilder html, ProjectInventory inventory, LineageCard card)
    {
        var context = card.PowerQuery!;
        html.AppendLine("<section class=\"lineage-group\" data-lineage-group=\"power-query\" data-lineage-side=\"upstream\"><h3>Power Query context</h3><ul class=\"lineage-list lineage-context-list\">");
        foreach (var query in context.TableQueries)
        {
            html.Append("<li class=\"lineage-item lineage-context\">");
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

            html.Append("</span><span class=\"lineage-meta\">").Append(Encode($"Loads table {query.Table}"));
            if (query.HasDynamicReferences)
            {
                html.Append(" · uses dynamic references");
            }

            html.AppendLine("</span></li>");
        }

        if (!string.IsNullOrWhiteSpace(context.SourceColumn))
        {
            html.Append("<li class=\"lineage-item lineage-context\"><span class=\"lineage-node\">")
                .Append(Encode($"Source column: {context.SourceColumn}")).AppendLine("</span></li>");
        }

        foreach (var evidence in context.ColumnEvidence)
        {
            html.Append("<li class=\"lineage-item lineage-context\"><span class=\"lineage-meta\">")
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

    private static string JoinAlternatives(IReadOnlyList<string> names) => JoinNames(names, "or");

    private static string JoinNames(IReadOnlyList<string> names, string conjunction) => names.Count switch
    {
        0 => string.Empty,
        1 => names[0],
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} {conjunction} {names[^1]}",
    };

    /// <summary>The entry point on a Semantic model row. The link text names the object for screen readers.</summary>
    private static void AppendLineageEntry(StringBuilder html, SemanticLineageProjection lineage, SemanticObjectUsage usage)
    {
        var card = lineage.CardFor(usage);
        if (card is null)
        {
            return;
        }

        html.Append("                <p class=\"lineage-entry\"><a href=\"#").Append(Encode(card.Id)).Append("\">View lineage<span class=\"visually-hidden\"> for ")
            .Append(Encode(card.Title)).AppendLine("</span></a></p>");
    }
}
