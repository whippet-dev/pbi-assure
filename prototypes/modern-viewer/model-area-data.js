import { identity, edgeId } from "./data.js";

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
export const areaId = (model, name) => JSON.stringify([model, name]);
export const path = (value) => String(value || "").replaceAll("\\", "/");
export function buildModelAreas(data) {
  const relationships = [],
    roles = [];
  for (const model of data.raw.SemanticModels) {
    for (const key of ["Relationships", "Roles"])
      if (model[key] != null && !Array.isArray(model[key]))
        throw new Error(`Invalid ${key} inventory.`);
    for (const relationship of model.Relationships || []) {
      if (
        !relationship ||
        ![
          "Name",
          "FromTable",
          "FromColumn",
          "ToTable",
          "ToColumn",
          "FromCardinality",
          "ToCardinality",
          "CrossFilteringBehavior",
        ].every((k) => typeof relationship[k] === "string") ||
        typeof relationship.IsActive !== "boolean" ||
        (relationship.Activation != null &&
          (!Array.isArray(relationship.Activation.Sources) ||
            typeof relationship.Activation.State !== "string"))
      )
        throw new Error(
          "Invalid relationship definition or activation evidence.",
        );
      relationships.push({
        id: areaId(model.Name, relationship.Name),
        model,
        relationship,
      });
      for (const source of relationship.Activation?.Sources || [])
        if (
          !source ||
          !["Table", "ObjectName", "ObjectType"].every(
            (k) => typeof source[k] === "string",
          ) ||
          typeof source.ReachableFromReport !== "boolean"
        )
          throw new Error("Invalid relationship activation source.");
    }
    for (const role of model.Roles || []) {
      if (
        !role ||
        typeof role.Name !== "string" ||
        !Array.isArray(role.TablePermissions) ||
        (role.UnanalyzedConstructs != null &&
          !Array.isArray(role.UnanalyzedConstructs))
      )
        throw new Error("Invalid security role definition.");
      for (const permission of role.TablePermissions) {
        if (
          !permission ||
          typeof permission.Table !== "string" ||
          typeof permission.FilterExpression !== "string" ||
          (permission.ColumnPermissions != null &&
            !Array.isArray(permission.ColumnPermissions))
        )
          throw new Error("Invalid security table permission.");
        for (const column of permission.ColumnPermissions || [])
          if (
            !column ||
            typeof column.Column !== "string" ||
            typeof column.Permission !== "string"
          )
            throw new Error("Invalid security column permission.");
      }
      roles.push({ id: areaId(model.Name, role.Name), model, role });
      if (
        (role.ModelPermission != null &&
          typeof role.ModelPermission !== "string") ||
        (role.RelativePath != null && typeof role.RelativePath !== "string") ||
        role.UnanalyzedConstructs?.some((c) => typeof c !== "string") ||
        role.TablePermissions.some(
          (p) =>
            p.MetadataPermission != null &&
            typeof p.MetadataPermission !== "string",
        )
      )
        throw new Error("Invalid security role evidence.");
    }
  }
  for (const rows of [relationships, roles])
    if (new Set(rows.map((row) => row.id)).size !== rows.length)
      throw new Error("Duplicate model-area identities.");
  return { data, relationships, roles };
}
export function modelObject(data, model, table, name, type = "Column") {
  const candidates = data.objects.filter(
    (o) =>
      o.SemanticModel === model &&
      o.Table === table &&
      o.ObjectName === name &&
      o.ObjectType === type,
  );
  return candidates.length === 1 ? candidates[0] : null;
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
  const { model, relationship: r } = row,
    source = identity(model.Name, "", r.Name, "Relationship");
  const edges = index.data.raw.SemanticDependencies.filter(
    (e) =>
      edgeId(e, "From") === source &&
      e.DependencyKind === "RelationshipEndpoint",
  );
  const endpoints = [
    ["From", r.FromTable, r.FromColumn, r.FromCardinality],
    ["To", r.ToTable, r.ToColumn, r.ToCardinality],
  ].map(([side, table, column, cardinality]) => ({
    side,
    table,
    column,
    cardinality,
    object: modelObject(index.data, model.Name, table, column),
  }));
  const activations = (r.Activation?.Sources || []).map((s) => ({
    ...s,
    object: modelObject(
      index.data,
      model.Name,
      s.Table,
      s.ObjectName,
      s.ObjectType,
    ),
  }));
  const unresolved = (
    index.data.raw.UnresolvedSemanticDependencies || []
  ).filter((u) => edgeId(u, "From") === source);
  return { edges, endpoints, activations, unresolved };
}
export function securityEvidence(index, row) {
  const source = identity(row.model.Name, "", row.role.Name, "Role");
  const edges = index.data.raw.SemanticDependencies.filter(
    (e) =>
      edgeId(e, "From") === source &&
      ["TablePermission", "ObjectLevelPermission"].includes(e.DependencyKind),
  );
  const unresolved = (
    index.data.raw.UnresolvedSemanticDependencies || []
  ).filter((u) => edgeId(u, "From") === source);
  const limitations = index.data.raw.AnalysisLimitations.filter(
    (l) =>
      l.SemanticModel === row.model.Name &&
      !!l.ArtifactPath &&
      !!row.role.RelativePath &&
      path(l.ArtifactPath) === path(row.role.RelativePath),
  );
  return { edges, unresolved, limitations };
}
