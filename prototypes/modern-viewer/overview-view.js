import { escape, icon } from "./ui.js";
import { STATES } from "./data.js";
import {
  overviewData,
  reviewRows,
  categoryLabel,
  findingContext,
  findingTarget,
} from "./overview-data.js";
import { findingList } from "./report-view.js";

export function overviewWorkspace(data) {
  const summary = overviewData(data);
  const inventory = data.raw.SemanticModels;
  return `<div class="overview-sheet" id="collection" tabindex="-1" aria-label="Project overview"><section class="overview-attention overview-section" aria-labelledby="attention-heading"><div class="overview-section-heading"><div><span class="eyebrow">START HERE</span><h2 id="attention-heading">Needs attention</h2></div><button class="text-button" data-overview-reviews="main">Review ${summary.main.length} findings ${icon("arrow")}</button></div><div class="attention-summary"><button data-overview-severity="Error"><strong>${summary.errors}</strong> errors</button><button data-overview-severity="Warning"><strong>${summary.warnings}</strong> warnings</button><button data-overview-required><strong>${summary.reviews}</strong> require review</button><span>Accessibility is counted separately.</span></div>${!summary.main.length ? '<p class="quiet">No non-accessibility findings recorded. Manual review and the analysis boundaries still apply.</p>' : ""}<p class="overview-footnote">Severity and review-required counts can overlap. Counts describe this saved analysis, not a live project.</p></section>
    <section class="overview-section overview-usage" aria-labelledby="usage-heading"><div class="overview-section-heading"><h2 id="usage-heading">Model usage</h2><button class="text-button" data-overview-models>Explore objects ${icon("arrow")}</button></div><p class="quiet">${summary.authored.length} authored model objects across ${inventory.length} models. ${data.objects.length - summary.authored.length} system-generated ${data.objects.length - summary.authored.length === 1 ? "object" : "objects"} excluded.</p><div class="overview-usage-rows">${summary.usage.map(({ state, count }) => `<button data-overview-usage="${state}" aria-label="Inspect ${count} ${escape(STATES[state].label.toLowerCase())} authored model objects"><span class="badge ${STATES[state].tone}"><i></i>${escape(STATES[state].label)}</span><strong>${count}</strong>${icon("arrow")}</button>`).join("")}</div><p class="review-note">Apparently unused is a review candidate, never permission to delete. Other reports and runtime behaviour may still use these objects.</p></section>
    <section class="overview-section overview-checks" aria-labelledby="checks-heading"><h2 id="checks-heading">Checks</h2><button class="checks-route" data-overview-checks><strong>${summary.limited.length}</strong><span>object results have Checks limited</span>${icon("arrow")}</button><p class="quiet">Missing or partly understood metadata may affect these results. Their usage states remain unchanged.</p><button class="text-button" data-overview-checks>What was checked ${icon("arrow")}</button><div class="checks-boundary"><strong>${data.raw.AnalysisLimitations.length ? `${data.raw.AnalysisLimitations.length} recorded limitations` : "No identified limitations recorded"}</strong><p class="quiet">${data.raw.AnalysisLimitations.length ? "Not every recorded limitation affects object usage." : "This does not mean everything was checked."} Static metadata analysis cannot establish runtime behaviour or use outside this project.</p></div></section>
    <section class="overview-section overview-snapshot" aria-labelledby="snapshot-heading"><div class="overview-section-heading"><h2 id="snapshot-heading">Project snapshot</h2><button class="text-button" data-overview-pages>Explore reports ${icon("arrow")}</button></div><p class="quiet">${inventory.length} semantic models · ${summary.reports.length} reports · ${summary.pages} saved pages · ${summary.visuals} visuals</p><table class="snapshot-reports"><thead><tr><th scope="col">Report</th><th scope="col">Pages</th><th scope="col">Visuals</th></tr></thead><tbody>${summary.reports.map((row) => `<tr><td><button class="related-object" data-overview-report="${escape(row.report.RelativePath)}">${escape(row.report.Name)} ${icon("arrow")}</button></td><td>${row.pages}</td><td>${row.visuals}</td></tr>`).join("")}</tbody></table><p class="overview-inventory">${inventory.reduce((n, model) => n + model.Tables.length, 0)} tables · ${inventory.reduce((n, model) => n + model.Tables.reduce((a, t) => a + t.Columns.length, 0), 0)} columns · ${inventory.reduce((n, model) => n + model.Tables.reduce((a, t) => a + t.Measures.length, 0), 0)} model measures<br>${inventory.reduce((n, model) => n + (model.Relationships?.length || 0), 0)} relationships · ${data.raw.PowerQueryUsages?.length || 0} Power Query queries</p></section>
    <section class="overview-section overview-other" aria-labelledby="other-heading"><h2 id="other-heading">Other reviews</h2><button class="other-review-route" data-overview-reviews="accessibility"><strong>Accessibility review</strong><span>${summary.accessibility.length} observations ${icon("arrow")}</span></button><p class="quiet">Counted separately from Needs attention. Saved settings and observations still require manual accessibility review.</p><details class="overview-theme"><summary>Theme review <span>${summary.themes.length} saved observations</span></summary><p class="quiet">Saved comparisons do not establish editing history or final rendered appearance.</p>${summary.themes.map(({ report, item, kind }, index) => `<div class="theme-observation"><strong>${escape(kind)} · ${escape(item.PropertyLabel || "Saved setting")}</strong><p>${escape(report.Name)} · ${escape(item.PageDisplayName || item.PageName)} · ${escape(item.VisualName)}</p><p>${escape(item.SavedValue || item.ObservedValue || "")}${item.ThemeValue != null ? " · Theme value " + escape(item.ThemeValue) : item.DominantValue != null ? " · Peer value " + escape(item.DominantValue) : ""}</p>${report.Pages.some((p) => p.Name === item.PageName && p.Visuals.some((v) => v.Name === item.VisualName)) ? `<button class="text-button" data-overview-theme="${index}">Inspect saved formatting ${icon("arrow")}</button>` : ""}</div>`).join("") || '<p class="quiet">No theme observations recorded in this snapshot.</p>'}</details></section></div>`;
}
export function reviewsWorkspace(data, state) {
  const rows = reviewRows(data, state),
    categories = [
      ...new Set(
        data.raw.Findings.filter((f) =>
          state.reviewMode === "accessibility"
            ? f.Category.toLowerCase() === "accessibility"
            : f.Category.toLowerCase() !== "accessibility",
        ).map((f) => f.Category),
      ),
    ];
  if (!rows.some((row) => row.index === state.reviewSelected))
    state.reviewSelected = rows[0]?.index ?? null;
  return `<section class="collection-pane" aria-label="Findings collection"><div class="collection-tools"><div class="collection-title"><strong>${state.reviewMode === "accessibility" ? "Accessibility observations" : "Findings"}</strong><span id="review-result-count">${rows.length} results</span></div><div class="type-filters review-modes">${[
    ["main", "Findings"],
    ["accessibility", "Accessibility"],
  ]
    .map(
      ([mode, label]) =>
        `<button data-review-mode="${mode}" aria-pressed="${mode === state.reviewMode}" class="${mode === state.reviewMode ? "active" : ""}">${label}</button>`,
    )
    .join(
      "",
    )}</div><div class="searchbox">${icon("search")}<input id="review-search" type="search" aria-label="Search findings" placeholder="Search findings, objects or reports…" value="${escape(state.reviewSearch)}"><kbd>/</kbd></div><div class="filter-line"><label class="sr-only" for="review-category">Finding category</label><select id="review-category"><option value="">All categories</option>${categories.map((category) => `<option value="${escape(category)}" ${category === state.reviewCategory ? "selected" : ""}>${escape(categoryLabel(category))}</option>`).join("")}</select><label class="sr-only" for="review-severity">Finding severity</label><select id="review-severity"><option value="">All severities</option>${["Error", "Warning", "Information"].map((severity) => `<option ${severity === state.reviewSeverity ? "selected" : ""}>${severity}</option>`).join("")}</select></div><div class="review-required-filter"><label><input id="review-required" type="checkbox" ${state.reviewRequired ? "checked" : ""}>Review required</label><button class="text-button" data-action="reset-reviews">Reset</button></div></div><div class="collection-scroll review-list" id="collection" tabindex="-1">${reviewCollection(data, state)}</div><div class="collection-footer">Original scanner findings · read-only</div></section><aside class="inspector" id="review-inspector" aria-label="Selected finding">${reviewInspector(data, state)}</aside>`;
}
export function reviewCollection(data, state) {
  const rows = reviewRows(data, state);
  if (!rows.some((row) => row.index === state.reviewSelected))
    state.reviewSelected = rows[0]?.index ?? null;
  return (
    rows
      .map(
        ({ finding: f, index }) =>
          `<button class="review-row ${state.reviewSelected === index ? "selected" : ""}" data-review="${index}" tabindex="${state.reviewSelected === index ? "0" : "-1"}" ${state.reviewSelected === index ? 'aria-current="true"' : ""}><div><span class="badge ${f.Severity === "Information" ? "unused" : "branch"}">${escape(f.Severity)}</span><small>${escape(f.RuleId)} · ${escape(categoryLabel(f.Category))}</small>${f.AssessmentType === "ReviewRequired" ? "<small>Review required</small>" : ""}</div><strong>${escape(f.Message)}</strong><small>${escape(findingContext(f))}</small></button>`,
      )
      .join("") ||
    '<div class="empty"><h2>No matching findings</h2><p>Adjust the filters. An empty result is not an assurance pass.</p><button class="primary-button" data-action="reset-reviews">Clear filters</button></div>'
  );
}
export function reviewInspector(data, state) {
  const f = data.raw.Findings[state.reviewSelected];
  if (!f)
    return '<div class="empty"><h2>No finding selected</h2><p>Original evidence will appear here.</p></div>';
  const target = findingTarget(data, f);
  return `<div class="inspector-toolbar"><span>${state.reviewMode === "accessibility" ? "ACCESSIBILITY OBSERVATION" : "FINDING INSPECTOR"}</span></div><div class="object-hero"><div class="object-breadcrumb">${escape(findingContext(f))}</div><h2 id="review-heading" tabindex="-1">${escape(f.RuleId)}</h2><p>${escape(categoryLabel(f.Category))} · ${f.AssessmentType === "ReviewRequired" ? "Review required" : "Finding"}</p></div><div class="inspector-content review-detail">${findingList([f], "")}${target ? `<section><h3>Investigate the source</h3>${target.note ? `<p class="quiet">${escape(target.note)}</p>` : ""}<button class="primary-button" data-action="review-source">${escape(target.label)} ${icon("arrow")}</button></section>` : '<p class="quiet">This finding remains available here with its source evidence. Its source has no dedicated workspace in this prototype.</p>'}<p class="footnote">Findings preserve scanner severity, assessment type and scope. Review required is separate from severity. ${state.reviewMode === "accessibility" ? "These are metadata observations, not an accessibility certification." : "Accessibility observations are in their own collection."}</p></div><div class="inspector-footer">${icon("check")}Original scanner evidence <span>Read-only</span></div>`;
}
