using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using PbiAssure.Reporting.Exports;

namespace PbiAssure.Core.Tests;

public sealed class ExportCsvRendererTests
{
    [Fact]
    public void DataCatalogueExportsEligibleObjectsAndIdentityBasedAggregates()
    {
        var firstPath = "First.Report/definition/pages/p1/visuals/shared/visual.json";
        var secondPath = "Second.Report/definition/pages/p2/visuals/shared/visual.json";
        var amountReferences = new[] { Reference("Amount", UsageContexts.Projection, "Values", "$.query.Values") };
        var inventory = Inventory(
            [
                Report("Same report", "First.Report", "p1", "Same page", "shared", amountReferences),
                Report("Same report", "Second.Report", "p2", "Same page", "shared", amountReferences),
            ],
            [
                Usage("Amount", [Evidence("Same report", "p1", "shared", firstPath, UsageContexts.Projection, "Values", "$.query.Values"),
                    Evidence("Same report", "p2", "shared", secondPath, UsageContexts.Projection, "Values", "$.query.Values")]),
                Usage("Filter Only", [Evidence("Same report", "p1", "shared", firstPath, UsageContexts.Filter, "filter", "$.filters[0]")]),
                Usage("Other Only", [Evidence("Same report", "p1", "shared", firstPath, UsageContexts.Other, null, "$.other")]),
                new SemanticObjectUsage("Model", "Fact", "Indirect", SemanticObjectTypes.Measure, null, [], SemanticUsageStates.IndirectlyUsed),
                new SemanticObjectUsage("Model", "Fact", "Structural", SemanticObjectTypes.Column, null, [], SemanticUsageStates.StructurallyRequired),
                new SemanticObjectUsage("Model", "Fact", "Unused", SemanticObjectTypes.Column, null, [], SemanticUsageStates.ApparentlyUnused),
                new SemanticObjectUsage("Model", "LocalDate", "Date", SemanticObjectTypes.Column, null, [], SemanticUsageStates.ApparentlyUnused),
            ],
            [ModelWithGeneratedTable()]);

        var rows = ReadCsv(DataCatalogueCsvRenderer.Render(inventory));
        var header = rows[0];
        var amount = RowFor(rows, header, "Amount");
        var filter = RowFor(rows, header, "Filter Only");
        var other = RowFor(rows, header, "Other Only");
        var unused = RowFor(rows, header, "Unused");

        Assert.Equal(
            ["SemanticModel", "Table", "Object", "ObjectType", "SemanticUsage", "ClassificationConfidence", "UserFacing", "DirectUsageCount", "ReportCount", "PageCount", "VisualCount", "UsageContexts"],
            header);
        Assert.Equal(6, rows.Count - 1);
        Assert.DoesNotContain(rows.Skip(1), row => Value(row, header, "Object") == "Date");
        Assert.Equal("Yes", Value(amount, header, "UserFacing"));
        Assert.Equal("2", Value(amount, header, "DirectUsageCount"));
        Assert.Equal("2", Value(amount, header, "ReportCount"));
        Assert.Equal("2", Value(amount, header, "PageCount"));
        Assert.Equal("2", Value(amount, header, "VisualCount"));
        Assert.Equal("No", Value(filter, header, "UserFacing"));
        Assert.Equal("Unclear", Value(other, header, "UserFacing"));
        Assert.Equal("ApparentlyUnused", Value(unused, header, "SemanticUsage"));
        Assert.Equal("No", Value(unused, header, "UserFacing"));
        Assert.Equal("0", Value(unused, header, "DirectUsageCount"));
    }

    [Fact]
    public void DataCatalogueSupportsOptionalColumnsAndUsesExistingSemanticReason()
    {
        var visualPath = "One.Report/definition/pages/p1/visuals/v1/visual.json";
        var inventory = Inventory(
            [Report("Report", "One.Report", "p1", "Overview", "v1", [Reference("Amount", UsageContexts.Projection, "Values", "$.query")])],
            [
                Usage("Amount", [Evidence("Report", "p1", "v1", visualPath, UsageContexts.Projection, "Values", "$.query")]),
                new SemanticObjectUsage("Model", "Fact", "Sort Key", SemanticObjectTypes.Column, null, [], SemanticUsageStates.IndirectlyUsed),
            ],
            dependencies:
            [new SemanticDependencyEdge("Model", "Fact", "Amount", SemanticObjectTypes.Measure, null, "Fact", "Sort Key", SemanticObjectTypes.Column, null,
                SemanticDependencyKinds.SortBy, "sortByColumn", "Sort Key")]) with
            {
                SemanticNodeReachability = [new SemanticNodeReachability("Model", "Fact", "Amount", SemanticObjectTypes.Measure, null, true, false)],
            };

        var rows = ReadCsv(DataCatalogueCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.DataCatalogue,
            ["Object", "ReportNames", "PageNames", "UsageRoles", "SemanticReason"])));
        var header = rows[0];
        var amount = RowFor(rows, header, "Amount");
        var sortKey = RowFor(rows, header, "Sort Key");

        Assert.Equal(["Object", "ReportNames", "PageNames", "UsageRoles", "SemanticReason"], header);
        Assert.Equal("Report", Value(amount, header, "ReportNames"));
        Assert.Equal("Overview", Value(amount, header, "PageNames"));
        Assert.Equal("Values", Value(amount, header, "UsageRoles"));
        Assert.Equal("Sorts Fact[Amount]", Value(sortKey, header, "SemanticReason"));
    }

    [Fact]
    public void UsageMappingPreservesEveryDirectContextAndOptionalMachineEvidence()
    {
        var visualPath = "One.Report/definition/pages/p1/visuals/v1/visual.json";
        var pagePath = "One.Report/definition/pages/p1/page.json";
        var references = new[]
        {
            Reference("Projection", UsageContexts.Projection, "Values", "$.query.Values"),
            Reference("Tooltip", UsageContexts.Projection, "tooltips", "$.query.Tooltips"),
            Reference("Filter", UsageContexts.Filter, "filter", "$.filters[0]"),
            Reference("Sort", UsageContexts.Sort, null, "$.sort"),
            Reference("Formatting", UsageContexts.Formatting, "colour", "$.objects.colour") with { ReferenceOrigin = VisualReferenceOrigins.FormattingPropertyExpression },
            Reference("Other", UsageContexts.Other, null, "$.other"),
        };
        var evidence = references.Select(reference => Usage(reference.ObjectName,
            [Evidence("Report", "p1", "v1", visualPath, reference.UsageContext, reference.Role, reference.EvidencePath)])).Append(
            Usage("Drillthrough", [Evidence("Report", "p1", null, pagePath, UsageContexts.Drillthrough, "drillthrough", "$.pageBinding")])).ToArray();
        var inventory = Inventory([Report("Report", "One.Report", "p1", "Overview", "v1", references)], evidence);

        var rows = ReadCsv(UsageMappingCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.UsageMapping,
            ["Object", "ReportPath", "Page", "PageId", "Visual", "VisualId", "VisualType", "UsageContext", "UsageRole", "UserFacing", "EvidenceCount", "ArtifactPaths", "EvidencePaths", "SemanticUsage", "ClassificationConfidence"])));
        var header = rows[0];

        Assert.Equal(8, rows.Count);
        Assert.Equal("Yes", Value(RowFor(rows, header, "Projection"), header, "UserFacing"));
        Assert.Equal("Yes", Value(RowFor(rows, header, "Tooltip"), header, "UserFacing"));
        Assert.Equal("No", Value(RowFor(rows, header, "Filter"), header, "UserFacing"));
        Assert.Equal("No", Value(RowFor(rows, header, "Sort"), header, "UserFacing"));
        Assert.Equal("Yes", Value(RowFor(rows, header, "Formatting"), header, "UserFacing"));
        Assert.Equal("Unclear", Value(RowFor(rows, header, "Other"), header, "UserFacing"));
        var drillthrough = RowFor(rows, header, "Drillthrough");
        Assert.Equal("", Value(drillthrough, header, "VisualId"));
        Assert.Equal("", Value(drillthrough, header, "Visual"));
        Assert.Equal("", Value(drillthrough, header, "VisualType"));
        Assert.Equal("One.Report", Value(drillthrough, header, "ReportPath"));
        Assert.Equal("p1", Value(drillthrough, header, "PageId"));
        Assert.Equal(pagePath, Value(drillthrough, header, "ArtifactPaths"));
        Assert.Equal("$.pageBinding", Value(drillthrough, header, "EvidencePaths"));
        Assert.Equal("1", Value(drillthrough, header, "EvidenceCount"));
        Assert.Equal("Card", Value(RowFor(rows, header, "Projection"), header, "Visual"));
    }

    [Fact]
    public void LogicalUsageCollapsesEvidencePathsWithoutChangingMachineLocationCounts()
    {
        var firstPath = "One.Report/definition/pages/p1/visuals/v1/visual.json";
        var secondPath = "Two.Report/definition/pages/p2/visuals/v2/visual.json";
        var references = new[] { Reference("Repeated", UsageContexts.Filter, "filter", "$.filter.field") };
        var inventory = Inventory(
            [
                Report("Report", "One.Report", "p1", "Overview", "v1", references),
                Report("Report", "Two.Report", "p2", "Overview", "v2", references),
            ],
            [
                Usage("Repeated",
                [
                    Evidence("Report", "p1", "v1", firstPath, UsageContexts.Filter, "filter", "$.filter.field"),
                    Evidence("Report", "p1", "v1", firstPath, UsageContexts.Filter, "filter", "$.filter.from"),
                    Evidence("Report", "p1", "v1", firstPath, UsageContexts.Filter, "filter", "$.filter.where"),
                    Evidence("Report", "p1", "v1", firstPath, UsageContexts.Filter, "alternateFilter", "$.filter.alternate"),
                    Evidence("Report", "p1", "v1", firstPath, UsageContexts.Projection, "Values", "$.query.values"),
                    Evidence("Report", "p2", "v2", secondPath, UsageContexts.Filter, "filter", "$.filter.field"),
                ]),
            ]);

        var mappingDefault = ReadCsv(UsageMappingCsvRenderer.Render(inventory));
        var mappingAdvanced = ReadCsv(UsageMappingCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.UsageMapping,
            ["Object", "ReportPath", "PageId", "Visual", "VisualId", "UsageContext", "UsageRole", "UserFacing", "EvidenceCount", "ArtifactPaths", "EvidencePaths"])));
        var catalogue = ReadCsv(DataCatalogueCsvRenderer.Render(inventory));
        var mappingHeader = mappingAdvanced[0];
        var filter = Assert.Single(mappingAdvanced.Skip(1), row =>
            Value(row, mappingHeader, "ReportPath") == "One.Report" && Value(row, mappingHeader, "UsageContext") == UsageContexts.Filter &&
            Value(row, mappingHeader, "UsageRole") == "filter");

        Assert.Equal(4, mappingDefault.Count - 1);
        Assert.Equal(mappingDefault.Count, mappingAdvanced.Count);
        Assert.Equal("No", Value(filter, mappingHeader, "UserFacing"));
        Assert.Equal("3", Value(filter, mappingHeader, "EvidenceCount"));
        Assert.Equal(firstPath, Value(filter, mappingHeader, "ArtifactPaths"));
        Assert.Equal("$.filter.field | $.filter.from | $.filter.where", Value(filter, mappingHeader, "EvidencePaths"));
        Assert.Equal("Card", Value(filter, mappingHeader, "Visual"));
        Assert.Equal("4", Value(RowFor(catalogue, catalogue[0], "Repeated"), catalogue[0], "DirectUsageCount"));
        Assert.Equal("2", Value(RowFor(catalogue, catalogue[0], "Repeated"), catalogue[0], "ReportCount"));
        Assert.Equal("2", Value(RowFor(catalogue, catalogue[0], "Repeated"), catalogue[0], "PageCount"));
        Assert.Equal("2", Value(RowFor(catalogue, catalogue[0], "Repeated"), catalogue[0], "VisualCount"));
    }

    [Fact]
    public void LogicalUsageRejectsConflictingUserFacingEvidenceInsteadOfChoosingOne()
    {
        var visualPath = "One.Report/definition/pages/p1/visuals/v1/visual.json";
        var active = Reference("Formatting", UsageContexts.Formatting, null, "$.objects.active") with
        {
            ReferenceOrigin = VisualReferenceOrigins.FormattingPropertyExpression,
            ReferenceRelevance = VisualReferenceRelevance.Active,
        };
        var selector = Reference("Formatting", UsageContexts.Formatting, null, "$.objects.selector") with
        {
            ReferenceOrigin = VisualReferenceOrigins.FormattingSelectorIdentity,
            ReferenceRelevance = VisualReferenceRelevance.Active,
        };
        var inventory = Inventory([Report("Report", "One.Report", "p1", "Overview", "v1", [active, selector])],
            [Usage("Formatting", [
                Evidence("Report", "p1", "v1", visualPath, UsageContexts.Formatting, null, active.EvidencePath),
                Evidence("Report", "p1", "v1", visualPath, UsageContexts.Formatting, null, selector.EvidencePath),
            ])]);

        var error = Assert.Throws<InvalidOperationException>(() => UsageMappingCsvRenderer.Render(inventory));

        Assert.Contains("conflicting UserFacing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidExportColumnSelectionsAndKeepsLegacyOutputByteCompatible()
    {
        var inventory = Inventory([], [new SemanticObjectUsage("Model", "Fact", "=Formula,\"quoted\"\n", SemanticObjectTypes.Measure, null, [], SemanticUsageStates.ApparentlyUnused)]);

        Assert.Throws<ArgumentException>(() => ExportCsvRenderer.Render(inventory,
            new ExportRequest(ExportPreset.DataCatalogue, ["Object", "Object"])));
        Assert.Throws<ArgumentException>(() => ExportCsvRenderer.Render(inventory,
            new ExportRequest(ExportPreset.DataCatalogue, ["NotAColumn"])));
        Assert.Throws<ArgumentException>(() => ExportCsvRenderer.Render(inventory,
            new ExportRequest(ExportPreset.DataCatalogue, [])));

        var catalogue = DataCatalogueCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.DataCatalogue, ["Object"]));
        Assert.Equal("Object\r\n\"'=Formula,\"\"quoted\"\"\n\"\r\n", catalogue);
        Assert.Equal(
            "Report,Table,Object,ObjectType,SemanticUsage,SemanticReason,ReportLocationCount,ReportLocations,PowerQueryUsed,PowerQueryConsumers,PowerQueryRoles,PowerQueryEvidence,ReviewCandidate\r\n" +
            "Model,Fact,\"'=Formula,\"\"quoted\"\"\n\",Measure,Apparently unused,,0,,No,,,,Yes\r\n",
            SemanticUsageCsvRenderer.Render(inventory));
    }

    private static ProjectInventory Inventory(
        IReadOnlyList<ReportInventory> reports,
        IReadOnlyList<SemanticObjectUsage> usages,
        IReadOnlyList<SemanticModelInventory>? models = null,
        IReadOnlyList<SemanticDependencyEdge>? dependencies = null) =>
        new("0.26", "test", DateTimeOffset.UnixEpoch, [], reports, models ?? [], usages, [], dependencies ?? [], [], [], [], [], [], [], [], []);

    private static SemanticModelInventory ModelWithGeneratedTable() => new("Model", "Model.SemanticModel",
        [new SemanticTableInventory("LocalDate", "definition/tables/LocalDate.tmdl", true, false, true,
            SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable, [], [], [], [], null, null)], [], []);

    private static ReportInventory Report(string name, string path, string pageId, string pageName, string visualId, IReadOnlyList<VisualFieldReference> references)
    {
        var visual = new VisualInventory(visualId, "card", $"{path}/definition/pages/{pageId}/visuals/{visualId}/visual.json", null, false, null,
            new VisualPosition(null, null, null, null, null, null), new VisualAccessibilityInventory(false, null, false, null, false, null, false),
            null, false, references, [], []);
        var page = new PageInventory(pageId, pageName, $"{path}/definition/pages/{pageId}", $"{path}/definition/pages/{pageId}/page.json", null, null, null,
            null, false, null, null, null, null, [], [], [], [], [visual]);
        return new ReportInventory(name, path, new ReportModelConnectionInventory("definition.pbir", null, null, ReportModelConnectionKinds.ByPath,
            "../Model.SemanticModel", "Model.SemanticModel", "Model", true), $"{path}/definition/report.json", null, null, null, null,
            [page], [], [], null, null, [], null, [], []);
    }

    private static SemanticObjectUsage Usage(string objectName, IReadOnlyList<SemanticUsageEvidence> evidence) =>
        new("Model", "Fact", objectName, SemanticObjectTypes.Measure, null, evidence, SemanticUsageStates.DirectlyUsed);

    private static SemanticUsageEvidence Evidence(string report, string? page, string? visual, string artifactPath, string context, string? role, string evidencePath) =>
        new(report, page, visual, artifactPath, context, role, evidencePath);

    private static VisualFieldReference Reference(string objectName, string context, string? role, string evidencePath) =>
        new("Fact", objectName, SemanticObjectTypes.Measure, null, context, role, evidencePath)
        {
            ReferenceOrigin = VisualReferenceOrigins.Binding,
            ReferenceRelevance = VisualReferenceRelevance.Active,
        };

    private static string[] RowFor(IReadOnlyList<string[]> rows, string[] header, string objectName) =>
        Assert.Single(rows.Skip(1), row => Value(row, header, "Object") == objectName);

    private static string Value(string[] row, string[] header, string name) => row[Array.IndexOf(header, name)];

    private static List<string[]> ReadCsv(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (quoted && index + 1 < csv.Length && csv[index + 1] == '"') { field.Append('"'); index++; }
                else { quoted = !quoted; }
            }
            else if (character == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if (character == '\r' && !quoted && index + 1 < csv.Length && csv[index + 1] == '\n')
            {
                row.Add(field.ToString()); rows.Add(row.ToArray()); row.Clear(); field.Clear(); index++;
            }
            else { field.Append(character); }
        }

        return rows;
    }
}
