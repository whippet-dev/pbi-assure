import { escape, icon } from "./ui.js";
import { words } from "./data.js";
import { visualLabel } from "./report-data.js";
import {
  REVIEW_TABS,
  allReviewRows,
  reviewRows,
  selectedReview,
  reviewOptions,
  categoryLabel,
  findingContext,
  reviewTarget,
  reviewQualifications,
} from "./review-data.js";

// Presentation titles only; original messages and recommendations remain unchanged.
const findingTitles = {
  "PBI-MODEL-001": "Referenced field was not found",
  "PBI-MODEL-002": "Referenced semantic model was not found",
  "PBI-MODEL-003": "Review bidirectional filtering",
  "PBI-MODEL-004": "Review a many-to-many relationship",
  "PBI-MODEL-005": "Sort-by column was not found",
  "PBI-NAV-001": "Action targets a missing bookmark",
  "PBI-NAV-002": "Bookmark action has no target",
  "PBI-NAV-003": "Bookmark targets a missing page",
  "PBI-NAV-004": "Bookmark references a missing visual",
  "PBI-NAV-005": "Bookmark definition is missing",
  "PBI-NAV-006": "Bookmark is absent from the report index",
  "PBI-NAV-007": "Action targets a missing page",
  "PBI-NAV-008": "Review a dynamic action target",
  "PBI-NAV-009": "Drillthrough page has no parameters",
  "PBI-NAV-010": "Drillthrough parameter has no page filter",
  "PBI-NAV-011": "Drillthrough filter was not found",
  "PBI-NAV-012": "Visual interaction has a missing endpoint",
  "PBI-NAV-013": "Tooltip targets a missing page",
  "PBI-NAV-014": "Tooltip has no target page",
  "PBI-NAV-015": "Target page is not configured as a tooltip",
  "PBI-NAV-016": "Review a dynamic tooltip target",
  "PBI-NAV-017": "Landing page was not found",
  "PBI-ACCESS-001": "Keyboard-accessible visual has no alt text",
  "PBI-ACCESS-002": "Page items share a tab-order rank",
  "PBI-ACCESS-003": "Review a visual excluded from tab order",
  "PBI-ACCESS-004": "Review a visual with its title disabled",
  "PBI-ACCESS-005": "Drillthrough page has no Back action",
  "PBI-COMPAT-001": "Review a retiring visual type",
  "PBI-COMPAT-002": "Custom theme resource was not found",
  "PBI-SOURCE-001": "File source may be unavailable elsewhere",
  "PBI-QUERY-001": "Review dynamic query references",
  "PBI-QUERY-002": "No use found for this Power Query expression"
};
const findingTitle = (finding) => findingTitles[finding.RuleId] || finding.Message || finding.RuleId;

const modeLabel = (mode) =>
  ({
    main: "Findings",
    accessibility: "Accessibility observations",
    theme: "Theme review",
  })[mode];
const severity = (f) =>
  `<span class="badge ${f.Severity === "Error" ? "severity-error" : f.Severity === "Warning" ? "severity-warning" : "severity-info"}">${escape(f.Severity)}</span>`;
const assessment = (f) =>
  f.AssessmentType === "ReviewRequired"
    ? `<span class="review-assessment">${icon("reviews")}Review required</span>`
    : "";
const options = (values, selected, label) =>
  `<option value="">${label}</option>${values.map((v) => `<option value="${escape(v.name ?? v)}" ${(v.name ?? v) === selected ? "selected" : ""}>${escape(v.label ?? v)}</option>`).join("")}`;
const facts = (rows) =>
  `<dl class="review-facts">${rows
    .filter(([, value]) => value != null && value !== "")
    .map(
      ([label, value]) => `<dt>${escape(label)}</dt><dd>${escape(value)}</dd>`,
    )
    .join("")}</dl>`;
const stateLabels = {
  BaseThemeOnly: "Base theme only",
  CustomThemeAppliedOverBase: "Custom theme applied over base",
  ThemeResourceUnresolved: "Theme resource unresolved",
  NoThemeMetadata: "No theme metadata",
};
const resourceLabels = {
  Available: "Available in this snapshot",
  ReferencedButUnavailable: "Referenced resource unavailable",
  MetadataUnavailable: "Theme metadata unavailable",
};
const themeTitle = (theme) =>
  theme.item
    ? theme.item.PropertyLabel || "Saved setting"
    : stateLabels[theme.status?.State] ||
      words(theme.status?.State || "Theme status not published");

export function reviewsNavigation(data, state) {
  const count = (mode) =>
    allReviewRows(data, mode).filter(
      (row) => mode !== "theme" || row.theme.item,
    ).length;
  const button = (mode, label) =>
    `<button data-review-mode="${mode}" class="${state.reviewMode === mode ? "chosen" : ""}" aria-pressed="${state.reviewMode === mode}"><span>${label}</span><span>${count(mode)}${mode === "theme" ? " observations" : ""}</span></button>`;
  return `<div class="reviews-navigation"><nav class="table-nav" aria-label="Main review view">${button("main", "Findings")}</nav><div class="project-label specialist-label">SPECIALIST REVIEWS</div><nav class="table-nav specialist-nav" aria-label="Specialist review views">${button("accessibility", "Accessibility")}${button("theme", "Theme")}</nav><p class="source-label review-mode-note">${state.reviewMode === "main" ? "Accessibility is counted separately. Theme comparisons remain in Theme." : state.reviewMode === "accessibility" ? "Saved settings and observations; manual review is still needed." : "Saved comparisons and report theme availability. Separate from finding severity."}</p></div>`;
}
export function reviewsWorkspace(data, state) {
  const rows = reviewRows(data, state),
    filters = reviewOptions(data, state),
    theme = state.reviewMode === "theme";
  if (!selectedReview(data, state))
    state.reviewSelected = rows[0]?.index ?? null;
  return `<section class="collection-pane" aria-label="Review collection"><div class="collection-tools"><div class="collection-title"><strong>${modeLabel(state.reviewMode)}</strong><span id="review-result-count">${theme ? `${rows.filter((r) => r.theme.item).length} observations · ${rows.filter((r) => !r.theme.item).length} report summaries` : `${rows.length} results`}</span></div><div class="searchbox">${icon("search")}<input id="review-search" type="search" aria-label="Search findings" placeholder="${theme ? "Search reports, comparisons or settings…" : "Search findings, objects or reports…"}" value="${escape(state.reviewSearch)}"><kbd>/</kbd></div>${
    !theme
      ? `<div class="filter-line"><label class="sr-only" for="review-category">Finding category</label><select id="review-category">${options(
          filters.categories.map((name) => ({
            name,
            label: categoryLabel(name),
          })),
          state.reviewCategory,
          "All categories",
        )}</select><label class="sr-only" for="review-severity">Finding severity</label><select id="review-severity">${options(["Error", "Warning", "Information"], state.reviewSeverity, "All severities")}</select></div>`
      : ""
  }<div class="review-context-controls"><label class="sr-only" for="review-report">Review report</label><select id="review-report">${options(filters.reports, state.reviewReport, "All reports")}</select>${!theme ? `<label class="sr-only" for="review-model">Review semantic model</label><select id="review-model">${options(filters.models, state.reviewModel, "All recorded models")}</select>` : ""}${state.reviewReport ? `<label class="sr-only" for="review-page">Review page</label><select id="review-page">${options(filters.pages, state.reviewPage, "All pages / report scope")}</select>` : ""}</div><div class="review-required-filter">${!theme ? `<label><input id="review-required" type="checkbox" ${state.reviewRequired ? "checked" : ""}>Review required</label>` : '<span class="source-label">Report status and saved observations</span>'}<button class="text-button" data-action="reset-reviews">Reset filters</button></div></div><div class="collection-scroll review-list" id="collection" tabindex="-1">${reviewCollection(data, state)}</div><div class="collection-footer"><span>${theme ? "Saved theme review" : "Recorded findings"} · read-only</span><span>↑ ↓ to explore · / to search</span></div></section><aside class="inspector" id="review-inspector" aria-label="Selected review">${reviewInspector(data, state)}</aside>`;
}
export function reviewCollection(data, state) {
  const rows = reviewRows(data, state);
  if (!selectedReview(data, state))
    state.reviewSelected = rows[0]?.index ?? null;
  const active = rows.some((row) => row.index === state.reviewSelected)
    ? state.reviewSelected
    : rows[0]?.index;
  return (
    rows
      .map(
        ({ finding: f, theme, index }) =>
          `<button class="review-row ${state.reviewSelected === index ? "selected" : ""}" data-review="${index}" tabindex="${active === index ? 0 : -1}" ${state.reviewSelected === index ? 'aria-current="true"' : ""}>${f ? `<div>${severity(f)}<small>${escape(categoryLabel(f.Category))} · ${escape(f.RuleId)}</small>${assessment(f)}</div><strong>${escape(f.Message)}</strong><small>${escape(findingContext(f))}</small>` : `<div><small>${escape(theme.kind)}</small></div><strong>${escape(themeTitle(theme))}</strong><small>${escape(theme.report.Name)}${theme.item ? ` / ${escape(theme.item.PageDisplayName || theme.item.PageName)} / ${escape(theme.item.VisualName)}` : ` · ${theme.observationCount} saved observations`}</small>`}</button>`,
      )
      .join("") ||
    `<div class="empty"><h2>${state.reviewMode === "theme" ? "No matching theme records" : "No matching findings"}</h2><p>Adjust the filters. An empty result is not an assurance pass.</p><button class="primary-button" data-action="reset-reviews">Clear filters</button></div>`
  );
}
function sourceSection(data, row) {
  const target = reviewTarget(data, row),
    f = row.finding;
  const affected = f
    ? [
        ["Report", f.Report],
        ["Page", target?.page?.DisplayName || f.PageDisplayName || f.Page],
        ["Visual", target?.visual ? visualLabel(target.visual) : f.Visual],
        ["Visual group", f.VisualGroup],
        ["Semantic model", f.SemanticModel],
        ["Table", f.Table],
        [
          "Object type",
          target?.object ? words(target.object.ObjectType) : null,
        ],
        [
          target?.kind === "query"
            ? "Query"
            : f.Report
              ? "Named target"
              : "Object",
          f.ObjectName,
        ],
      ]
    : [
        ["Report", row.theme.report.Name],
        ["Page", row.theme.item?.PageDisplayName || row.theme.item?.PageName],
        [
          "Visual",
          target?.visual
            ? visualLabel(target.visual)
            : row.theme.item?.VisualName,
        ],
      ];
  return `<section class="review-source"><h3>Affected context</h3>${facts(affected)}${target?.note ? `<p class="quiet">${escape(target.note)}</p>` : ""}${!target ? `<p class="quiet">${f?.Report ? "The report is missing or cannot be identified uniquely in this inventory." : /^PBI-(QUERY|SOURCE)-/.test(f?.RuleId || "") ? "No unique query identity is available for this finding." : row.theme?.item ? "The source visual is not available." : "No dedicated source workspace is available for this record."} The original evidence remains here.</p>` : ""}</section>`;
}
function qualificationSummary(data, f) {
  const qualification = reviewQualifications(data, f);
  return `${qualification.object?.ClassificationConfidence === "QualifiedByLimitation" ? '<p class="review-qualification">Checks limited for this model object. Its recorded usage state is retained.</p>' : ""}${qualification.direct.length ? `<p class="quiet">${qualification.direct.length} limitation record${qualification.direct.length === 1 ? "" : "s"} at this source. Inspect Evidence before interpreting absence.</p>` : ""}`;
}
function findingReview(data, row) {
  const f = row.finding;
  return `<section class="review-message"><h3>What was found</h3><p class="review-lead">${escape(f.Message)}</p></section><section><h3>${f.AssessmentType === "ReviewRequired" ? "What to review" : "Suggested next step"}</h3><p class="review-recommendation">${escape(f.Recommendation || "No recommendation published for this finding.")}</p></section>${sourceSection(data, row)}${qualificationSummary(data, f)}<p class="footnote">${stateModeNote(f)}</p>`;
}
const stateModeNote = (f) =>
  f.Category === "Accessibility"
    ? "These are metadata observations, not an accessibility certification."
    : "Review required is separate from severity. Accessibility observations have their own collection.";
function themeReview(data, row) {
  const { theme } = row,
    item = theme.item;
  if (!item)
    return `<section><h3>Report theme</h3><p class="review-lead">${escape(themeTitle(theme))}</p>${facts(
      [
        ["Report", theme.report.Name],
        ["Base theme", theme.status?.BaseThemeName],
        ["Custom theme", theme.status?.CustomThemeName],
        [
          "Theme resource",
          resourceLabels[theme.status?.ResourceStatus] ||
            words(theme.status?.ResourceStatus),
        ],
        ["Saved observations", theme.observationCount],
      ],
    )}<p class="quiet">Resource availability describes this saved inventory. No observations does not establish a complete theme or accessibility review.</p></section>`;
  return `<section><h3>${escape(theme.kind)}</h3><p class="review-lead">${escape(item.PropertyLabel || "Saved setting")}</p>${facts(
    [
      ["Saved value", item.SavedValue ?? item.ObservedValue],
      ["Theme value", item.ThemeValue],
      ["Dominant peer value", item.DominantValue],
      [
        "Matching peers",
        item.DominantCount != null
          ? `${item.DominantCount} of ${item.PeerCount}`
          : null,
      ],
    ],
  )}<p class="quiet">${theme.kind === "Theme deviation" ? "A saved value differs from the resolved theme rule. This does not establish how or why it was set." : theme.kind === "Consistency observation" ? "A saved value differs from the dominant peer value. Peer consistency is descriptive, not a required standard." : "A theme accessibility observation is separate from the accessibility finding collection; manual review is still needed."}</p></section>${sourceSection(data, row)}<p class="footnote">Saved comparisons do not establish editing history or final rendered appearance.</p>`;
}
function evidenceView(data, row, state) {
  const f = row.finding;
  if (!f) {
    const { theme } = row;
    return `<section><h3>Theme evidence</h3>${facts([
      ["Report", theme.report.Name],
      ["Property", theme.item?.PropertyKey],
      ["Theme rule", theme.item?.ThemeRuleEvidencePath],
      ["Status", theme.status?.State],
      ["Resource status", theme.status?.ResourceStatus],
    ])}<p class="quiet">${theme.item?.ThemeRuleEvidencePath ? "Comparison against the scanner-resolved theme rule." : theme.kind === "Consistency observation" ? "Comparison of saved values across the scanner’s peer group." : "Original report theme status from the scanner."}</p></section><p class="footnote">No finding severity or review-required state is assigned to theme observations.</p>`;
  }
  const q = reviewQualifications(data, f);
  const limits = (rows) =>
    rows
      .map(
        (l) =>
          `<div class="review-limit"><strong>${escape(words(l.Cause))}</strong><p>${escape(l.Reason)}</p><small>${escape(l.ArtifactPath)} · ${escape(l.EvidencePath)}</small></div>`,
      )
      .join("");
  const url = /^https?:\/\//i.test(f.ReferenceUrl || "")
    ? f.ReferenceUrl
    : null;
  return `<section><h3>Recorded evidence</h3>${(f.EvidencePaths || []).length ? `<ul class="review-evidence-paths">${f.EvidencePaths.map((e) => `<li><code>${escape(e)}</code></li>`).join("")}</ul>` : '<p class="quiet">No evidence paths published.</p>'}${facts(
    [
      ["Source artifact", f.ArtifactPath],
      ["Rule", f.RuleId],
      ["Rule version", f.RuleVersion],
      [
        "Assessment",
        f.AssessmentType === "ReviewRequired" ? "Review required" : "Finding",
      ],
    ],
  )}${url ? `<a class="review-reference" href="${escape(url)}" target="_blank" rel="noopener noreferrer">Rule reference ${icon("open")}</a>` : ""}</section>${qualificationSummary(data, f)}<section><h3>Limitations at this source</h3>${limits(q.direct) || '<p class="quiet">No matching limitation record identified. This does not mean every construct was checked.</p>'}${q.context.length ? `<details class="review-context-limits" ${state.reviewLimitOpen ? "open" : ""}><summary>${q.context.length} other limitations in this ${f.Report ? "report" : "model"}</summary><p class="quiet">Context only. The snapshot does not establish that each limitation affects this finding or object.</p>${limits(q.context)}</details>` : ""}</section><p class="footnote">Evidence and recommendations are original scanner output. The viewer does not execute DAX, M or report actions.</p>`;
}
export function reviewInspector(data, state) {
  const row = selectedReview(data, state);
  if (!row)
    return '<div class="empty"><h2>No finding selected</h2><p>Original evidence will appear here.</p></div>';
  const f = row.finding,
    tab = state.reviewTab || "Review",
    outside = !reviewRows(data, state).some((r) => r.index === row.index),
    target = reviewTarget(data, row);
  return `<div class="inspector-toolbar"><span>${f ? (state.reviewMode === "accessibility" ? "ACCESSIBILITY OBSERVATION" : "FINDING INSPECTOR") : "THEME REVIEW"}</span></div><div class="object-hero review-hero"><div class="eyebrow">${escape(f ? categoryLabel(f.Category) : row.theme.kind)}</div><h2 id="review-heading" tabindex="-1">${escape(f ? findingTitle(f) : row.theme.item?.PropertyLabel || row.theme.report.Name)}</h2>${f ? `<div class="hero-badges">${severity(f)}${assessment(f)}<code class="rule-id">${escape(f.RuleId)}</code></div>` : ""}</div>${outside ? '<div class="outside-filter">Selected review is outside your filters.<button data-action="reveal-review">Show in collection</button></div>' : ""}<div class="inspector-tabs" role="tablist" aria-label="Review information">${REVIEW_TABS.map((t) => `<button role="tab" id="review-tab-${t}" data-review-tab="${t}" aria-controls="review-panel" aria-selected="${tab === t}" tabindex="${tab === t ? 0 : -1}">${t}</button>`).join("")}</div><div class="inspector-content review-detail" id="review-panel" role="tabpanel" aria-labelledby="review-tab-${tab}" tabindex="0">${tab === "Evidence" ? evidenceView(data, row, state) : f ? findingReview(data, row) : themeReview(data, row)}</div><div class="inspector-footer"><span>Saved analysis · Read-only</span>${target ? `<button class="primary-button" data-action="review-source">${escape(target.label)} ${icon("arrow")}</button>` : ""}</div>`;
}
