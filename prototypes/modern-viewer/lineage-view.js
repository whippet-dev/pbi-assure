import { escape, icon, glyph } from "./ui.js";
import { STATES, words } from "./data.js";
import { usedAs, visualLabel } from "./report-data.js";
import {
  BRANCH_SIZE,
  LINEAGE_TABS,
  neighbours,
  traverse,
  cycles,
  visibleLineage,
  lineageEndpoints,
  lineageSearch,
  lineageNodeEvidence,
} from "./lineage-data.js";

const label = (n) =>
  `${n.Table ? n.Table + " · " : ""}${words(n.ObjectType)}${n.HierarchyName ? " · " + n.HierarchyName : ""}`;
const nodeLink = (n, attr = "lineage-select") =>
  `<button class="related-object" data-${attr}="${escape(n.id)}">${escape(n.ObjectName)} ${icon("arrow")}</button>`;
const status = (n) =>
  n.usage
    ? `<span class="badge ${STATES[n.usage.UsageState].tone}"><i></i>${STATES[n.usage.UsageState].label}</span>`
    : n.tableUsage && STATES[n.tableUsage.UsageState]
      ? `<span class="source-label">Table usage: ${STATES[n.tableUsage.UsageState].label}</span>`
      : '<span class="source-label">Definition only · no object usage result</span>';
export function lineageNavigation(index, state) {
  return `<div class="lineage-navigation"><label class="project-label" for="lineage-model">SEMANTIC MODEL</label><select id="lineage-model" aria-label="Lineage semantic model"><option value="">All models</option>${index.data.raw.SemanticModels.map((m) => `<option ${m.Name === state.lineageModel ? "selected" : ""}>${escape(m.Name)}</option>`).join("")}</select><label class="project-label" for="lineage-search">STARTING OBJECT</label><div class="searchbox">${icon("search")}<input type="search" id="lineage-search" aria-label="Find lineage object" placeholder="Object, table or type…" value="${escape(state.lineageSearch)}"></div><div id="lineage-search-results">${lineageSearchResults(index, state)}</div>${index.data.byId.has(state.selected) ? '<button class="text-button lineage-current-model" data-action="lineage-current-model">Start from Model selection →</button>' : ""}</div>`;
}
export function lineageSearchResults(index, state) {
  const rows = lineageSearch(index, state),
    limit = state.lineageSearchLimit || BRANCH_SIZE;
  return `<small class="source-label">${rows.length} objects available in Lineage</small><nav class="table-nav lineage-picker" aria-label="Choose starting object">${
    rows
      .slice(0, limit)
      .map(
        (n) =>
          `<button data-lineage-centre="${escape(n.id)}" class="${n.id === state.lineageCentre ? "chosen" : ""}" ${n.id === state.lineageCentre ? 'aria-current="true"' : ""}><span><strong>${escape(n.ObjectName)}</strong><small>${escape(label(n))}</small></span></button>`,
      )
      .join("") || '<p class="quiet">No matching objects.</p>'
  }</nav>${rows.length > limit ? '<button class="text-button" data-action="lineage-more-search">Show more objects</button>' : ""}`;
}
function branch(index, session, direction, id, visible, cycleMap) {
  const node = index.nodes.get(id),
    rows = neighbours(index, id, direction, session.membership),
    expanded = session.expanded[direction].includes(id),
    limit = session.limits[direction][id] || BRANCH_SIZE,
    parents = [...(visible.parents.get(id) || [])],
    unresolved = node.unresolved.length;
  return `<div class="lineage-node ${session.selected === id ? "selected" : ""}" data-lineage-node="${escape(id)}"><div class="lineage-node-main"><button class="lineage-node-select" data-lineage-select="${escape(id)}" ${session.selected === id ? 'aria-current="true"' : ""}>${glyph(node.ObjectType)}<span><strong>${escape(node.ObjectName)}</strong><small>${escape(label(node))}</small></span></button>${rows.length ? `<button class="lineage-expand" data-lineage-expand="${escape(id)}" data-direction="${direction}" aria-expanded="${expanded}" aria-label="${expanded ? "Collapse" : "Expand"} ${escape(node.ObjectName)} ${direction === "up" ? "dependencies" : "dependents"}">${expanded ? "Hide" : "Show"}<small>${rows.length}</small></button>` : ""}</div><div class="lineage-node-notes">${parents.length > 1 ? `<span>Shared · ${parents.length} connections</span>` : ""}${cycleMap.has(id) ? "<span>Cycle</span>" : ""}${node.blocked ? "<span>Ownership unresolved · traversal stops</span>" : ""}${unresolved ? `<span>${unresolved} unresolved reference${unresolved === 1 ? "" : "s"}</span>` : ""}${node.usage?.ClassificationConfidence === "QualifiedByLimitation" ? "<span>Checks limited</span>" : ""}</div><div class="lineage-parent-links">${direction === "up" ? "Used by" : "Depends on"} ${parents
    .slice(0, 2)
    .map(
      (parent) =>
        `<button data-lineage-select="${escape(parent)}">${escape(index.nodes.get(parent).ObjectName)}</button>`,
    )
    .join(
      " · ",
    )}${parents.length > 2 ? ` +${parents.length - 2} more in Connections` : ""}</div>${expanded && rows.length > limit ? `<button class="text-button lineage-more" data-lineage-more="${escape(id)}" data-direction="${direction}">Show next ${Math.min(BRANCH_SIZE, rows.length - limit)} of ${rows.length - limit} remaining</button>` : ""}</div>`;
}
export function lineageLane(index, session, direction) {
  const visible = visibleLineage(index, session, direction),
    full = traverse(index, session.centre, direction, session.membership),
    cycleMap = cycles(index, session.membership);
  const depths = [
      ...new Set(visible.ids.map((id) => visible.distance.get(id))),
    ].sort((a, b) => a - b),
    centreRows = neighbours(
      index,
      session.centre,
      direction,
      session.membership,
    ),
    centreLimit = session.limits[direction][session.centre] || BRANCH_SIZE;
  return `<header class="lineage-lane-heading"><h3>${direction === "up" ? "Depends on" : "Used by"}</h3><span>${visible.ids.length} shown / ${full.ids.length - 1} in scope</span><p>${direction === "up" ? "Dependencies of the starting object" : "Objects that use the starting object"}</p></header><div class="lineage-lane-scroll" id="lineage-${direction}" tabindex="0">${
    depths
      .map(
        (depth) =>
          `<section class="lineage-level"><div class="lineage-level-heading">${depth === 1 ? (direction === "up" ? "Direct dependencies" : "Direct dependents") : `${depth} steps from starting object`} <span>${visible.ids.filter((id) => visible.distance.get(id) === depth).length} objects</span></div>${visible.ids
            .filter((id) => visible.distance.get(id) === depth)
            .map((id) =>
              branch(index, session, direction, id, visible, cycleMap),
            )
            .join("")}</section>`,
      )
      .join("") ||
    `<p class="quiet lineage-empty">${index.nodes.get(session.centre).blocked ? "The starting object’s owner could not be identified. Inspect Evidence; connections beyond this object cannot be followed." : "No dependencies recorded in this direction within the chosen scope."}</p>`
  }${centreRows.length > centreLimit ? `<button class="text-button lineage-root-more" data-lineage-more="${escape(session.centre)}" data-direction="${direction}">Show next ${Math.min(BRANCH_SIZE, centreRows.length - centreLimit)} direct connections (${centreRows.length - centreLimit} remaining)</button>` : ""}${visible.parents.get(session.centre)?.size ? '<p class="quiet lineage-empty">A connection returns to the starting object. The cycle is retained without repeating the object.</p>' : ""}</div>`;
}
export function lineageWorkspace(index, state, session) {
  const node = index.nodes.get(session.centre),
    up = traverse(index, node.id, "up", session.membership),
    endpoints = lineageEndpoints(index, session),
    down = endpoints.downstream;
  const stopped = [...new Set([...up.ids, ...down.ids])].filter(
    (id) => index.nodes.get(id).blocked,
  ).length;
  const notes = [
    node.usage?.ClassificationConfidence === "QualifiedByLimitation"
      ? "Checks limited for the starting object"
      : "",
    node.unresolved.length
      ? `${node.unresolved.length} unresolved reference${node.unresolved.length === 1 ? "" : "s"} at starting object`
      : "",
    stopped
      ? `Traversal stops at ${stopped} ownership ${stopped === 1 ? "boundary" : "boundaries"}`
      : "",
    cycles(index, session.membership).has(node.id)
      ? "Cycle includes starting object"
      : "",
  ]
    .filter(Boolean)
    .join(" · ");
  return `<section class="lineage-map" id="collection" tabindex="-1" aria-label="Semantic lineage map"><header class="lineage-centre"><div class="lineage-centre-toolbar"><span class="eyebrow starting-marker">${icon("anchor")}STARTING OBJECT</span><div class="history-buttons"><button data-action="lineage-back" aria-label="Previous starting object" ${state.lineageCursor <= 0 ? "disabled" : ""}>${icon("back")}</button><button data-action="lineage-forward" aria-label="Next starting object" ${state.lineageCursor >= state.lineageHistory.length - 1 ? "disabled" : ""}>${icon("arrow")}</button></div><span class="source-label">${state.lineageCursor + 1} / ${state.lineageHistory.length} starting object${state.lineageHistory.length === 1 ? "" : "s"}</span></div><button class="lineage-centre-title" data-lineage-select="${escape(node.id)}">${glyph(node.ObjectType)}<span><strong id="lineage-centre-heading">${escape(node.ObjectName)}</strong><small>${escape(node.SemanticModel)} / ${escape(label(node))}</small></span></button><div class="lineage-scale"><span><strong>${up.ids.length - 1}</strong> dependencies</span><span><strong>${down.ids.length - 1}</strong> dependents</span><button data-lineage-tab="Reports"><strong>${endpoints.rows.length}</strong> recorded report location${endpoints.rows.length === 1 ? "" : "s"} ${icon("arrow")}</button></div><div class="lineage-scope"><label><input id="lineage-membership" type="checkbox" ${session.membership ? "checked" : ""}>Include table membership</label><button class="text-button" data-action="lineage-reset">Reset branches</button></div><p class="lineage-boundary">${session.membership ? "Membership included; table context can bring in sibling objects." : "Definition and structural dependencies."} Counts include direct and further dependencies in this analysis.</p>${notes ? `<div class="lineage-boundary lineage-qualification">${escape(notes)}${node.unresolved.length || node.blocked || node.usage?.ClassificationConfidence === "QualifiedByLimitation" ? '<button class="text-button" data-action="lineage-centre-evidence">Inspect starting object evidence</button>' : ""}</div>` : ""}</header><div class="lineage-lanes"><section class="lineage-lane" aria-label="Depends on">${lineageLane(index, session, "up")}</section><section class="lineage-lane" aria-label="Used by">${lineageLane(index, session, "down")}</section></div></section><aside class="inspector" id="lineage-inspector" aria-label="Selected lineage object">${lineageInspector(index, session)}</aside>`;
}
function connections(index, session, node) {
  const limit = session.connectionLimit || BRANCH_SIZE;
  const group = (direction, title) => {
    const rows = neighbours(
      index,
      node.id,
      direction,
      session.membership,
      true,
    );
    return `<section><h3>${title} <span class="source-label">${rows.length}</span></h3>${
      rows
        .slice(0, limit)
        .map(
          (row) =>
            `<div class="lineage-connection">${nodeLink(row.node)}<small>${escape(label(row.node))} · ${[...new Set(row.edges.map((e) => words(e.DependencyKind)))].map(escape).join(" / ")}</small></div>`,
        )
        .join("") ||
      '<p class="quiet">No recorded connections in this scope.</p>'
    }${rows.length > limit ? '<button class="text-button" data-action="lineage-more-connections">Show more connections</button>' : ""}</section>`;
  };
  const up = traverse(index, session.centre, "up", session.membership),
    down = traverse(index, session.centre, "down", session.membership),
    traversal = up.distance.has(node.id) ? up : down;
  let route = [],
    cursor = node.id;
  if (traversal.distance.has(cursor)) {
    route.push(cursor);
    while (traversal.parent.has(cursor)) {
      cursor = traversal.parent.get(cursor);
      route.push(cursor);
    }
    route.reverse();
  }
  return `${status(node)}${node.blocked ? '<p class="quiet">Report-measure ownership is unresolved. The connections below are projected graph records; traversal stops here to avoid combining different owners.</p>' : ""}${node.id !== session.centre ? `<section class="supporting-section"><h3>Connection to starting object</h3>${route.length ? `<div class="lineage-route">${route.map((id) => nodeLink(index.nodes.get(id))).join("<span>→</span>")}</div><p class="footnote">One recorded path. Other connections may also exist.</p><button class="text-button" data-lineage-reveal="${escape(node.id)}">Show connection</button>` : '<p class="quiet">No connection to the starting object was found in this scope. Start from this object to investigate it.</p>'}</section>` : ""}${group("up", "Direct dependencies")}${group("down", "Direct dependents")}<p class="footnote">Dependencies describe saved definitions and model requirements, not runtime filter propagation.</p>`;
}
function reports(index, session) {
  const endpoints = lineageEndpoints(index, session);
  return `<section><h3>Report use of the starting object</h3><p class="quiet">References in the analysed reports, to the starting object or objects that depend on it. Each location is counted once; all recorded dependency paths are considered.</p>${
    endpoints.rows
      .slice(0, session.endpointLimit)
      .map((endpoint) => {
        const first = endpoint.references[0].reference;
        return `<div class="lineage-endpoint"><strong>${escape(endpoint.report?.Name || first.Report)}</strong><small>${escape(endpoint.page?.DisplayName || first.Page || "Report scope")} / ${escape(endpoint.visual ? visualLabel(endpoint.visual) : first.Visual || "Page / report reference")}</small><span>${[...new Set(endpoint.references.map((e) => usedAs(e.reference)))].map(escape).join(" · ")}</span><small>${endpoint.direct ? "Direct reference to the starting object" : "Through objects that use the starting object"}</small><div class="lineage-endpoint-owners">${[
          ...endpoint.owners,
        ]
          .slice(0, session.connectionLimit || BRANCH_SIZE)
          .map(
            (id) =>
              `<button class="text-button" data-lineage-reveal="${escape(id)}" data-direction="down">Show connection to ${escape(index.nodes.get(id).ObjectName)}</button>`,
          )
          .join(
            "",
          )}${endpoint.owners.size > (session.connectionLimit || BRANCH_SIZE) ? '<button class="text-button" data-action="lineage-more-connections">Show more owners</button>' : ""}</div>${endpoint.report && (!first.Page || endpoint.page) && (!first.Visual || endpoint.visual) ? `<button class="text-button" data-lineage-endpoint="${escape(endpoint.key)}">Inspect ${endpoint.visual ? "visual" : endpoint.page ? "page" : "report"} ${icon("arrow")}</button>` : '<p class="quiet">Location recorded; the exact saved entity is unavailable in this inventory.</p>'}</div>`;
      })
      .join("") ||
    '<p class="quiet">No exact report locations were found through these dependencies. This does not establish no report use.</p>'
  }${endpoints.rows.length > session.endpointLimit ? '<button class="text-button" data-action="lineage-more-endpoints">Show more report locations</button>' : ""}</section>${
    endpoints.unlocated.length
      ? `<section><h3>Report reach without locations</h3><p class="quiet">Report use is recorded for these objects, but exact locations are not included in this analysis.</p>${endpoints.unlocated
          .slice(0, session.connectionLimit || BRANCH_SIZE)
          .map(
            (n) =>
              `<div class="lineage-connection">${nodeLink(n)}<small>${escape(label(n))}</small></div>`,
          )
          .join(
            "",
          )}${endpoints.unlocated.length > (session.connectionLimit || BRANCH_SIZE) ? '<button class="text-button" data-action="lineage-more-connections">Show more report-reached nodes</button>' : ""}</section>`
      : ""
  }<p class="footnote">Report-measure placements and table/function direct-use evidence are not published here. Saved selectors and unresolved report references are not converted into endpoints.</p>`;
}
function evidence(index, session, node) {
  const detail = lineageNodeEvidence(index, node),
    edges = [
      ...(index.outgoing.get(node.id) || []),
      ...(index.incoming.get(node.id) || []),
    ],
    limit = session.connectionLimit || BRANCH_SIZE;
  return `<section><h3>Identity & confidence</h3><p class="quiet">${escape(node.SemanticModel)} / ${escape(label(node))}</p>${status(node)}${node.usage?.ClassificationConfidence === "QualifiedByLimitation" ? '<p class="review-qualification">Checks limited for this object. Counts and paths may be incomplete; its usage state is retained.</p>' : '<p class="quiet">A recorded edge is evidence within the saved scan, not a complete-runtime-lineage claim.</p>'}</section>${node.unresolved.length ? `<section><h3>Unresolved references</h3>${node.unresolved.map((u) => `<div class="lineage-unresolved"><strong>${escape(u.ReferenceText)}</strong><small>${escape(words(u.ResolutionOutcome || "Unresolved"))} · ${escape(words(u.DependencyKind))}</small><p>${escape(u.Reason)}</p><code>${escape(u.EvidencePath)}</code></div>`).join("")}<p class="footnote">These are missing or ambiguous references, not graph edges. Candidate target sets are not published.</p></section>` : ""}${node.ObjectType === "ReportMeasure" ? `<section><h3>Report-measure ownership</h3><p class="quiet">The public graph omits report owners. ${node.blocked ? "No unique locally bound definition is available. Traversal stops at this boundary." : "One locally bound definition matches this projected identity in the snapshot."}</p>${node.candidates.map((c) => `<p class="quiet">${escape(c.report.Name)} / ${escape(c.measure.Entity)}[${escape(c.measure.Name)}] · ${c.bound ? "Local model binding" : "Binding unavailable"}</p>`).join("")}</section>` : ""}<section><h3>Connection evidence</h3>${
    edges
      .slice(0, limit)
      .map(
        (e) =>
          `<details class="evidence"><summary>${escape(e.FromObjectName)} → ${escape(e.ToObjectName)} · ${escape(words(e.DependencyKind))}</summary><code>${escape(e.EvidenceText)}<br>${escape(e.EvidencePath)}</code></details>`,
      )
      .join("") || '<p class="quiet">No dependency evidence recorded.</p>'
  }${edges.length > limit ? '<button class="text-button" data-action="lineage-more-connections">Show more connection evidence</button>' : ""}</section>${detail.definition?.Expression ? `<section><details class="evidence"><summary>Saved DAX definition</summary><pre class="query-m"><code>${escape(detail.definition.Expression)}</code></pre></details></section>` : ""}<section><h3>Limitations</h3>${detail.direct.map((l) => `<p class="quiet">${escape(l.Reason)}</p>`).join("") || '<p class="quiet">No object-specific limitation record identified. This does not mean everything was checked.</p>'}${detail.context.length ? `<details class="evidence"><summary>${detail.context.length} other limitations associated with this model</summary><p class="quiet">Context only; the snapshot does not establish that each limitation affects this node.</p>${detail.context.map((l) => `<p>${escape(l.Reason)}</p>`).join("")}</details>` : ""}</section><p class="footnote">Semantic dependencies only. Power Query execution, source-to-column lineage and runtime report behaviour are outside this view.</p>`;
}
export function lineageInspector(index, session) {
  const node =
      index.nodes.get(session.selected) || index.nodes.get(session.centre),
    tab = session.tab;
  return `<div class="inspector-toolbar ${node.id === session.centre ? "starting-inspection" : "related-inspection"}"><span>${node.id === session.centre ? "STARTING OBJECT" : "INSPECTING RELATED OBJECT"}</span></div><div class="object-hero lineage-node-hero ${node.id === session.centre ? "starting-inspection" : "related-inspection"}"><div class="object-breadcrumb">${escape(node.SemanticModel)} / ${escape(label(node))}</div><h2 id="lineage-node-heading" tabindex="-1">${escape(node.ObjectName)}</h2></div><div class="inspector-tabs" role="tablist" aria-label="Lineage information">${LINEAGE_TABS.map((t) => `<button role="tab" id="lineage-tab-${t}" data-lineage-tab="${t}" aria-controls="lineage-panel" aria-selected="${tab === t}" tabindex="${tab === t ? 0 : -1}">${t}</button>`).join("")}</div><div class="inspector-content lineage-detail" id="lineage-panel" role="tabpanel" aria-labelledby="lineage-tab-${tab}" tabindex="0">${tab === "Reports" ? reports(index, session) : tab === "Evidence" ? evidence(index, session, node) : connections(index, session, node)}</div><div class="inspector-footer lineage-actions">${node.id !== session.centre ? `<button class="text-button" data-lineage-centre="${escape(node.id)}">Start from this object</button>` : "<span>Saved dependencies</span>"}${node.usage ? `<button class="primary-button" data-action="lineage-model">Open in Model ${icon("arrow")}</button>` : '<span class="source-label">Definition evidence</span>'}</div>`;
}
