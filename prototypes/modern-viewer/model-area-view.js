import { escape, icon } from "./ui.js";
import { STATES, words, edgeId } from "./data.js";
import {
  relationshipRows,
  securityRows,
  roleCounts,
  relationshipEvidence,
  securityEvidence,
  modelObject,
} from "./model-area-data.js";

export const AREA_TABS = {
  relationships: ["Summary", "Evidence"],
  security: ["Permissions", "Objects", "Evidence"],
};
export const targetButton = (target, label = target?.label, primary = false) =>
  target
    ? `<button class="${primary ? "primary-button" : "text-button"}" data-core-target="${escape(JSON.stringify(target))}">${escape(label)} ${icon("arrow")}</button>`
    : "";
export const facts = (rows) =>
  `<dl class="area-facts">${rows
    .filter(([, v]) => v != null && v !== "")
    .map(([k, v]) => `<dt>${escape(k)}</dt><dd>${escape(v)}</dd>`)
    .join("")}</dl>`;
const objectButton = (o, label) =>
  o ? targetButton({ kind: "object", id: o.id }, label || o.ObjectName) : "";
const usage = (object) =>
  object
    ? `<span class="badge ${STATES[object.UsageState].tone}"><i></i>${STATES[object.UsageState].label}</span>`
    : "";
export const directionLabel = (value) =>
  ({
    oneDirection: "Single direction",
    bothDirections: "Both directions",
    automatic: "Automatic",
  })[value] || words(value || "Not recorded");
const activationLabel = (value) =>
  ({
    ActivatedByReportUsedDax: "Referenced by DAX with recorded report use",
    ReferencedOnlyByUnusedDax: "Referenced only by unused DAX",
    NoDetectedActivation: "No detected activation",
  })[value] || words(value || "Not published");
const options = (values, current, first) =>
  `<option value="">${first}</option>${values.map(([value, label]) => `<option value="${escape(value)}" ${value === current ? "selected" : ""}>${escape(label)}</option>`).join("")}`;
export function modelAreaNavigation(index, state) {
  const key =
    state.view === "relationships" ? "relationshipModel" : "securityModel";
  return `<div class="area-navigation"><label class="project-label" for="area-model">SEMANTIC MODEL</label><select id="area-model" aria-label="${state.view === "relationships" ? "Relationship" : "Security"} semantic model">${options(
    index.data.raw.SemanticModels.map((m) => [m.Name, m.Name]),
    state[key],
    "All models",
  )}</select><p class="source-label">${state.view === "relationships" ? "Saved relationship definitions and structural evidence." : "Saved role definitions. Service membership and effective user access are not tested."}</p></div>`;
}
export function areaSelection(index, state) {
  const relationships = state.view === "relationships",
    all = relationships ? index.relationships : index.roles,
    rows = relationships
      ? relationshipRows(index, state)
      : securityRows(index, state),
    key = relationships ? "relationshipSelected" : "securitySelected";
  if (!all.some((row) => row.id === state[key]))
    state[key] = rows[0]?.id ?? null;
  return { rows, selected: all.find((row) => row.id === state[key]), key };
}
export function modelAreaWorkspace(index, state) {
  const relationships = state.view === "relationships",
    prefix = relationships ? "relationship" : "security",
    { rows } = areaSelection(index, state);
  const directionValues = [
    ...new Set(
      index.relationships.map((row) => row.relationship.CrossFilteringBehavior),
    ),
  ].map((v) => [v, directionLabel(v)]);
  return `<section class="collection-pane" aria-label="${relationships ? "Relationship" : "Security role"} collection"><div class="collection-tools"><div class="collection-title"><strong>${relationships ? "Relationships" : "Roles"}</strong><span id="area-count">${rows.length} ${relationships ? "relationships" : "roles"}</span></div><div class="searchbox">${icon("search")}<input id="area-search" type="search" aria-label="${relationships ? "Search relationships" : "Search security roles"}" placeholder="${relationships ? "Name, table or column…" : "Role, table, filter or column…"}" value="${escape(state[prefix + "Search"])}"><kbd>/</kbd></div><div class="filter-line">${
    relationships
      ? `<label class="sr-only" for="relationship-active">Relationship state</label><select id="relationship-active">${options(
          [
            ["active", "Active"],
            ["inactive", "Inactive"],
          ],
          state.relationshipActive,
          "All states",
        )}</select><label class="sr-only" for="relationship-direction">Cross-filter direction</label><select id="relationship-direction">${options(directionValues, state.relationshipDirection, "All directions")}</select>`
      : `<label class="sr-only" for="security-kind">Security evidence type</label><select id="security-kind">${options(
          [
            ["rls", "Row-level filters"],
            ["ols", "Object-level permissions"],
            ["limited", "Unanalysed role content"],
          ],
          state.securityKind,
          "All security evidence",
        )}</select>`
  }<button class="text-button" data-action="area-reset">Reset</button></div></div><div id="collection" class="collection-scroll area-list" tabindex="-1">${modelAreaCollection(index, state)}</div><div class="collection-footer"><span>Saved definitions · read-only</span><span>↑ ↓ to explore · / to search</span></div></section><aside class="inspector" id="area-inspector" aria-label="Selected ${relationships ? "relationship" : "security role"}">${modelAreaInspector(index, state)}</aside>`;
}
export function modelAreaCollection(index, state) {
  const relationships = state.view === "relationships",
    { rows, selected } = areaSelection(index, state),
    active = rows.some((row) => row.id === selected?.id)
      ? selected.id
      : rows[0]?.id;
  return (
    rows
      .map((row) => {
        const r = row.relationship,
          role = row.role,
          counts = role && roleCounts(role);
        return `<button class="area-row ${row.id === selected?.id ? "selected" : ""}" data-area-row="${escape(row.id)}" tabindex="${row.id === active ? 0 : -1}" ${row.id === selected?.id ? 'aria-current="true"' : ""}><div><strong>${r ? `${escape(r.FromTable)}[${escape(r.FromColumn)}] <span class="endpoint-separator">—</span> ${escape(r.ToTable)}[${escape(r.ToColumn)}]` : escape(role.Name)}</strong>${r ? `<span class="badge ${r.IsActive ? "direct" : "unused"}">${r.IsActive ? "Active" : "Inactive"}</span>` : ""}</div><small>${escape(row.model.Name)}${r ? ` · ${escape(r.Name)}` : ""}</small>${r ? `<p class="relationship-summary">${escape(r.FromCardinality)} — ${escape(r.ToCardinality)} · ${escape(directionLabel(r.CrossFilteringBehavior))}</p>${!r.IsActive && r.Activation ? `<small>USERELATIONSHIP · ${escape(activationLabel(r.Activation.State))}</small>` : ""}` : `<p>${counts.filters} row filters · ${counts.permissions} explicit object permissions</p><small>${role.TablePermissions.map((p) => escape(p.Table)).join(" · ") || "No table permissions stored"}${role.UnanalyzedConstructs?.length ? " · Some role content unanalysed" : ""}</small>`}</button>`;
      })
      .join("") ||
    `<div class="empty"><h2>No matching ${relationships ? "relationships" : "roles"}</h2><p>Choose another model or clear the filters. No recorded definition is not a runtime assurance result.</p><button class="primary-button" data-action="area-reset">Clear filters</button></div>`
  );
}
const edgeEvidence = (edges) =>
  edges
    .map(
      (e) =>
        `<details class="evidence"><summary>${escape(e.ToTable)}[${escape(e.ToObjectName)}] · ${escape(words(e.DependencyKind))}</summary><code>${escape(e.EvidenceText)}<br>${escape(e.EvidencePath)}</code></details>`,
    )
    .join("") ||
  '<p class="quiet">No matching dependency records published.</p>';
const unresolvedEvidence = (rows) =>
  rows
    .map(
      (u) =>
        `<div class="area-unresolved"><strong>${escape(u.ReferenceText)}</strong><p>${escape(u.Reason)}</p><small>${escape(words(u.ResolutionOutcome))} · ${escape(u.EvidencePath)}</small></div>`,
    )
    .join("");
function relationshipSummary(index, row) {
  const r = row.relationship,
    evidence = relationshipEvidence(index, row);
  return `<section><h3>Relationship ends</h3><div class="relationship-ends">${evidence.endpoints.map((e) => `<div class="relationship-end"><span class="eyebrow">${e.side.toUpperCase()} · ${escape(e.cardinality)}</span><strong>${escape(e.table)}</strong>${e.object ? objectButton(e.object, e.column) : `<span>${escape(e.column)}</span><small>No matching model usage object</small>`}${usage(e.object)}</div>`).join('<div class="relationship-connector" aria-hidden="true">—</div>')}</div>${facts(
    [
      ["Cross-filter direction", directionLabel(r.CrossFilteringBehavior)],
      ["State", r.IsActive ? "Active" : "Inactive"],
    ],
  )}</section><section><h3>Structural evidence</h3><p class="quiet">${evidence.edges.length} recorded relationship-endpoint dependencies. Endpoint usage and relationship state are separate results.</p>${evidence.unresolved.length ? unresolvedEvidence(evidence.unresolved) : ""}</section>${!r.IsActive ? `<section><h3>USERELATIONSHIP evidence</h3><p class="area-lead">${escape(activationLabel(r.Activation?.State))}</p>${evidence.activations.map((s) => `<div class="area-reference">${s.object ? objectButton(s.object, `${s.Table}[${s.ObjectName}]`) : `<strong>${escape(s.Table)}[${escape(s.ObjectName)}]</strong>`}<small>${escape(words(s.ObjectType))} · ${s.ReachableFromReport ? "Report reach recorded" : "Not report-reachable in this saved graph"}</small></div>`).join("")}<p class="footnote">${r.Activation ? "Static evidence from bounded USERELATIONSHIP calls; this does not establish runtime activation or change the saved inactive state." : "No activation annotation is published. No absence of runtime use is inferred."}</p></section>` : ""}`;
}
function relationshipDetails(index, row) {
  const r = row.relationship,
    evidence = relationshipEvidence(index, row);
  return `<section><h3>Saved relationship definition</h3>${facts([
    ["Model", row.model.Name],
    ["Name", r.Name],
    ["Cross-filter value", r.CrossFilteringBehavior],
    ["Activation state", r.Activation?.State],
  ])}</section><section><h3>Structural dependency evidence</h3>${edgeEvidence(evidence.edges)}</section>${evidence.unresolved.length ? `<section><h3>Unresolved endpoints</h3>${unresolvedEvidence(evidence.unresolved)}</section>` : ""}<section><h3>Model confidence context</h3><p class="quiet">Relationship endpoints do not establish runtime filter propagation. Object-level confidence and original limitations remain separate evidence.</p>${targetButton({ kind: "scope", model: row.model.Name }, "Inspect model limitations")}</section>`;
}
const permissionMeaning = (value) => ({ none: "Metadata access denied (OLS)", read: "Metadata read permission" })[String(value).trim().toLowerCase()] || `Saved permission: ${value}`;
function securityPermissions(index, row) {
  const { role, model } = row;
  return `<p class="security-boundary">Saved definitions only. Effective user access and Service role membership are not tested.</p>${facts([["Model permission", role.ModelPermission === "read" ? "Read (saved role permission)" : role.ModelPermission || "Not recorded"]])}${
    role.TablePermissions.map(
      (p) =>
        `<section class="security-permission"><div class="section-heading"><h3>${escape(p.Table)}</h3>${model.Tables.some((t) => t.Name === p.Table) ? targetButton({ kind: "model", model: model.Name, table: p.Table }, "Browse table") : '<small class="quiet">Table not found in model inventory</small>'}</div><div class="security-row-filter"><h4>Row-level security (RLS)</h4>${p.FilterExpression.trim() ? `<pre class="security-expression"><code>${escape(p.FilterExpression)}</code></pre>` : '<p class="quiet">No row filter stored in this table permission.</p>'}</div>${p.MetadataPermission?.trim() ? `<div class="security-access"><span>Table metadata permission</span><strong>${escape(permissionMeaning(p.MetadataPermission))}</strong><small>Stored value: <code>${escape(p.MetadataPermission)}</code></small></div>` : ""}${
          (p.ColumnPermissions || []).length
            ? `<h4>Column permissions (OLS)</h4>${p.ColumnPermissions.map((c) => {
                const object = modelObject(
                  index.data,
                  model.Name,
                  p.Table,
                  c.Column,
                );
                return `<div class="security-access"><span>${object ? objectButton(object, c.Column) : escape(c.Column)}</span><strong>${escape(permissionMeaning(c.Permission))}</strong><small>Stored value: <code>${escape(c.Permission)}</code></small>${!object ? "<small>Definition only · no matching model usage object</small>" : ""}</div>`;
              }).join("")}`
            : ""
        }</section>`,
    ).join("") ||
    '<p class="quiet">No table permissions stored in this role.</p>'
  }${role.UnanalyzedConstructs?.length ? '<p class="review-qualification">Some content in this role definition was not analysed. Inspect Evidence for the recorded boundaries.</p>' : ""}`;
}
function securityObjects(index, row) {
  const evidence = securityEvidence(index, row);
  return `<section><h3>Recorded security dependencies</h3><p class="quiet">Objects explicitly referenced by the role’s saved definitions. These edges do not describe effective access or every object affected at runtime.</p>${
    evidence.edges
      .map((e) => {
        const object = index.data.byId.get(edgeId(e, "To"));
        return `<div class="area-reference">${object ? objectButton(object, `${e.ToTable}[${e.ToObjectName}]`) : targetButton({ kind: "model", model: row.model.Name, table: e.ToTable }, e.ToTable)}<small>${escape(words(e.ToObjectType))} · ${e.DependencyKind === "TablePermission" ? "Row filter reference" : "Explicit object-level permission"}</small>${usage(object)}</div>`;
      })
      .join("") ||
    '<p class="quiet">No matching security dependency edges recorded.</p>'
  }</section>${evidence.unresolved.length ? `<section><h3>Unresolved references</h3>${unresolvedEvidence(evidence.unresolved)}</section>` : ""}`;
}
function securityDetails(index, row) {
  const evidence = securityEvidence(index, row),
    role = row.role;
  return `<section><h3>Role definition evidence</h3>${facts([
    ["Model", row.model.Name],
    ["Role", role.Name],
    ["Source", role.RelativePath],
    [
      "Dependency content in this role file",
      role.DependencyContentFullyAccountedFor === true
        ? "Accounted for in the saved file"
        : role.DependencyContentFullyAccountedFor === false
          ? "Not fully accounted for"
          : "Not published",
    ],
  ])}<p class="quiet">Dependency accounting describes reference discovery in this file, not complete role support or user-access verification.</p>${role.UnanalyzedConstructs?.length ? `<h4>Unanalysed constructs</h4><ul>${role.UnanalyzedConstructs.map((c) => `<li>${escape(c)}</li>`).join("")}</ul>` : ""}</section><section><h3>Security dependency evidence</h3>${edgeEvidence(evidence.edges)}</section>${evidence.unresolved.length ? `<section><h3>Unresolved references</h3>${unresolvedEvidence(evidence.unresolved)}</section>` : ""}<section><h3>Role/file limitations</h3>${evidence.limitations.map((l) => `<p class="quiet">${escape(l.Reason)}</p>`).join("") || '<p class="quiet">No limitation record matched to this role file. Runtime user access remains outside the scan.</p>'}${targetButton({ kind: "scope", model: row.model.Name }, "Inspect model limitations")}</section>`;
}
export function modelAreaInspector(index, state) {
  const relationships = state.view === "relationships",
    prefix = relationships ? "relationship" : "security",
    { rows, selected: row } = areaSelection(index, state),
    tab = state[prefix + "Tab"];
  if (!row)
    return `<div class="empty"><h2>Select a ${relationships ? "relationship" : "role"}</h2><p>Saved definitions and supporting evidence appear here.</p></div>`;
  const name = row.relationship?.Name || row.role.Name,
    outside = !rows.some((r) => r.id === row.id);
  return `<div class="inspector-toolbar"><span>${relationships ? "RELATIONSHIP" : "SECURITY ROLE"} INSPECTOR</span></div><div class="object-hero"><div class="object-breadcrumb">${escape(row.model.Name)}</div><h2 id="area-heading" tabindex="-1">${relationships ? `${escape(row.relationship.FromTable)}[${escape(row.relationship.FromColumn)}] <span class="endpoint-separator">—</span> ${escape(row.relationship.ToTable)}[${escape(row.relationship.ToColumn)}]` : escape(name)}</h2>${relationships ? `<p class="definition-id">${escape(name)}</p>` : ""}${relationships ? `<div class="hero-badges"><span class="badge ${row.relationship.IsActive ? "direct" : "unused"}">${row.relationship.IsActive ? "Active" : "Inactive"}</span></div>` : ""}</div>${outside ? `<div class="outside-filter">Selected ${relationships ? "relationship" : "role"} is outside your filters.<button data-action="area-reveal">Show in collection</button></div>` : ""}<div class="inspector-tabs" role="tablist" aria-label="${relationships ? "Relationship" : "Security"} information">${AREA_TABS[state.view].map((t) => `<button role="tab" id="area-tab-${t}" data-area-tab="${t}" aria-controls="area-panel" aria-selected="${tab === t}" tabindex="${tab === t ? 0 : -1}">${t}</button>`).join("")}</div><div class="inspector-content area-detail" id="area-panel" role="tabpanel" aria-labelledby="area-tab-${tab}" tabindex="0">${relationships ? (tab === "Summary" ? relationshipSummary(index, row) : relationshipDetails(index, row)) : tab === "Permissions" ? securityPermissions(index, row) : tab === "Objects" ? securityObjects(index, row) : securityDetails(index, row)}</div><div class="inspector-footer"><span>Saved project definitions</span><span>Read-only</span></div>`;
}
