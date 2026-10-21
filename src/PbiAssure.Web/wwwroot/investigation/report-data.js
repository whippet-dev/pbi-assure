import { words } from "./data.js";
export const pageId = (report, page) => page.Id;
const same = (a, b) =>
  String(a ?? "").toLowerCase() === String(b ?? "").toLowerCase();
export const pagesFor = (report) =>
  [...report.Pages].sort(
    (a, b) => (a.Order ?? Infinity) - (b.Order ?? Infinity),
  );
export function reportContext(data, state) {
  const report = state.report
    ? data.reports.get(state.report)
    : data.raw.Reports[0];
  const page =
    report &&
    (state.page
      ? report.Pages.find((p) => p.Id === state.page)
      : pagesFor(report)[0]);
  return { report, page };
}
export function boundModel(data, report) {
  return data.models.get(report.LocalModelId) || null;
}

export function bindingLabel(data, report) {
  const model = boundModel(data, report),
    connection = report.ModelConnection;
  return model
    ? model.Name
    : connection?.ConnectionKind === "ByConnection"
      ? "Remote model · not available locally"
      : connection?.ConnectionKind === "ByPath"
        ? `${connection.TargetSemanticModelName || "Target model"} · not available locally`
        : "Model binding not established in this snapshot";
}
export const visualType = (value) =>
  ({
    tableEx: "Table",
    card: "Card",
    actionButton: "Button",
    qnaVisual: "Q&A",
    textbox: "Text box",
    image: "Image",
  })[value] || words(value || "Visual").replace(/^./, (c) => c.toUpperCase());
export function visualLabel(visual) {
  return visual?.DisplayName || visual?.Name || "Visual";
}

export function usedAs(reference, scope = "visual") {
  if (
    same(reference.Role, "filter") ||
    (!reference.Role && reference.UsageContext === "Filter")
  )
    return `${scope === "visual" ? "Visual" : scope === "page" ? "Page" : "Report"} filter`;
  if (
    same(reference.Role, "drillthrough") ||
    reference.UsageContext === "Drillthrough"
  )
    return "Drillthrough field";
  return words(reference.Role || reference.UsageContext || "Role not recorded");
}
// Core's direct evidence is authoritative. Raw FieldReferences also contain saved selector identities.
export function visualUses(data, report, page, visual) {
  const records = data.directUsages.filter(
    (u) =>
      u.Location.Status === "Resolved" && u.Location.TargetIds[0] === visual.Id,
  );
  const groups = new Map();
  for (const u of records) {
    if (!groups.has(u.ObjectId)) groups.set(u.ObjectId, []);
    groups.get(u.ObjectId).push(u);
  }
  const confirmed = [],
    other = [];
  for (const [id, references] of groups) {
    const object = data.byId.get(id),
      reportMeasure = report.ReportMeasures.find((m) => m.Id === id),
      node = data.nodes.get(id);
    const row = {
      object,
      reportMeasure,
      id,
      name:
        node?.ObjectName ||
        object?.ObjectName ||
        reportMeasure?.Name ||
        "Recorded object",
      table: node?.Table || reportMeasure?.Entity,
      type: node?.ObjectType,
      hierarchy: node?.HierarchyName,
      references,
      roles: [...new Set(references.map((e) => usedAs(e)))],
      status: "Recorded direct usage",
    };
    (object ? confirmed : other).push(row);
  }
  for (const ref of visual.FieldReferences.filter(
    (r) => !r.ConfirmedUsageTargetIds.length,
  )) {
    const id =
      ref.RecordedObject.Status === "Resolved"
        ? ref.RecordedObject.TargetIds[0]
        : null;
    other.push({
      object: data.byId.get(id),
      reportMeasure: report.ReportMeasures.find((m) => m.Id === id),
      id,
      name: ref.ObjectName,
      table: ref.Table,
      type: ref.ObjectType,
      hierarchy: ref.HierarchyName,
      references: [ref],
      roles: [usedAs(ref)],
      status:
        ref.RecordedObject.Status === "Ambiguous"
          ? "Ambiguous saved reference; no usage credited"
          : "Saved reference; no direct usage evidence recorded",
    });
  }
  return { confirmed, other };
}

export function scopedFindings(data, report, page, visual = null) {
  const id = visual?.Id || page?.Id || report.Id;
  return data.raw.Findings.filter(
    (f) => f.Target.Status === "Resolved" && f.Target.TargetIds[0] === id,
  );
}

export function reportLimitations(data, report) {
  return data.raw.AnalysisLimitations.filter(
    (l) =>
      l.Context.Status === "Resolved" &&
      data.location(l.Context).report?.Id === report.Id,
  );
}

export function groupPath(page, visual) {
  const result = [],
    seen = new Set();
  let ref = visual.ParentGroup;
  while (ref?.Status === "Resolved" && !seen.has(ref.TargetIds[0])) {
    const id = ref.TargetIds[0];
    seen.add(id);
    const group = page.VisualGroups.find((g) => g.Id === id);
    if (!group) break;
    result.unshift(group.DisplayName || group.Name);
    ref = group.ParentGroup;
  }
  return result;
}
