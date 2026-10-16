import { words } from "./data.js";
export const pageId = (report, page) =>
  JSON.stringify([report.RelativePath, page.Name]);
const same = (a, b) =>
  String(a ?? "").toLowerCase() === String(b ?? "").toLowerCase();
export const pagesFor = (report) =>
  [...report.Pages].sort(
    (a, b) => (a.Order ?? Infinity) - (b.Order ?? Infinity),
  );
export function reportContext(data, state) {
  const report =
    data.raw.Reports.find((r) => r.RelativePath === state.report) ||
    data.raw.Reports.find((r) => r.Name === state.model && r.Pages.length) ||
    data.raw.Reports[0];
  const page =
    report &&
    (report.Pages.find((p) => pageId(report, p) === state.page) ||
      pagesFor(report)[0]);
  return { report, page };
}
export function boundModel(data, report) {
  const connection = report.ModelConnection;
  // An explicit remote/missing binding must never fall back to a similarly named local model.
  if (
    connection?.ConnectionKind !== "ByPath" ||
    !connection.IsTargetAvailableLocally
  )
    return null;
  return (
    data.raw.SemanticModels.find(
      (m) => m.RelativePath === connection.TargetSemanticModelPath,
    ) || null
  );
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
        : "Model binding not established in this JSON";
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
  if (
    visual.Accessibility?.TitleText &&
    !visual.Accessibility.TitleTextIsDynamic
  )
    return visual.Accessibility.TitleText;
  if (visual.OnCanvasText && !visual.OnCanvasTextIsDynamic)
    return visual.OnCanvasText.trim().slice(0, 70);
  return /^[a-f0-9]{12,}$/i.test(visual.Name)
    ? `${visualType(visual.VisualType)} · ${visual.Name.slice(0, 8)}`
    : visual.Name.replace(/[-_]/g, " ").replace(/^./, (c) => c.toUpperCase());
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
function referenceKey(ref) {
  return JSON.stringify([
    ref.Table,
    ref.ObjectName,
    ref.ObjectType,
    ref.HierarchyName ?? null,
  ]).toLowerCase();
}
// Core's direct evidence is authoritative. Raw FieldReferences also contain saved selector identities.
export function visualUses(data, report, page, visual) {
  const model = boundModel(data, report),
    references = visual.FieldReferences || [],
    confirmed = [],
    other = [];
  const consumed = new Set();
  if (model)
    for (const object of data.objects.filter(
      (o) => o.SemanticModel === model.Name,
    )) {
      const evidence = object.DirectReportReferences.filter(
        (e) =>
          (e.Report === report.Name || e.Report === report.RelativePath) &&
          e.Page === page.Name &&
          e.Visual === visual.Name &&
          e.ArtifactPath === visual.RelativePath,
      );
      if (!evidence.length) continue;
      const refs = references.filter((ref) =>
        evidence.some(
          (e) =>
            e.EvidencePath === ref.EvidencePath &&
            e.UsageContext === ref.UsageContext,
        ),
      );
      refs.forEach((ref) => consumed.add(ref));
      confirmed.push({
        object,
        name: object.ObjectName,
        table: object.Table,
        type: object.ObjectType,
        hierarchy: object.HierarchyName,
        references: evidence,
        roles: [...new Set(evidence.map((e) => usedAs(e)))],
        status: "Scanner-confirmed direct usage",
      });
    }
  const groups = new Map();
  for (const ref of references.filter((ref) => !consumed.has(ref))) {
    const key = referenceKey(ref);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(ref);
  }
  for (const refs of groups.values()) {
    const ref = refs[0];
    const object =
      model &&
      data.objects.find(
        (o) =>
          o.SemanticModel === model.Name &&
          same(o.Table, ref.Table) &&
          same(o.ObjectName, ref.ObjectName) &&
          same(o.ObjectType, ref.ObjectType) &&
          same(o.HierarchyName, ref.HierarchyName),
      );
    const measures = (report.ReportMeasures || []).filter(
      (m) =>
        same(m.Entity, ref.Table) &&
        same(m.Name, ref.ObjectName) &&
        ref.ObjectType === "Measure",
    );
    const reportMeasure = !object && measures.length === 1 ? measures[0] : null;
    const row = {
      object,
      reportMeasure,
      name: ref.ObjectName,
      table: ref.Table,
      type: reportMeasure ? "ReportMeasure" : ref.ObjectType,
      hierarchy: ref.HierarchyName,
      references: refs,
      roles: [...new Set(refs.map((r) => usedAs(r)))],
      status: reportMeasure
        ? "Saved report-measure reference; usage classification not published"
        : object
          ? "Saved reference; no direct usage evidence published for this visual"
          : model
            ? "Unresolved in the analysed model"
            : "Local model binding unavailable",
    };
    other.push(row);
  }
  const sort = (a, b) => a.name.localeCompare(b.name);
  return { confirmed: confirmed.sort(sort), other: other.sort(sort) };
}
export function scopedFindings(data, report, page, visual = null) {
  return data.raw.Findings.filter(
    (f) =>
      (f.Report === report.Name || f.Report === report.RelativePath) &&
      (page ? f.Page === page.Name : !f.Page) &&
      (visual ? f.Visual === visual.Name : !f.Visual && !f.VisualGroup),
  );
}
export function reportLimitations(data, report) {
  return data.raw.AnalysisLimitations.filter(
    (l) =>
      l.Scope === "Report" &&
      (l.ArtifactPath === report.RelativePath ||
        l.ArtifactPath.startsWith(report.RelativePath + "/")),
  );
}
export function groupPath(page, visual) {
  const result = [],
    seen = new Set();
  let name = visual.ParentGroupName;
  while (name && !seen.has(name)) {
    seen.add(name);
    const group = (page.VisualGroups || []).find((g) => g.Name === name);
    result.unshift(group?.DisplayName || name);
    name = group?.ParentGroupName;
  }
  return result;
}
