using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    // The existing lineage id remains the Lineage route; collection ids remain Summary aliases.
    // Views share a single heading, definition and evidence DOM, with no second data payload.
    private static string ObjectSummaryId(LineageCard card) => "sum-" + card.Id[4..];
    private static string ObjectDefinitionId(LineageCard card) => "def-" + card.Id[4..];

    private static LineageCard? TableContext(SemanticModelInventory model, SemanticTableInventory table) =>
        table.IsFieldParameter || table.IsCalculationGroup || table.RefreshPolicy is not null ||
        table.Partitions.Any(partition => string.Equals(partition.SourceType, "calculated", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(partition.Expression))
            ? new LineageCard(LineageIds.Create("lin", "table " + table.Name, $"table-context\u001f{model.Name}\u001f{table.Name}"),
                LineageFocusKind.SemanticObject, table.Name, SemanticObjectTypes.Table) { SemanticModel = model.Name, Table = table.Name }
            : null;

    private static void AppendTableContexts(StringBuilder html, ProjectInventory inventory, AnalysisCoverage coverage)
    {
        foreach (var model in inventory.SemanticModels)
        foreach (var table in model.Tables)
        {
            if (TableContext(model, table) is not { } card) continue;
            html.Append("<article id=\"").Append(Encode(card.Id)).Append("\" class=\"lineage-card\" data-lineage-card=\"semantic\" aria-labelledby=\"")
                .Append(Encode(card.Id + "-title")).AppendLine("\">");
            AppendObjectContextStart(html, inventory, card, coverage);
            html.AppendLine("<p class=\"group-explanation\">Lineage is shown for this table's columns and measures. Explore them in <a href=\"#semantic-usage\">Model</a>.</p></section>");
            AppendObjectDefinition(html, inventory, card);
            AppendObjectDetails(html, inventory, card, coverage);
            html.AppendLine("</article>");
        }
    }

    private static void AppendObjectContextStart(StringBuilder html, ProjectInventory inventory, LineageCard card, AnalysisCoverage coverage)
    {
        html.AppendLine("<header class=\"object-context-header\" data-object-context-header>");
        html.Append("<p class=\"kicker\">").Append(card.Kind == LineageFocusKind.ReportMeasure ? "Report · Measure" : "Semantic model · Object").AppendLine("</p>");
        html.Append("<h2 id=\"").Append(Encode(card.Id + "-title")).Append("\" tabindex=\"-1\" class=\"object-context-title\">")
            .Append(Encode(card.Title)).AppendLine("</h2>");
        AppendLineageFacts(html, inventory, card);
        AppendLineageStatus(html, card, coverage);
        if (!string.IsNullOrWhiteSpace(card.Reason))
            html.Append("<p class=\"object-context-why\">Why: ").Append(Encode(card.Reason)).AppendLine("</p>");
        html.AppendLine("</header>");
        html.AppendLine("<nav class=\"object-view-nav\" aria-label=\"Object views\">");
        foreach (var (label, id) in new[] { ("Summary", ObjectSummaryId(card)), ("Lineage", card.Id), ("Definition", ObjectDefinitionId(card)) })
            html.Append("<a href=\"#").Append(Encode(id)).Append("\" data-object-view-link=\"").Append(label.ToLowerInvariant())
                .Append("\">").Append(label).AppendLine("</a>");
        html.AppendLine("</nav>");
        html.Append("<section id=\"").Append(Encode(ObjectSummaryId(card))).AppendLine("\" class=\"object-local-view object-summary\" data-object-view=\"summary\">");
        html.AppendLine("<h3 tabindex=\"-1\">Summary</h3><dl class=\"object-summary-facts\">");
        if (card.ObjectType != SemanticObjectTypes.Table)
        {
            AppendFact(html, "Used by", card.UsedBy.TotalCount == 0 ? "None found in the model" : $"{card.UsedBy.TotalCount} {Pluralize(card.UsedBy.TotalCount, "object", "objects")} in the model");
            AppendFact(html, "Used in the report", card.UsedInReport.TotalCount == 0 ? "Not used directly" : $"{card.UsedInReport.TotalCount} {Pluralize(card.UsedInReport.TotalCount, "place", "places")}");
            AppendFact(html, "Required by the model", StructuralSummary(card));
            AppendFact(html, "Power Query", card.PowerQuery is null ? "None found" : "See Details");
        }
        html.AppendLine("</dl>");
        if (card.ObjectType == SemanticObjectTypes.Table)
            html.AppendLine("<p>Usage is shown for this table's columns and measures, not the table itself.</p>");
        if (card.Usage?.UsageState == SemanticUsageStates.ApparentlyUnused)
        {
            var hasQueryUse = card.PowerQuery?.ColumnEvidence.Count > 0;
            html.Append("<p>").Append(hasQueryUse
                ? "PBI Assure didn't find any model or report use for this object in the project. Power Query use is shown in Details."
                : "PBI Assure didn't find any use for this object in the project.")
                .AppendLine(" Check before removing it: other reports and dynamic behaviour can't be seen here.</p>");
        }
        if (card.UsedInReport.Items.Count > 0)
        {
            html.AppendLine("<section class=\"object-summary-group\"><h4>Report usage</h4><ul class=\"plain-list\">");
            foreach (var location in card.UsedInReport.Items.Take(2))
            {
                html.Append("<li>");
                AppendLineageLocation(html, inventory, location, compact: true);
                html.AppendLine("</li>");
            }
            html.AppendLine("</ul></section>");
        }
        AppendObjectSummaryNeighbours(html, "Used by", card.UsedBy);
        AppendObjectSummaryNeighbours(html, "Required by the model", card.RequiredByModel);
        if (card.PossibleUse.TotalCount > 0 || card.NotResolved.TotalCount > 0)
            html.AppendLine("<p class=\"group-explanation\">Possible use or unresolved references are recorded. Review the distinctions in Lineage and the evidence in Details.</p>");
        if (card.Usage is { ClassificationConfidence: ClassificationConfidences.QualifiedByLimitation })
            html.AppendLine("<p class=\"group-explanation\">Some checks were limited for this model, so this result could miss some usage. See Details.</p>");
        html.AppendLine("<p class=\"group-explanation\">Explore dependencies and all listed locations in Lineage. Supporting evidence is in Details.</p></section>");
        html.Append("<section class=\"object-local-view\" data-object-view=\"lineage\" aria-labelledby=\"").Append(Encode(card.Id + "-view-heading")).AppendLine("\">");
        html.Append("<h3 id=\"").Append(Encode(card.Id + "-view-heading")).AppendLine("\" tabindex=\"-1\">Lineage</h3>");
    }

    private static string StructuralSummary(LineageCard card)
    {
        if (card.RequiredByModel.TotalCount == 0)
            return card.Usage?.UsageState == SemanticUsageStates.StructurallyRequired ? "Yes – model structure" : "No";
        if (card.RequiredByModel.HiddenCount > 0)
            return $"Yes – see Lineage ({card.RequiredByModel.TotalCount} items)";
        var kinds = card.RequiredByModel.Items.GroupBy(item => item.ObjectType)
            .Select(group =>
            {
                var singular = SemanticLineageProjection.ObjectTypeLabel(group.Key).ToLowerInvariant();
                var plural = group.Key == SemanticObjectTypes.RefreshPolicy ? singular[..^1] + "ies" : singular + "s";
                return $"{group.Count()} {Pluralize(group.Count(), singular, plural)}";
            });
        return "Yes – " + string.Join(", ", kinds);
    }

    private static void AppendObjectSummaryNeighbours(StringBuilder html, string label, LineageGroup<LineageNeighbour> group)
    {
        if (group.Items.Count == 0) return;
        html.Append("<section class=\"object-summary-group\"><h4>").Append(Encode(label)).AppendLine("</h4><ul class=\"plain-list\">");
        foreach (var neighbour in group.Items.Take(2))
        {
            html.Append("<li>");
            AppendLineageNeighbour(html, neighbour);
            html.AppendLine("</li>");
        }
        html.AppendLine("</ul></section>");
    }

    private static SemanticTableInventory? ObjectTable(ProjectInventory inventory, LineageCard card) => inventory.SemanticModels
        .FirstOrDefault(model => string.Equals(model.Name, card.SemanticModel, StringComparison.OrdinalIgnoreCase))?.Tables
        .FirstOrDefault(table => string.Equals(table.Name, card.Table, StringComparison.OrdinalIgnoreCase));

    private static ReportMeasureInventory? ObjectReportMeasure(LineageCard card) => card.Report?.ReportMeasures
        .FirstOrDefault(measure => SemanticLineageProjection.ReportMeasureRowId(card.SemanticModel!, card.ReportName!, measure.Entity, measure.Name) == card.DetailsAnchor);

    private static void AppendObjectDefinition(StringBuilder html, ProjectInventory inventory, LineageCard card)
    {
        html.Append("<section id=\"").Append(Encode(ObjectDefinitionId(card))).AppendLine("\" class=\"object-local-view object-definition\" data-object-view=\"definition\"><h3 tabindex=\"-1\">Definition</h3>");
        var table = ObjectTable(inventory, card);
        var name = card.Usage?.ObjectName ?? card.Reachability?.ObjectName;
        var definitions = new List<(string Label, string? Expression)>();
        switch (card.ObjectType)
        {
            case SemanticObjectTypes.Measure:
                var measure = table?.Measures.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                definitions.Add(("DAX expression", measure?.Expression));
                definitions.Add(("Format-string expression", measure?.FormatStringExpression));
                break;
            case SemanticObjectTypes.Column:
                definitions.Add(("DAX expression", table?.Columns.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Expression));
                break;
            case SemanticObjectTypes.Table:
                definitions.AddRange(table?.Partitions.Where(partition => string.Equals(partition.SourceType, "calculated", StringComparison.OrdinalIgnoreCase))
                    .Select(partition => ("Calculated-table expression", partition.Expression)) ?? []);
                break;
            case SemanticObjectTypes.CalculationItem:
                var item = table?.CalculationGroup?.Items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                definitions.Add(("Calculation-item expression", item?.Expression));
                definitions.Add(("Format-string expression", item?.FormatStringExpression));
                break;
            case SemanticObjectTypes.Function:
                var function = inventory.SemanticModels.FirstOrDefault(model => string.Equals(model.Name, card.SemanticModel, StringComparison.OrdinalIgnoreCase))?.Functions
                    .FirstOrDefault(function => string.Equals(function.Name, name, StringComparison.OrdinalIgnoreCase));
                if (function?.Parameters.Count > 0)
                    html.Append("<p>Parameters: ").Append(Encode(string.Join(", ", function.Parameters.Select(parameter => parameter.Name + (parameter.TypeHint is null ? "" : $": {parameter.TypeHint}"))))).AppendLine("</p>");
                definitions.Add(("Saved function definition", function?.Expression));
                break;
            case SemanticObjectTypes.ReportMeasure:
                definitions.Add(("Report-measure definition", ObjectReportMeasure(card)?.Expression));
                break;
        }
        var saved = definitions.Where(definition => !string.IsNullOrWhiteSpace(definition.Expression)).ToArray();
        if (saved.Length == 0) html.Append("<p class=\"group-explanation\">").Append(card.ObjectType == SemanticObjectTypes.Column
            ? "This column has no DAX expression of its own." : "No DAX expression.").AppendLine("</p>");
        foreach (var (label, expression) in saved)
            html.Append("<h4>").Append(Encode(label)).Append("</h4><pre><code>").Append(Encode(expression!)).AppendLine("</code></pre>");
        html.AppendLine("</section>");
    }

    private static void AppendObjectDetails(StringBuilder html, ProjectInventory inventory, LineageCard card, AnalysisCoverage coverage)
    {
        html.Append("<details id=\"").Append(Encode(card.Id + "-details")).AppendLine("\" class=\"object-context-details\"><summary>Details</summary>");
        html.AppendLine("<section><h3>Evidence</h3>");
        var sourcePath = ObjectTable(inventory, card)?.RelativePath ?? ObjectReportMeasure(card)?.RelativePath;
        if (sourcePath is null && card.ObjectType == SemanticObjectTypes.Function)
            sourcePath = inventory.SemanticModels.FirstOrDefault(model => model.Name == card.SemanticModel)?.Functions
                .FirstOrDefault(function => function.Name == card.Reachability?.ObjectName)?.RelativePath;
        if (sourcePath is not null)
            html.Append("<p>Defined in: <code>").Append(Encode(DisplayPath(sourcePath))).AppendLine("</code></p>");
        AppendLineageEvidence(html, card);
        if (card.Usage is { } usage) AppendUsageDetails(html, inventory, usage);
        html.AppendLine("</section>");
        if (card.Usage is { ClassificationConfidence: ClassificationConfidences.QualifiedByLimitation })
        {
            html.AppendLine("<section><h3>Checks limited</h3>");
            var modelCoverage = coverage.Models.FirstOrDefault(model => string.Equals(model.ModelName, card.SemanticModel, StringComparison.OrdinalIgnoreCase));
            if (modelCoverage is not null)
                html.Append("<p><a href=\"#").Append(Encode(modelCoverage.AnchorId)).AppendLine("\">See what PBI Assure couldn't fully check</a></p>");
            html.AppendLine("</section>");
        }
        if (card.PowerQuery is not null)
        {
            html.AppendLine("<section><h3>Power Query</h3>");
            AppendLineagePowerQuery(html, inventory, card);
            if (card.Usage is { } column) AppendPowerQueryColumnUsage(html, inventory, column);
            html.AppendLine("<p class=\"group-explanation\">This shows which query loads the table. It doesn't follow the column through every query step, and a query could depend on it without appearing here.</p></section>");
        }
        if (card.ObjectType == SemanticObjectTypes.Table && ObjectTable(inventory, card) is { } table)
        {
            html.AppendLine("<section><h3>Table settings</h3>");
            AppendSemanticFeatures(html, table);
            html.AppendLine("</section>");
        }
        if (ObjectReportMeasure(card) is { } reportMeasure)
        {
            html.AppendLine("<section><h3>Report measure details</h3><dl class=\"facts\">");
            AppendFact(html, "Data type", reportMeasure.DataType);
            if (!string.IsNullOrWhiteSpace(reportMeasure.Description)) AppendFact(html, "Description", reportMeasure.Description);
            if (!string.IsNullOrWhiteSpace(reportMeasure.FormatString)) AppendFact(html, "Display format", reportMeasure.FormatString, code: true);
            AppendFact(html, "Uses", reportMeasure.References.Count == 0 ? "No measure dependencies listed" : string.Join(", ", reportMeasure.References.Select(reference => $"{reference.Entity}[{reference.Name}] ({(reference.IsReportMeasureReference ? "report measure" : "model measure")})")));
            if (reportMeasure.HasUnrecognizedReferences) AppendFact(html, "Dependency check", "Power BI could not identify every reference in this formula; review it manually.");
            html.AppendLine("</dl></section>");
        }
        html.AppendLine("</details>");
    }
}
