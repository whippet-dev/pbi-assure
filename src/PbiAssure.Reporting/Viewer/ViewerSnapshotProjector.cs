using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting.Viewer;

/// <summary>Projects recorded analysis facts. Does not parse, bind, classify or compute reachability.</summary>
public static partial class ViewerSnapshotProjector
{
    public const string ContractVersion = "1.0";

    public static ViewerSnapshot Project(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return new Builder(inventory).Build();
    }

    private sealed partial class Builder
    {
        private readonly ProjectInventory inventory;
        private readonly SemanticGraphIndex graph;
        private readonly DirectUsageProvenanceAnalysis provenance;
        private readonly Dictionary<string, ViewerTarget> targets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ViewerNode> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<AnalysisLimitation, string> limitationIds = new();
        private readonly List<ViewerUsage> usages = [];

        public Builder(ProjectInventory inventory)
        {
            this.inventory = inventory;
            graph = SemanticGraphIndex.For(inventory);
            provenance = DirectUsageProvenanceAnalyzer.Analyze(inventory);
            RegisterTargets();
        }

        // Semantic keys are Core's keys. Encoding makes them opaque, JSON/DOM-safe presentation IDs.
        private static string Encode(string kind, string key) => kind + ":" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(key.ToUpperInvariant()))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static string Id(string kind, params string?[] parts) => Encode(kind, JsonSerializer.Serialize(parts));
        private static string EvidenceId(string kind, params string?[] parts) => kind + ":" +
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
        private static string PathKey(string path) => SemanticNodeIdentity.ReportOwner(path);
        private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static string ModelId(string name) => Id("model", name);
        private string? AvailableModelId(string? name) => name is not null && targets.ContainsKey(ModelId(name)) ? ModelId(name) : null;
        private static string SemanticId(string model, string table, string name, string type,
            string? hierarchy = null, string? report = null) => Encode("semantic",
                SemanticNodeIdentity.Create(model, table, name, type, hierarchy, report is null ? null : PathKey(report)));
        private static string ObjectId(SemanticObjectUsage o) => SemanticId(o.SemanticModel, o.Table, o.ObjectName, o.ObjectType, o.HierarchyName);
        private static string TableId(string model, string table) => SemanticId(model, table, table, SemanticObjectTypes.Table);
        private static string ReportId(ReportInventory report) => Id("report", PathKey(report.RelativePath));
        private static string PageId(ReportInventory report, string page) => Id("page", PathKey(report.RelativePath), page);
        private static string VisualId(ReportInventory report, string page, string visual) => Id("visual", PathKey(report.RelativePath), page, visual);
        private static string GroupId(ReportInventory report, string page, string group) => Id("group", PathKey(report.RelativePath), page, group);
        private static string BookmarkId(ReportInventory report, string bookmark) => Id("bookmark", PathKey(report.RelativePath), bookmark);
        private static string QueryId(PowerQueryUsage q) => Id("query", q.SemanticModel, q.QueryName, q.SourceKind, q.Table, q.Partition);
        private static string DependencyId(SemanticDependencyEdge e) => EvidenceId("dependency",
            SemanticGraphIndex.SourceKey(e), SemanticGraphIndex.TargetKey(e), e.DependencyKind, e.EvidencePath, e.EvidenceText);
        private static string ReportMeasureId(ReportInventory r, SemanticModelInventory? m, ReportMeasureInventory measure) =>
            m is null ? Id("unbound-report-measure", PathKey(r.RelativePath), measure.Entity, measure.Name) :
                SemanticId(m.Name, measure.Entity, measure.Name, SemanticObjectTypes.ReportMeasure, report: r.RelativePath);

        private ViewerReference Reference(IEnumerable<string> candidates)
        {
            var ids = candidates.Where(targets.ContainsKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            return new(ids.Length switch { 0 => "Unavailable", 1 => "Resolved", _ => "Ambiguous" }, ids);
        }
        private ViewerReference Reference(params string[] ids) => Reference((IEnumerable<string>)ids);
        private void Add(ViewerTarget target) => targets.TryAdd(target.Id, target);

        private void RegisterNode(string model, string table, string name, string type, string? hierarchy = null, string? report = null)
        {
            // Never give an unowned report-measure row a synthetic model-global identity.
            if (type == SemanticObjectTypes.ReportMeasure && report is null) return;
            var id = SemanticId(model, table, name, type, hierarchy, report);
            var reportId = report is null ? null : Id("report", PathKey(report));
            var tableId = string.IsNullOrEmpty(table) ? null : TableId(model, table);
            if (tableId is not null && !targets.ContainsKey(tableId)) tableId = null;
            var reach = graph.ReachabilityOfNode(SemanticNodeIdentity.Create(model, table, name, type, hierarchy,
                report is null ? null : PathKey(report)));
            nodes.TryAdd(id, new(id, ModelId(model), tableId, name, type, hierarchy, reportId,
                reach?.ReachableFromReport, reach?.ReachableFromModelStructure));
            Add(new(id, type, name, ModelId(model), tableId, reportId));
        }

        private void RegisterTargets()
        {
            foreach (var model in inventory.SemanticModels)
            {
                Add(new(ModelId(model.Name), "Model", model.Name));
                foreach (var table in model.Tables)
                {
                    Add(new(TableId(model.Name, table.Name), "Table", table.Name, ModelId(model.Name)));
                    RegisterNode(model.Name, table.Name, table.Name, SemanticObjectTypes.Table);
                    if (table.RefreshPolicy is not null)
                        RegisterNode(model.Name, table.Name, table.Name, SemanticObjectTypes.RefreshPolicy);
                }
                foreach (var r in model.Relationships) RegisterNode(model.Name, "", r.Name, SemanticObjectTypes.Relationship);
                foreach (var r in model.Roles) RegisterNode(model.Name, "", r.Name, SemanticObjectTypes.Role);
                foreach (var f in model.Functions) RegisterNode(model.Name, "", f.Name, SemanticObjectTypes.Function);
                foreach (var p in model.Perspectives) RegisterNode(model.Name, "", p.Name, SemanticObjectTypes.Perspective);
            }
            foreach (var o in inventory.SemanticObjectUsages)
                RegisterNode(o.SemanticModel, o.Table, o.ObjectName, o.ObjectType, o.HierarchyName);
            foreach (var r in inventory.Reports)
            {
                var model = ReportModelBinder.FindLocalModel(r, inventory.SemanticModels);
                var modelId = model is null ? null : ModelId(model.Name);
                Add(new(ReportId(r), "Report", r.Name, modelId));
                foreach (var b in r.Bookmarks) Add(new(BookmarkId(r, b.Name), "Bookmark", b.DisplayName, modelId, ReportId: ReportId(r)));
                foreach (var p in r.Pages)
                {
                    Add(new(PageId(r, p.Name), "Page", p.DisplayName, modelId, ReportId: ReportId(r)));
                    foreach (var v in p.Visuals)
                        Add(new(VisualId(r, p.Name, v.Name), "Visual", VisualPresentation.DisplayName(v), modelId,
                            ReportId: ReportId(r), PageId: PageId(r, p.Name)));
                    foreach (var g in p.VisualGroups)
                        Add(new(GroupId(r, p.Name, g.Name), "VisualGroup", g.DisplayName ?? g.Name, modelId,
                            ReportId: ReportId(r), PageId: PageId(r, p.Name)));
                }
                foreach (var measure in r.ReportMeasures)
                {
                    if (model is not null) RegisterNode(model.Name, measure.Entity, measure.Name, SemanticObjectTypes.ReportMeasure, report: r.RelativePath);
                    else Add(new(ReportMeasureId(r, null, measure), "ReportMeasure", measure.Name, ReportId: ReportId(r)));
                }
            }
            foreach (var n in graph.Nodes) RegisterNode(n.SemanticModel, n.Table, n.ObjectName, n.ObjectType, n.HierarchyName, n.Report);
            foreach (var e in inventory.SemanticDependencies.Where(SemanticGraphIndex.HasConsistentOwnership))
            {
                RegisterNode(e.SemanticModel, e.FromTable, e.FromObjectName, e.FromObjectType, e.FromHierarchyName, e.FromReport);
                RegisterNode(e.SemanticModel, e.ToTable, e.ToObjectName, e.ToObjectType, e.ToHierarchyName, e.ToReport);
            }
            foreach (var q in inventory.PowerQueryUsages)
                Add(new(QueryId(q), "Query", q.QueryName, ModelId(q.SemanticModel),
                    q.Table is null ? null : TableId(q.SemanticModel, q.Table)));
            foreach (var l in inventory.AnalysisLimitations)
            {
                var id = EvidenceId("limitation", l.LimitationId, l.Scope, l.SemanticModel, l.Table, l.ObjectName,
                    PathKey(l.ArtifactPath), l.EvidencePath, l.Cause, l.ConstructType, l.DependencyImpact, l.Reason);
                limitationIds[l] = id;
                Add(new(id, "Limitation", l.Reason, AvailableModelId(l.SemanticModel)));
            }
        }

        public ViewerSnapshot Build()
        {
            var objects = inventory.SemanticObjectUsages.Select(ProjectObject).ToArray();
            var measures = ProjectReportMeasures();
            var reports = inventory.Reports.Select(ProjectReport).ToArray();
            var findings = inventory.Findings.Select(ProjectFinding).ToArray();
            var limitations = inventory.AnalysisLimitations.Select(ProjectLimitation).ToArray();
            var qualifications = inventory.SemanticObjectUsages.SelectMany(o =>
                SemanticUsageConfidenceQualifier.Qualifying(o, inventory.AnalysisLimitations).Select(l =>
                    new ViewerQualification(ObjectId(o), limitationIds[l], l.Reach is null ? "ConservativeModelScope" : "BoundedReach"))).ToArray();
            var dependencies = inventory.SemanticDependencies.Select(e => new ViewerDependency(DependencyId(e),
                SemanticGraphIndex.HasConsistentOwnership(e) ? Reference(Encode("semantic", SemanticGraphIndex.SourceKey(e))) : Reference(),
                SemanticGraphIndex.HasConsistentOwnership(e) ? Reference(Encode("semantic", SemanticGraphIndex.TargetKey(e))) : Reference(),
                e.DependencyKind, e.EvidencePath, e.EvidenceText, e.StructuralProvenance)).ToArray();
            var models = inventory.SemanticModels.Select(ProjectModel).ToArray();
            return new(ContractVersion, inventory.SchemaVersion,
                inventory.RootPath.Replace('\\', '/').TrimEnd('/').Split('/').Last(), inventory.ScannedAtUtc,
                new(inventory.ReportCount, inventory.SemanticModelCount, inventory.Reports.Count, inventory.SemanticModels.Count,
                    inventory.PageCount, inventory.VisualCount, objects.Length, inventory.DeveloperSemanticObjectCount,
                    inventory.DeveloperApparentlyUnusedSemanticObjectCount,
                    objects.Count(o => o.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation),
                    limitations.Length, inventory.PowerQueryCount, findings.Length,
                    objects.GroupBy(o => o.UsageState).ToDictionary(g => g.Key, g => g.Count()),
                    objects.Where(o => !o.IsSystemGenerated).GroupBy(o => o.UsageState).ToDictionary(g => g.Key, g => g.Count())),
                inventory.Artifacts.ToArray(), targets.Values.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray(), models, objects,
                nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray(), dependencies,
                inventory.UnresolvedSemanticDependencies.Select(ProjectUnresolved).ToArray(), reports, measures, usages.ToArray(),
                inventory.SemanticModels.SelectMany(m => m.Relationships.Select(r => ProjectRelationship(m, r))).ToArray(),
                inventory.SemanticModels.SelectMany(m => m.Roles.Select(r => ProjectRole(m, r))).ToArray(),
                inventory.PowerQueryUsages.Select(ProjectQuery).ToArray(),
                inventory.PowerQueryDependencies.Select(e => new ViewerQueryDependency(e,
                    Queries(e.SemanticModel, e.FromQueryName, e.FromSourceKind, e.FromTable, e.FromPartition, exact: true),
                    Queries(e.SemanticModel, e.ToQueryName, e.ToSourceKind), "QueryNameAndSourceKindOnly")).ToArray(),
                inventory.DataSources.Select(s => new ViewerDataSource(s,
                    Queries(s.SemanticModel, s.QueryName, s.QuerySourceKind, s.Table, s.Partition, exact: true))).ToArray(),
                inventory.PowerQueryColumnUsages.Select(c => new ViewerQueryColumnUsage(c,
                    Queries(c.SemanticModel, c.SourceQuery, table: c.SourceTable, partition: c.SourcePartition, exact: true),
                    Queries(c.SemanticModel, c.ConsumerQuery, table: c.ConsumerTable, partition: c.ConsumerPartition, exact: true),
                    Reference(SemanticId(c.SemanticModel, c.SourceTable, c.SourceColumn, SemanticObjectTypes.Column)))).ToArray(),
                inventory.SemanticTablePowerQueryContexts.Select(c => new ViewerTableQueryContext(c,
                    Reference(TableId(c.SemanticModel, c.Table)),
                    Queries(c.SemanticModel, c.QueryName, table: c.Table, partition: c.Partition, exact: true))).ToArray(),
                findings, limitations, qualifications,
                inventory.UnresolvedSemanticReferences.Select(r => new ViewerUnresolvedReportReference(r,
                    Location(r.Report, r.Page, r.Visual, r.ArtifactPath), r.ReferenceOrigin, r.ReferenceRelevance,
                    r.FormattingObject, r.FormattingProperty, r.SelectorKind, r.MatchedProjectionQueryRef)).ToArray());
        }

        private ViewerReference Queries(string model, string name, string? kind = null, string? table = null,
            string? partition = null, bool exact = false) => Reference(inventory.PowerQueryUsages.Where(q =>
                Same(q.SemanticModel, model) && Same(q.QueryName, name) && (kind is null || Same(q.SourceKind, kind)) &&
                (!exact || (Same(q.Table, table) && Same(q.Partition, partition)))).Select(QueryId));

        private ViewerObject ProjectObject(SemanticObjectUsage o)
        {
            var table = inventory.SemanticModels.FirstOrDefault(m => Same(m.Name, o.SemanticModel))?.Tables.FirstOrDefault(t => Same(t.Name, o.Table));
            var column = o.ObjectType == SemanticObjectTypes.Column ? table?.Columns.FirstOrDefault(c => Same(c.Name, o.ObjectName)) : null;
            var measure = o.ObjectType == SemanticObjectTypes.Measure ? table?.Measures.FirstOrDefault(m => Same(m.Name, o.ObjectName)) : null;
            var level = o.ObjectType == SemanticObjectTypes.HierarchyLevel ? table?.Hierarchies.FirstOrDefault(h => Same(h.Name, o.HierarchyName))?.Levels.FirstOrDefault(l => Same(l.Name, o.ObjectName)) : null;
            var item = o.ObjectType == SemanticObjectTypes.CalculationItem ? table?.CalculationGroup?.Items.FirstOrDefault(i => Same(i.Name, o.ObjectName)) : null;
            var summary = provenance.ObjectSummaries.FirstOrDefault(s => Same(s.SemanticModel, o.SemanticModel) &&
                Same(s.Table, o.Table) && Same(s.ObjectName, o.ObjectName) && Same(s.ObjectType, o.ObjectType));
            var reason = SemanticUsagePresentation.ExplainReason(inventory, o);
            foreach (var evidence in o.DirectReportReferences)
            {
                var direct = provenance.Usages.FirstOrDefault(u => Same(u.SemanticModel, o.SemanticModel) &&
                    Same(u.Table, o.Table) && Same(u.ObjectName, o.ObjectName) && Same(u.ObjectType, o.ObjectType) &&
                    Same(u.ArtifactPath, evidence.ArtifactPath) && u.EvidencePath == evidence.EvidencePath &&
                    u.UsageContext == evidence.UsageContext && u.UsageRole == evidence.Role);
                usages.Add(new(ObjectId(o), Location(evidence.Report, evidence.Page, evidence.Visual, evidence.ArtifactPath),
                    evidence, evidence.IsHiddenProjection, direct?.UserFacing));
            }
            return new(ObjectId(o), ModelId(o.SemanticModel), TableId(o.SemanticModel, o.Table), o.ObjectName,
                o.ObjectType, o.HierarchyName, o.UsageState, o.ClassificationConfidence, summary?.UserFacing,
                inventory.IsSystemGeneratedSemanticObject(o),
                o.UsageState == SemanticUsageStates.ApparentlyUnused && !inventory.IsSystemGeneratedSemanticObject(o),
                o.StructuralRequirementProvenance, reason?.Text, reason?.Dependency is null ? null : DependencyId(reason.Dependency),
                new(column?.Description ?? measure?.Description, column, measure, level, item,
                    measure?.FormatStringExpression, measure?.DetailRowsDefinitionExpression),
                o.DirectReportReferences.Count, o.DirectReportLocations.Count);
        }
    }
}
