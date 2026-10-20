import { resolvedId } from "./snapshot-data.js";

// Translate already-resolved snapshot targets into controller routes. Never bind names here.
export function navigationTarget(data, reference) {
  const id = resolvedId(reference),
    target = data.targets.get(id);
  if (!target) return null;
  const object = data.byId.get(id),
    model = data.models.get(target.ModelId || id),
    table = data.tables.get(target.TableId || id);
  if (object)
    return {
      kind: "model",
      model,
      table,
      object,
      id,
      label: "Inspect model object",
    };
  if (target.Kind === "Query")
    return {
      kind: "query",
      model,
      query: data.queryById.get(id),
      id,
      label: "Inspect Power Query",
    };
  if (["Role", "Relationship"].includes(target.Kind))
    return {
      kind: target.Kind === "Role" ? "security" : "relationships",
      model,
      id,
      label:
        "Inspect " +
        (target.Kind === "Role" ? "security role" : "relationship"),
    };
  if (target.Kind === "ReportMeasure")
    return { kind: "lineage", id, model, label: "Inspect report measure" };
  const { report, page, visual } = data.location(reference);
  if (report)
    return {
      kind: "report",
      report,
      page,
      visual,
      label: visual
        ? "Inspect visual"
        : page
          ? "Inspect page context"
          : "Browse report",
    };
  return model
    ? {
        kind: "model",
        model,
        table,
        label: table ? "Browse model table" : "Browse semantic model",
      }
    : null;
}
export function coreTarget(data, reference) {
  const target = navigationTarget(data, reference);
  if (!target) return null;
  if (target.kind === "model")
    return {
      kind: target.object ? "object" : "model",
      id: target.object?.id,
      model: target.model.Name,
      table: target.table?.Name || "",
      label: target.label,
    };
  if (target.kind === "report")
    return {
      kind: "report",
      report: target.report.RelativePath,
      page: target.page?.Name,
      visualId: target.visual?.Id,
      label: target.label,
    };
  return { ...target, model: target.model?.Name };
}
