import { STATES } from "./data.js";

import { isAccessibility, severityRank } from "./review-data.js";
export {
  isAccessibility,
  severityRank,
  categoryLabel,
  reviewRows,
  findingContext,
  findingTarget,
} from "./review-data.js";
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
