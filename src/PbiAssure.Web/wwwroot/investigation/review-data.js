import { navigationTarget } from "./navigation.js";

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
export function reportForFinding(data, finding) {
  return (
    data.location(finding.Target).report ||
    data.location(finding.DefinitionContext).report ||
    null
  );
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
  // A known source can be opened while the affected destination remains unavailable.
  const target =
    navigationTarget(data, finding.Target) ||
    navigationTarget(data, finding.DefinitionContext);
  return target
    ? {
        ...target,
        note:
          finding.Target.Status === "Resolved"
            ? ""
            : "The affected destination is " +
              finding.Target.Status.toLowerCase() +
              ". This opens the recorded definition context.",
      }
    : null;
}

export function reviewTarget(data, row) {
  if (row?.finding) return findingTarget(data, row.finding);
  const target = navigationTarget(data, row?.theme?.item?.Target);
  return target
    ? { ...target, tab: "Behaviour", label: "Inspect saved formatting" }
    : null;
}

export function reviewQualifications(data, finding) {
  if (!finding) return { direct: [], context: [], object: null };
  const target = findingTarget(data, finding),
    object = target?.object;
  const links = data.qualifications.get(object?.id) || [];
  const direct = data.raw.AnalysisLimitations.filter((l) =>
    links.some((q) => q.LimitationId === l.Id && q.Basis === "BoundedReach"),
  );
  const context = data.raw.AnalysisLimitations.filter(
    (l) =>
      !direct.includes(l) &&
      (finding.SemanticModel
        ? l.SemanticModel === finding.SemanticModel
        : target?.report &&
          data.location(l.Context).report?.Id === target.report.Id),
  );
  return { direct, context, object };
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
