// Review presentation only: membership is the existing usage state plus the Core system-table flag.
import { escape, icon } from "./ui.js";
import { words } from "./data.js";

const same = (a, b) => String(a ?? "").toUpperCase() === String(b ?? "").toUpperCase();

export function unusedReviewIntro(data) {
  const branchCount = data.objects.filter(o => !o.system && o.UsageState === "UsedOnlyByUnusedBranch").length;
  return `<div class="unused-review-intro"><p>Review candidates, never permission to delete. System-generated objects are excluded.</p><div><button class="text-button" data-action="unused-branches">Used only by unused branch · ${branchCount} in project ${icon("arrow")}</button><span>Separate usage state</span></div></div>`;
}

export function preparationFor(data, index, object) {
  const operations = object.ObjectType === "Column"
    ? (data.raw.PowerQueryColumnUsages || []).filter(c => same(c.SemanticModel, object.SemanticModel) &&
      same(c.SourceTable, object.Table) && same(c.SourceColumn, object.ObjectName)) : [];
  const queries = index.queries.filter(q => same(q.SemanticModel, object.SemanticModel) &&
    ((q.SourceKind === "TablePartition" && same(q.Table, object.Table)) ||
      operations.some(c => same(c.ConsumerQuery, q.QueryName) || same(c.SourceQuery, q.QueryName))));
  return { operations, queries };
}

export function objectReviewContext(data, index, object) {
  // The live application supplies the same Core-derived summaries used by Data Catalogue.
  // Older/imported JSON may lack them; never infer UserFacing from a usage state.
  const summaries = Array.isArray(data.raw.ViewerObjectSummaries) ? data.raw.ViewerObjectSummaries : [];
  const summary = summaries.find(s => s && same(s.SemanticModel, object.SemanticModel) &&
    same(s.Table, object.Table) && same(s.ObjectName, object.ObjectName) && s.ObjectType === object.ObjectType);
  const facing = ["Yes", "No", "Unclear"].includes(summary?.UserFacing) ? summary.UserFacing : null;
  const { operations, queries } = preparationFor(data, index, object);
  return `<section class="object-review-context"><div class="section-heading"><h3>Investigation context</h3><button class="text-button" data-action="object-lineage">Explore lineage ${icon("arrow")}</button></div>
    <dl class="metadata"><dt>UserFacing</dt><dd>${facing || "Not included in this snapshot"}</dd></dl>
    <p class="quiet">${facing === "No" ? "No qualifying direct presentation or interaction evidence found; not proof that users cannot encounter this object." : facing === "Yes" ? "Qualifying direct presentation or interaction evidence recorded." : facing === "Unclear" ? "Retained direct evidence could not be classified confidently." : "This snapshot does not publish a UserFacing result for this object."} Separate from usage state and confidence.</p>
    <details class="object-preparation"><summary>Power Query context${operations.length ? ` · ${operations.length} recorded column operations` : ""}</summary>
      <p class="quiet">Preparation evidence does not change report or semantic-model usage. Table queries provide context; they do not establish use of every column or measure.</p>
      ${operations.map(c => `<div class="preparation-operation"><strong>${escape(words(c.UsageKind))}</strong><span>${escape(c.ConsumerQuery)}</span><details class="evidence"><summary>Technical evidence</summary><code>${escape(c.MFunction)}${c.StepName ? ` · ${escape(c.StepName)}` : ""}<br>${escape(c.ArtifactPath)}<br>${escape(c.EvidencePath || "")}</code></details></div>`).join("") || '<p class="quiet">No matching column-operation evidence recorded. This does not establish that preparation use is absent.</p>'}
      ${queries.length ? `<div class="query-relations">${queries.map(q => `<div class="query-relation"><button class="related-object" data-object-query="${escape(q.id)}">${escape(q.QueryName)} ${icon("arrow")}</button><small>${q.SourceKind === "TablePartition" ? "Table partition" : "Named expression"}${q.Partition ? ` · ${escape(q.Partition)}` : ""}</small></div>`).join("")}</div>` : '<p class="quiet">No matching query available in this snapshot.</p>'}
    </details></section>`;
}
