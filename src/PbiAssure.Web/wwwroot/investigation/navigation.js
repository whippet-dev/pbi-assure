import { resolvedId } from "./snapshot-data.js";

// A resolution is a navigation result, never a new analysis conclusion.
export function resolveNavigation(data, reference) {
  const id = resolvedId(reference),
    target = data.targets.get(id);
  if (!id)
    return {
      status: reference?.Status === "Ambiguous" ? "Ambiguous" : "Unavailable",
      reason:
        reference?.Status === "Ambiguous"
          ? "Several recorded candidates exist; no destination has been selected."
          : "No resolved destination is recorded.",
    };
  if (!target)
    return {
      status: "Unavailable",
      reason: "The recorded destination is absent from this snapshot.",
    };
  const object = data.byId.get(id),
    model = data.models.get(target.ModelId || id),
    table = data.tables.get(target.TableId || id);
  let route;
  if (target.Kind === "Finding" && data.raw.Findings.some((f) => f.Id === id))
    route = { kind: "finding", id, label: "Inspect finding" };
  else if (
    target.Kind === "Limitation" &&
    data.raw.AnalysisLimitations.some((l) => l.Id === id)
  )
    route = { kind: "limitation", id, label: "Inspect limitation" };
  else if (object)
    route = {
      kind: "object",
      id,
      modelId: object.ModelId,
      tableId: object.TableId,
      object,
      model,
      table,
      label: "Inspect model object",
    };
  else if (target.Kind === "Query" && data.queryById.has(id))
    route = {
      kind: "query",
      id,
      modelId: target.ModelId,
      label: "Inspect Power Query",
    };
  else if (
    target.Kind === "Role" &&
    data.snapshot.Roles.some((r) => r.Id === id)
  )
    route = {
      kind: "security",
      id,
      modelId: target.ModelId,
      label: "Inspect security role",
    };
  else if (
    target.Kind === "Relationship" &&
    data.snapshot.Relationships.some((r) => r.Id === id)
  )
    route = {
      kind: "relationships",
      id,
      modelId: target.ModelId,
      label: "Inspect relationship",
    };
  else if (data.nodes.has(id) && !["Table", "Model"].includes(target.Kind))
    route = {
      kind: "lineage",
      id,
      modelId: target.ModelId,
      label:
        target.Kind === "ReportMeasure"
          ? "Inspect report measure"
          : "Inspect definition in Lineage",
    };
  else if (["Report", "Page", "Visual"].includes(target.Kind)) {
    const { report, page, visual } = data.location(reference);
    if (
      report &&
      (target.Kind === "Report" || page) &&
      (!page || report.Pages.some((p) => p.Id === page.Id)) &&
      (target.Kind !== "Visual" ||
        (visual && page.Visuals.some((v) => v.Id === visual.Id)))
    )
      route = {
        kind: "report",
        id,
        reportId: report.Id,
        pageId: page?.Id,
        visualId: visual?.Id,
        report,
        page,
        visual,
        label: visual
          ? "Inspect visual"
          : page
            ? "Inspect page context"
            : "Browse report",
      };
    else
      return {
        status: "Unavailable",
        reason:
          "The recorded report, page or visual is not available. No alternative destination was inferred.",
      };
  } else if (target.Kind === "Model" && model)
    route = {
      kind: "model",
      id,
      modelId: model.Id,
      label: "Browse semantic model",
    };
  else if (target.Kind === "Table" && table && model)
    route = {
      kind: "model",
      id,
      modelId: model.Id,
      tableId: table.Id,
      label: "Browse model table",
    };
  if (route) return { status: "Resolved", route };
  return {
    status: "Unsupported",
    reason: `The recorded ${target.Kind} has no dedicated investigation destination. Its evidence remains available.`,
  };
}
export function navigationTarget(data, reference) {
  return resolveNavigation(data, reference).route || null;
}
export function coreTarget(data, reference) {
  const result = resolveNavigation(data, reference);
  return result.route || { kind: "unavailable", ...result };
}
export const targetReference = (id) => ({
  Status: "Resolved",
  TargetIds: [id],
});
