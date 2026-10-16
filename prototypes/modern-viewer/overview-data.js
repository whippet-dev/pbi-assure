import { STATES } from "./data.js";

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
export function overviewData(data) {
  const authored = data.objects.filter((o) => !o.system);
  const main = data.raw.Findings.filter((f) => !isAccessibility(f)),
    accessibility = data.raw.Findings.filter(isAccessibility);
  const categories = [...new Set(main.map((f) => f.Category))]
    .map((category) => {
      const findings = main
        .filter((f) => f.Category === category)
        .sort((a, b) => severityRank(a) - severityRank(b));
      return {
        category,
        findings,
        errors: findings.filter((f) => f.Severity === "Error").length,
        warnings: findings.filter((f) => f.Severity === "Warning").length,
        reviews: findings.filter((f) => f.AssessmentType === "ReviewRequired")
          .length,
      };
    })
    .sort(
      (a, b) =>
        b.errors - a.errors || b.warnings - a.warnings || b.reviews - a.reviews,
    );
  const limited = data.objects.filter(
    (o) => o.ClassificationConfidence === "QualifiedByLimitation",
  );
  const reports = data.raw.Reports.map((report) => ({
    report,
    pages: report.Pages.length,
    visuals: report.Pages.reduce((n, page) => n + page.Visuals.length, 0),
  }));
  const themes = data.raw.Reports.flatMap((report) => [
    ...(report.ThemeReview?.Deviations || []).map((item) => ({
      report,
      item,
      kind: "Theme deviation",
    })),
    ...(report.ThemeReview?.ConsistencyObservations || []).map((item) => ({
      report,
      item,
      kind: "Consistency observation",
    })),
    ...(report.ThemeReview?.AccessibilityObservations || []).map((item) => ({
      report,
      item,
      kind: "Theme accessibility observation",
    })),
  ]);
  return {
    authored,
    main,
    accessibility,
    categories,
    limited,
    themes,
    reports,
    errors: main.filter((f) => f.Severity === "Error").length,
    warnings: main.filter((f) => f.Severity === "Warning").length,
    reviews: main.filter((f) => f.AssessmentType === "ReviewRequired").length,
    usage: Object.keys(STATES).map((state) => ({
      state,
      count: authored.filter((o) => o.UsageState === state).length,
    })),
    pages: reports.reduce((n, r) => n + r.pages, 0),
    visuals: reports.reduce((n, r) => n + r.visuals, 0),
  };
}
export function reviewRows(data, state) {
  const query = state.reviewSearch.trim().toLowerCase();
  return data.raw.Findings.map((finding, index) => ({ finding, index }))
    .filter(
      ({ finding: f }) =>
        (state.reviewMode === "accessibility"
          ? isAccessibility(f)
          : !isAccessibility(f)) &&
        (!state.reviewCategory || f.Category === state.reviewCategory) &&
        (!state.reviewSeverity || f.Severity === state.reviewSeverity) &&
        (!state.reviewRequired || f.AssessmentType === "ReviewRequired") &&
        (!query ||
          [
            f.RuleId,
            f.Message,
            f.Report,
            f.PageDisplayName,
            f.Visual,
            f.SemanticModel,
            f.Table,
            f.ObjectName,
          ]
            .filter(Boolean)
            .join(" ")
            .toLowerCase()
            .includes(query)),
    )
    .sort(
      (a, b) =>
        severityRank(a.finding) - severityRank(b.finding) || a.index - b.index,
    );
}
export function findingContext(f) {
  return (
    [
      f.Report,
      f.PageDisplayName || f.Page,
      f.Visual || f.VisualGroup,
      !f.Report ? f.SemanticModel : null,
      !f.Report ? f.Table : null,
    ]
      .filter(Boolean)
      .join(" / ") || "Project metadata"
  );
}
export function findingTarget(data, finding) {
  const report = data.raw.Reports.find(
    (r) => r.Name === finding.Report || r.RelativePath === finding.Report,
  );
  if (report) {
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
  if (model) {
    const objects = data.objects.filter(
      (o) =>
        o.SemanticModel === model.Name &&
        o.Table === finding.Table &&
        o.ObjectName === finding.ObjectName,
    );
    return {
      kind: "model",
      model,
      object: objects.length === 1 ? objects[0] : null,
      label:
        objects.length === 1 ? "Inspect model object" : "Browse semantic model",
    };
  }
  return null;
}
