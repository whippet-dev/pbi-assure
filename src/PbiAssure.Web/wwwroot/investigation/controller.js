import { unusedReviewIntro, objectReviewContext } from "./unused-review.js";
import { returnToProject } from "./hosted-entry.js";
import {
  createState,
  beginVisit,
  returnVisit,
  unusedCheckpointIndex,
} from "./state.js";
import { coreTarget, targetReference } from "./navigation.js";
import { shellMarkup } from "./shell.js";
import {
  adaptSnapshot,
  STATES,
  filterObjects,
  edgeId,
  supportingEdges,
  words,
} from "./data.js";
import { escape, icon, glyph } from "./ui.js";
import { visualType } from "./report-data.js";
import {
  MODEL_AREA_DEFAULTS,
  buildModelAreas,
  relationshipRows,
  securityRows,
} from "./model-area-data.js";
import {
  AREA_TABS,
  targetButton,
  modelAreaNavigation,
  modelAreaWorkspace,
  modelAreaCollection,
  modelAreaInspector,
  areaSelection,
} from "./model-area-view.js";
import {
  SCOPE_DEFAULTS,
  scopeRows,
  constructLabel,
  supportLabel,
} from "./scope-data.js";
import {
  SCOPE_TABS,
  scopeNavigation,
  scopeWorkspace,
  scopeCollection,
  scopeInspector,
} from "./scope-view.js";
import {
  buildLineageData,
  createLineageSession,
  BRANCH_SIZE,
  LINEAGE_TABS,
  revealLineage,
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
const state = createState();
let data, queryIndex, lineageIndex, modelAreas;

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
  $("#app").innerHTML = shellMarkup();
  renderSidebar();
  renderWorkspace();
}
function workspaceNavigation() {
  const button = (view, label) =>
    `<button data-view="${view}" class="nav-item ${state.view === view ? "active" : ""}" ${state.view === view ? 'aria-current="page"' : ""}>${icon(view)}${label}</button>`;
  return `<nav class="primary-nav" aria-label="Investigation areas">${button("overview", "Overview")}<div class="nav-group-label">MODEL</div>${button("objects", "Model objects")}${button("relationships", "Relationships")}${button("security", "Security")}${button("lineage", "Lineage")}<div class="nav-group-label">REPORT & DATA</div>${button("pages", "Report pages")}${button("queries", "Power Query")}<div class="nav-group-label">REVIEW</div>${button("reviews", "Reviews & findings")}<div class="nav-group-label">ANALYSIS</div>${button("check", "Analysis scope")}</nav>`;
}
function renderSidebar() {
  if (state.view === "lineage") ensureLineage();
  const tables = data.models.get(state.model)?.Tables || [];
  const modelObjects = data.objects.filter(
    (o) =>
      (!state.model || o.ModelId === state.model) &&
      ((state.usage !== "ApparentlyUnused" && state.system) || !o.system),
  );
  $(".sidebar").innerHTML =
    `<div class="project-label">OPEN ANALYSIS</div><div class="project-name">${escape(state.source)}</div><div class="source-label">Viewer snapshot 1.0 · read-only</div>
    ${workspaceNavigation()}
    <div class="sidebar-context">${
      state.view === "pages"
        ? reportNavigation(data, state)
        : ["relationships", "security"].includes(state.view)
          ? modelAreaNavigation(modelAreas, state)
          : state.view === "check"
            ? scopeNavigation(data, state)
            : state.view === "lineage"
              ? lineageNavigation(lineageIndex, state)
              : state.view === "queries"
                ? queryNavigation(queryIndex, state)
                : state.view === "reviews"
                  ? reviewsNavigation(data, state)
                  : state.view !== "objects"
                    ? ""
                    : `    <div class="model-navigation"><label class="project-label" for="model-select">SEMANTIC MODEL</label><select id="model-select" aria-label="Semantic model"><option value="" ${!state.model ? "selected" : ""}>All models</option>${data.raw.SemanticModels.map((m) => `<option value="${escape(m.Id)}" ${m.Id === state.model ? "selected" : ""}>${escape(m.Name)}</option>`).join("")}</select>${
                        !state.model
                          ? `<div class="table-heading">Choose a model to browse its tables${state.usage === "ApparentlyUnused" ? " · counts include all usage states" : ""}</div><nav class="table-nav" aria-label="Filter by model">${data.raw.SemanticModels.map((m) => `<button data-model="${escape(m.Id)}">${icon("objects")}<span class="table-name">${escape(m.Name)}</span><span>${modelObjects.filter((o) => o.ModelId === m.Id).length}</span></button>`).join("")}</nav>`
                          : `<div class="table-heading">${state.usage === "ApparentlyUnused" ? "Tables · all usage states" : "Tables"} <span>${tables.filter((t) => state.system || !t.IsSystemGenerated).length}</span></div><nav class="table-nav" aria-label="Filter by table"><button data-table="" class="${!state.table ? "chosen" : ""}">${icon("objects")}All tables<span>${modelObjects.length}</span></button>${tables
                              .filter(
                                (t) => state.system || !t.IsSystemGenerated,
                              )
                              .map(
                                (t) =>
                                  `<button data-table="${escape(t.Id)}" class="${state.table === t.Id ? "chosen" : ""}" title="${escape(t.Name)}">${glyph("Table")}<span class="table-name">${escape(t.Name)}</span><span>${modelObjects.filter((o) => o.TableId === t.Id).length}</span></button>`,
                              )
                              .join("")}</nav>`
                      }</div>`
    }
    </div><div class="sidebar-bottom"><span class="project-label">ANALYSIS SCOPE</span><p>${data.raw.Reports.length} reports · ${data.raw.SemanticModels.length} models</p><span class="source-label">Metadata only. No DAX execution.</span><button class="text-button" data-action="scope">Scope and limitations ${icon("arrow")}</button></div>`;
  const controls = $(".context-controls");
  controls.replaceChildren();
  const selector = $(".sidebar-context select");
  if (selector) {
    const label = $(".sidebar-context label");
    if (label) controls.append(label);
    controls.append(selector);
  }
  controls.hidden = !selector;
}
function renderWorkspace() {
  document.querySelectorAll(".report-origin").forEach((el) => el.remove());
  $(".workspace").classList.toggle("report-workspace", state.view === "pages");
  $(".workspace").classList.toggle("query-workspace", state.view === "queries");
  $(".workspace").classList.toggle(
    "model-area-workspace",
    ["relationships", "security"].includes(state.view),
  );
  $(".workspace").classList.toggle("scope-workspace", state.view === "check");
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
      : state.view === "relationships"
        ? [
            "MODEL / RELATIONSHIPS",
            "Relationships",
            "Inspect saved relationships, their ends and supporting evidence.",
          ]
        : state.view === "security"
          ? [
              "MODEL / SECURITY",
              "Security",
              "Explore saved roles, filters and explicit permissions.",
            ]
          : state.view === "lineage"
            ? [
                "MODEL / TRACE",
                "Lineage",
                "Follow dependencies while keeping your starting object in view.",
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
                    "Model objects",
                    state.usage === "ApparentlyUnused"
                      ? "Apparently unused review · inspect evidence before making changes."
                      : "Inspect usage, definitions and dependencies.",
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
                        "Analysis scope",
                        "Understand what this saved analysis can establish.",
                      ];
  $(".main-heading").innerHTML =
    `<div><div class="eyebrow">${heading[0]}</div><h1 id="workspace-heading" tabindex="-1">${heading[1]}</h1><p>${heading[2]}</p></div><div class="heading-note"><span class="fixture-mark">LOCAL</span> Your analysis snapshot<br><small>Kept in this browser tab</small></div>`;
  renderReturnTrail();
  if (state.view === "objects") {
    $(".workspace").innerHTML =
      `<section class="collection-pane" aria-label="Model object collection"><div class="collection-tools">${state.usage === "ApparentlyUnused" ? unusedReviewIntro(data) : ""}<div class="collection-title"><strong>${escape(data.tables.get(state.table)?.Name || (state.model ? "All tables" : "All models"))}</strong><span id="result-count"></span></div><div class="searchbox">${icon("search")}<input id="search" type="search" placeholder="Search objects or tables…" aria-label="Search objects or tables" value="${escape(state.search)}"><kbd>/</kbd></div><div class="filter-line"><label class="sr-only" for="usage-filter">Usage state</label><select id="usage-filter"><option value="">All usage states</option>${Object.entries(
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
        )}</div></div><div id="collection" class="collection-scroll" tabindex="-1"></div><div class="collection-footer"><label><input id="system" type="checkbox" ${state.usage === "ApparentlyUnused" ? "disabled" : state.system ? "checked" : ""}>Include system-generated</label><span>↑ ↓ to explore · Enter to select</span></div></section><aside id="inspector" class="inspector" aria-label="Selected object"></aside>`;
    renderCollection();
    renderInspector();
  } else if (state.view === "pages") renderPages();
  else if (["relationships", "security"].includes(state.view))
    $(".workspace").innerHTML = modelAreaWorkspace(modelAreas, state);
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
  else $(".workspace").innerHTML = scopeWorkspace(data, state);
  renderReturnTrail();
}
function renderCollection() {
  const rows = visible();
  $("#result-count").textContent =
    state.usage === "ApparentlyUnused"
      ? `${rows.length} of ${data.objects.filter((o) => o.UsageState === "ApparentlyUnused" && !o.system).length} in project`
      : `${rows.length} objects`;
  const active = rows.some((o) => o.id === state.selected)
    ? state.selected
    : rows[0]?.id;
  $("#collection").innerHTML = rows.length
    ? `<table class="objects-table"><thead><tr><th scope="col"><button data-action="sort-name">Object ${state.sort === "name" ? "↓" : ""}</button></th><th scope="col"><button data-action="sort-usage">Usage ${state.sort === "usage" ? "↓" : ""}</button></th><th scope="col" class="numeric"><span title="Distinct immediate dependency sources">Used by</span></th></tr></thead><tbody>${rows.map((o) => `<tr class="${o.id === state.collectionSelected ? "selected" : ""}"><td><button class="object-select" data-object="${escape(o.id)}" tabindex="${o.id === active ? "0" : "-1"}" ${o.id === state.collectionSelected ? 'aria-current="true"' : ""}>${glyph(o.ObjectType)}<span><strong>${escape(o.ObjectName)}</strong><small>${!state.model ? escape(o.SemanticModel) + " · " : ""}${escape(o.Table)} · ${escape(words(o.ObjectType))}${o.detail?.IsHidden ? " · Hidden" : ""}</small></span></button></td><td>${badge(o)}${o.ClassificationConfidence === "QualifiedByLimitation" ? '<small class="checks-limited">◐ Checks limited</small>' : ""}</td><td class="numeric">${incoming(o).length || '<span class="muted">—</span>'}</td></tr>`).join("")}</tbody></table>`
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
      return `<div class="relation-row">${glyph(e[side + "ObjectType"])}<div>${object ? `<button class="related-object" data-related="${escape(id)}">${escape(e[side + "ObjectName"])}${icon("arrow")}</button>` : data.nodes.get(id)?.ObjectType === "ReportMeasure" ? `<button class="related-object" data-report-measure="${escape(id)}">${escape(e[side + "ObjectName"])}${icon("arrow")}</button>` : `<strong>${escape(e[side + "ObjectName"])}</strong>`}<small>${escape(e[side + "Table"] || words(e[side + "ObjectType"]))} · ${escape(words(e.DependencyKind))}</small>${!object ? '<small class="quiet">Related definition · no selectable object usage result</small>' : ""}</div>${object ? `<span class="relation-state" title="${escape(STATES[object.UsageState].label)}">${escape(STATES[object.UsageState].label)}</span>` : ""}</div>`;
    })
    .join("")}</div>`;
}
function reportEvidence(o, full = false) {
  if (!o.DirectReportLocations.length)
    return `<p class="quiet">No direct report locations recorded.${o.UsageState === "IndirectlyUsed" ? " This object is reached through dependencies; see Used by." : ""}</p>`;
  return `<div class="report-locations">${o.DirectReportLocations.map((loc) => {
    const { report, page, visual } = loc;
    return `<div class="report-location">${icon("pages")}<div>${targetButton(coreTarget(data, loc.Location), page?.DisplayName || loc.Page || "Report-level reference")}<small>${escape(report?.Name || loc.Report)} · ${escape(visual?.Accessibility?.TitleText || (visual?.VisualType ? visualType(visual.VisualType) : null) || loc.UsageContext || loc.LocationKind)}</small>${loc.Visual ? `<small class="technical-identity"><span>Visual ID</span> <code>${escape(loc.Visual)}</code></small>` : ""}${full ? `<small>${escape(loc.LocationKind)}${visual?.IsHidden ? " · Hidden visual" : ""}</small>` : ""}</div></div>`;
  }).join("")}</div>`;
}
function expression(o, full = false) {
  const dax = o.detail?.Expression;
  return dax
    ? `<div class="code-heading"><span>DAX EXPRESSION</span><button class="text-button" data-action="copy">Copy</button></div><pre class="dax ${full ? "full" : ""}"><code>${highlight(dax)}</code></pre>${o.detail.FormatString ? `<p class="format-label">Format string <code>${escape(o.detail.FormatString)}</code></p>` : ""}`
    : `<p class="quiet">No DAX expression is published for this ${escape(words(o.ObjectType).toLowerCase())} in this snapshot.${o.ObjectType === "Column" ? " Imported columns receive their data from the model’s partition." : ""}</p>`;
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
  renderReturnTrail();
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
    <div class="object-hero"><div class="object-breadcrumb">${escape(o.SemanticModel)} <span>/</span> ${escape(o.Table)}</div><div class="object-title">${glyph(o.ObjectType)}<h2 id="object-heading" tabindex="-1">${escape(o.ObjectName)}</h2></div><div class="object-meta"><span>${escape(words(o.ObjectType))}</span>${o.detail?.DataType ? `<span>${escape(o.detail.DataType)}</span>` : ""}${o.detail?.IsHidden ? "<span>Hidden</span>" : ""}${o.system ? "<span>System-generated</span>" : ""}</div><div class="hero-badges">${badge(o)}${o.ClassificationConfidence === "QualifiedByLimitation" ? '<button class="limit-badge" data-action="object-coverage">◐ Checks limited</button>' : '<span class="established">No limitation recorded for this result</span>'}</div></div>
    ${outside ? '<div class="outside-filter">Selected object is outside the current filters.<button data-action="reveal">Show in collection</button></div>' : ""}
    <div class="inspector-tabs" role="tablist" aria-label="Object information">${["Summary", "Dependencies", "DAX", "Evidence"].map((tab) => `<button role="tab" id="tab-${tab}" aria-controls="detail-panel" aria-selected="${state.tab === tab}" tabindex="${state.tab === tab ? "0" : "-1"}" data-tab="${tab}">${tab}${tab === "Dependencies" ? `<span>${inEdges.length + outEdges.length}</span>` : ""}</button>`).join("")}</div><div id="detail-panel" class="inspector-content" role="tabpanel" aria-labelledby="tab-${state.tab}" tabindex="0">${
      state.tab === "Summary"
        ? `<section class="why-section"><div class="section-label"><span class="small-dot ${STATES[o.UsageState].tone}"></span>WHY ${escape(STATES[o.UsageState].label.toUpperCase())}</div><p class="why-text">${escape(o.UsageReason || "No usage explanation was supplied for this result.")}</p>${support.length ? `<div class="supporting"><span>Referenced by these objects with recorded report or model-structure use</span>${relationList(uniqueEdges(support), "From", 2)}</div>` : ""}${["ApparentlyUnused", "UsedOnlyByUnusedBranch"].includes(o.UsageState) ? '<div class="review-note">Review candidate, never permission to delete. Other reports, external consumers and runtime behaviour may use this object.</div>' : ""}${o.ClassificationConfidence === "QualifiedByLimitation" ? '<p class="limitation-note">Incomplete analysis may affect this classification. Usage state and confidence are separate. <button data-action="object-coverage">Read model limitations →</button></p>' : ""}</section>
      ${objectReviewContext(data, queryIndex, o)}<section><div class="section-heading"><h3>Used by <span>${inEdges.length}</span></h3>${inEdges.length > 3 ? '<button class="text-button" data-go-tab="Dependencies">View all →</button>' : ""}</div>${relationList(inEdges, "From", 3)}</section><section><div class="section-heading"><h3>Used in the report <span>${o.DirectLocationCount}</span></h3><span class="subtle-label">DIRECT LOCATIONS</span></div>${reportEvidence(o)}</section><section>${expression(o)}</section>`
        : state.tab === "Dependencies"
          ? `<div class="panel-intro"><h3>Follow the dependency chain</h3><p>Each link opens in this inspector. Your collection filters and position stay in place.</p></div><section><h3>Used by <span>${inEdges.length}</span></h3><p class="quiet">Objects that directly reference this object, including objects with no detected report use.</p>${relationList(inEdges, "From")}</section><section><h3>Depends on <span>${outEdges.length}</span></h3>${relationList(outEdges, "To")}</section><p class="footnote">Some related definitions, including report measures, have no selectable object usage result. Report measures retain their recorded report owner and can be inspected in Lineage.</p>`
          : state.tab === "DAX"
            ? `<div class="panel-intro"><h3>Calculation definition</h3><p>Saved model metadata. Expressions are displayed, never executed.</p></div>${expression(o, true)}`
            : `<div class="panel-intro"><h3>Evidence & provenance</h3><p>Inspect the saved references behind this result.</p></div><section><h3>Object definition</h3><dl class="metadata"><dt>Semantic model</dt><dd>${escape(o.SemanticModel)}</dd><dt>Table</dt><dd>${escape(o.Table)}</dd><dt>Source</dt><dd><code>${escape(o.table?.RelativePath || "Not included in this analysis")}</code></dd><dt>Confidence</dt><dd>${o.ClassificationConfidence === "QualifiedByLimitation" ? "Checks limited" : "No limitation recorded for this result within the analysed scope"}</dd>${o.detail?.SourceColumn ? `<dt>Source column</dt><dd>${escape(o.detail.SourceColumn)}</dd>` : ""}${o.detail?.SortByColumn ? `<dt>Sort by column</dt><dd>${escape(o.detail.SortByColumn)}</dd>` : ""}</dl></section><section><h3>Direct report evidence</h3>${reportEvidence(o, true)}${o.DirectReportReferences.map((e) => `<details class="evidence"><summary>${escape(words(e.UsageContext))}${e.Role ? " · " + escape(e.Role) : ""}</summary><code>${escape(e.ArtifactPath)}<br>${escape(e.EvidencePath)}</code></details>`).join("")}</section><section><h3>Dependency evidence</h3>${[...(data.incoming.get(o.id) || []), ...outEdges].map((e) => `<details class="evidence"><summary>${escape(words(e.DependencyKind))} · ${escape(e.FromObjectName)} → ${escape(e.ToObjectName)}</summary><code>${escape(e.EvidenceText)}<br><br>${escape(e.EvidencePath)}</code></details>`).join("") || '<p class="quiet">No dependencies recorded.</p>'}</section><section><h3>Model-level limitations</h3><p class="quiet">These limitations belong to this model. This analysis does not map each limitation to every affected object. Use this object’s confidence label when interpreting its usage.</p>${objectQualifications(o)}${modelLimitations(o.ModelId)}<button class="text-button" data-action="scope">Analysis scope →</button></section>`
    }</div><div class="inspector-footer">${icon("check")} Evidence from saved metadata <span>Read-only</span></div>`;
}
function objectQualifications(object) {
  const links = data.qualifications.get(object.id) || [];
  return links.length
    ? '<details class="evidence"><summary>' +
        links.length +
        " recorded qualification associations for this object</summary>" +
        links
          .map(
            (q) =>
              "<p>" +
              targetButton(
                coreTarget(data, targetReference(q.LimitationId)),
                data.raw.AnalysisLimitations.find(
                  (l) => l.Id === q.LimitationId,
                )?.Reason || "Recorded limitation",
              ) +
              " <small>" +
              (q.Basis === "BoundedReach"
                ? "Bounded reach"
                : "Conservative model scope; exact reach unknown") +
              "</small></p>",
          )
          .join("") +
        "</details>"
    : "";
}
function modelLimitations(model) {
  const limitations = data.raw.AnalysisLimitations.filter(
    (l) => l.ModelId === model,
  );
  return (
    limitations
      .map(
        (l) =>
          `<details class="evidence limitation"><summary>${escape(constructLabel(l.ConstructType))} · ${escape(supportLabel(l.SupportState))}</summary><p>${escape(l.Reason)}</p><code>${escape(l.ArtifactPath)}<br>${escape(l.EvidencePath)}</code></details>`,
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
function selectVisual(id, { focusHeading = false } = {}) {
  const { page } = reportContext(data, state);
  if (!page?.Visuals.some((v) => v.Id === id)) return;
  state.reportVisuals[state.page] = id;
  state.reportPageDetails = false;
  renderVisualCollection();
  renderVisualInspector();
  announce(
    `Inspecting ${visualLabel(page.Visuals.find((v) => v.Id === id))} on ${page.DisplayName || page.Name}.`,
  );
  if (focusHeading) $("#visual-heading").focus();
  else $(`[data-visual="${CSS.escape(id)}"]`)?.focus();
}
const workspaceLabels = {
  overview: "Overview",
  objects: "Model objects",
  pages: "Report pages",
  queries: "Power Query",
  lineage: "Lineage",
  reviews: "Reviews & findings",
  check: "Analysis scope",
  relationships: "Relationships",
  security: "Security",
};
function renderReturnTrail() {
  document.querySelectorAll(".report-origin").forEach((el) => el.remove());
  const last = state.returnTrail.at(-1);
  if (!last || last.destination !== state.view) return;
  const unused = unusedCheckpointIndex(state);
  const immediateUnused = unused === state.returnTrail.length - 1;
  $(".main-heading").insertAdjacentHTML(
    "afterend",
    `<div class="report-origin"><button data-action="${immediateUnused ? "return-unused" : "return-investigation"}">${icon("back")}Back to ${escape(immediateUnused ? "apparently unused" : last.label)}</button>${unused >= 0 && !immediateUnused ? '<button data-action="return-unused">Back to apparently unused</button>' : ""}<span>Selection, filters and position preserved</span></div>`,
  );
}
function returnInvestigation(index) {
  rememberWorkspace();
  if (!returnVisit(state, index)) return;
  renderSidebar();
  renderWorkspace();
  restoreWorkspacePresentation();
  (
    $("#object-heading") ||
    $("#visual-heading") ||
    $("#query-heading") ||
    $("#review-heading") ||
    $("#workspace-heading")
  )?.focus({ preventScroll: true });
}
function returnToUnusedReview() {
  const i = unusedCheckpointIndex(state);
  if (i >= 0) returnInvestigation(i);
}

function resetObjectFilters() {
  const review = state.usage === "ApparentlyUnused";
  resetFilters();
  if (review) state.usage = "ApparentlyUnused";
}
function renderAreaResults({ resetDetail = false } = {}) {
  const list = $("#collection").scrollTop,
    detail = resetDetail ? 0 : $(".area-detail")?.scrollTop || 0;
  $("#collection").innerHTML = modelAreaCollection(modelAreas, state);
  $("#collection").scrollTop = list;
  $("#area-inspector").innerHTML = modelAreaInspector(modelAreas, state);
  if ($(".area-detail")) $(".area-detail").scrollTop = detail;
  const rows =
    state.view === "relationships"
      ? relationshipRows(modelAreas, state)
      : securityRows(modelAreas, state);
  $("#area-count").textContent =
    `${rows.length} ${state.view === "relationships" ? "relationships" : "roles"}`;
  announce(`${rows.length} results in this model workspace.`);
}
function selectArea(id) {
  const prefix = state.view === "relationships" ? "relationship" : "security";
  state[prefix + "Selected"] = id;
  renderAreaResults({ resetDetail: true });
  $(`[data-area-row="${CSS.escape(id)}"]`)?.focus();
}
function renderScopeResults({ resetDetail = false } = {}) {
  const list = $("#collection").scrollTop,
    detail = resetDetail ? 0 : $(".scope-detail")?.scrollTop || 0;
  $("#collection").innerHTML = scopeCollection(data, state);
  $("#collection").scrollTop = list;
  $("#scope-inspector").innerHTML = scopeInspector(data, state);
  if ($(".scope-detail")) $(".scope-detail").scrollTop = detail;
  $("#scope-count").textContent = `${scopeRows(data, state).length} entries`;
}
function selectScope(index) {
  state.checkSelected = index;
  renderScopeResults({ resetDetail: true });
  $(`[data-scope-row="${index}"]`)?.focus();
}
function openCoreTarget(target) {
  if (!target) return;
  if (target.kind === "unavailable") {
    toast(target.reason);
    return;
  }
  const route = target;
  const view = {
    object: "objects",
    model: "objects",
    limited: "objects",
    query: "queries",
    "query-scope": "queries",
    report: "pages",
    lineage: "lineage",
    security: "security",
    relationships: "relationships",
    scope: "check",
    limitation: "check",
    finding: "reviews",
    view: route.view,
  }[route.kind];
  if (!view) return;
  // Validate destinations before recording a visit or touching workspace state.
  if (
    route.id &&
    [
      "object",
      "model",
      "query",
      "report",
      "lineage",
      "security",
      "relationships",
      "finding",
      "limitation",
    ].includes(route.kind)
  ) {
    const resolution = coreTarget(data, targetReference(route.id));
    if (resolution.kind === "unavailable") {
      toast(resolution.reason);
      return;
    }
  }
  rememberWorkspace();
  beginVisit(
    state,
    view,
    state.view === "objects" && state.usage === "ApparentlyUnused"
      ? "apparently unused"
      : workspaceLabels[state.view],
  );
  if (
    route.kind === "object" ||
    route.kind === "model" ||
    route.kind === "limited"
  ) {
    const object = route.kind === "object" ? data.byId.get(route.id) : null;
    if (!route.keepCollection) {
      resetFilters();
      Object.assign(state, {
        model: object?.ModelId || route.modelId || "",
        table: object?.TableId || route.tableId || "",
        system: route.kind === "limited" || !!object?.system,
        limited: route.kind === "limited",
        tab: "Summary",
        selected: object?.id || null,
        collectionSelected: object?.id || null,
        history: object ? [object.id] : [],
        cursor: object ? 0 : -1,
      });
    } else state.selected = object.id;
  } else if (route.kind === "query" || route.kind === "query-scope") {
    const query = queryIndex.byId.get(route.id);
    if (!route.keepCollection)
      Object.assign(state, {
        queryModel: query?.ModelId || route.modelId || "",
        querySearch: route.search || "",
        queryState: "",
        queryKind: "",
        queryTab: "Summary",
        queryHistory: query ? [query.id] : [],
        queryCursor: query ? 0 : -1,
      });
    else if (query && query.id !== state.querySelected) {
      state.queryHistory = state.queryHistory.slice(0, state.queryCursor + 1);
      state.queryHistory.push(query.id);
      state.queryCursor = state.queryHistory.length - 1;
    }
    state.querySelected = query?.id || null;
  } else if (route.kind === "report") {
    const report = data.reports.get(route.reportId);
    Object.assign(state, {
      report: report.Id,
      page: route.pageId || pagesFor(report)[0]?.Id || null,
      visualSearch: "",
      visualTab: route.tab || "Uses",
      reportPageDetails: !route.visualId,
    });
    if (route.visualId) state.reportVisuals[state.page] = route.visualId;
    delete state.reportScroll[state.page];
  } else if (route.kind === "lineage") setLineageDestination(route.id);
  else if (route.kind === "relationships" || route.kind === "security") {
    const prefix = route.kind === "relationships" ? "relationship" : "security";
    Object.assign(state, {
      [prefix + "Model"]: route.modelId || "",
      [prefix + "Search"]: "",
      [prefix + "Selected"]: route.id,
      [prefix + "Tab"]: prefix === "relationship" ? "Summary" : "Permissions",
    });
  } else if (["scope", "limitation"].includes(route.kind)) {
    Object.assign(state, SCOPE_DEFAULTS, {
      checkModel: route.modelId || "",
      checkSelected:
        route.kind === "limitation"
          ? data.raw.AnalysisLimitations.findIndex((l) => l.Id === route.id)
          : null,
    });
  } else if (route.kind === "finding") {
    const f = data.raw.Findings.find((f) => f.Id === route.id);
    Object.assign(state, REVIEW_DEFAULTS, {
      reviewMode: f.Category === "Accessibility" ? "accessibility" : "main",
      reviewSelected: data.raw.Findings.indexOf(f),
    });
  }
  state.view = view;
  renderSidebar();
  renderWorkspace();
  if (route.keepCollection) {
    const collection = $("#collection");
    if (collection)
      collection.scrollTop = state.workspaceScroll[view]?.[0] || 0;
  }
  $("#workspace-heading")?.focus({ preventScroll: true });
}
function openRecorded(id, options = {}) {
  openCoreTarget({ ...coreTarget(data, targetReference(id)), ...options });
}

const lineageSession = () => state.lineageSessions[state.lineageCentre];
function ensureLineage() {
  if (!lineageIndex.nodes.has(state.lineageCentre)) {
    const node =
      lineageIndex.nodes.get(state.selected) ||
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
      '<div class="empty"><h2>No objects available in Lineage</h2><p>Open an analysis with semantic dependency data.</p></div>';
    return;
  }
  $(".workspace").innerHTML = lineageWorkspace(lineageIndex, state, session);
  ["#lineage-up", "#lineage-down", ".lineage-detail"].forEach((selector, i) => {
    if ($(selector)) $(selector).scrollTop = session.scroll[i] || 0;
  });
}
function setLineageDestination(id) {
  const node = lineageIndex.nodes.get(id);
  if (!node) return;
  if (id !== state.lineageCentre || state.lineageCursor < 0) {
    state.lineageHistory = state.lineageHistory.slice(
      0,
      state.lineageCursor + 1,
    );
    state.lineageHistory.push(id);
    state.lineageCursor = state.lineageHistory.length - 1;
  }
  state.lineageCentre = id;
  state.lineageModel = node.ModelId;
  state.lineageSearch = "";
  state.lineageSessions[id] ||= createLineageSession(id);
  state.lineageSessions[id].selected = id;
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
  announce(`Lineage starting from ${lineageIndex.nodes.get(id).ObjectName}.`);
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
function openLineageModel() {
  const node = lineageIndex.nodes.get(lineageSession()?.selected);
  if (node?.usage) openRecorded(node.id);
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
function openQueryTable(id) {
  openRecorded(id);
}
function openModelFromVisual(id) {
  openRecorded(id);
}

function inspect(id, { focus = false, push = true } = {}) {
  if (!data.byId.has(id)) return;
  if (push && id !== state.selected) {
    state.history = state.history.slice(0, state.cursor + 1);
    state.history.push(id);
    state.cursor = state.history.length - 1;
  }
  state.selected = id;
  state.collectionSelected = id;
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
  relationships: ["#collection", ".area-detail"],
  security: ["#collection", ".area-detail"],
  lineage: ["#lineage-up", "#lineage-down", ".lineage-detail"],
  overview: ["#collection"],
  objects: ["#collection", "#detail-panel"],
  reviews: ["#collection", ".review-detail"],
  check: ["#collection", ".scope-detail"],
  queries: ["#collection", ".query-detail"],
};
function rememberWorkspace() {
  state.workspaceDetails[state.view] = {
    key: inspectionKey(),
    open: [...document.querySelectorAll(".workspace details")].flatMap(
      (el, i) => (el.open ? [i] : []),
    ),
  };
  if (state.view === "lineage") rememberLineage();
  if (state.view === "pages") rememberReportScroll();
  else
    state.workspaceScroll[state.view] = (scrollSelectors[state.view] || []).map(
      (selector) => $(selector)?.scrollTop || 0,
    );
}
function restoreWorkspacePresentation() {
  const saved = state.workspaceDetails[state.view];
  if (saved?.key === inspectionKey())
    document.querySelectorAll(".workspace details").forEach((el, i) => {
      el.open = saved.open.includes(i);
    });
  if (state.view === "pages") {
    const scroll = state.reportScroll[state.page];
    if (scroll) {
      if ($(".visual-list")) $(".visual-list").scrollTop = scroll.list;
      if ($(".visual-detail")) $(".visual-detail").scrollTop = scroll.detail;
    }
  } else
    (scrollSelectors[state.view] || []).forEach((selector, i) => {
      if ($(selector))
        $(selector).scrollTop = state.workspaceScroll[state.view]?.[i] || 0;
    });
}
function inspectionKey() {
  return JSON.stringify(
    {
      objects: [state.selected, state.tab],
      pages: [
        state.page,
        state.reportVisuals[state.page],
        state.visualTab,
        state.reportPageDetails,
      ],
      queries: [state.querySelected, state.queryTab],
      lineage: [
        state.lineageCentre,
        lineageSession()?.selected,
        lineageSession()?.tab,
      ],
      reviews: [state.reviewMode, state.reviewSelected, state.reviewTab],
      relationships: [state.relationshipSelected, state.relationshipTab],
      security: [state.securitySelected, state.securityTab],
      check: [state.checkSelected, state.checkTab, state.checkMode],
      overview: [],
    }[state.view],
  );
}
function showView(view, { restore = true, focus = true } = {}) {
  rememberWorkspace();
  state.view = view;
  renderSidebar();
  renderWorkspace();
  if (restore) restoreWorkspacePresentation();
  if (focus) $("#workspace-heading")?.focus({ preventScroll: true });
}

function openOverviewReviews({
  mode = "main",
  category = "",
  severity = "",
  required = false,
} = {}) {
  rememberWorkspace();
  beginVisit(state, "reviews", workspaceLabels[state.view]);
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
  rememberWorkspace();
  beginVisit(state, "objects", workspaceLabels[state.view]);
  resetFilters();
  state.model = "";
  state.system = false;
  state.usage = usage;
  state.tab = "Summary";
  state.selected = null;
  showView("objects", { restore: false });
  const first = visible()[0];
  if (first) inspect(first.id, { focus: true });
}
function openReportTarget(report, page, visual, tab = "Uses") {
  if (!report) return;
  openRecorded(visual?.Id || page?.Id || report.Id, { tab });
}

function renderReviewResults({ resetDetail = false } = {}) {
  const scroll = $(".review-list")?.scrollTop || 0;
  const detail = resetDetail ? 0 : $(".review-detail")?.scrollTop || 0;
  $(".review-list").innerHTML = reviewCollection(data, state);
  $(".review-list").scrollTop = scroll;
  $("#review-inspector").innerHTML = reviewInspector(data, state);
  if ($(".review-detail")) $(".review-detail").scrollTop = detail;
  const rows = reviewRows(data, state);
  $("#review-result-count").textContent =
    state.reviewMode === "theme"
      ? `${rows.filter((r) => r.theme.item).length} observations · ${rows.filter((r) => !r.theme.item).length} report summaries`
      : `${rows.length} results`;
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
  if (target) openCoreTarget(target);
}

document.addEventListener("click", async (event) => {
  const b = event.target.closest("button");
  if (!b) return;
  if (b.dataset.reportMeasure) {
    openRecorded(b.dataset.reportMeasure);
    return;
  }
  if (b.dataset.objectQuery) {
    openRecorded(b.dataset.objectQuery);
    return;
  }

  if (b.dataset.coreTarget) {
    openCoreTarget(JSON.parse(b.dataset.coreTarget));
    return;
  }
  if (b.dataset.areaRow) {
    selectArea(b.dataset.areaRow);
    return;
  }
  if (b.dataset.areaTab) {
    state[state.view === "relationships" ? "relationshipTab" : "securityTab"] =
      b.dataset.areaTab;
    $("#area-inspector").innerHTML = modelAreaInspector(modelAreas, state);
    $(`#area-tab-${b.dataset.areaTab}`).focus();
    return;
  }
  if (b.hasAttribute("data-scope-row")) {
    selectScope(Number(b.dataset.scopeRow));
    return;
  }
  if (b.dataset.scopeTab) {
    state.checkTab = b.dataset.scopeTab;
    $("#scope-inspector").innerHTML = scopeInspector(data, state);
    $(`#scope-tab-${state.checkTab}`).focus();
    return;
  }
  if (b.dataset.checkMode) {
    rememberWorkspace();
    state.checkViews[state.checkMode] = state.workspaceScroll.check;
    state.checkMode = b.dataset.checkMode;
    renderSidebar();
    renderWorkspace();
    scrollSelectors.check.forEach((selector, index) => {
      if ($(selector))
        $(selector).scrollTop = state.checkViews[state.checkMode]?.[index] || 0;
    });
    $(`[data-check-mode="${state.checkMode}"]`).focus();
    return;
  }
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
  if (b.hasAttribute("data-query-state")) {
    state.queryState = b.dataset.queryState;
    renderSidebar();
    renderQueries();
    $(`[data-query-state="${state.queryState}"]`).focus();
    return;
  }
  if (b.dataset.query || b.dataset.queryRelated) {
    if (b.dataset.queryRelated) {
      openRecorded(b.dataset.queryRelated, { keepCollection: true });
      return;
    }
    selectQuery(b.dataset.query, {
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
    openQueryTable(b.dataset.queryTable);
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
    Object.assign(state, SCOPE_DEFAULTS);
    renderSidebar();
    renderWorkspace();
    return;
  }
  if (b.hasAttribute("data-overview-pages")) {
    showView("pages");
    return;
  }
  if (b.dataset.overviewReport) {
    const report = data.raw.Reports.find(
      (r) => r.Id === b.dataset.overviewReport,
    );
    if (report) openReportTarget(report, pagesFor(report)[0], null);
    return;
  }
  if (b.hasAttribute("data-overview-theme")) {
    const observation =
      overviewData(data).themes[Number(b.dataset.overviewTheme)];
    if (observation)
      openCoreTarget({
        ...coreTarget(data, observation.item.Target),
        tab: "Behaviour",
      });

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
    resetObjectFilters();
    renderSidebar();
    renderWorkspace();
    $("#model-select").focus();
    return;
  }
  if (b.dataset.view) {
    if (
      state.view === "objects" &&
      state.usage === "ApparentlyUnused" &&
      b.dataset.view !== "objects"
    )
      openCoreTarget({ kind: "view", view: b.dataset.view });
    else showView(b.dataset.view, { focus: false });
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
    openRecorded(b.dataset.related, { keepCollection: true });
    return;
  }
  if (b.dataset.pageObject) {
    openModelFromVisual(b.dataset.pageObject);
    return;
  }
  if (b.dataset.page) {
    rememberWorkspace();
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
    case "return-investigation":
      returnInvestigation();
      break;
    case "return-unused":
      returnToUnusedReview();
      break;
    case "unused-branches":
      openOverviewUsage("UsedOnlyByUnusedBranch");
      break;
    case "object-lineage":
      openCoreTarget({ kind: "lineage", id: state.selected });
      break;

    case "area-reset":
    case "area-reveal": {
      const prefix =
          state.view === "relationships" ? "relationship" : "security",
        row = areaSelection(modelAreas, state).selected;
      Object.assign(state, {
        [prefix + "Search"]: "",
        [prefix + "Model"]:
          b.dataset.action === "area-reveal"
            ? row.model.Id
            : state[prefix + "Model"],
        ...(prefix === "relationship"
          ? { relationshipActive: "", relationshipDirection: "" }
          : { securityKind: "" }),
      });
      renderSidebar();
      renderWorkspace();
      $("#area-search").focus();
      break;
    }
    case "scope-reset":
      Object.assign(state, { checkSearch: "", checkModel: "", checkArea: "" });
      renderWorkspace();
      $("#scope-search").focus();
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
        state.queryModel = queryIndex.byId.get(state.querySelected).ModelId;
      renderSidebar();
      renderWorkspace();
      $("#query-search").focus();
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
    case "reset":
      resetObjectFilters();
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
      openCoreTarget({ kind: "scope" });
      break;
    case "object-coverage":
      state.tab = "Evidence";
      renderInspector();
      $("#tab-Evidence").focus();
      break;
    case "reveal":
      state.model = selected().ModelId;
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
  if (event.target.id === "area-search") {
    state[
      state.view === "relationships" ? "relationshipSearch" : "securitySearch"
    ] = event.target.value;
    renderAreaResults();
  }
  if (event.target.id === "scope-search") {
    state.checkSearch = event.target.value;
    renderScopeResults();
  }
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
  const areaFields = {
    "area-model":
      state.view === "relationships" ? "relationshipModel" : "securityModel",
    "relationship-active": "relationshipActive",
    "relationship-direction": "relationshipDirection",
    "security-kind": "securityKind",
  };
  if (areaFields[target.id]) {
    state[areaFields[target.id]] = target.value;
    renderAreaResults();
  }
  if (target.id === "scope-area" || target.id === "scope-model") {
    state[target.id === "scope-area" ? "checkArea" : "checkModel"] =
      target.value;
    renderScopeResults();
  }
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
    rememberWorkspace();
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
    resetObjectFilters();
    renderSidebar();
    renderWorkspace();
    $("#model-select").focus();
  }
  if (target.id === "usage-filter") {
    state.usage = target.value;
    if (state.usage === "ApparentlyUnused") {
      state.system = false;
      state.selected = visible()[0]?.id || null;
      state.history = state.selected ? [state.selected] : [];
      state.cursor = state.selected ? 0 : -1;
    }
    renderSidebar();
    renderWorkspace();
    $("#usage-filter").focus();
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
});
document.addEventListener("keydown", (event) => {
  const target = event.target;
  if (
    (target.matches("[data-area-tab]") || target.matches("[data-scope-tab]")) &&
    ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const area = target.hasAttribute("data-area-tab"),
      tabs = area ? AREA_TABS[state.view] : SCOPE_TABS,
      key = area
        ? state.view === "relationships"
          ? "relationshipTab"
          : "securityTab"
        : "checkTab";
    const next =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? tabs.length - 1
          : (tabs.indexOf(state[key]) +
              (event.key === "ArrowRight" ? 1 : tabs.length - 1)) %
            tabs.length;
    state[key] = tabs[next];
    $(area ? "#area-inspector" : "#scope-inspector").innerHTML = area
      ? modelAreaInspector(modelAreas, state)
      : scopeInspector(data, state);
    $(`#${area ? "area" : "scope"}-tab-${state[key]}`).focus();
    return;
  }
  if (
    (target.matches("[data-area-row]") || target.matches("[data-scope-row]")) &&
    ["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)
  ) {
    event.preventDefault();
    const area = target.hasAttribute("data-area-row"),
      rows = area
        ? state.view === "relationships"
          ? relationshipRows(modelAreas, state)
          : securityRows(modelAreas, state)
        : scopeRows(data, state);
    const current = rows.findIndex((row) =>
        area
          ? row.id === target.dataset.areaRow
          : row.index === Number(target.dataset.scopeRow),
      ),
      next =
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
    if (rows[next])
      area ? selectArea(rows[next].id) : selectScope(rows[next].index);
    return;
  }
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
    [
      "objects",
      "pages",
      "reviews",
      "queries",
      "lineage",
      "relationships",
      "security",
      "check",
    ].includes(state.view)
  ) {
    event.preventDefault();
    (state.view === "objects"
      ? $("#search")
      : ["relationships", "security"].includes(state.view)
        ? $("#area-search")
        : state.view === "check"
          ? $("#scope-search")
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
      current = rows.findIndex((v) => v.Id === target.dataset.visual);
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
    if (rows[index]) selectVisual(rows[index].Id);
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
export function mountSnapshot(snapshot, { workspace } = {}) {
  data = adaptSnapshot(snapshot);
  Object.assign(state, {
    source: snapshot.ProjectName,
    view:
      workspace === "check"
        ? "check"
        : workspace === "unused"
          ? "objects"
          : "overview",
    usage: workspace === "unused" ? "ApparentlyUnused" : "",
    model: workspace === "unused" ? "" : data.raw.SemanticModels[0]?.Id || "",
    lineageModel: data.raw.SemanticModels[0]?.Id || "",
  });
  queryIndex = buildQueryData(data);
  lineageIndex = buildLineageData(data);
  modelAreas = buildModelAreas(data);
  const first = visible()[0];
  if (first)
    Object.assign(state, {
      selected: first.id,
      collectionSelected: first.id,
      history: [first.id],
      cursor: 0,
    });
  shell();
  announce("Investigation loaded. Counts describe the analysis snapshot.");
}
document.addEventListener("click", (event) => {
  if (event.target.closest("[data-project-return]")) returnToProject(event);
});
