import { queryId } from "./query-data.js";

export const REVIEW_TABS = ["Review", "Evidence"];
export const REVIEW_DEFAULTS = {
  reviewCategory: "",
  reviewSeverity: "",
  reviewRequired: false,
  reviewSearch: "",
  reviewReport: "",
  reviewPage: "",
  reviewModel: "",
  reviewSelected: null,
  reviewTab: "Review",
  reviewLimitOpen: false,
};
export const isAccessibility = (f) =>
  String(f.Category).toLowerCase() === "accessibility";
export const severityRank = (f) =>
  ({ Error: 0, Warning: 1, Information: 2 })[f.Severity] ?? 3;
export const categoryLabel = (category) =>
  ({
    ModelIntegrity: "Model integrity",
    Accessibility: "Accessibility",
    Navigation: "Navigation",
    Compatibility: "Compatibility",
  })[category] || category;
const path = (value) => String(value || "").replaceAll("\\", "/");
const within = (artifact, root) =>
  !!root &&
  (path(artifact) === path(root) ||
    path(artifact).startsWith(path(root) + "/"));
export function reportForFinding(data, finding) {
  const reports = data.raw.Reports.filter(
    (r) => r.RelativePath === finding.Report || r.Name === finding.Report,
  );
  if (reports.length === 1) return reports[0];
  const scoped = reports.filter((r) =>
    within(finding.ArtifactPath, r.RelativePath),
  );
  return scoped.length === 1 ? scoped[0] : null;
}
export function themeRows(data) {
  let index = -1;
  return data.raw.Reports.flatMap((report) => {
    if (!report.ThemeReview) return [];
    const review = report.ThemeReview;
    const observations = [
      ...(review.Deviations || []).map((item) => ({
        item,
        kind: "Theme deviation",
      })),
      ...(review.ConsistencyObservations || []).map((item) => ({
        item,
        kind: "Consistency observation",
      })),
      ...(review.AccessibilityObservations || []).map((item) => ({
        item,
        kind: "Theme accessibility observation",
      })),
    ];
    return [
      {
        index: index--,
        theme: {
          report,
          kind: "Report theme",
          status: review.Status,
          observationCount: observations.length,
        },
      },
      ...observations.map((observation) => ({
        index: index--,
        theme: { report, ...observation },
      })),
    ];
  });
}
export function allReviewRows(data, mode = "main") {
  if (mode === "theme") return themeRows(data);
  return data.raw.Findings.map((finding, index) => ({ finding, index })).filter(
    ({ finding }) =>
      mode === "accessibility"
        ? isAccessibility(finding)
        : !isAccessibility(finding),
  );
}
export function reviewRows(data, state) {
  const search = (state.reviewSearch || "").trim().toLowerCase();
  return allReviewRows(data, state.reviewMode)
    .filter((row) => {
      const f = row.finding,
        theme = row.theme;
      const report = f?.Report || theme?.report.Name,
        page = f?.Page || theme?.item?.PageName;
      return (
        (!state.reviewReport || report === state.reviewReport) &&
        (!state.reviewPage || page === state.reviewPage) &&
        (!state.reviewModel || f?.SemanticModel === state.reviewModel) &&
        (!f ||
          ((!state.reviewCategory || f.Category === state.reviewCategory) &&
            (!state.reviewSeverity || f.Severity === state.reviewSeverity) &&
            (!state.reviewRequired ||
              f.AssessmentType === "ReviewRequired"))) &&
        (!search ||
          (f
            ? [
                f.RuleId,
                f.Message,
                f.Recommendation,
                f.Report,
                f.PageDisplayName,
                f.Page,
                f.Visual,
                f.VisualGroup,
                f.SemanticModel,
                f.Table,
                f.ObjectName,
              ]
            : [
                theme.report.Name,
                theme.kind,
                theme.item?.PropertyLabel,
                theme.item?.PageDisplayName,
                theme.item?.VisualName,
                theme.status?.State,
                theme.status?.CustomThemeName,
              ]
          )
            .filter(Boolean)
            .join(" ")
            .toLowerCase()
            .includes(search))
      );
    })
    .sort((a, b) =>
      a.finding
        ? severityRank(a.finding) - severityRank(b.finding) || a.index - b.index
        : b.index - a.index,
    );
}
export function selectedReview(data, state) {
  return allReviewRows(data, state.reviewMode).find(
    (row) => row.index === state.reviewSelected,
  );
}
export function reviewOptions(data, state) {
  const rows = allReviewRows(data, state.reviewMode);
  const unique = (values) =>
    [...new Set(values.filter(Boolean))].sort((a, b) => a.localeCompare(b));
  const pageRows = rows.filter(
    (row) =>
      (row.finding?.Report || row.theme?.report.Name) === state.reviewReport,
  );
  return {
    categories: unique(rows.map((row) => row.finding?.Category)),
    reports: unique(
      rows.map((row) => row.finding?.Report || row.theme?.report.Name),
    ),
    models: unique(rows.map((row) => row.finding?.SemanticModel)),
    pages: unique(
      pageRows.map((row) => row.finding?.Page || row.theme?.item?.PageName),
    ).map((name) => ({
      name,
      label:
        pageRows.find(
          (row) => (row.finding?.Page || row.theme?.item?.PageName) === name,
        )?.finding?.PageDisplayName ||
        pageRows.find((row) => row.theme?.item?.PageName === name)?.theme.item
          .PageDisplayName ||
        name,
    })),
  };
}
export function findingContext(f) {
  return (
    [
      f.Report,
      f.PageDisplayName || f.Page,
      f.Visual || f.VisualGroup,
      !f.Report ? f.SemanticModel : null,
      !f.Report ? f.Table : null,
      !f.Report ? f.ObjectName : null,
    ]
      .filter(Boolean)
      .join(" / ") || "Project metadata"
  );
}
export function findingTarget(data, finding) {
  if (!finding) return null;
  if (finding.Report) {
    const report = reportForFinding(data, finding);
    if (!report) return null;
    const page = report.Pages.find((p) => p.Name === finding.Page);
    const visual = page?.Visuals.find((v) => v.Name === finding.Visual);
    return {
      kind: "report",
      report,
      page,
      visual,
      label: visual
        ? "Inspect visual reviews"
        : page
          ? "Inspect page context"
          : "Browse report",
      note:
        finding.Page && !page
          ? "The referenced page is absent from this inventory. The finding evidence remains here."
          : finding.Visual && !visual
            ? "The referenced visual is absent from this page. The finding evidence remains here."
            : "",
    };
  }
  const model = data.raw.SemanticModels.find(
    (m) => m.Name === finding.SemanticModel,
  );
  if (!model) return null;
  if (/^PBI-(QUERY|SOURCE)-/.test(finding.RuleId)) {
    const queries = (data.raw.PowerQueryUsages || []).filter(
      (q) =>
        q.SemanticModel === finding.SemanticModel &&
        q.QueryName === finding.ObjectName &&
        (q.Table ?? null) === (finding.Table ?? null) &&
        path(q.ArtifactPath) === path(finding.ArtifactPath),
    );
    if (queries.length !== 1) return null;
    const query = queries[0];
    return {
      kind: "query",
      model,
      query,
      id: queryId(
        query.SemanticModel,
        query.QueryName,
        query.SourceKind,
        query.Table,
        query.Partition,
      ),
      label: "Inspect Power Query",
    };
  }
  const objects = data.objects.filter(
    (o) =>
      o.SemanticModel === model.Name &&
      o.Table === finding.Table &&
      o.ObjectName === finding.ObjectName,
  );
  const table = model.Tables.find((t) => t.Name === finding.Table);
  return {
    kind: "model",
    model,
    table,
    object: objects.length === 1 ? objects[0] : null,
    label:
      objects.length === 1
        ? "Inspect model object"
        : table
          ? "Browse model table"
          : "Browse semantic model",
    note:
      finding.ObjectName && objects.length !== 1
        ? "No unique model usage object is published for this reference. Open the broader model context."
        : "",
  };
}
export function reviewTarget(data, row) {
  if (row?.finding) return findingTarget(data, row.finding);
  if (!row?.theme?.item) return null;
  const { report, item } = row.theme,
    page = report.Pages.find((p) => p.Name === item.PageName),
    visual = page?.Visuals.find((v) => v.Name === item.VisualName);
  return {
    kind: "report",
    report,
    page,
    visual,
    tab: "Behaviour",
    label: visual
      ? "Inspect saved formatting"
      : page
        ? "Inspect page context"
        : "Browse report",
    note: !visual
      ? "The observation’s visual is absent. Only broader report/page context is available."
      : "",
  };
}
export function reviewQualifications(data, finding) {
  if (!finding) return { direct: [], context: [], object: null };
  const target = findingTarget(data, finding),
    query = /^PBI-(QUERY|SOURCE)-/.test(finding.RuleId);
  const limits = data.raw.AnalysisLimitations.filter((l) =>
    finding.Report
      ? l.Scope === "Report" &&
        target?.report &&
        within(l.ArtifactPath, target.report.RelativePath)
      : l.Scope === "SemanticModel" &&
        l.SemanticModel === finding.SemanticModel &&
        (!query || l.DependencyImpact === "MayCreateQueryDependencies"),
  );
  const direct = limits.filter(
    (l) =>
      path(l.ArtifactPath) === path(finding.ArtifactPath) &&
      (!l.ObjectName || l.ObjectName === finding.ObjectName) &&
      (!l.Table || l.Table === finding.Table),
  );
  return {
    direct,
    context: limits.filter((l) => !direct.includes(l)),
    object: target?.object,
  };
}
export function reviewLabel(data, state) {
  const row = selectedReview(data, state);
  return (
    row?.finding?.RuleId ||
    row?.theme?.item?.PropertyLabel ||
    row?.theme?.report.Name ||
    "Review"
  );
}
