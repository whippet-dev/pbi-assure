using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting.Viewer;

public static partial class ViewerSnapshotProjector
{
    private sealed partial class Builder
    {
        private ViewerModel ProjectModel(SemanticModelInventory m) => new(ModelId(m.Name), m.Name, m.RelativePath,
            new(m.TableCount, m.ColumnCount, m.MeasureCount, m.HierarchyCount, m.HierarchyLevelCount, m.CalculationItemCount,
                m.RelationshipCount, m.RoleCount, m.TablePermissionCount, m.Roles.Sum(r => r.ObjectLevelPermissionCount),
                inventory.SemanticObjectUsages.Count(o => Same(o.SemanticModel, m.Name)),
                inventory.SemanticObjectUsages.Count(o => Same(o.SemanticModel, m.Name) && o.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation),
                inventory.PowerQueryUsages.Count(q => Same(q.SemanticModel, m.Name))),
            m.Tables.Select(t => new ViewerTable(TableId(m.Name, t.Name), t.Name, t.RelativePath,
                t.DefinitionPaths.ToArray(), t.Description, t.IsHidden, t.IsPrivate, t.IsSystemGenerated, t.SystemGeneratedKind,
                inventory.SemanticTableUsages.FirstOrDefault(u => Same(u.SemanticModel, m.Name) && Same(u.Table, t.Name))?.UsageState,
                inventory.SemanticObjectUsages.Where(o => Same(o.SemanticModel, m.Name) && Same(o.Table, t.Name)).Select(ObjectId).ToArray(),
                t.Partitions.Select(p => new ViewerPartition(p.Name, p.SourceType, p.Mode, p.Expression, p.EntityName, p.ExpressionSource,
                    Reference(inventory.PowerQueryUsages.Where(q => Same(q.SemanticModel, m.Name) && Same(q.Table, t.Name) &&
                        Same(q.Partition, p.Name) && q.SourceKind == PowerQuerySourceKinds.TablePartition).Select(QueryId)))).ToArray(),
                t.Hierarchies, t.FieldParameter,
                t.CalculationGroup is { } g ? new(g.Precedence, g.SelectionExpression, g.MultipleOrEmptySelectionExpression,
                    g.NoSelectionExpression, g.NoSelectionFormatStringExpression, g.MultipleOrEmptySelectionFormatStringExpression) : null,
                t.RefreshPolicy, t.UnanalyzedDependencyConstructs)).ToArray(),
            m.Functions.Select(f => new ViewerFunction(SemanticId(m.Name, "", f.Name, SemanticObjectTypes.Function), f)).ToArray(),
            m.Perspectives.Select(p => new ViewerPerspective(SemanticId(m.Name, "", p.Name, SemanticObjectTypes.Perspective), p)).ToArray());

        private ViewerRelationship ProjectRelationship(SemanticModelInventory m, SemanticRelationshipInventory r) => new(
            SemanticId(m.Name, "", r.Name, SemanticObjectTypes.Relationship), ModelId(m.Name), r,
            Reference(SemanticId(m.Name, r.FromTable, r.FromColumn, SemanticObjectTypes.Column)),
            Reference(SemanticId(m.Name, r.ToTable, r.ToColumn, SemanticObjectTypes.Column)),
            (r.Activation?.Sources ?? []).Select(s => new ViewerActivationSource(s,
                // The activation record has no report owner. Keep multiple report-owned candidates ambiguous.
                Reference(nodes.Values.Where(n => n.ModelId == ModelId(m.Name) && Same(n.Name, s.ObjectName) &&
                    Same(n.ObjectType, s.ObjectType) && n.TableId == (string.IsNullOrEmpty(s.Table) ? null : TableId(m.Name, s.Table))).Select(n => n.Id)))).ToArray());

        private ViewerRole ProjectRole(SemanticModelInventory m, SemanticRoleInventory r) => new(
            SemanticId(m.Name, "", r.Name, SemanticObjectTypes.Role), ModelId(m.Name), r.Name, r.ModelPermission,
            r.RelativePath, r.UnanalyzedConstructs,
            r.TablePermissions.Select(p => new ViewerPermissionFact(p, Reference(TableId(m.Name, p.Table)),
                p.ColumnPermissions.Select(c => new ViewerColumnPermissionFact(c,
                    Reference(SemanticId(m.Name, p.Table, c.Column, SemanticObjectTypes.Column)))).ToArray())).ToArray());

        private ViewerQuery ProjectQuery(PowerQueryUsage q) => new(QueryId(q), ModelId(q.SemanticModel),
            q.Table is null ? Reference() : Reference(TableId(q.SemanticModel, q.Table)), q,
            q.IsParameter, q.ParameterType, q.IsParameterRequired,
            q.RefreshPolicyTables.Select(t => Reference(TableId(q.SemanticModel, t))).ToArray());

        private (string[] Ids, string[] Unmapped) MapFields(string? model, IEnumerable<string> keys)
        {
            var ids = new List<string>();
            var unmapped = new List<string>();
            foreach (var key in keys.Order(StringComparer.Ordinal))
            {
                var matches = inventory.SemanticObjectUsages.Where(o => Same(o.SemanticModel, model) &&
                    Same(FieldIdentity.Create(o.Table, o.ObjectName, o.ObjectType, o.HierarchyName), key)).Select(ObjectId).ToArray();
                if (matches.Length == 0) unmapped.Add(key);
                else ids.AddRange(matches);
            }
            return (ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), unmapped.ToArray());
        }

        private ViewerUnresolvedDependency ProjectUnresolved(UnresolvedSemanticDependency u)
        {
            var candidates = MapFields(u.SemanticModel, u.CandidateTargets ?? Enumerable.Empty<string>());
            return new(EvidenceId("unresolved", u.SemanticModel, u.FromTable, u.FromObjectName, u.FromObjectType,
                    u.FromHierarchyName, u.FromReport, u.DependencyKind, u.EvidencePath, u.ReferenceText),
                u.FromObjectType == SemanticObjectTypes.ReportMeasure && u.FromReport is null ? Reference() :
                    Reference(SemanticId(u.SemanticModel, u.FromTable, u.FromObjectName, u.FromObjectType, u.FromHierarchyName, u.FromReport)),
                u.DependencyKind, u.ReferenceText, u.Reason, u.EvidencePath, u.ResolutionOutcome,
                u.CandidateTargets is not null, candidates.Ids, candidates.Unmapped);
        }

        private ViewerLimitation ProjectLimitation(AnalysisLimitation l)
        {
            var reach = MapFields(l.SemanticModel, l.Reach ?? Enumerable.Empty<string>());
            return new(limitationIds[l], l, Context(l.SemanticModel, l.Table, l.ObjectName, l.ArtifactPath),
                l.Reach is null ? "Unknown" : "Bounded", l.Reach is null ? null : reach.Ids, reach.Unmapped);
        }

        private ViewerFinding ProjectFinding(AssuranceFinding f)
        {
            var id = EvidenceId("finding", f.RuleId, f.RuleVersion, f.Report, f.Page, f.Visual, f.VisualGroup,
                f.SemanticModel, f.Table, f.ObjectName, f.ArtifactPath, f.Message, string.Join("\n", f.EvidencePaths));
            Add(new(id, "Finding", f.Message, AvailableModelId(f.SemanticModel)));
            var affected = f.Report is not null
                ? Location(f.Report, f.Page, f.Visual, f.ArtifactPath, f.VisualGroup)
                : Context(f.SemanticModel, f.Table, f.ObjectName, f.ArtifactPath);
            return new(id, f, f.ReferenceContexts.ToArray(), affected, FindingDefinitionContext(f));
        }

        private ViewerReference FindingDefinitionContext(AssuranceFinding finding)
        {
            var bookmarks = inventory.Reports.SelectMany(r => r.Bookmarks
                .Where(b => Same(PathKey(b.RelativePath), PathKey(finding.ArtifactPath)))
                .Select(b => BookmarkId(r, b.Name))).ToArray();
            if (bookmarks.Length > 0) return Reference(bookmarks);
            // A missing referenced page/visual does not remove its known owning report context.
            if (finding.Report is not null) return Location(finding.Report, null, null, finding.ArtifactPath);
            return Context(finding.SemanticModel, finding.Table, finding.ObjectName, finding.ArtifactPath);
        }

        // Resolve only recorded coordinates. Candidate ambiguity stays explicit; no rule-ID heuristics.
        // For limitations this is the definition context, never an assertion about affected objects.
        private ViewerReference Context(string? model, string? table, string? name, string artifact)
        {
            if (model is null) return Location(null, null, null, artifact);
            if (name is not null)
            {
                var candidates = new List<string>();
                candidates.AddRange(inventory.SemanticObjectUsages.Where(o => Same(o.SemanticModel, model) &&
                    (table is null || Same(o.Table, table)) && Same(o.ObjectName, name)).Select(ObjectId));
                candidates.AddRange(inventory.PowerQueryUsages.Where(q => Same(q.SemanticModel, model) && Same(q.QueryName, name) &&
                    (table is null || Same(q.Table, table)) && Same(PathKey(q.ArtifactPath), PathKey(artifact))).Select(QueryId));
                candidates.AddRange(inventory.SemanticModels.Where(m => Same(m.Name, model)).SelectMany(m => m.Roles)
                    .Where(r => Same(r.Name, name) && Same(PathKey(r.RelativePath), PathKey(artifact)))
                    .Select(r => SemanticId(model, "", r.Name, SemanticObjectTypes.Role)));
                candidates.AddRange(inventory.SemanticModels.Where(m => Same(m.Name, model)).SelectMany(m => m.Relationships)
                    .Where(r => Same(r.Name, name)).Select(r => SemanticId(model, "", r.Name, SemanticObjectTypes.Relationship)));
                candidates.AddRange(inventory.SemanticModels.Where(m => Same(m.Name, model)).SelectMany(m => m.Functions)
                    .Where(f => Same(f.Name, name) && Same(PathKey(f.RelativePath), PathKey(artifact)))
                    .Select(f => SemanticId(model, "", f.Name, SemanticObjectTypes.Function)));
                return Reference(candidates);
            }
            if (table is not null) return Reference(TableId(model, table));
            return Reference(ModelId(model));
        }
    }
}
