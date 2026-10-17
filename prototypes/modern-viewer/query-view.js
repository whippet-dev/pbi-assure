import { escape, icon, glyph } from "./ui.js";
import { words } from "./data.js";
import { findingList } from "./report-view.js";
import {
  QUERY_STATES,
  QUERY_ROLES,
  filterQueries,
  queryRelations,
  sourceEvidence,
  loadedTables,
  queryObservations,
} from "./query-data.js";

export const QUERY_TABS = ["Summary", "Dependencies", "Sources", "M"];
const kind = (q) =>
  q.SourceKind === "TablePartition" ? "Table partition" : "Named expression";
const badge = (q) =>
  `<span class="badge ${QUERY_STATES[q.UsageState].tone}"><i></i>${QUERY_STATES[q.UsageState].label}</span>`;
const queryLink = (q) =>
  `<button class="related-object" data-query-related="${escape(q.id)}">${escape(q.QueryName)}${icon("arrow")}</button><small>${escape(q.SemanticModel)} · ${kind(q)}${q.Partition ? ` · ${escape(q.Partition)}` : ""}</small>`;
const empty = (text) => `<p class="quiet">${text}</p>`;
const tabButton = (tab, text) =>
  `<button class="text-button" data-query-tab="${tab}">${text} ${icon("arrow")}</button>`;
const locations = {
  LocalFile: "Local file",
  NetworkFile: "Network file",
  RelativeFile: "Relative file",
  WebAddress: "Web address",
  NamedServer: "Named server",
  EmbeddedInModel: "Embedded in model",
  OnlineService: "Online service",
  DynamicOrUnspecified: "Dynamic or unspecified",
};

export function queryNavigation(index, state) {
  const queries = index.queries.filter(
    (q) => !state.queryModel || q.SemanticModel === state.queryModel,
  );
  return `<div class="query-navigation"><label class="project-label" for="query-model">SEMANTIC MODEL</label><select id="query-model" aria-label="Query semantic model"><option value="">All models</option>${index.data.raw.SemanticModels.map((m) => `<option ${m.Name === state.queryModel ? "selected" : ""}>${escape(m.Name)}</option>`).join("")}</select><div class="table-heading">Query usage <span>${queries.length}</span></div><nav class="table-nav" aria-label="Filter query usage">${[["", "All queries"], ...Object.entries(QUERY_STATES).map(([k, v]) => [k, v.label])].map(([value, label]) => `<button data-query-state="${value}" class="${state.queryState === value ? "chosen" : ""}" aria-pressed="${state.queryState === value}"><span>${label}</span><span>${queries.filter((q) => !value || q.UsageState === value).length}</span></button>`).join("")}</nav><p class="source-label query-scope">Usage in the analysed queries.<br>Separate from report/model object usage.</p></div>`;
}
export function queryWorkspace(index, state) {
  if (!index.byId.has(state.querySelected)) {
    const first =
      filterQueries(index, state).find(
        (q) => q.QueryName === "LoadedFactQuery",
      ) || filterQueries(index, state)[0];
    if (first) {
      state.querySelected = first.id;
      state.queryHistory = [first.id];
      state.queryCursor = 0;
    }
  }
  return `<section class="collection-pane query-collection" aria-label="Power Query collection"><div class="collection-tools"><div class="collection-title"><strong>Queries</strong><span id="query-result-count"></span></div><div class="searchbox">${icon("search")}<input id="query-search" type="search" aria-label="Search queries" placeholder="Search queries, tables or models…" value="${escape(state.querySearch)}"><kbd>/</kbd></div><div class="filter-line"><label class="sr-only" for="query-kind">Query type</label><select id="query-kind"><option value="">All query types</option><option value="TablePartition" ${state.queryKind === "TablePartition" ? "selected" : ""}>Table partitions</option><option value="NamedExpression" ${state.queryKind === "NamedExpression" ? "selected" : ""}>Named expressions</option></select><button class="text-button" data-action="query-reset">Reset filters</button></div></div><div id="collection" class="collection-scroll query-list" tabindex="-1"></div><div class="collection-footer"><span>Saved M expressions</span><span>↑ ↓ to explore · / to search</span></div></section><aside class="inspector" id="query-inspector" aria-label="Selected query"></aside>`;
}
export function queryCollection(index, state) {
  const rows = filterQueries(index, state),
    active = rows.some((q) => q.id === state.querySelected)
      ? state.querySelected
      : rows[0]?.id;
  if (!rows.length)
    return `<div class="empty"><h2>${index.available ? "No matching queries" : "Query inventory not published"}</h2><p>${index.available ? "Try another model, query name or usage state." : "This JSON does not include Power Query usage records."}</p>${index.queries.length ? '<button class="primary-button" data-action="query-reset">Clear filters</button>' : ""}</div>`;
  return rows
    .map((q) => {
      const relations = queryRelations(index, q);
      return `<button class="query-select ${q.id === state.querySelected ? "selected" : ""}" data-query="${escape(q.id)}" tabindex="${q.id === active ? 0 : -1}" ${q.id === state.querySelected ? 'aria-current="true"' : ""}><span class="query-row-title">${glyph(q.SourceKind === "TablePartition" ? "Table" : "Function")}<strong>${escape(q.QueryName)}</strong>${q.HasDynamicReferences ? '<span class="query-dynamic" title="Dynamic query references recorded">◐</span>' : ""}</span><small>${!state.queryModel ? `${escape(q.SemanticModel)} · ` : ""}${kind(q)}${q.Partition ? ` · ${escape(q.Partition)}` : ""}</small><span class="query-row-foot">${badge(q)}<span>${relations.outgoing.length} dependencies · ${relations.incoming.length} dependents</span></span></button>`;
    })
    .join("");
}

function modelLinks(index, query) {
  const bindings = loadedTables(index, query);
  return bindings.length
    ? `<div class="query-relations">${bindings.map((b) => `<div class="query-relation"><button class="related-object" data-query-table="${escape(b.table.Name)}" data-query-model="${escape(b.model.Name)}">${escape(b.table.Name)} ${icon("arrow")}</button><small>${escape(b.model.Name)} · partition ${escape(b.partition.Name)}</small><span class="connection-meaning">${b.direct ? "Loads this table partition" : "Reached through recorded query dependencies"}</span></div>`).join("")}</div>`
    : empty(
        "No loaded model table was found through the recorded dependencies.",
      );
}
function summary(index, query, relations, sources, observations) {
  const use =
    query.UsageState === "LoadedToModel"
      ? "This M expression belongs to a table partition loaded into the semantic model."
      : query.UsageState === "SupportingQuery"
        ? "This expression supports a query loaded into the model, directly or through other queries."
        : "No use by a loaded model query was established in this snapshot. Apparently unused is a review candidate, never permission to delete.";
  const families = [
    ...new Set(sources.rows.map((r) => r.source.ConnectorFamily)),
  ];
  return `<section><h3>Query usage</h3><p class="why-text">${use}</p><dl class="query-facts"><dt>Type</dt><dd>${kind(query)}</dd><dt>Role</dt><dd>${escape(QUERY_ROLES[query.QueryRole] || "Not published for this query")}</dd>${query.Table ? `<dt>Model table</dt><dd>${escape(query.Table)}</dd><dt>Partition</dt><dd>${escape(query.Partition)}</dd>` : ""}</dl></section><section class="supporting-section"><div class="section-heading"><h3>Semantic model connection</h3><span class="source-label">Open table context</span></div>${modelLinks(index, query)}</section><section><div class="section-heading"><h3>Query dependencies</h3>${tabButton("Dependencies", "Explore dependencies")}</div><p class="quiet">${relations.outgoing.length} direct dependencies · ${relations.incoming.length} recorded dependents</p>${query.HasDynamicReferences ? '<div class="review-note">Dynamic query references are recorded. Static dependencies may be incomplete.</div>' : ""}</section><section><div class="section-heading"><h3>Recognised data sources</h3>${tabButton("Sources", "Inspect sources")}</div><p class="quiet">${families.length ? escape(families.join(" · ")) : "No recognised data source was found through the recorded dependencies."}</p></section>${observations.findings.length || observations.limits.length || observations.contextLimits.length ? `<section><div class="section-heading"><h3>Evidence to review</h3>${tabButton("M", "Inspect evidence")}</div><p class="quiet">${observations.findings.length} query findings/reviews · ${observations.limits.length} query limitations${observations.contextLimits.length ? ` · ${observations.contextLimits.length} other query-dependency limitations in this model` : ""}</p></section>` : ""}`;
}
function relationsView(index, query, relations, observations) {
  const outgoing = relations.outgoing
    .map(
      (r) =>
        `<div class="query-relation">${r.targets.length === 1 ? queryLink(r.targets[0]) : `<strong>${escape(r.edge.ToQueryName)}</strong>${empty(r.targets.length ? "Target partition is not published. Choose a candidate to inspect; the edge does not identify one." : "Dependency recorded; no matching M expression is published.")}${r.targets.map(queryLink).join("")}`}</div>`,
    )
    .join("");
  const incoming = relations.incoming
    .map(
      (r) =>
        `<div class="query-relation">${r.source ? queryLink(r.source) : `<strong>${escape(r.edge.FromQueryName)}</strong><small>${escape(r.edge.FromTable || r.edge.FromSourceKind)} · ${escape(r.edge.FromPartition)}</small>${empty("Dependency recorded; the M expression is not included for this source.")}`}${r.targets.length > 1 ? '<span class="quiet">Target identity is ambiguous between same-name partitions.</span>' : ""}</div>`,
    )
    .join("");
  return `<section><h3>Depends on <span class="source-label">${relations.outgoing.length}</span></h3><p class="quiet">Queries referenced by this expression.</p><div class="query-relations">${outgoing || empty("No static query dependencies recorded.")}</div></section><section><h3>Depended on by <span class="source-label">${relations.incoming.length}</span></h3><p class="quiet">Expressions that reference this query.</p><div class="query-relations">${incoming || empty("No static dependents recorded.")}</div></section>${query.HasDynamicReferences ? '<div class="review-note">Dynamic query names cannot be resolved into static edges here.</div>' : ""}<section><h3>Column preparation evidence</h3>${observations.columns.length ? `<details><summary>${observations.columns.length} recorded column operations</summary><div class="query-relations">${observations.columns.map((c) => `<div class="query-relation"><strong>${escape(c.SourceTable)}[${escape(c.SourceColumn)}]</strong><small>${escape(c.SourceQuery)} → ${escape(c.ConsumerQuery)}${c.ConsumerTable ? ` · ${escape(c.ConsumerTable)}` : ""}</small><span>${escape(words(c.UsageKind))} · <code>${escape(c.MFunction)}</code>${c.StepName ? ` · ${escape(c.StepName)}` : ""}</span></div>`).join("")}</div></details><p class="footnote">Preparation evidence from recognised M operations. This does not establish report usage or complete column lineage.</p>` : empty("No column-operation evidence published for this query.")}</section><p class="footnote">Saved static graph. An absent edge does not establish that no runtime dependency exists.</p>`;
}
function sourcesView(query, sources) {
  const list = (rows) =>
    `<div class="query-relations">${rows.map(({ source, query: origin, direct }) => `<div class="query-relation"><strong>${escape(source.ConnectorFamily)}</strong><small><code>${escape(source.ConnectorFunction)}</code> · ${escape(locations[source.LocationKind] || words(source.LocationKind))}</small>${!direct ? queryLink(origin) : '<span class="quiet">Recognised in this expression</span>'}</div>`).join("")}</div>`;
  return `<section><h3>In this query</h3>${sources.rows.some((r) => r.direct) ? list(sources.rows.filter((r) => r.direct)) : empty("No connector record published directly for this expression.")}</section><section><h3>Through dependencies</h3>${sources.rows.some((r) => !r.direct) ? list(sources.rows.filter((r) => !r.direct)) : empty("No additional connector reached through unambiguous static dependencies.")}</section>${sources.unresolved ? `<div class="review-note">${sources.unresolved} dependency target${sources.unresolved === 1 ? " is" : "s are"} missing or ambiguous. Source traversal stops at those edges.</div>` : ""}${sources.dynamicQueries.length ? `<section><h3>Dynamic query references</h3><p class="quiet">These expressions can leave additional sources unresolved.</p>${sources.dynamicQueries.map(queryLink).join("")}</section>` : ""}<p class="footnote">Recognised connector functions and coarse location kinds only. No credentials, connectivity, refresh behaviour or complete end-to-end lineage are established. No source record does not mean no data source.</p>`;
}
function evidenceView(query, observations) {
  const limits = (rows) =>
    rows
      .map(
        (l) =>
          `<div class="query-relation"><strong>${escape(words(l.Cause))}</strong><p class="quiet">${escape(l.Reason)}</p><small>${escape(l.ObjectName || l.Table || "Model scope")} · ${escape(l.ArtifactPath)}${l.EvidencePath ? ` · ${escape(l.EvidencePath)}` : ""}</small></div>`,
      )
      .join("");
  return `<section><h3>Saved M expression</h3><p class="quiet">Source text only. May contain source locations or other literals.</p>${query.Expression ? `<pre class="query-m"><code>${escape(query.Expression)}</code></pre>` : empty("No expression text published.")}<dl class="query-facts"><dt>Artifact</dt><dd>${escape(query.ArtifactPath)}</dd><dt>Dynamic references</dt><dd>${query.HasDynamicReferences ? "Recorded" : "Not recorded"}</dd></dl></section><section><h3>Query findings & reviews</h3>${findingList(observations.findings, "No query/source findings recorded for this expression.")}</section><section><h3>Query limitations</h3>${limits(observations.limits) || empty("No query-specific limitation record identified. This does not mean every M construct was checked.")}${observations.contextLimits.length ? `<details><summary>${observations.contextLimits.length} other query-dependency limitations in this model</summary><p class="quiet">Model context; the snapshot does not establish that these affect the selected query.</p>${limits(observations.contextLimits)}</details>` : ""}</section><p class="footnote">Parameter flags are not published in this JSON schema. A missing role is left unspecified. Query graph and source records are scanner evidence; the viewer does not evaluate M.</p>`;
}
export function queryInspector(index, state) {
  const query = index.byId.get(state.querySelected);
  if (!query)
    return '<div class="empty"><h2>Select a query</h2><p>Its model connection and saved evidence will appear here.</p></div>';
  const relations = queryRelations(index, query),
    sources = sourceEvidence(index, query),
    observations = queryObservations(index, query),
    tab = state.queryTab;
  return `<div class="inspector-toolbar"><span>QUERY INSPECTOR</span><div class="history-buttons"><button data-action="query-back" aria-label="Previous query" ${state.queryCursor <= 0 ? "disabled" : ""}>${icon("back")}</button><button data-action="query-forward" aria-label="Next query" ${state.queryCursor >= state.queryHistory.length - 1 ? "disabled" : ""}>${icon("arrow")}</button></div><span class="history-count">${state.queryCursor + 1} / ${state.queryHistory.length}</span></div><div class="object-hero"><div class="object-breadcrumb">${escape(query.SemanticModel)} <span>/</span> ${kind(query).toUpperCase()}</div><div class="object-title">${glyph(query.SourceKind === "TablePartition" ? "Table" : "Function")}<h2 id="query-heading" tabindex="-1">${escape(query.QueryName)}</h2></div><div class="hero-badges">${badge(query)}${query.HasDynamicReferences ? '<span class="limit-badge">Dynamic references</span>' : ""}</div></div>${!filterQueries(index, state).some((q) => q.id === query.id) ? '<div class="outside-filter">Selected query is outside your filters.<button data-action="query-reveal">Show in collection</button></div>' : ""}<div class="inspector-tabs" role="tablist" aria-label="Query information">${QUERY_TABS.map((t) => `<button role="tab" id="query-tab-${t}" data-query-tab="${t}" aria-controls="query-panel" aria-selected="${t === tab}" tabindex="${t === tab ? 0 : -1}">${t === "M" ? "M & evidence" : t}${t === "Dependencies" ? `<span>${relations.outgoing.length + relations.incoming.length}</span>` : ""}</button>`).join("")}</div><div class="inspector-content query-detail" id="query-panel" role="tabpanel" aria-labelledby="query-tab-${tab}" tabindex="0">${tab === "Summary" ? summary(index, query, relations, sources, observations) : tab === "Dependencies" ? relationsView(index, query, relations, observations) : tab === "Sources" ? sourcesView(query, sources) : evidenceView(query, observations)}</div>`;
}
