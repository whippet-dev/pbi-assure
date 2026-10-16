import {
  adaptInventory,
  STATES,
  filterObjects,
  edgeId,
  supportingEdges,
  words,
} from "./data.js";
import { escape, icon, glyph } from "./ui.js";
import {
  buildLineageData,
  createLineageSession,
  BRANCH_SIZE,
  LINEAGE_TABS,
  revealLineage,
  lineageEndpoints,
} from "./lineage-data.js";
import {
  lineageNavigation,
  lineageSearchResults,
  lineageWorkspace,
  lineageInspector,
} from "./lineage-view.js";
import { buildQueryData, filterQueries } from "./query-data.js";
import {
  QUERY_TABS,
  queryNavigation,
  queryWorkspace,
  queryCollection,
  queryInspector,
} from "./query-view.js";
import { reportContext, visualLabel } from "./report-data.js";
import { pageId, pagesFor } from "./report-data.js";
import { overviewData } from "./overview-data.js";
import { overviewWorkspace } from "./overview-view.js";
import {
  REVIEW_DEFAULTS,
  REVIEW_TABS,
  reviewRows,
  selectedReview,
  reviewTarget,
  reviewLabel,
} from "./review-data.js";
import {
  reviewsWorkspace,
  reviewsNavigation,
  reviewCollection,
  reviewInspector,
} from "./review-view.js";
import {
  VISUAL_TABS,
  reportSelection,
  reportNavigation,
  reportWorkspace,
  visualCollection,
  visualInspector,
  visibleVisuals,
} from "./report-view.js";

const $ = (s) => document.querySelector(s);
const state = {
  view: "overview",
  model: "PbiAssureCoverage",
  table: "",
  type: "",
  usage: "",
  search: "",
  system: false,
  limited: false,
  sort: "name",
  tab: "Summary",
  selected: null,
  history: [],
  cursor: -1,
  page: null,
  report: null,
  reportVisuals: {},
  reportScroll: {},
  visualSearch: "",
  visualTab: "Uses",
  reportPageDetails: false,
  reportOrigin: null,
  reviewMode: "main",
  reviewCategory: "",
  reviewSeverity: "",
  reviewRequired: false,
  reviewSearch: "",
  reviewSelected: null,
  reviewReturn: false,
  reviewReport: "",
  reviewPage: "",
  reviewModel: "",
  reviewTab: "Review",
  reviewLimitOpen: false,
  reviewViews: {},
  reviewOrigin: null,
  queryModel: "",
  queryState: "",
  queryKind: "",
  querySearch: "",
  querySelected: null,
  queryTab: "Summary",
  queryHistory: [],
  queryCursor: -1,
  queryOrigin: null,
  lineageCentre: null,
  lineageSessions: {},
  lineageHistory: [],
  lineageCursor: -1,
  lineageModel: "PbiAssureCoverage",
  lineageSearch: "",
  lineageSearchLimit: BRANCH_SIZE,
  lineageOrigin: null,
  workspaceScroll: {},
  source: "Repository fixture",
  fixture: true,
};
let data, queryIndex, lineageIndex;
const announce = (text) => {
  $("#announcement").textContent = text;
};
const badge = (o) =>
  `<span class="badge ${STATES[o.UsageState].tone}"><i></i>${escape(STATES[o.UsageState].label)}</span>`;
const selected = () => data.byId.get(state.selected);
const uniqueEdges = (edges) => [
  ...new Map(edges.map((e) => [edgeId(e, "From"), e])).values(),
];
const incoming = (o) => uniqueEdges(data.incoming.get(o.id) || []);
const visible = () => filterObjects(data, state);
const filtersActive = () =>
  state.table || state.type || state.usage || state.search || state.limited;
const resetFilters = () =>
  Object.assign(state, {
    table: "",
    type: "",
    usage: "",
    search: "",
    limited: false,
  });

function shell() {
  $("#app").innerHTML =
    `<header class="topbar"><a class="brand" href="./" aria-label="PBI Assure home"><img src="./mark.svg" alt="">PBI <strong>Assure</strong></a><span class="top-divider"></span><span class="workspace-name">Investigation workspace</span><span class="prototype">PROTOTYPE</span><div class="top-actions"><span class="local-dot"></span><span>Local & read-only</span><button class="open-button" data-action="import">${icon("folder")}Open analysis JSON</button><input type="file" id="file" accept=".json,application/json" hidden></div></header>
    <div class="app-layout"><aside class="sidebar" aria-label="Workspace navigation"></aside><main id="main"><header class="main-heading"></header><div class="workspace"></div></main></div><div id="toast" class="toast" role="alert" hidden></div>`;
  renderSidebar();
  renderWorkspace();
}
function renderSidebar() {
  if (state.view === "lineage") ensureLineage();
  const tables =
    data.raw.SemanticModels.find((m) => m.Name === state.model)?.Tables || [];
  const modelObjects = data.objects.filter(
    (o) =>
      (!state.model || o.SemanticModel === state.model) &&
      (state.system || !o.system),
  );
  $(".sidebar").innerHTML =
    `<div class="project-label">OPEN ANALYSIS</div><div class="project-name">${state.fixture ? "Coverage study" : escape(state.source)}</div><div class="source-label">${state.fixture ? "Synthetic repository fixture" : "Local JSON inventory"}</div>
    <nav class="primary-nav" aria-label="Investigation areas">${[
      ["overview", "Overview"],
      ["objects", "Model objects"],
      ["lineage", "Lineage"],
      ["pages", "Report pages"],
      ["queries", "Power Query"],
      ["reviews", "Reviews & findings"],
      ["check", "What was checked"],
    ]
      .map(
        ([v, label]) =>
          `<button data-view="${v}" class="nav-item ${state.view === v ? "active" : ""}" ${state.view === v ? 'aria-current="page"' : ""}>${icon(v)}${label}${v === "objects" ? `<span>${data.objects.length}</span>` : ""}</button>`,
      )
      .join("")}</nav>
    ${
      state.view === "pages"
        ? reportNavigation(data, state)
        : state.view === "lineage"
          ? lineageNavigation(lineageIndex, state)
          : state.view === "queries"
            ? queryNavigation(queryIndex, state)
            : state.view === "reviews"
              ? reviewsNavigation(data, state)
              : state.view !== "objects"
                ? ""
                : `    <div class="model-navigation"><label class="project-label" for="model-select">SEMANTIC MODEL</label><select id="model-select" aria-label="Semantic model"><option value="" ${!state.model ? "selected" : ""}>All models</option>${data.raw.SemanticModels.map((m) => `<option ${m.Name === state.model ? "selected" : ""}>${escape(m.Name)}</option>`).join("")}</select>${
                    !state.model
                      ? `<div class="table-heading">Choose a model to browse its tables</div><nav class="table-nav" aria-label="Filter by model">${data.raw.SemanticModels.map((m) => `<button data-model="${escape(m.Name)}">${icon("objects")}<span class="table-name">${escape(m.Name)}</span><span>${modelObjects.filter((o) => o.SemanticModel === m.Name).length}</span></button>`).join("")}</nav>`
                      : `<div class="table-heading">Tables <span>${tables.filter((t) => state.system || !t.IsSystemGenerated).length}</span></div><nav class="table-nav" aria-label="Filter by table"><button data-table="" class="${!state.table ? "chosen" : ""}">${icon("objects")}All tables<span>${modelObjects.length}</span></button>${tables
                          .filter((t) => state.system || !t.IsSystemGenerated)
                          .map(
                            (t) =>
                              `<button data-table="${escape(t.Name)}" class="${state.table === t.Name ? "chosen" : ""}" title="${escape(t.Name)}"><span class="table-symbol">▦</span><span class="table-name">${escape(t.Name)}</span><span>${modelObjects.filter((o) => o.Table === t.Name).length}</span></button>`,
                          )
                          .join("")}</nav>`
                  }</div>`
    }
    <div class="sidebar-bottom"><span class="project-label">ANALYSIS SCOPE</span><p>${data.raw.Reports.length} reports · ${data.raw.SemanticModels.length} models</p><span class="source-label">Metadata only. No DAX execution.</span><button class="text-button" data-action="scope">View evidence boundaries ${icon("arrow")}</button></div>`;
}
function renderWorkspace() {
  document.querySelectorAll(".report-origin").forEach((el) => el.remove());
  $(".workspace").classList.toggle("report-workspace", state.view === "pages");
  $(".workspace").classList.toggle("query-workspace", state.view === "queries");
  $(".workspace").classList.toggle(
    "lineage-workspace",
    state.view === "lineage",
  );
  $(".workspace").classList.toggle(
    "reviews-workspace",
    state.view === "reviews",
  );
  $(".workspace").classList.toggle(
    "overview-workspace",
    state.view === "overview",
  );
  const heading =
    state.view === "overview"
      ? [
          "PROJECT / INVESTIGATE",
          "Overview",
          "What needs attention, and where to investigate next.",
        ]
      : state.view === "lineage"
        ? [
            "MODEL / TRACE",
            "Lineage",
            "Explore every recorded branch. Keep the object at the centre.",
          ]
        : state.view === "reviews"
          ? [
              "ANALYSIS / REVIEW",
              "Reviews & findings",
              "Inspect the original evidence, then follow its source.",
            ]
          : state.view === "objects"
            ? [
                "MODEL / INVESTIGATE",
                "Objects",
                "Follow the evidence. Keep the context.",
              ]
            : state.view === "pages"
              ? [
                  "REPORT / EXPLORE",
                  "Report pages",
                  "Stay on the page. Follow each visual’s evidence.",
                ]
              : state.view === "queries"
                ? [
                    "DATA / PREPARE",
                    "Power Query",
                    "Explore queries, their dependencies and their model connections.",
                  ]
                : [
                    "ANALYSIS / SCOPE",
                    "What was checked",
                    "Understand the evidence before interpreting an absence.",
                  ];
  $(".main-heading").innerHTML =
    `<div><div class="eyebrow">${heading[0]}</div><h1 id="workspace-heading" tabindex="-1">${heading[1]}</h1><p>${heading[2]}</p></div><div class="heading-note">${state.fixture ? '<span class="fixture-mark">FIXTURE</span> Real scanner output<br><small>Synthetic PBIP · schema 0.26</small>' : '<span class="fixture-mark">LOCAL</span> Your analysis snapshot<br><small>Kept in this browser tab</small>'}</div>`;
  if (
    state.reportOrigin &&
    !state.lineageOrigin &&
    !state.queryOrigin &&
    !state.reviewReturn &&
    ["objects", "check"].includes(state.view)
  ) {
    $(".main-heading").insertAdjacentHTML(
      "afterend",
      `<div class="report-origin"><button data-action="return-visual">${icon("back")}Back to visual</button><span>${escape(state.reportOrigin.report)} / ${escape(state.reportOrigin.page)} / <strong>${escape(state.reportOrigin.visual)}</strong></span><small>Report context preserved</small></div>`,
    );
  }
  if (
    state.reviewReturn &&
    !state.lineageOrigin &&
    !["overview", "reviews"].includes(state.view)
  )
    $(".main-heading").insertAdjacentHTML(
      "afterend",
      `<div class="report-origin"><button data-action="return-reviews">${icon("back")}Back to reviews</button><span>${escape(reviewLabel(data, state))} · Review selection and filters preserved</span></div>`,
    );
  if (
    state.queryOrigin &&
    !state.lineageOrigin &&
    !state.reviewReturn &&
    ["objects", "check"].includes(state.view)
  )
    $(".main-heading").insertAdjacentHTML(
      "afterend",
      `<div class="report-origin"><button data-action="return-query">${icon("back")}Back to Power Query</button><span>${escape(state.queryOrigin.name)}</span><small>Query selection and filters preserved</small></div>`,
    );
  if (
    state.lineageOrigin &&
    ["objects", "pages", "check", "queries"].includes(state.view)
  )
    $(".main-heading").insertAdjacentHTML(
      "afterend",
      `<div class="report-origin"><button data-action="return-lineage">${icon("back")}Back to lineage</button><span>${escape(lineageIndex.nodes.get(state.lineageCentre)?.ObjectName)} · Centre, branches and selection preserved</span></div>`,
    );
  if (state.view === "objects") {
    $(".workspace").innerHTML =
      `<section class="collection-pane" aria-label="Model object collection"><div class="collection-tools"><div class="collection-title"><strong>${escape(state.table || (state.model ? "All tables" : "All models"))}</strong><span id="result-count"></span></div><div class="searchbox">${icon("search")}<input id="search" type="search" placeholder="Search objects or tables…" aria-label="Search objects or tables" value="${escape(state.search)}"><kbd>/</kbd></div><div class="filter-line"><label class="sr-only" for="usage-filter">Usage state</label><select id="usage-filter"><option value="">All usage states</option>${Object.entries(
        STATES,
      )
        .map(
          ([k, v]) =>
            `<option value="${k}" ${state.usage === k ? "selected" : ""}>${v.label}</option>`,
        )
        .join(
          "",
        )}</select><label class="limited-filter"><input id="limited" type="checkbox" ${state.limited ? "checked" : ""}>Checks limited</label><button class="text-button" data-action="reset" title="Clear collection filters">Reset</button></div><div class="type-filters" aria-label="Filter object type">${[
        ["", "All objects"],
        ["Measure", "Measures"],
        ["Column", "Columns"],
        ["HierarchyLevel", "Hierarchy levels"],
        ["CalculationItem", "Calculation items"],
      ]
        .map(
          ([k, label]) =>
            `<button data-type="${k}" aria-pressed="${state.type === k}" class="${state.type === k ? "active" : ""}">${label}</button>`,
        )
        .join(
          "",
        )}</div></div><div id="collection" class="collection-scroll" tabindex="-1"></div><div class="collection-footer"><label><input id="system" type="checkbox" ${state.system ? "checked" : ""}>Include system-generated</label><span>↑ ↓ to explore · Enter to select</span></div></section><aside id="inspector" class="inspector" aria-label="Selected object"></aside>`;
    renderCollection();
    renderInspector();
  } else if (state.view === "pages") renderPages();
  else if (state.view === "lineage") renderLineage();
  else if (state.view === "queries") {
    const firstVisit = !queryIndex.byId.has(state.querySelected);
    $(".workspace").innerHTML = queryWorkspace(queryIndex, state);
    renderQueries();
    if (firstVisit)
      $(`[data-query="${CSS.escape(state.querySelected)}"]`)?.scrollIntoView({
        block: "center",
      });
  } else if (state.view === "overview")
    $(".workspace").innerHTML = overviewWorkspace(data);
  else if (state.view === "reviews")
    $(".workspace").innerHTML = reviewsWorkspace(data, state);
  else renderCoverage();
}
function renderCollection() {
  const rows = visible();
  $("#result-count").textContent = `${rows.length} objects`;
  const active = rows.some((o) => o.id === state.selected)
    ? state.selected
    : rows[0]?.id;
  $("#collection").innerHTML = rows.length
    ? `<table class="objects-table"><thead><tr><th scope="col"><button data-action="sort-name">Object ${state.sort === "name" ? "↓" : ""}</button></th><th scope="col"><button data-action="sort-usage">Usage ${state.sort === "usage" ? "↓" : ""}</button></th><th scope="col" class="numeric"><span title="Distinct immediate dependency sources">Used by</span></th></tr></thead><tbody>${rows.map((o) => `<tr class="${o.id === state.selected ? "selected" : ""}"><td><button class="object-select" data-object="${escape(o.id)}" tabindex="${o.id === active ? "0" : "-1"}" ${o.id === state.selected ? 'aria-current="true"' : ""}>${glyph(o.ObjectType)}<span><strong>${escape(o.ObjectName)}</strong><small>${!state.model ? escape(o.SemanticModel) + " · " : ""}${escape(o.Table)} · ${escape(words(o.ObjectType))}${o.detail?.IsHidden ? " · Hidden" : ""}</small></span></button></td><td>${badge(o)}${o.ClassificationConfidence === "QualifiedByLimitation" ? '<small class="checks-limited">◐ Checks limited</small>' : ""}</td><td class="numeric">${incoming(o).length || '<span class="muted">—</span>'}</td></tr>`).join("")}</tbody></table>`
    : `<div class="empty"><span class="empty-symbol">⌕</span><h2>No matching objects</h2><p>Try a different name, table or usage state.</p><button class="primary-button" data-action="reset">Clear filters</button></div>`;
  announce(`${rows.length} objects in collection.`);
}
function relationList(edges, side, limit = Infinity) {
  if (!edges.length)
    return '<p class="quiet">No dependency evidence recorded in this direction.</p>';
  return `<div class="relations">${edges
    .slice(0, limit)
    .map((e) => {
      const id = edgeId(e, side),
        object = data.byId.get(id);
      return `<div class="relation-row">${glyph(e[side + "ObjectType"])}<div>${object ? `<button class="related-object" data-related="${escape(id)}">${escape(e[side + "ObjectName"])}${icon("arrow")}</button>` : `<strong>${escape(e[side + "ObjectName"])}</strong>`}<small>${escape(e[side + "Table"] || words(e[side + "ObjectType"]))} · ${escape(words(e.DependencyKind))}</small>${!object ? '<small class="quiet">Graph evidence · outside this object collection</small>' : ""}</div>${object ? `<span class="relation-state" title="${escape(STATES[object.UsageState].label)}">${escape(STATES[object.UsageState].label)}</span>` : ""}</div>`;
    })
    .join("")}</div>`;
}
function reportEvidence(o, full = false) {
  if (!o.DirectReportLocations.length)
    return `<p class="quiet">No direct report locations recorded.${o.UsageState === "IndirectlyUsed" ? " This object is reached through dependencies; see Used by." : ""}</p>`;
  return `<div class="report-locations">${o.DirectReportLocations.map((loc) => {
    const report = data.raw.Reports.find(
      (r) => r.RelativePath === loc.Report || r.Name === loc.Report,
    );
    const page = report?.Pages.find((p) => p.Name === loc.Page);
    const visual = page?.Visuals.find((v) => v.Name === loc.Visual);
    return `<div class="report-location">${icon("pages")}<div><strong>${escape(page?.DisplayName || loc.Page || "Report-level reference")}</strong><small>${escape(report?.Name || loc.Report)} · ${escape(visual?.Accessibility?.TitleText || visual?.VisualType || loc.UsageContext || loc.LocationKind)}</small>${loc.Visual ? `<code>${escape(loc.Visual)}</code>` : ""}${full ? `<small>${escape(loc.LocationKind)}${visual?.IsHidden ? " · Hidden visual" : ""}</small>` : ""}</div></div>`;
  }).join("")}</div>`;
}
function expression(o, full = false) {
  const dax = o.detail?.Expression;
  return dax
    ? `<div class="code-heading"><span>DAX EXPRESSION</span><button class="text-button" data-action="copy">Copy</button></div><pre class="dax ${full ? "full" : ""}"><code>${highlight(dax)}</code></pre>${o.detail.FormatString ? `<p class="format-label">Format string <code>${escape(o.detail.FormatString)}</code></p>` : ""}`
    : `<p class="quiet">No DAX expression is published for this ${escape(words(o.ObjectType).toLowerCase())} in the JSON inventory.${o.ObjectType === "Column" ? " Imported columns receive their data from the model’s partition." : ""}</p>`;
}
function highlight(dax) {
  // Tokenise before escaping so metadata cannot introduce markup.
  return (
    dax.match(
      /"(?:[^"]|"")*"|'(?:[^']|'')*'|\[[^\]]*\]|\b[A-Za-z_][A-Za-z_0-9]*(?=\s*\()|\b(?:VAR|RETURN|TRUE|FALSE)\b|[^\["'A-Za-z]+|[A-Za-z_0-9]+|./g,
    ) || []
  )
    .map((t) => {
      const cls = t.startsWith("[")
        ? "dax-field"
        : /^[A-Z_]+$/.test(t)
          ? "dax-function"
          : t.startsWith('"')
            ? "dax-string"
            : "";
      return cls ? `<span class="${cls}">${escape(t)}</span>` : escape(t);
    })
    .join("");
}
function renderInspector() {
  const o = selected();
  if (!o) {
    $("#inspector").innerHTML =
      '<div class="empty"><h2>Select an object</h2><p>Its evidence and dependencies will appear here.</p></div>';
    return;
  }
  const inEdges = incoming(o),
    outEdges = data.outgoing.get(o.id) || [],
    support = supportingEdges(data, o);
  const outside = !visible().some((v) => v.id === o.id);
  $("#inspector").innerHTML =
    `<div class="inspector-toolbar"><div class="history-buttons"><button data-action="back" aria-label="Previous inspected object" ${state.cursor <= 0 ? "disabled" : ""}>${icon("back")}</button><button data-action="forward" aria-label="Next inspected object" ${state.cursor >= state.history.length - 1 ? "disabled" : ""}>${icon("arrow")}</button></div><span>OBJECT INSPECTOR</span><span class="history-count">${state.cursor + 1} / ${state.history.length}</span></div>
    <div class="object-hero"><div class="object-breadcrumb">${escape(o.SemanticModel)} <span>/</span> ${escape(o.Table)}</div><div class="object-title">${glyph(o.ObjectType)}<h2 id="object-heading" tabindex="-1">${escape(o.ObjectName)}</h2></div><div class="object-meta"><span>${escape(words(o.ObjectType))}</span>${o.detail?.DataType ? `<span>${escape(o.detail.DataType)}</span>` : ""}${o.detail?.IsHidden ? "<span>Hidden</span>" : ""}${o.system ? "<span>System-generated</span>" : ""}</div><div class="hero-badges">${badge(o)}${o.ClassificationConfidence === "QualifiedByLimitation" ? '<button class="limit-badge" data-action="object-coverage">◐ Checks limited</button>' : '<span class="established">Established within scope</span>'}</div></div>
    ${outside ? '<div class="outside-filter">Following a dependency outside your collection filters.<button data-action="reveal">Show in collection</button></div>' : ""}
    <div class="inspector-tabs" role="tablist" aria-label="Object information">${["Summary", "Dependencies", "DAX", "Evidence"].map((tab) => `<button role="tab" id="tab-${tab}" aria-controls="detail-panel" aria-selected="${state.tab === tab}" tabindex="${state.tab === tab ? "0" : "-1"}" data-tab="${tab}">${tab}${tab === "Dependencies" ? `<span>${inEdges.length + outEdges.length}</span>` : ""}</button>`).join("")}</div><div id="detail-panel" class="inspector-content" role="tabpanel" aria-labelledby="tab-${state.tab}" tabindex="0">${
      state.tab === "Summary"
        ? `<section class="why-section"><div class="section-label"><span class="small-dot ${STATES[o.UsageState].tone}"></span>WHY ${escape(STATES[o.UsageState].label.toUpperCase())}</div><p class="why-text">${escape(STATES[o.UsageState].why)}</p>${support.length ? `<div class="supporting"><span>Supporting predecessor${support.length === 1 ? "" : "s"} from the scanner’s reachability</span>${relationList(uniqueEdges(support), "From", 2)}</div>` : ""}${["ApparentlyUnused", "UsedOnlyByUnusedBranch"].includes(o.UsageState) ? '<div class="review-note">Review candidate, never permission to delete. Other reports, external consumers and runtime behaviour may use this object.</div>' : ""}${o.ClassificationConfidence === "QualifiedByLimitation" ? '<p class="limitation-note">Incomplete analysis may affect this classification. Usage state and confidence are separate. <button data-action="object-coverage">Read model limitations →</button></p>' : ""}</section>
      <section><div class="section-heading"><h3>Used by <span>${inEdges.length}</span></h3>${inEdges.length > 3 ? '<button class="text-button" data-go-tab="Dependencies">View all →</button>' : ""}</div>${relationList(inEdges, "From", 3)}</section><section><div class="section-heading"><h3>Used in the report <span>${o.DirectReportLocations.length}</span></h3><span class="subtle-label">DIRECT LOCATIONS</span></div>${reportEvidence(o)}</section><section>${expression(o)}</section>`
        : state.tab === "Dependencies"
          ? `<div class="panel-intro"><h3>Follow the dependency chain</h3><p>Each link opens in this inspector. Your collection filters and position stay in place.</p></div><section><h3>Used by <span>${inEdges.length}</span></h3><p class="quiet">Immediate sources referencing this object. These may include unused branches.</p>${relationList(inEdges, "From")}</section><section><h3>Depends on <span>${outEdges.length}</span></h3>${relationList(outEdges, "To")}</section><p class="footnote">Report measures and other graph-only nodes retain their evidence but cannot be opened as model objects. Same-named report-measure owners are not distinguished by this JSON contract.</p>`
          : state.tab === "DAX"
            ? `<div class="panel-intro"><h3>Calculation definition</h3><p>Saved model metadata. Expressions are displayed, never executed.</p></div>${expression(o, true)}`
            : `<div class="panel-intro"><h3>Evidence & provenance</h3><p>Inspect the saved references behind this result.</p></div><section><h3>Object definition</h3><dl class="metadata"><dt>Semantic model</dt><dd>${escape(o.SemanticModel)}</dd><dt>Table</dt><dd>${escape(o.Table)}</dd><dt>Source</dt><dd><code>${escape(o.table?.RelativePath || "Not published in JSON")}</code></dd><dt>Confidence</dt><dd>${o.ClassificationConfidence === "QualifiedByLimitation" ? "Checks limited" : "Established within analysed scope"}</dd>${o.detail?.SourceColumn ? `<dt>Source column</dt><dd>${escape(o.detail.SourceColumn)}</dd>` : ""}${o.detail?.SortByColumn ? `<dt>Sort by column</dt><dd>${escape(o.detail.SortByColumn)}</dd>` : ""}</dl></section><section><h3>Direct report evidence</h3>${reportEvidence(o, true)}${o.DirectReportReferences.map((e) => `<details class="evidence"><summary>${escape(words(e.UsageContext))}${e.Role ? " · " + escape(e.Role) : ""}</summary><code>${escape(e.ArtifactPath)}<br>${escape(e.EvidencePath)}</code></details>`).join("")}</section><section><h3>Dependency evidence</h3>${[...(data.incoming.get(o.id) || []), ...outEdges].map((e) => `<details class="evidence"><summary>${escape(words(e.DependencyKind))} · ${escape(e.FromObjectName)} → ${escape(e.ToObjectName)}</summary><code>${escape(e.EvidenceText)}<br><br>${escape(e.EvidencePath)}</code></details>`).join("") || '<p class="quiet">No dependency edges recorded.</p>'}</section><section><h3>Model-level limitations</h3><p class="quiet">These belong to this model. JSON does not publish the exact affected-object reach of each limitation; the object’s confidence above is authoritative.</p>${modelLimitations(o.SemanticModel)}<button class="text-button" data-action="scope">What was checked →</button></section>`
    }</div><div class="inspector-footer">${icon("check")} Evidence from saved metadata <span>Read-only</span></div>`;
}
function modelLimitations(model) {
  const limitations = data.raw.AnalysisLimitations.filter(
    (l) => l.SemanticModel === model,
  );
  return (
    limitations
      .map(
        (l) =>
          `<details class="evidence limitation"><summary>${escape(l.ConstructType)} · ${escape(words(l.SupportState))}</summary><p>${escape(l.Reason)}</p><code>${escape(l.ArtifactPath)}<br>${escape(l.EvidencePath)}</code></details>`,
      )
      .join("") ||
    '<p class="quiet">No model-level limitations recorded by this scan. Permanent analysis boundaries still apply.</p>'
  );
}
function rememberReportScroll() {
  const list = $(".visual-list");
  if (list)
    state.reportScroll[list.dataset.pageContext] = {
      list: list.scrollTop,
      detail: $(".visual-detail")?.scrollTop || 0,
    };
}
function renderPages() {
  rememberReportScroll();
  $(".workspace").innerHTML = reportWorkspace(data, state);
  if (!$("#visual-inspector")) return;
  renderVisualCollection();
  renderVisualInspector();
  const scroll = state.reportScroll[state.page];
  if (scroll) {
    $(".visual-list").scrollTop = scroll.list;
    $(".visual-detail").scrollTop = scroll.detail;
  }
}
function renderVisualCollection() {
  $("#collection").innerHTML = visualCollection(data, state);
  $("#visual-result-count").textContent =
    `${visibleVisuals(data, state).length} of ${reportContext(data, state).page?.Visuals.length || 0} visuals`;
}
function renderVisualInspector() {
  $("#visual-inspector").innerHTML = visualInspector(data, state);
  $("#visual-inspector").setAttribute(
    "aria-label",
    state.reportPageDetails || !reportSelection(data, state).visual
      ? "Selected page details"
      : "Selected visual",
  );
}
function selectVisual(name, { focusHeading = false } = {}) {
  const { page } = reportContext(data, state);
  if (!page?.Visuals.some((v) => v.Name === name)) return;
  state.reportVisuals[state.page] = name;
  state.reportPageDetails = false;
  renderVisualCollection();
  renderVisualInspector();
  announce(
    `Inspecting ${visualLabel(page.Visuals.find((v) => v.Name === name))} on ${page.DisplayName || page.Name}.`,
  );
  if (focusHeading) $("#visual-heading").focus();
  else $(`[data-visual="${CSS.escape(name)}"]`)?.focus();
}
const modelContextKeys = [
  "model",
  "table",
  "type",
  "usage",
  "search",
  "system",
  "limited",
  "sort",
  "tab",
  "selected",
  "history",
  "cursor",
];
const lineageSession = () => state.lineageSessions[state.lineageCentre];
function ensureLineage() {
  if (!lineageIndex.nodes.has(state.lineageCentre)) {
    const node =
      lineageIndex.nodes.get(state.selected) ||
      [...lineageIndex.nodes.values()].find(
        (n) => n.ObjectName === "BaseAmount",
      ) ||
      lineageIndex.nodes.values().next().value;
    if (!node) return null;
    state.lineageCentre = node.id;
    state.lineageHistory = [node.id];
    state.lineageCursor = 0;
  }
  state.lineageSessions[state.lineageCentre] ||= createLineageSession(
    state.lineageCentre,
  );
  return lineageSession();
}
function rememberLineage() {
  const session = lineageSession();
  if (state.view === "lineage" && session)
    session.scroll = ["#lineage-up", "#lineage-down", ".lineage-detail"].map(
      (selector) => $(selector)?.scrollTop || 0,
    );
}
function renderLineage() {
  const session = ensureLineage();
  if (!session) {
    $(".workspace").innerHTML =
      '<div class="empty"><h2>No semantic graph objects</h2><p>Open an analysis with semantic dependency data.</p></div>';
    return;
  }
  $(".workspace").innerHTML = lineageWorkspace(lineageIndex, state, session);
  ["#lineage-up", "#lineage-down", ".lineage-detail"].forEach((selector, i) => {
    if ($(selector)) $(selector).scrollTop = session.scroll[i] || 0;
  });
}
function centreLineage(id, { push = true } = {}) {
  if (!lineageIndex.nodes.has(id)) return;
  rememberLineage();
  if (push && id !== state.lineageCentre) {
    state.lineageHistory = state.lineageHistory.slice(
      0,
      state.lineageCursor + 1,
    );
    state.lineageHistory.push(id);
    state.lineageCursor = state.lineageHistory.length - 1;
  }
  state.lineageCentre = id;
  state.lineageSessions[id] ||= createLineageSession(id);
  renderSidebar();
  renderLineage();
  announce(`Lineage centred on ${lineageIndex.nodes.get(id).ObjectName}.`);
  $("#lineage-node-heading")?.focus({ preventScroll: true });
}
function selectLineage(id, { focus = true } = {}) {
  if (!lineageIndex.nodes.has(id)) return;
  rememberLineage();
  const session = lineageSession();
  session.selected = id;
  session.scroll[2] = 0;
  renderLineage();
  if (focus) $("#lineage-node-heading")?.focus({ preventScroll: true });
}
function lineageVisit() {
  rememberWorkspace();
  const keys = [
    ...modelContextKeys,
    "report",
    "page",
    "reportVisuals",
    "reportScroll",
    "visualSearch",
    "visualTab",
    "reportPageDetails",
    "reportOrigin",
    "queryOrigin",
    "reviewReturn",
    "workspaceScroll",
  ];
  state.lineageOrigin = {
    context: Object.fromEntries(
      keys.map((key) => [key, structuredClone(state[key])]),
    ),
  };
}
function openLineageModel() {
  const node = lineageIndex.nodes.get(lineageSession().selected);
  if (!node?.usage) return;
  lineageVisit();
  resetFilters();
  Object.assign(state, {
    model: node.SemanticModel,
    table: node.Table,
    system: node.usage.system,
    tab: "Summary",
    selected: null,
  });
  showView("objects", { restore: false });
  inspect(node.id, { focus: true });
}
function renderQueries({ resetDetail = false } = {}) {
  const scroll = $("#collection")?.scrollTop || 0;
  const detailScroll = resetDetail ? 0 : $(".query-detail")?.scrollTop || 0;
  $("#collection").innerHTML = queryCollection(queryIndex, state);
  $("#collection").scrollTop = scroll;
  $("#query-result-count").textContent =
    `${filterQueries(queryIndex, state).length} queries`;
  $("#query-inspector").innerHTML = queryInspector(queryIndex, state);
  if ($(".query-detail")) $(".query-detail").scrollTop = detailScroll;
}
function selectQuery(id, { push = true, focus = false } = {}) {
  if (!queryIndex.byId.has(id)) return;
  if (push && id !== state.querySelected) {
    state.queryHistory = state.queryHistory.slice(0, state.queryCursor + 1);
    state.queryHistory.push(id);
    state.queryCursor = state.queryHistory.length - 1;
  }
  state.querySelected = id;
  renderQueries({ resetDetail: true });
  announce(`Inspecting query ${queryIndex.byId.get(id).QueryName}.`);
  (focus ? $("#query-heading") : $(`[data-query="${CSS.escape(id)}"]`))?.focus({
    preventScroll: focus,
  });
}
function resetQueryFilters() {
  Object.assign(state, {
    queryModel: "",
    queryState: "",
    queryKind: "",
    querySearch: "",
  });
}
function openQueryTable(model, table) {
  rememberWorkspace();
  state.queryOrigin = {
    name: queryIndex.byId.get(state.querySelected).QueryName,
    modelScroll: structuredClone(state.workspaceScroll.objects || []),
    model: Object.fromEntries(
      modelContextKeys.map((key) => [key, structuredClone(state[key])]),
    ),
  };
  resetFilters();
  Object.assign(state, {
    model,
    table,
    view: "objects",
    tab: "Summary",
    selected: null,
    reviewReturn: !!state.reviewOrigin,
  });
  state.system = !!data.raw.SemanticModels.find(
    (m) => m.Name === model,
  )?.Tables.find((t) => t.Name === table)?.IsSystemGenerated;
  renderSidebar();
  renderWorkspace();
  const object = visible()[0];
  if (object) inspect(object.id, { focus: true });
  else $("#workspace-heading").focus();
}
function returnToQuery() {
  const origin = state.queryOrigin;
  state.queryOrigin = null;
  showView("queries");
  if (origin) {
    Object.assign(state, origin.model);
    state.workspaceScroll.objects = origin.modelScroll;
  }
  $("#query-heading")?.focus({ preventScroll: true });
}
function openModelFromVisual(id) {
  const object = data.byId.get(id);
  if (!object) return;
  state.queryOrigin = null;
  rememberReportScroll();
  const { report, page, visual } = reportSelection(data, state);
  state.reportOrigin = {
    report: report.Name,
    page: page.DisplayName || page.Name,
    visual: visualLabel(visual),
    model: Object.fromEntries(
      modelContextKeys.map((key) => [key, structuredClone(state[key])]),
    ),
  };
  state.view = "objects";
  state.model = object.SemanticModel;
  resetFilters();
  state.table = object.Table;
  state.system ||= object.system;
  state.tab = "Summary";
  renderSidebar();
  renderWorkspace();
  inspect(id, { focus: true });
}
function returnToVisual() {
  if (state.reportOrigin) Object.assign(state, state.reportOrigin.model);
  state.reportOrigin = null;
  state.view = "pages";
  renderSidebar();
  renderWorkspace();
  ($("#visual-heading") || $("#page-heading"))?.focus();
}
function renderCoverage() {
  const counts = Object.entries(STATES).map(([k, v]) => [
    v,
    data.objects.filter((o) => o.UsageState === k).length,
  ]);
  $(".workspace").innerHTML =
    `<section class="coverage-main" id="collection" tabindex="-1"><div class="section-label">ANALYSED SNAPSHOT</div><h2>Evidence has boundaries.</h2><p class="coverage-lead">PBI Assure reads saved project metadata. A usage classification describes what this scan found within that scope.</p><div class="scope-numbers"><div><strong>${data.raw.SemanticModels.length}</strong>semantic models</div><div><strong>${data.raw.Reports.length}</strong>reports</div><div><strong>${data.objects.length}</strong>model objects</div></div><h3>Usage in this snapshot</h3><div class="usage-breakdown">${counts.map(([v, n]) => `<div><span class="badge ${v.tone}"><i></i>${v.label}</span><span class="bar-track"><i style="width:${data.objects.length ? (n / data.objects.length) * 100 : 0}%"></i></span><strong>${n}</strong></div>`).join("")}</div><div class="review-note">Apparently unused is a review candidate, never permission to delete. Check other reports, external consumers and runtime behaviour before making changes.</div><h3>What this viewer exposes</h3><p>Scanner-produced usage and confidence, direct report locations, semantic dependency edges, saved DAX definitions and recorded limitations.</p><h3>What the scan does not establish</h3><ul class="boundaries"><li>DAX or Power Query execution, refresh success or performance.</li><li>External consumers and other reports outside the selected project.</li><li>Runtime visual behaviour or a reproduction of Power BI rendering.</li><li>Absence of use where metadata could not be fully analysed.</li></ul></section><aside class="inspector"><div class="object-hero"><div class="eyebrow">ANALYSIS COVERAGE</div><h2>Checks & limitations</h2><p>Confidence is separate from usage state.</p></div><div class="inspector-content"><div class="review-note">${data.objects.filter((o) => o.ClassificationConfidence === "QualifiedByLimitation").length} objects have Checks limited in this snapshot.</div><p class="quiet">A limitation may affect only part of a model. This viewer preserves each object’s scanner-supplied confidence, and lists limitations at model level.</p>${data.raw.SemanticModels.map((m) => `<section><h3>${escape(m.Name)}</h3>${modelLimitations(m.Name)}</section>`).join("")}<p class="footnote">JSON schema 0.26 omits some in-process details, including descriptions, hidden projection provenance and precise per-limitation reach. This prototype does not infer them.</p></div></aside>`;
}
function inspect(id, { focus = false, push = true } = {}) {
  if (!data.byId.has(id)) return;
  if (push && id !== state.selected) {
    state.history = state.history.slice(0, state.cursor + 1);
    state.history.push(id);
    state.cursor = state.history.length - 1;
  }
  state.selected = id;
  renderCollection();
  renderInspector();
  announce(
    `Inspecting ${selected().ObjectName}. ${STATES[selected().UsageState].label}.`,
  );
  if (focus) $("#object-heading")?.focus();
}
function toast(message) {
  $("#toast").textContent = message;
  $("#toast").hidden = false;
  setTimeout(() => {
    $("#toast").hidden = true;
  }, 5500);
}
const scrollSelectors = {
  lineage: ["#lineage-up", "#lineage-down", ".lineage-detail"],
  overview: ["#collection"],
  objects: ["#collection", "#detail-panel"],
  reviews: ["#collection", ".review-detail"],
  check: ["#collection", ".inspector-content"],
  queries: ["#collection", ".query-detail"],
};
function rememberWorkspace() {
  if (state.view === "lineage") rememberLineage();
  if (state.view === "pages") rememberReportScroll();
  else
    state.workspaceScroll[state.view] = (scrollSelectors[state.view] || []).map(
      (selector) => $(selector)?.scrollTop || 0,
    );
}
function showView(view, { restore = true, focus = true } = {}) {
  rememberWorkspace();
  if (view === "lineage" && state.lineageOrigin) {
    Object.assign(state, state.lineageOrigin.context);
    state.lineageOrigin = null;
  }
  if (view === "reviews" && state.reviewOrigin) {
    Object.assign(state, state.reviewOrigin.context);
    state.reviewOrigin = null;
    state.lineageOrigin = null;
  }
  state.view = view;
  if (view === "reviews") state.reviewReturn = false;
  renderSidebar();
  renderWorkspace();
  if (restore && state.workspaceScroll[view] && view !== "lineage")
    (scrollSelectors[view] || []).forEach((selector, index) => {
      if ($(selector))
        $(selector).scrollTop = state.workspaceScroll[view][index] || 0;
    });
  if (focus) $("#workspace-heading")?.focus({ preventScroll: true });
}
function openOverviewReviews({
  mode = "main",
  category = "",
  severity = "",
  required = false,
} = {}) {
  Object.assign(state, {
    ...REVIEW_DEFAULTS,
    reviewMode: mode,
    reviewCategory: category,
    reviewSeverity: severity,
    reviewRequired: required,
    reviewSearch: "",
    reviewSelected: null,
  });
  showView("reviews", { restore: false });
}
function openOverviewUsage(usage = "") {
  resetFilters();
  state.model = "";
  state.system = false;
  state.usage = usage;
  state.tab = "Summary";
  state.reportOrigin = null;
  state.queryOrigin = null;
  state.reviewReturn = false;
  state.selected = null;
  showView("objects", { restore: false });
  const first = visible()[0];
  if (first) inspect(first.id, { focus: true });
}
function openReportTarget(report, page, visual, tab = "Reviews") {
  state.reportOrigin = null;
  state.report = report.RelativePath;
  state.page = page
    ? pageId(report, page)
    : pagesFor(report)[0]
      ? pageId(report, pagesFor(report)[0])
      : null;
  state.visualSearch = "";
  state.visualTab = tab;
  state.reportPageDetails = !visual;
  if (visual) state.reportVisuals[state.page] = visual.Name;
  delete state.reportScroll[state.page];
  showView("pages", { restore: false });
  ($("#visual-heading") || $("#page-heading"))?.focus({ preventScroll: true });
}
function renderReviewResults({ resetDetail = false } = {}) {
  const scroll = $(".review-list")?.scrollTop || 0;
  const detail = resetDetail ? 0 : $(".review-detail")?.scrollTop || 0;
  $(".review-list").innerHTML = reviewCollection(data, state);
  $(".review-list").scrollTop = scroll;
  $("#review-inspector").innerHTML = reviewInspector(data, state);
  if ($(".review-detail")) $(".review-detail").scrollTop = detail;
  $("#review-result-count").textContent =
    `${reviewRows(data, state).length} results`;
  announce(
    `${reviewRows(data, state).length} results in the selected review collection.`,
  );
}
function selectReview(index, { focusHeading = false } = {}) {
  if (!reviewRows(data, state).some((row) => row.index === index)) return;
  if (state.reviewSelected !== index) state.reviewLimitOpen = false;
  state.reviewSelected = index;
  renderReviewResults({ resetDetail: true });
  if (focusHeading) $("#review-heading").focus();
  else $(`[data-review="${index}"]`)?.focus();
}
function investigateReview() {
  const target = reviewTarget(data, selectedReview(data, state));
  if (!target) return;
  rememberWorkspace();
  // Source visits are temporary investigations. Restore all prior workspace states on return.
  const keys = [
    ...modelContextKeys,
    "report",
    "page",
    "reportVisuals",
    "reportScroll",
    "visualSearch",
    "visualTab",
    "reportPageDetails",
    "reportOrigin",
    "queryModel",
    "queryState",
    "queryKind",
    "querySearch",
    "querySelected",
    "queryTab",
    "queryHistory",
    "queryCursor",
    "queryOrigin",
    "workspaceScroll",
  ];
  state.reviewOrigin = {
    context: Object.fromEntries(
      keys.map((key) => [key, structuredClone(state[key])]),
    ),
  };
  state.queryOrigin = null;
  state.reviewReturn = true;
  if (target.kind === "report")
    openReportTarget(
      target.report,
      target.page,
      target.visual,
      target.tab || "Reviews",
    );
  else if (target.kind === "query") {
    Object.assign(state, {
      queryModel: target.model.Name,
      queryState: "",
      queryKind: "",
      querySearch: "",
      querySelected: target.id,
      queryTab: "Summary",
      queryHistory: [target.id],
      queryCursor: 0,
    });
    showView("queries", { restore: false });
    $("#query-heading")?.focus({ preventScroll: true });
  } else {
    resetFilters();
    state.model = target.model.Name;
    state.system = !!target.object?.system;
    state.table = target.table?.Name || target.object?.Table || "";
    state.selected = null;
    state.tab = "Summary";
    showView("objects", { restore: false });
    const object = target.object;
    if (object) inspect(object.id, { focus: true });
  }
}
document.addEventListener("click", async (event) => {
  const b = event.target.closest("button");
  if (!b) return;
  if (b.dataset.lineageCentre) {
    centreLineage(b.dataset.lineageCentre);
    return;
  }
  if (b.dataset.lineageSelect) {
    selectLineage(b.dataset.lineageSelect);
    return;
  }
  if (b.dataset.lineageExpand || b.dataset.lineageMore) {
    rememberLineage();
    const session = lineageSession(),
      direction = b.dataset.direction,
      id = b.dataset.lineageExpand || b.dataset.lineageMore;
    if (b.dataset.lineageMore)
      session.limits[direction][id] =
        (session.limits[direction][id] || BRANCH_SIZE) + BRANCH_SIZE;
    else if (session.expanded[direction].includes(id))
      session.expanded[direction] = session.expanded[direction].filter(
        (key) => key !== id,
      );
    else session.expanded[direction].push(id);
    renderLineage();
    $(
      `[data-lineage-expand="${CSS.escape(id)}"][data-direction="${direction}"]`,
    )?.focus({ preventScroll: true });
    return;
  }
  if (b.dataset.lineageTab) {
    rememberLineage();
    const session = lineageSession();
    session.tab = b.dataset.lineageTab;
    session.scroll[2] = 0;
    $("#lineage-inspector").innerHTML = lineageInspector(lineageIndex, session);
    $(`#lineage-tab-${session.tab}`).focus();
    return;
  }
  if (b.dataset.lineageReveal) {
    rememberLineage();
    const session = lineageSession(),
      id = b.dataset.lineageReveal;
    const found =
      revealLineage(lineageIndex, session, id, b.dataset.direction || "up") ||
      revealLineage(lineageIndex, session, id, "down");
    if (found) {
      session.tab = "Connections";
      session.scroll[2] = 0;
      renderLineage();
      $(`[data-lineage-node="${CSS.escape(id)}"]`)?.scrollIntoView({
        block: "nearest",
      });
      $("#lineage-node-heading")?.focus({ preventScroll: true });
    }
    return;
  }
  if (b.dataset.lineageEndpoint) {
    const endpoint = lineageEndpoints(lineageIndex, lineageSession()).rows.find(
      (row) => row.key === b.dataset.lineageEndpoint,
    );
    if (endpoint?.report) {
      lineageVisit();
      openReportTarget(endpoint.report, endpoint.page, endpoint.visual, "Uses");
    }
    return;
  }
  if (b.hasAttribute("data-query-state")) {
    state.queryState = b.dataset.queryState;
    renderSidebar();
    renderQueries();
    $(`[data-query-state="${state.queryState}"]`).focus();
    return;
  }
  if (b.dataset.query || b.dataset.queryRelated) {
    selectQuery(b.dataset.query || b.dataset.queryRelated, {
      focus: !!b.dataset.queryRelated,
    });
    return;
  }
  if (b.dataset.queryTab) {
    state.queryTab = b.dataset.queryTab;
    $("#query-inspector").innerHTML = queryInspector(queryIndex, state);
    $(`#query-tab-${state.queryTab}`).focus();
    return;
  }
  if (b.dataset.queryTable) {
    openQueryTable(b.dataset.queryModel, b.dataset.queryTable);
    return;
  }
  if (b.dataset.overviewReviews) {
    openOverviewReviews({ mode: b.dataset.overviewReviews });
    return;
  }
  if (b.dataset.overviewCategory) {
    openOverviewReviews({ category: b.dataset.overviewCategory });
    return;
  }
  if (b.dataset.overviewSeverity) {
    openOverviewReviews({ severity: b.dataset.overviewSeverity });
    return;
  }
  if (b.hasAttribute("data-overview-required")) {
    openOverviewReviews({ required: true });
    return;
  }
  if (b.dataset.overviewUsage) {
    openOverviewUsage(b.dataset.overviewUsage);
    return;
  }
  if (b.hasAttribute("data-overview-models")) {
    openOverviewUsage();
    return;
  }
  if (b.hasAttribute("data-overview-checks")) {
    showView("check", { restore: false });
    return;
  }
  if (b.hasAttribute("data-overview-pages")) {
    state.reviewReturn = false;
    showView("pages");
    return;
  }
  if (b.dataset.overviewReport) {
    const report = data.raw.Reports.find(
      (r) => r.RelativePath === b.dataset.overviewReport,
    );
    state.reviewReturn = false;
    if (report) openReportTarget(report, pagesFor(report)[0], null);
    return;
  }
  if (b.hasAttribute("data-overview-theme")) {
    const observation =
      overviewData(data).themes[Number(b.dataset.overviewTheme)];
    if (observation) {
      const page = observation.report.Pages.find(
          (p) => p.Name === observation.item.PageName,
        ),
        visual = page?.Visuals.find(
          (v) => v.Name === observation.item.VisualName,
        );
      state.reviewReturn = false;
      if (visual)
        openReportTarget(observation.report, page, visual, "Behaviour");
    }
    return;
  }
  if (b.dataset.reviewMode) {
    rememberWorkspace();
    state.reviewViews[state.reviewMode] = {
      values: Object.fromEntries(
        Object.keys(REVIEW_DEFAULTS).map((key) => [
          key,
          structuredClone(state[key]),
        ]),
      ),
      scroll: state.workspaceScroll.reviews,
    };
    const saved = state.reviewViews[b.dataset.reviewMode];
    Object.assign(state, REVIEW_DEFAULTS, saved?.values || {}, {
      reviewMode: b.dataset.reviewMode,
    });
    state.workspaceScroll.reviews = saved?.scroll || [0, 0];
    renderSidebar();
    renderWorkspace();
    scrollSelectors.reviews.forEach((selector, index) => {
      if ($(selector))
        $(selector).scrollTop = state.workspaceScroll.reviews[index] || 0;
    });
    $(`[data-review-mode="${state.reviewMode}"]`).focus();
    return;
  }
  if (b.dataset.reviewTab) {
    state.reviewTab = b.dataset.reviewTab;
    $("#review-inspector").innerHTML = reviewInspector(data, state);
    $(`#review-tab-${state.reviewTab}`).focus();
    return;
  }
  if (b.hasAttribute("data-review")) {
    selectReview(Number(b.dataset.review));
    return;
  }
  if (b.dataset.model) {
    state.model = b.dataset.model;
    resetFilters();
    renderSidebar();
    renderWorkspace();
    $("#model-select").focus();
    return;
  }
  if (b.dataset.view) {
    if (b.dataset.view === "queries" && state.queryOrigin) {
      returnToQuery();
      return;
    }
    if (b.dataset.view === "pages" && state.reportOrigin) {
      rememberWorkspace();
      returnToVisual();
      return;
    }
    showView(b.dataset.view, { focus: false });
    $(`[data-view="${state.view}"]`)?.focus();
    return;
  }
  if (b.hasAttribute("data-table")) {
    state.table = b.dataset.table;
    state.view = "objects";
    renderSidebar();
    renderWorkspace();
    $(`[data-table="${CSS.escape(state.table)}"]`)?.focus();
    return;
  }
  if (b.hasAttribute("data-type")) {
    state.type = b.dataset.type;
    renderWorkspace();
    $(`[data-type="${state.type}"]`)?.focus();
    return;
  }
  if (b.dataset.object) {
    inspect(b.dataset.object);
    $(`[data-object="${CSS.escape(b.dataset.object)}"]`)?.focus();
    return;
  }
  if (b.dataset.related) {
    inspect(b.dataset.related, { focus: true });
    return;
  }
  if (b.dataset.pageObject) {
    openModelFromVisual(b.dataset.pageObject);
    return;
  }
  if (b.dataset.page) {
    state.page = b.dataset.page;
    state.visualSearch = "";
    state.reportPageDetails = false;
    renderSidebar();
    renderPages();
    $(`[data-page="${CSS.escape(state.page)}"]`)?.focus();
    return;
  }
  if (b.dataset.visualReview) {
    state.visualTab = "Reviews";
    selectVisual(b.dataset.visualReview, { focusHeading: true });
    return;
  }
  if (b.dataset.visual) {
    selectVisual(b.dataset.visual, {
      focusHeading: !b.classList.contains("visual-select"),
    });
    return;
  }
  if (b.dataset.visualTab) {
    state.visualTab = b.dataset.visualTab;
    renderVisualInspector();
    $(`#visual-tab-${state.visualTab}`).focus();
    return;
  }
  if (b.dataset.tab || b.dataset.goTab) {
    state.tab = b.dataset.tab || b.dataset.goTab;
    renderInspector();
    $(`#tab-${state.tab}`)?.focus();
    return;
  }
  switch (b.dataset.action) {
    case "return-lineage":
      showView("lineage");
      $("#lineage-node-heading")?.focus({ preventScroll: true });
      break;
    case "lineage-centre-evidence":
      rememberLineage();
      lineageSession().selected = state.lineageCentre;
      lineageSession().tab = "Evidence";
      lineageSession().scroll[2] = 0;
      renderLineage();
      $("#lineage-tab-Evidence").focus();
      break;
    case "lineage-model":
      openLineageModel();
      break;
    case "lineage-current-model":
      centreLineage(state.selected);
      break;
    case "lineage-more-search":
      state.lineageSearchLimit += BRANCH_SIZE;
      $("#lineage-search-results").innerHTML = lineageSearchResults(
        lineageIndex,
        state,
      );
      break;
    case "lineage-back":
    case "lineage-forward":
      state.lineageCursor += b.dataset.action === "lineage-back" ? -1 : 1;
      centreLineage(state.lineageHistory[state.lineageCursor], { push: false });
      break;
    case "lineage-reset": {
      const session = lineageSession(),
        membership = session.membership;
      Object.assign(session, createLineageSession(state.lineageCentre), {
        membership,
      });
      renderLineage();
      break;
    }
    case "lineage-more-connections":
    case "lineage-more-endpoints": {
      rememberLineage();
      const session = lineageSession();
      if (b.dataset.action === "lineage-more-connections")
        session.connectionLimit =
          (session.connectionLimit || BRANCH_SIZE) + BRANCH_SIZE;
      else session.endpointLimit += BRANCH_SIZE;
      renderLineage();
      break;
    }
    case "return-query":
      returnToQuery();
      break;
    case "query-back":
    case "query-forward":
      state.queryCursor += b.dataset.action === "query-back" ? -1 : 1;
      selectQuery(state.queryHistory[state.queryCursor], {
        push: false,
        focus: true,
      });
      break;
    case "query-reset":
    case "query-reveal":
      resetQueryFilters();
      if (b.dataset.action === "query-reveal")
        state.queryModel = queryIndex.byId.get(
          state.querySelected,
        ).SemanticModel;
      renderSidebar();
      renderWorkspace();
      $("#query-search").focus();
      break;
    case "return-reviews":
      showView("reviews");
      $("#review-heading")?.focus({ preventScroll: true });
      break;
    case "review-source":
      investigateReview();
      break;
    case "reset-reviews":
    case "reveal-review":
      Object.assign(state, {
        reviewCategory: "",
        reviewSeverity: "",
        reviewRequired: false,
        reviewSearch: "",
        reviewReport: "",
        reviewPage: "",
        reviewModel: "",
      });
      renderWorkspace();
      $("#review-search").focus();
      break;
    case "return-visual":
      returnToVisual();
      break;
    case "page-details":
      state.reportPageDetails = true;
      renderVisualCollection();
      renderVisualInspector();
      $("#page-heading").focus();
      break;
    case "resume-visual":
      state.reportPageDetails = false;
      renderVisualCollection();
      renderVisualInspector();
      $("#visual-heading").focus();
      break;
    case "clear-visual-search":
      state.visualSearch = "";
      $("#visual-search").value = "";
      renderVisualCollection();
      renderVisualInspector();
      $("#visual-search").focus();
      break;
    case "import":
      $("#file").click();
      break;
    case "reset":
      resetFilters();
      renderSidebar();
      renderWorkspace();
      $("#search").focus();
      break;
    case "sort-name":
      state.sort = "name";
      renderCollection();
      $('[data-action="sort-name"]').focus();
      break;
    case "sort-usage":
      state.sort = "usage";
      renderCollection();
      $('[data-action="sort-usage"]').focus();
      break;
    case "back":
    case "forward":
      state.cursor += b.dataset.action === "back" ? -1 : 1;
      inspect(state.history[state.cursor], { focus: true, push: false });
      break;
    case "scope":
      showView("check");
      break;
    case "object-coverage":
      state.tab = "Evidence";
      renderInspector();
      $("#tab-Evidence").focus();
      break;
    case "reveal":
      state.model = selected().SemanticModel;
      state.system ||= selected().system;
      resetFilters();
      renderSidebar();
      renderWorkspace();
      break;
    case "copy":
      try {
        await navigator.clipboard.writeText(selected().detail.Expression);
        toast("DAX expression copied.");
      } catch {
        toast(
          "Copy is unavailable here. Select the expression text to copy it.",
        );
      }
      break;
  }
});
document.addEventListener(
  "toggle",
  (event) => {
    if (
      event.target.matches(".review-context-limits") &&
      event.target.isConnected &&
      state.view === "reviews"
    )
      state.reviewLimitOpen = event.target.open;
  },
  true,
);
document.addEventListener("input", (event) => {
  if (event.target.id === "lineage-search") {
    state.lineageSearch = event.target.value;
    state.lineageSearchLimit = BRANCH_SIZE;
    $("#lineage-search-results").innerHTML = lineageSearchResults(
      lineageIndex,
      state,
    );
  }
  if (event.target.id === "query-search") {
    state.querySearch = event.target.value;
    renderQueries();
  }
  if (event.target.id === "review-search") {
    state.reviewSearch = event.target.value;
    renderReviewResults();
  }
  if (event.target.id === "visual-search") {
    state.visualSearch = event.target.value;
    renderVisualCollection();
    renderVisualInspector();
  }
  if (event.target.id === "search") {
    state.search = event.target.value;
    renderCollection();
    renderInspector();
  }
});
document.addEventListener("change", async (event) => {
  const target = event.target;
  if (target.id === "lineage-model") {
    state.lineageModel = target.value;
    state.lineageSearchLimit = BRANCH_SIZE;
    renderSidebar();
    $("#lineage-model").focus();
  }
  if (target.id === "lineage-membership") {
    rememberLineage();
    lineageSession().membership = target.checked;
    renderLineage();
    $("#lineage-membership").focus();
  }
  if (["query-model", "query-kind"].includes(target.id)) {
    state[target.id === "query-model" ? "queryModel" : "queryKind"] =
      target.value;
    renderSidebar();
    renderQueries();
    $(`#${target.id}`).focus();
  }
  if (
    [
      "review-category",
      "review-severity",
      "review-required",
      "review-report",
      "review-page",
      "review-model",
    ].includes(target.id)
  ) {
    if (target.id === "review-category") state.reviewCategory = target.value;
    else if (target.id === "review-severity")
      state.reviewSeverity = target.value;
    else if (target.id === "review-required")
      state.reviewRequired = target.checked;
    else if (target.id === "review-report") {
      state.reviewReport = target.value;
      state.reviewPage = "";
    } else if (target.id === "review-page") state.reviewPage = target.value;
    else state.reviewModel = target.value;
    if (target.id === "review-report") {
      const panes = scrollSelectors.reviews.map(
        (selector) => $(selector)?.scrollTop || 0,
      );
      renderWorkspace();
      scrollSelectors.reviews.forEach((selector, index) => {
        if ($(selector)) $(selector).scrollTop = panes[index];
      });
      $("#review-report").focus();
    } else renderReviewResults();
  }
  if (target.id === "report-select") {
    state.report = target.value;
    state.page = null;
    state.visualSearch = "";
    state.reportPageDetails = false;
    renderSidebar();
    renderPages();
    $("#report-select").focus();
  }
  if (target.id === "model-select") {
    state.model = target.value;
    resetFilters();
    renderSidebar();
    renderWorkspace();
    $("#model-select").focus();
  }
  if (target.id === "usage-filter") {
    state.usage = target.value;
    renderCollection();
    renderInspector();
  }
  if (target.id === "limited") {
    state.limited = target.checked;
    renderCollection();
    renderInspector();
  }
  if (target.id === "system") {
    state.system = target.checked;
    renderSidebar();
    renderCollection();
    renderInspector();
  }
  if (target.id === "file" && target.files[0]) {
    const file = target.files[0];
    const previousData = data,
      previousQueries = queryIndex,
      previousLineage = lineageIndex,
      previousState = structuredClone(state);
    try {
      if (file.size > 30 * 1024 * 1024)
        throw new Error("This prototype opens JSON inventories up to 30 MB.");
      const next = adaptInventory(JSON.parse(await file.text()));
      const nextQueries = buildQueryData(next);
      const nextLineage = buildLineageData(next);
      data = next;
      queryIndex = nextQueries;
      lineageIndex = nextLineage;
      resetFilters();
      Object.assign(state, {
        model: data.raw.SemanticModels[0]?.Name || "",
        selected: null,
        history: [],
        cursor: -1,
        view: "overview",
        source: file.name,
        fixture: false,
        tab: "Summary",
        report: null,
        page: null,
        reportVisuals: {},
        reportScroll: {},
        visualSearch: "",
        visualTab: "Uses",
        reportPageDetails: false,
        reportOrigin: null,
        reviewMode: "main",
        reviewCategory: "",
        reviewSeverity: "",
        reviewRequired: false,
        reviewSearch: "",
        reviewSelected: null,
        reviewReturn: false,
        ...REVIEW_DEFAULTS,
        reviewViews: {},
        reviewOrigin: null,
        workspaceScroll: {},
        queryModel: "",
        queryState: "",
        queryKind: "",
        querySearch: "",
        querySelected: null,
        queryHistory: [],
        queryCursor: -1,
        queryTab: "Summary",
        queryOrigin: null,
        lineageCentre: null,
        lineageSessions: {},
        lineageHistory: [],
        lineageCursor: -1,
        lineageModel: data.raw.SemanticModels[0]?.Name || "",
        lineageSearch: "",
        lineageSearchLimit: BRANCH_SIZE,
        lineageOrigin: null,
      });
      const first = visible()[0];
      if (first) {
        state.selected = first.id;
        state.history = [first.id];
        state.cursor = 0;
      }
      shell();
      toast("Analysis opened locally. Nothing was uploaded.");
    } catch (e) {
      // Opening is transactional: malformed nested metadata must not discard the investigation.
      data = previousData;
      queryIndex = previousQueries;
      lineageIndex = previousLineage;
      Object.assign(state, previousState);
      shell();
      toast(
        e instanceof SyntaxError
          ? "This file is not valid JSON. Your current investigation is still open."
          : e.message,
      );
    }
    target.value = "";
  }
});
document.addEventListener("keydown", (event) => {
  const target = event.target;
  if (
    target.matches("[data-lineage-tab]") &&
    ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    rememberLineage();
    const session = lineageSession();
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? LINEAGE_TABS.length - 1
          : (LINEAGE_TABS.indexOf(session.tab) +
              (event.key === "ArrowRight" ? 1 : LINEAGE_TABS.length - 1)) %
            LINEAGE_TABS.length;
    session.tab = LINEAGE_TABS[index];
    session.scroll[2] = 0;
    $("#lineage-inspector").innerHTML = lineageInspector(lineageIndex, session);
    $(`#lineage-tab-${session.tab}`).focus();
    return;
  }
  if (
    target.matches(".lineage-node-select") &&
    ["ArrowUp", "ArrowDown", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const laneId = target.closest(".lineage-lane-scroll").id,
      rows = [
        ...target
          .closest(".lineage-lane-scroll")
          .querySelectorAll(".lineage-node-select"),
      ],
      current = rows.indexOf(target);
    const next =
        event.key === "Home"
          ? 0
          : event.key === "End"
            ? rows.length - 1
            : Math.max(
                0,
                Math.min(
                  rows.length - 1,
                  current + (event.key === "ArrowDown" ? 1 : -1),
                ),
              ),
      id = rows[next]?.dataset.lineageSelect;
    if (id) {
      selectLineage(id, { focus: false });
      $(`#${laneId} [data-lineage-select="${CSS.escape(id)}"]`)?.focus();
    }
    return;
  }
  if (
    target.matches("[data-review-tab]") &&
    ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? REVIEW_TABS.length - 1
          : (REVIEW_TABS.indexOf(state.reviewTab) + 1) % REVIEW_TABS.length;
    state.reviewTab = REVIEW_TABS[index];
    $("#review-inspector").innerHTML = reviewInspector(data, state);
    $(`#review-tab-${state.reviewTab}`).focus();
    return;
  }
  if (
    event.key === "/" &&
    !/INPUT|TEXTAREA|SELECT/.test(target.tagName) &&
    ["objects", "pages", "reviews", "queries", "lineage"].includes(state.view)
  ) {
    event.preventDefault();
    (state.view === "objects"
      ? $("#search")
      : state.view === "lineage"
        ? $("#lineage-search")
        : state.view === "queries"
          ? $("#query-search")
          : state.view === "reviews"
            ? $("#review-search")
            : $("#visual-search")
    )?.focus();
  }
  if (
    target.matches("[data-query]") &&
    ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const rows = filterQueries(queryIndex, state),
      current = rows.findIndex((q) => q.id === target.dataset.query);
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? rows.length - 1
          : Math.max(
              0,
              Math.min(
                rows.length - 1,
                current + (event.key === "ArrowDown" ? 1 : -1),
              ),
            );
    if (rows[index]) selectQuery(rows[index].id);
    return;
  }
  if (
    target.matches("[data-query-tab]") &&
    ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? QUERY_TABS.length - 1
          : (QUERY_TABS.indexOf(state.queryTab) +
              (event.key === "ArrowRight" ? 1 : QUERY_TABS.length - 1)) %
            QUERY_TABS.length;
    state.queryTab = QUERY_TABS[index];
    $("#query-inspector").innerHTML = queryInspector(queryIndex, state);
    $(`#query-tab-${state.queryTab}`).focus();
    return;
  }
  if (
    target.matches("[data-review]") &&
    ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const rows = reviewRows(data, state),
      current = rows.findIndex(
        (row) => row.index === Number(target.dataset.review),
      );
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? rows.length - 1
          : Math.max(
              0,
              Math.min(
                rows.length - 1,
                current + (event.key === "ArrowDown" ? 1 : -1),
              ),
            );
    if (rows[index]) selectReview(rows[index].index);
    return;
  }
  if (
    target.matches("[data-visual]") &&
    ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const rows = visibleVisuals(data, state),
      current = rows.findIndex((v) => v.Name === target.dataset.visual);
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? rows.length - 1
          : Math.max(
              0,
              Math.min(
                rows.length - 1,
                current + (event.key === "ArrowDown" ? 1 : -1),
              ),
            );
    if (rows[index]) selectVisual(rows[index].Name);
    return;
  }
  if (
    target.matches("[data-visual-tab]") &&
    ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? VISUAL_TABS.length - 1
          : (VISUAL_TABS.indexOf(state.visualTab) +
              (event.key === "ArrowRight" ? 1 : VISUAL_TABS.length - 1)) %
            VISUAL_TABS.length;
    state.visualTab = VISUAL_TABS[index];
    renderVisualInspector();
    $(`#visual-tab-${state.visualTab}`).focus();
    return;
  }
  if (
    target.matches("[data-object]") &&
    ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const rows = visible(),
      current = rows.findIndex((o) => o.id === target.dataset.object);
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? rows.length - 1
          : Math.max(
              0,
              Math.min(
                rows.length - 1,
                current + (event.key === "ArrowDown" ? 1 : -1),
              ),
            );
    inspect(rows[index].id);
    $(`[data-object="${CSS.escape(rows[index].id)}"]`).focus();
  }
  if (
    target.matches('[role="tab"]') &&
    ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const tabs = ["Summary", "Dependencies", "DAX", "Evidence"];
    const index =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? 3
          : (tabs.indexOf(state.tab) + (event.key === "ArrowRight" ? 1 : 3)) %
            4;
    state.tab = tabs[index];
    renderInspector();
    $(`#tab-${state.tab}`).focus();
  }
});
try {
  const response = await fetch("./data/coverage.json");
  if (!response.ok) throw new Error("The fixture could not be loaded.");
  data = adaptInventory(await response.json());
  queryIndex = buildQueryData(data);
  lineageIndex = buildLineageData(data);
  const first =
    visible().find((o) => o.ObjectName === "DirectlyUsedMeasure") ||
    visible()[0];
  if (first) {
    state.selected = first.id;
    state.history = [first.id];
    state.cursor = 0;
  }
  shell();
  announce(
    "Project overview loaded. Counts describe the saved analysis snapshot.",
  );
} catch (e) {
  $("#app").innerHTML =
    `<div class="empty"><h1>Could not open the workspace</h1><p>${escape(e.message)}</p><p>Start the local server with <code>npm run dev</code>.</p></div>`;
}
