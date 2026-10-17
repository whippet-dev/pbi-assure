import { escape, icon } from "./ui.js";
import { words } from "./data.js";
import {
  scopeRows,
  scopeSummary,
  limitArea,
  areaLabel,
  constructLabel,
  supportLabel,
  confidenceMeaning,
  limitationTarget,
  availableEvidence,
} from "./scope-data.js";
import { targetButton, facts } from "./model-area-view.js";
export const SCOPE_TABS = ["Meaning", "Evidence"];
export function scopeNavigation(data, state) {
  const summary = scopeSummary(data);
  return `<div class="scope-navigation"><nav class="table-nav" aria-label="Confidence views">${[
    ["limits", "Recorded limitations"],
    ["available", "Evidence available"],
    ["boundaries", "Outside scope"],
  ]
    .map(
      ([mode, label]) =>
        `<button data-check-mode="${mode}" class="${state.checkMode === mode ? "chosen" : ""}" aria-pressed="${state.checkMode === mode}"><span>${label}</span>${mode === "limits" ? `<span>${summary.limitations.length}</span>` : ""}</button>`,
    )
    .join(
      "",
    )}</nav><p class="source-label">A usage result describes what was found. Confidence describes the limits of that result.</p></div>`;
}
function scopeSelection(data, state) {
  const rows = scopeRows(data, state);
  if (!data.raw.AnalysisLimitations[state.checkSelected])
    state.checkSelected = rows[0]?.index ?? null;
  return rows;
}
export function scopeWorkspace(data, state) {
  if (state.checkMode === "available") return evidenceSheet(data);
  if (state.checkMode === "boundaries") return boundarySheet();
  const rows = scopeSelection(data, state),
    summary = scopeSummary(data);
  return `<section class="collection-pane scope-collection" aria-label="Recorded analysis limitations"><div class="collection-tools"><div class="scope-intro"><h2>Analysis scope and limitations</h2><p>Usage and confidence are separate. This analysis does not include a complete register of successful checks.</p><div class="scope-summary-line">${targetButton({ kind: "limited", model: "" }, `${summary.limited.length} object results have Checks limited`)}<span>${summary.limitations.length} recorded limitations</span></div>${!summary.limitations.length ? '<p class="quiet">No identified limitations. This does not mean everything was checked.</p>' : ""}</div><div class="collection-title"><strong>Recorded limitations</strong><span id="scope-count">${rows.length} entries</span></div><div class="searchbox">${icon("search")}<input id="scope-search" type="search" aria-label="Search analysis limitations" placeholder="Reason, object or area…" value="${escape(state.checkSearch)}"><kbd>/</kbd></div><div class="filter-line"><label class="sr-only" for="scope-area">Limitation area</label><select id="scope-area"><option value="">All areas</option>${Object.entries(
    areaLabel,
  )
    .map(
      ([value, label]) =>
        `<option value="${value}" ${state.checkArea === value ? "selected" : ""}>${label} · ${summary.areaCounts[value]}</option>`,
    )
    .join(
      "",
    )}</select><label class="sr-only" for="scope-model">Limitation semantic model</label><select id="scope-model"><option value="">All models</option>${[...new Set(data.raw.AnalysisLimitations.map((l) => l.SemanticModel).filter(Boolean))].map((name) => `<option ${state.checkModel === name ? "selected" : ""}>${escape(name)}</option>`).join("")}</select><button class="text-button" data-action="scope-reset">Reset</button></div></div><div id="collection" class="collection-scroll scope-list" tabindex="-1">${scopeCollection(data, state)}</div><div class="collection-footer"><span>Original limitation records</span><span>↑ ↓ to explore · / to search</span></div></section><aside class="inspector" id="scope-inspector" aria-label="Selected analysis limitation">${scopeInspector(data, state)}</aside>`;
}
export function scopeCollection(data, state) {
  const rows = scopeSelection(data, state),
    active = rows.some((row) => row.index === state.checkSelected)
      ? state.checkSelected
      : rows[0]?.index;
  return (
    rows
      .map(
        ({ limitation: l, index }) =>
          `<button class="area-row ${index === state.checkSelected ? "selected" : ""}" data-scope-row="${index}" tabindex="${index === active ? 0 : -1}" ${index === state.checkSelected ? 'aria-current="true"' : ""}><div><strong>${escape(constructLabel(l.ConstructType))}</strong><small>${escape(areaLabel[limitArea(l)])}</small></div><p>${escape(l.SemanticModel || "Project scope")}${l.Table || l.ObjectName ? ` / ${escape([l.Table, l.ObjectName].filter(Boolean).join(" / "))}` : ""}</p><small>${escape(supportLabel(l.SupportState))}</small></button>`,
      )
      .join("") ||
    '<div class="empty"><h2>No matching limitation entries</h2><p>No identified limitations in this view is not a claim that everything was checked. Evidence available and Outside scope describe other boundaries.</p><button class="primary-button" data-action="scope-reset">Clear filters</button></div>'
  );
}
export function scopeInspector(data, state) {
  const l = data.raw.AnalysisLimitations[state.checkSelected];
  if (!l)
    return '<div class="empty"><h2>No limitation selected</h2><p>No identified limitations does not mean exhaustive checking. Explore Evidence available and Outside scope.</p></div>';
  const outside = !scopeRows(data, state).some(
      (row) => row.index === state.checkSelected,
    ),
    target = limitationTarget(data, l),
    limited = data.objects.filter(
      (o) =>
        o.SemanticModel === l.SemanticModel &&
        o.ClassificationConfidence === "QualifiedByLimitation",
    );
  return `<div class="inspector-toolbar"><span>ANALYSIS SCOPE</span></div><div class="object-hero"><div class="object-breadcrumb">${escape(areaLabel[limitArea(l)])} / ${escape(l.SemanticModel || "Project")}</div><h2 id="scope-heading" tabindex="-1">${escape(constructLabel(l.ConstructType))}</h2><p>${escape(supportLabel(l.SupportState))}</p></div>${outside ? '<div class="outside-filter">Selected limitation is outside your filters.<button data-action="scope-reset">Show in collection</button></div>' : ""}<div class="inspector-tabs" role="tablist" aria-label="Limitation information">${SCOPE_TABS.map((t) => `<button role="tab" id="scope-tab-${t}" data-scope-tab="${t}" aria-controls="scope-panel" aria-selected="${state.checkTab === t}" tabindex="${state.checkTab === t ? 0 : -1}">${t}</button>`).join("")}</div><div class="inspector-content scope-detail" id="scope-panel" role="tabpanel" aria-labelledby="scope-tab-${state.checkTab}" tabindex="0">${
    state.checkTab === "Meaning"
      ? `<section><h3>Why this matters</h3><p class="area-lead">${escape(l.Reason)}</p></section><section class="qualification-section"><h3>How to interpret results</h3><p class="area-lead">${escape(confidenceMeaning(l))}</p></section><section><h3>Recorded scope</h3>${facts(
          [
            ["Area", areaLabel[limitArea(l)]],
            ["Model", l.SemanticModel],
            ["Table", l.Table],
            ["Object / query", l.ObjectName],
          ],
        )}${l.DependencyImpact !== "MayCreateQueryDependencies" && l.SemanticModel ? `<p class="quiet">${limited.length} model result${limited.length === 1 ? " carries" : "s carry"} Checks limited. This analysis does not map their individual qualification to this limitation.</p>${targetButton({ kind: "limited", model: l.SemanticModel }, "View model objects with Checks limited")}` : ""}</section>`
      : `<section><h3>Original limitation evidence</h3>${facts([
          ["Cause", words(l.Cause)],
          ["Source", l.ArtifactPath],
          ["Evidence location", l.EvidencePath],
          ["Limitation ID", l.LimitationId],
          ["Support state", l.SupportState],
          ["Dependency impact", l.DependencyImpact],
          ["Concerns", (l.Concerns || []).join(" · ")],
        ])}</section><p class="footnote">Per-limitation affected-object reach is not published. A record can describe an analysis boundary without qualifying every object in that model.</p>`
  }</div><div class="inspector-footer scope-actions"><span>Saved analysis</span>${targetButton(target, target?.label, true)}</div>`;
}
function evidenceSheet(data) {
  return `<section id="collection" class="scope-sheet" tabindex="-1"><div class="eyebrow">EVIDENCE AVAILABLE</div><h2>What is present in this snapshot</h2><p class="scope-lead">This snapshot contains definitions, usage results and recorded limitations. These are available evidence, not a pass/fail checklist. A complete register of successful checks is not included.</p><div class="scope-evidence-rows">${availableEvidence(
    data,
  )
    .map(
      ([title, count, explanation, view]) =>
        `<section><div><h3>${title}</h3><p>${escape(count)}</p></div><div><p>${explanation}</p>${targetButton({ kind: "view", view }, `Explore ${title.toLowerCase()}`)}</div></section>`,
    )
    .join(
      "",
    )}</div><p class="footnote">Limitation records are not mapped to every affected object. Interpret each usage result using its own confidence label.</p></section>`;
}
function boundarySheet() {
  return `<section id="collection" class="scope-sheet" tabindex="-1"><div class="eyebrow">OUTSIDE SCOPE</div><h2>Saved definitions cannot establish everything</h2><p class="scope-lead">No identified limitations means none were recorded within the scan. It does not mean everything was checked.</p><div class="scope-boundary-rows"><section><h3>Runtime calculations and refresh</h3><p>The scan does not execute DAX or Power Query, test connector availability, verify refresh success or measure runtime performance.</p></section><section><h3>Effective user access</h3><p>Saved roles, filters and explicit permissions are inspectable. Power BI Service membership and effective user-access testing are outside this analysis.</p></section><section><h3>Consumers outside this project</h3><p>Other reports, external tools and runtime consumers outside the scanned project can still use objects. Apparently unused remains a review candidate, never permission to delete.</p></section><section><h3>Rendered report behaviour</h3><p>Saved visual configuration, navigation and accessibility observations do not reproduce final rendering or establish an accessibility certification.</p></section><section><h3>Evidence not included in this analysis</h3><p>This snapshot does not include every object affected by each limitation, all structural provenance, or complete report-measure ownership and usage locations. These gaps remain explicit.</p></section></div></section>`;
}
