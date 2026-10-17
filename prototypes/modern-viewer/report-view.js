import { escape, icon, glyph } from "./ui.js";
import { words } from "./data.js";
import {
  reportContext,
  pageId,
  pagesFor,
  bindingLabel,
  visualLabel,
  visualType,
  visualUses,
  scopedFindings,
  reportLimitations,
  groupPath,
  usedAs,
} from "./report-data.js";

export const VISUAL_TABS = ["Uses", "Behaviour", "Reviews", "Evidence"];
export function reportSelection(data, state) {
  const { report, page } = reportContext(data, state);
  state.report = report?.RelativePath ?? null;
  state.page = page ? pageId(report, page) : null;
  state.reportVisuals ||= {};
  const visual =
    page &&
    (page.Visuals.find((v) => v.Name === state.reportVisuals[state.page]) ||
      [...page.Visuals].sort(
        (a, b) =>
          visualUses(data, report, page, b).confirmed.length -
          visualUses(data, report, page, a).confirmed.length,
      )[0]);
  if (visual) state.reportVisuals[state.page] = visual.Name;
  return { report, page, visual };
}
export function reportNavigation(data, state) {
  const { report, page } = reportSelection(data, state);
  return `<div class="report-navigation"><label class="project-label" for="report-select">REPORT</label><select id="report-select" aria-label="Report">${data.raw.Reports.map((r) => `<option value="${escape(r.RelativePath)}" ${r === report ? "selected" : ""}>${escape(r.Name)}</option>`).join("")}</select>
    ${
      report
        ? `<p class="source-label report-binding">${escape(bindingLabel(data, report))}</p><div class="table-heading">Pages <span>${report.Pages.length}</span></div><nav class="table-nav report-page-nav" aria-label="Report pages">${
            pagesFor(report)
              .map(
                (p) =>
                  `<button data-page="${escape(pageId(report, p))}" class="${p === page ? "chosen" : ""}" ${p === page ? 'aria-current="page"' : ""} title="${escape(p.DisplayName || p.Name)}">${icon("pages")}<span class="report-page-label">${escape(p.DisplayName || p.Name)}${p.Visibility === "HiddenInViewMode" || p.PageType || p.PageBinding?.Type ? `<small>${[p.Visibility === "HiddenInViewMode" ? "Hidden page" : "", p.PageType, p.PageBinding?.Type].filter(Boolean).map(escape).join(" · ")}</small>` : ""}</span><span>${p.Visuals.length}</span></button>`,
              )
              .join("") || '<p class="quiet">No saved pages.</p>'
          }</nav>`
        : '<p class="quiet">No reports in this inventory.</p>'
    }</div>`;
}
export function reportWorkspace(data, state) {
  const { report, page } = reportSelection(data, state);
  if (!report || !page)
    return '<section class="collection-pane"><div id="collection" class="empty" tabindex="-1"><h2>No saved pages</h2><p>Select another report or open a different analysis.</p></div></section>';
  const pageFindings = scopedFindings(data, report, page),
    reportFindings = scopedFindings(data, report, null);
  return `<section class="collection-pane report-collection" aria-label="Selected page visuals"><div class="report-page-heading"><div class="object-breadcrumb">${escape(report.Name)} <span>/</span> PAGE ${page.Order == null ? "ORDER NOT SAVED" : page.Order + 1}</div><div class="section-heading"><h2>${escape(page.DisplayName || page.Name)}</h2><button class="text-button" data-action="page-details" aria-pressed="${state.reportPageDetails}">Page details ${icon("arrow")}</button></div><div class="page-facts"><span>${page.Visuals.length} visuals</span>${page.Width != null && page.Height != null ? `<span>${escape(page.Width)} × ${escape(page.Height)}</span>` : ""}${page.Visibility === "HiddenInViewMode" ? "<span>Hidden page</span>" : ""}${page.PageType ? `<span>${escape(page.PageType)}</span>` : ""}${page.PageBinding?.Type ? `<span>${escape(page.PageBinding.Type)}</span>` : ""}</div>${pageFindings.length || reportFindings.length ? `<button class="page-review-cue" data-action="page-details">${pageFindings.length} page-level · ${reportFindings.length} report-level findings/reviews ${icon("arrow")}</button>` : ""}<p class="source-label">Saved metadata · visuals are not rendered here</p></div>
    <div class="visual-search"><div class="searchbox">${icon("search")}<input type="search" id="visual-search" aria-label="Search visuals on this page" placeholder="Search this page’s visuals…" value="${escape(state.visualSearch)}"><kbd>/</kbd></div></div><div id="collection" class="collection-scroll visual-list" tabindex="-1" data-page-context="${escape(state.page)}"></div><div class="collection-footer"><span id="visual-result-count"></span><span>↑ ↓ to explore · Enter to inspect</span></div></section><aside id="visual-inspector" class="inspector" aria-label="Selected visual"></aside>`;
}
export function visibleVisuals(data, state) {
  const { page } = reportContext(data, state),
    query = state.visualSearch.toLowerCase().trim();
  return (page?.Visuals || []).filter(
    (v) =>
      !query ||
      `${visualLabel(v)} ${v.Name} ${visualType(v.VisualType)}`
        .toLowerCase()
        .includes(query),
  );
}
export function visualCollection(data, state) {
  const { report, page, visual } = reportSelection(data, state),
    visuals = visibleVisuals(data, state);
  const active = visuals.includes(visual) ? visual : visuals[0];
  if (!visuals.length)
    return `<div class="empty"><h2>${page.Visuals.length ? "No matching visuals" : "No visuals on this saved page"}</h2><p>${page.Visuals.length ? "Try a different name or visual type. The selected visual remains in the inspector." : "Page configuration, scoped references and findings are still available."}</p><button class="primary-button" data-action="${page.Visuals.length ? "clear-visual-search" : "page-details"}">${page.Visuals.length ? "Clear visual search" : "Inspect page details"}</button></div>`;
  return `<table class="objects-table visuals-table"><thead><tr><th scope="col">Visual</th><th scope="col" class="numeric">Objects used</th><th scope="col" class="numeric">Reviews</th></tr></thead><tbody>${visuals
    .map((v) => {
      const uses = visualUses(data, report, page, v),
        findings = scopedFindings(data, report, page, v);
      return `<tr class="${v === visual && !state.reportPageDetails ? "selected" : ""}"><td><button class="object-select visual-select" data-visual="${escape(v.Name)}" tabindex="${v === active ? "0" : "-1"}" ${v === visual && !state.reportPageDetails ? 'aria-current="true"' : ""}>${icon("pages")}<span><strong>${escape(visualLabel(v))}</strong><small>${escape(visualType(v.VisualType))}${v.IsHidden ? " · Explicitly hidden" : ""}${v.ParentGroupName ? " · Grouped" : ""}</small><small class="visual-preview">${
        uses.confirmed.length
          ? escape(
              uses.confirmed
                .slice(0, 2)
                .map((u) => u.name)
                .join(" · "),
            ) +
            (uses.confirmed.length > 2 ? ` +${uses.confirmed.length - 2}` : "")
          : v.Actions?.length
            ? `${v.Actions.length} saved actions`
            : v.TooltipBindings?.length
              ? `${v.TooltipBindings.length} tooltip bindings`
              : "No confirmed model usage"
      }</small></span></button></td><td class="numeric">${uses.confirmed.length}${uses.other.length ? `<small>${uses.other.length} other refs</small>` : ""}</td><td class="numeric">${findings.length ? `<button class="text-button" data-visual-review="${escape(v.Name)}" aria-label="Inspect ${findings.length} findings and reviews for ${escape(visualLabel(v))}">${findings.length}</button>` : '<span class="muted">—</span>'}</td></tr>`;
    })
    .join("")}</tbody></table>`;
}
const meta = (rows) =>
  `<dl class="metadata">${rows.map(([k, v]) => `<dt>${escape(k)}</dt><dd>${v}</dd>`).join("")}</dl>`;
const evidence = (label, text, path) =>
  `<details class="evidence"><summary>${escape(label)}</summary>${text ? `<p>${escape(text)}</p>` : ""}<code>${escape(path || "Evidence path not published")}</code></details>`;
const setting = (value) =>
  value === true
    ? "Enabled in saved metadata"
    : value === false
      ? "Disabled in saved metadata"
      : "Not explicitly specified";
function useRows(rows) {
  if (!rows.length) return "";
  return `<div class="visual-use-list">${rows.map((row) => `<article class="visual-use-row"><div class="visual-use-identity">${glyph(row.type)}<div>${row.object ? `<button class="related-object" data-page-object="${escape(row.object.id)}">${escape(row.name)} ${icon("arrow")}</button>` : `<strong>${escape(row.name)}</strong>`}<small>${escape(row.table)} · ${row.type === "ReportMeasure" ? "Report measure" : escape(words(row.type))}${row.hierarchy ? ` · ${escape(row.hierarchy)}` : ""}</small></div></div><div class="used-as"><span>${row.status === "Scanner-confirmed direct usage" ? "Used as" : "Recorded as"}</span><strong>${row.roles.map(escape).join(" · ")}</strong></div>${row.status === "Scanner-confirmed direct usage" ? "" : `<p class="reference-status">${escape(row.status)}</p>`}${row.reportMeasure?.Expression ? `<details class="evidence"><summary>Report measure DAX</summary><pre class="dax">${escape(row.reportMeasure.Expression)}</pre><code>${escape(row.reportMeasure.RelativePath)}</code></details>` : ""}<details class="evidence usage-evidence"><summary>Reference evidence <span>${row.references.length}</span></summary>${row.references.map((ref) => `<code>${escape(ref.UsageContext)}${ref.Role ? " · " + escape(ref.Role) : ""}<br>${escape(ref.ArtifactPath || "")}<br>${escape(ref.EvidencePath)}</code>`).join("")}</details></article>`).join("")}</div>`;
}
export function findingList(findings, empty) {
  if (!findings.length) return `<p class="quiet">${escape(empty)}</p>`;
  return findings
    .map(
      (f) =>
        `<article class="finding"><div class="finding-heading"><span class="badge ${f.Severity === "Error" ? "severity-error" : f.Severity === "Warning" ? "severity-warning" : "severity-info"}">${escape(f.Severity)}</span><strong>${f.AssessmentType === "ReviewRequired" ? "Review required" : "Finding"}</strong><span>${escape(words(f.Category))}</span></div><p>${escape(f.Message)}</p><p class="finding-recommendation">${escape(f.Recommendation)}</p>${evidence(f.RuleId + " · v" + f.RuleVersion + (f.VisualGroup ? " · Group: " + f.VisualGroup : ""), null, [f.ArtifactPath, ...(f.EvidencePaths || [])].join("\n"))}</article>`,
    )
    .join("");
}
function limitationList(data, report) {
  const limits = reportLimitations(data, report);
  return (
    limits
      .map((l) =>
        evidence(
          words(l.ConstructType),
          l.Reason,
          l.ArtifactPath + "\n" + l.EvidencePath,
        ),
      )
      .join("") ||
    '<p class="quiet">No report-level limitations recorded by this scan. Static analysis boundaries still apply.</p>'
  );
}
function usesPanel(data, report, page, visual) {
  const uses = visualUses(data, report, page, visual);
  return `<div class="panel-intro"><h3>Objects used by this visual <span>${uses.confirmed.length}</span></h3><p>References found in the analysed report metadata. Open an object to inspect its model usage.</p></div>${useRows(uses.confirmed) || '<p class="quiet">No recorded direct model usage was published for this visual. This is not proof that it uses no data.</p>'}${uses.other.length ? `<section class="other-references"><h3>Other saved references <span>${uses.other.length}</span></h3><p class="quiet">Report-measure bindings, unmatched fields and metadata references without confirmed direct usage. Saved selectors can outlive active use; this analysis does not establish whether every saved reference is active.</p>${useRows(uses.other)}</section>` : ""}<p class="footnote">Page and report filters belong to their own scopes; inspect Page details to see them. Some saved fields may be hidden from the rendered visual; that detail is not included in this analysis.</p>`;
}
function behaviourPanel(report, page, visual) {
  const interactions = (page.VisualInteractions || []).filter(
    (i) => i.SourceVisual === visual.Name || i.TargetVisual === visual.Name,
  );
  const a = visual.Accessibility || {},
    position = visual.Position || {},
    groups = groupPath(page, visual);
  return `<div class="panel-intro"><h3>Saved behaviour & configuration</h3><p>Configuration evidence only. Actions, navigation, URLs and tooltips are never executed here.</p></div><section><h3>Actions <span>${visual.Actions?.length || 0}</span></h3>${
    (visual.Actions || [])
      .map((action) => {
        const target = report.Pages.find((p) => p.Name === action.PageTarget),
          bookmark = (report.Bookmarks || []).find(
            (b) => b.Name === action.BookmarkTarget,
          );
        return `<div class="configuration-item"><div class="section-heading"><strong>${escape(words(action.ActionType || "Action type not specified"))}</strong><span>${escape(setting(action.IsEnabled))}</span></div>${meta([["Target", escape(action.HasDynamicConfiguration ? "Dynamic configuration · not resolved" : target?.DisplayName || bookmark?.DisplayName || action.PageTarget || action.BookmarkTarget || action.WebUrl || (action.ActionType === "Back" ? "Previous report location (runtime)" : "Not specified"))]])}${action.HasDynamicConfiguration ? '<p class="quiet">The target cannot be fully reconciled from static metadata.</p>' : ""}${evidence("Action evidence", null, action.EvidencePath)}</div>`;
      })
      .join("") || '<p class="quiet">No actions recorded for this visual.</p>'
  }</section><section><h3>Tooltip bindings <span>${visual.TooltipBindings?.length || 0}</span></h3>${
    (visual.TooltipBindings || [])
      .map(
        (t) =>
          `<div class="configuration-item">${meta([
            ["Binding", escape(words(t.BindingKind))],
            ["Enabled", escape(setting(t.IsEnabled))],
            ["Type", escape(t.TooltipType || "Not specified")],
            [
              "Target",
              escape(
                t.HasDynamicConfiguration
                  ? "Dynamic configuration · not resolved"
                  : report.Pages.find((p) => p.Name === t.TargetPage)
                      ?.DisplayName ||
                      t.TargetPage ||
                      "No target recorded",
              ),
            ],
          ])}${evidence("Tooltip evidence", null, t.EvidencePath)}</div>`,
      )
      .join("") ||
    '<p class="quiet">No tooltip bindings recorded. This does not establish runtime tooltip behaviour.</p>'
  }</section><section><h3>Visual interactions <span>${interactions.length}</span></h3>${interactions.map((i) => `<div class="configuration-item"><p>${escape(words(i.InteractionType || "Type not specified"))} · ${i.SourceVisual === visual.Name ? "This visual → " : ""}${i.SourceVisual === visual.Name ? interactionTarget(page, i.TargetVisual) : interactionTarget(page, i.SourceVisual) + " → this visual"}</p>${evidence("Interaction evidence", null, i.EvidencePath)}</div>`).join("") || '<p class="quiet">No explicit interactions involving this visual recorded.</p>'}</section><section><h3>Display & position</h3>${meta(
    [
      [
        "Own visibility",
        visual.IsHidden ? "Explicitly hidden" : "Not explicitly hidden",
      ],
      ["Group path", escape(groups.join(" / ") || "Page root")],
      ["Title visible", escape(setting(a.TitleIsVisible))],
      [
        "Title text",
        escape(
          a.TitleTextIsDynamic
            ? "Dynamic expression; displayed value not known"
            : a.TitleText || "No static title text recorded",
        ),
      ],
      [
        "On-canvas text",
        escape(
          visual.OnCanvasTextIsDynamic
            ? "Dynamic text; displayed value not known"
            : visual.OnCanvasText || "Not recorded",
        ),
      ],
      [
        "Position",
        `X ${escape(position.X ?? "—")} · Y ${escape(position.Y ?? "—")} · Z ${escape(position.Z ?? "—")}`,
      ],
      [
        "Size",
        `${escape(position.Width ?? "—")} × ${escape(position.Height ?? "—")}`,
      ],
    ],
  )}${groups.length ? '<p class="quiet">Coordinates are group-local where grouped. Group visibility is not published in this JSON; own visibility is not effective runtime visibility.</p>' : ""}</section><section><h3>Saved formatting</h3>${
    (visual.PersistedFormatting || [])
      .filter((f) => f.Classification !== "NoPersistedValue")
      .map(
        (f) =>
          `<div class="configuration-item"><div class="section-heading"><strong>${escape(f.PropertyLabel)}</strong><span>${escape(words(f.Classification))}</span></div><p>${escape(f.NormalizedValue || f.ExpressionSource || "No normalized value available")}</p>${f.SelectorScope ? `<p class="quiet">${escape(f.SelectorScope)} · ${escape(f.SelectorRelevance || "Relevance not published")}</p>` : ""}${f.IsAmbiguous ? '<p class="quiet">Ambiguous saved configuration.</p>' : ""}${evidence("Formatting evidence", null, f.EvidencePath)}</div>`,
      )
      .join("") ||
    '<p class="quiet">No saved values recorded in the inspected formatting properties. Theme and runtime values may still apply.</p>'
  }<p class="quiet">Saved values do not establish editing history or final rendered appearance.</p></section>`;
}
function interactionTarget(page, name) {
  const target = page.Visuals.find((v) => v.Name === name);
  return target
    ? `<button class="related-object" data-visual="${escape(target.Name)}">${escape(visualLabel(target))}</button>`
    : escape(name || "Endpoint not specified") + " (not found on this page)";
}
function reviewsPanel(data, report, page, visual) {
  const a = visual.Accessibility || {},
    tab = visual.Position?.TabOrder,
    findings = scopedFindings(data, report, page, visual);
  const pageFindings = scopedFindings(data, report, page);
  return `<div class="panel-intro"><h3>Accessibility & reviews</h3><p>Saved settings and scanner observations. This is not an accessibility certification or a runtime keyboard test.</p></div><section><h3>Accessibility metadata</h3>${meta(
    [
      [
        "Alt text",
        escape(
          a.AltTextIsDynamic
            ? "Dynamic expression; displayed value not known"
            : a.HasAltText
              ? a.AltText || "Configured; text not published"
              : "No configured alt text recorded",
        ),
      ],
      [
        "Tab order",
        tab == null
          ? "Not explicitly saved"
          : tab < 0
            ? `Explicitly excluded (${escape(tab)})`
            : `Saved rank ${escape(tab)}${visual.ParentGroupName ? " within parent group" : " at page root"}`,
      ],
      ["Title visible", escape(setting(a.TitleIsVisible))],
      [
        "Own visibility",
        visual.IsHidden ? "Explicitly hidden" : "Not explicitly hidden",
      ],
    ],
  )}${visual.ParentGroupName ? '<p class="quiet">Group visibility is not published by JSON. Do not infer effective visibility or accessibility from this visual’s own settings alone.</p>' : ""}</section><section><h3>Visual findings & reviews <span>${findings.length}</span></h3>${findingList(findings, "No findings or reviews recorded specifically against this visual. This is not a pass result.")}</section>${pageFindings.length ? `<section><h3>Page-level context <span>${pageFindings.length}</span></h3><p class="quiet">These observations apply to the page, not specifically to this visual.</p>${findingList(pageFindings, "")}</section>` : ""}`;
}
export function visualInspector(data, state) {
  const { report, page, visual } = reportSelection(data, state);
  if (state.reportPageDetails || !visual)
    return pageInspector(data, report, page);
  const findings = scopedFindings(data, report, page, visual),
    uses = visualUses(data, report, page, visual),
    tab = state.visualTab;
  return `<div class="inspector-toolbar"><span>VISUAL INSPECTOR</span><button class="text-button" data-action="page-details">Page details</button></div><div class="object-hero"><div class="object-breadcrumb">${escape(report.Name)} <span>/</span> ${escape(page.DisplayName || page.Name)}</div><h2 id="visual-heading" tabindex="-1">${escape(visualLabel(visual))}</h2><div class="object-meta visual-meta"><span>${escape(visualType(visual.VisualType))}</span>${visual.IsHidden ? "<span>Explicitly hidden</span>" : ""}${visual.ParentGroupName ? "<span>Grouped</span>" : ""}</div><code class="visual-id">${escape(visual.Name)}</code></div>
    ${!visibleVisuals(data, state).includes(visual) ? '<div class="outside-filter">The selected visual is outside your search.<button data-action="clear-visual-search">Show in collection</button></div>' : ""}<div class="inspector-tabs" role="tablist" aria-label="Visual information">${VISUAL_TABS.map((t) => `<button role="tab" id="visual-tab-${t}" data-visual-tab="${t}" aria-controls="visual-panel" aria-selected="${tab === t}" tabindex="${tab === t ? "0" : "-1"}">${t}${t === "Uses" ? `<span>${uses.confirmed.length}</span>` : t === "Reviews" && findings.length ? `<span>${findings.length}</span>` : ""}</button>`).join("")}</div><div class="inspector-content visual-detail" id="visual-panel" role="tabpanel" aria-labelledby="visual-tab-${tab}" tabindex="0">${
      tab === "Uses"
        ? usesPanel(data, report, page, visual)
        : tab === "Behaviour"
          ? behaviourPanel(report, page, visual)
          : tab === "Reviews"
            ? reviewsPanel(data, report, page, visual)
            : `<div class="panel-intro"><h3>Visual evidence & scope</h3><p>The saved definition and report-level analysis boundaries behind this inspector.</p></div><section><h3>Definition</h3>${meta(
                [
                  ["Source", `<code>${escape(visual.RelativePath)}</code>`],
                  ["Visual type", escape(visual.VisualType || "Not published")],
                  ["Model binding", escape(bindingLabel(data, report))],
                ],
              )}</section><section><h3>Report limitations</h3>${limitationList(data, report)}</section><p class="footnote">JSON omits reference relevance/hidden projection flags and group visibility. Confirmed use comes only from Core’s direct usage evidence. Report measures retain their owner’s saved definition without an invented model usage state. Saved settings do not prove runtime behaviour.</p>`
    }</div><div class="inspector-footer">${icon("check")}Saved metadata evidence <span>Read-only</span></div>`;
}
function scopedReferences(data, report, references, scope) {
  if (!references?.length)
    return '<p class="quiet">No references recorded at this scope.</p>';
  const unique = [
    ...new Map(
      references.map((r) => [
        JSON.stringify([
          r.Table,
          r.ObjectName,
          r.ObjectType,
          r.HierarchyName,
          usedAs(r, scope),
        ]),
        r,
      ]),
    ).values(),
  ];
  return unique
    .map(
      (r) =>
        `<div class="scoped-reference">${glyph(r.ObjectType)}<div><strong>${escape(r.ObjectName)}</strong><small>${escape(r.Table)} · ${escape(words(r.ObjectType))} · ${escape(usedAs(r, scope))}</small>${evidence("Reference evidence", null, r.EvidencePath)}</div></div>`,
    )
    .join("");
}
function pageInspector(data, report, page) {
  if (!page) return '<div class="empty"><h2>No page selected</h2></div>';
  const pageFindings = data.raw.Findings.filter(
    (f) =>
      (f.Report === report.Name || f.Report === report.RelativePath) &&
      f.Page === page.Name &&
      !f.Visual,
  );
  return `<div class="inspector-toolbar"><span>PAGE CONTEXT</span><button class="text-button" data-action="resume-visual" ${!page.Visuals.length ? "disabled" : ""}>Back to selected visual ${icon("arrow")}</button></div><div class="object-hero"><div class="eyebrow">${escape(report.Name)}</div><h2 id="page-heading" tabindex="-1">${escape(page.DisplayName || page.Name)}</h2><p>${page.Visuals.length} saved visuals · page configuration</p></div><div class="inspector-content visual-detail"><section><h3>Page configuration</h3>${meta(
    [
      ["Page type", escape(page.PageType || "No special type recorded")],
      [
        "Visibility",
        escape(page.Visibility || "No explicit visibility setting recorded"),
      ],
      ["Binding", escape(page.PageBinding?.Type || "No page binding recorded")],
      [
        "Filter context",
        escape(setting(page.PageBinding?.AcceptsFilterContext)),
      ],
      ["Model", escape(bindingLabel(data, report))],
      ["Source", `<code>${escape(page.DefinitionPath)}</code>`],
    ],
  )}${page.PageBinding?.Parameters?.length ? `<h3>Drillthrough parameters</h3>${page.PageBinding.Parameters.map((p) => `<p class="quiet">${escape(p.Name)} · bound filter: ${escape(p.BoundFilter || "not recorded")}</p>`).join("")}` : ""}</section><section><h3>Page-level references</h3>${scopedReferences(data, report, page.FieldReferences, "page")}</section><section><h3>Report-level references</h3><p class="quiet">These are not bindings of the selected visual.</p>${scopedReferences(data, report, report.FieldReferences, "report")}</section><section><h3>Page findings & reviews <span>${pageFindings.length}</span></h3>${findingList(pageFindings, "No page-level findings or reviews recorded. This does not establish a pass result.")}</section><section><h3>Report findings & reviews</h3>${findingList(scopedFindings(data, report, null), "No report-level findings or reviews recorded.")}</section><section><h3>Report limitations</h3>${limitationList(data, report)}</section><p class="footnote">Visual findings are shown against each visual. Group findings remain page context. The viewer does not reproduce Power BI rendering or execute navigation.</p></div>`;
}
