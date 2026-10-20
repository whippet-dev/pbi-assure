import { edgeId } from "./data.js";

export const MODEL_AREA_DEFAULTS = {
  relationshipModel: "",
  relationshipSearch: "",
  relationshipActive: "",
  relationshipDirection: "",
  relationshipSelected: null,
  relationshipTab: "Summary",
  securityModel: "",
  securitySearch: "",
  securityKind: "",
  securitySelected: null,
  securityTab: "Permissions",
};

export const path = (value) => String(value || "").replaceAll("\\", "/");
export function buildModelAreas(data) {
  return {
    data,
    relationships: data.raw.SemanticModels.flatMap((model) =>
      model.Relationships.map((relationship) => ({
        id: relationship.Id,
        model,
        relationship,
      })),
    ),
    roles: data.raw.SemanticModels.flatMap((model) =>
      model.Roles.map((role) => ({ id: role.Id, model, role })),
    ),
  };
}

export function modelObject(data, reference) {
  return reference?.Status === "Resolved"
    ? data.byId.get(reference.TargetIds[0])
    : null;
}

export function relationshipRows(index, state) {
  const search = state.relationshipSearch.trim().toLowerCase();
  return index.relationships.filter(
    ({ model, relationship: r }) =>
      (!state.relationshipModel || model.Name === state.relationshipModel) &&
      (!state.relationshipActive ||
        (state.relationshipActive === "active") === r.IsActive) &&
      (!state.relationshipDirection ||
        r.CrossFilteringBehavior === state.relationshipDirection) &&
      (!search ||
        [r.Name, r.FromTable, r.FromColumn, r.ToTable, r.ToColumn, model.Name]
          .join(" ")
          .toLowerCase()
          .includes(search)),
  );
}
export function roleCounts(role) {
  return {
    filters: role.TablePermissions.filter((p) => p.FilterExpression.trim())
      .length,
    permissions: role.TablePermissions.reduce(
      (n, p) =>
        n +
        (p.MetadataPermission?.trim() ? 1 : 0) +
        (p.ColumnPermissions || []).length,
      0,
    ),
  };
}
export function securityRows(index, state) {
  const search = state.securitySearch.trim().toLowerCase();
  return index.roles.filter(({ model, role }) => {
    const counts = roleCounts(role);
    return (
      (!state.securityModel || model.Name === state.securityModel) &&
      (!state.securityKind ||
        (state.securityKind === "rls"
          ? counts.filters > 0
          : state.securityKind === "ols"
            ? counts.permissions > 0
            : role.UnanalyzedConstructs?.length > 0)) &&
      (!search ||
        [
          role.Name,
          model.Name,
          ...role.TablePermissions.flatMap((p) => [
            p.Table,
            p.FilterExpression,
            ...(p.ColumnPermissions || []).map((c) => c.Column),
          ]),
        ]
          .join(" ")
          .toLowerCase()
          .includes(search))
    );
  });
}
export function relationshipEvidence(index, row) {
  const r = row.relationship,
    p = r.projected;
  const edges = (index.data.outgoing.get(row.id) || []).filter(
    (e) => e.DependencyKind === "RelationshipEndpoint",
  );
  const endpoints = [
    ["From", r.FromTable, r.FromColumn, r.FromCardinality, p.FromColumn],
    ["To", r.ToTable, r.ToColumn, r.ToCardinality, p.ToColumn],
  ].map(([side, table, column, cardinality, ref]) => ({
    side,
    table,
    column,
    cardinality,
    object: modelObject(index.data, ref),
  }));
  return {
    edges,
    endpoints,
    activations: p.ActivationSources.map((s) => ({
      ...s.Evidence,
      object: modelObject(index.data, s.Source),
    })),
    unresolved: index.data.raw.UnresolvedSemanticDependencies.filter(
      (u) => u.FromId === row.id,
    ),
  };
}

export function securityEvidence(index, row) {
  return {
    edges: (index.data.outgoing.get(row.id) || []).filter((e) =>
      ["TablePermission", "ObjectLevelPermission"].includes(e.DependencyKind),
    ),
    unresolved: index.data.raw.UnresolvedSemanticDependencies.filter(
      (u) => u.FromId === row.id,
    ),
    limitations: index.data.raw.AnalysisLimitations.filter(
      (l) =>
        l.Context.Status === "Resolved" && l.Context.TargetIds[0] === row.id,
    ),
  };
}
