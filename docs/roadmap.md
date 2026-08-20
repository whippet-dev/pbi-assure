# Product roadmap

PBI Assure is being developed in vertical slices that produce reviewable value while protecting the accuracy of unused-object classifications. Priorities may change after each real-report review.

## Next product priorities

1. **Small, evidence-backed integrity checks.** Prioritise an explicitly referenced custom-theme resource
   that is missing or malformed. The explicit landing-page target check is complete; do not add an
   active-page check without evidence that its saved authoring state has a meaningful consequence.
2. **PBIR schema compatibility guard.** The scanner retains encountered schema URIs but does not yet
   compare them with an explicit support policy. Define and fixture that policy before producing a
   compatibility finding.
3. **Measured connector expansion.** First identify unrecognised connector calls in real,
   redistribution-safe examples. Expand the connector inventory only for concrete observed families and
   location types rather than treating “broader coverage” as an open-ended feature.

Cross-artifact and external-consumer evidence remains strategically useful but is not an actionable next
slice while those consumers are absent from the selected PBIP input. Revisit it when a concrete optional
input and privacy-safe identity/join contract can be specified.

## Completed core-analysis slices

- Row-level security review: conditional HTML inventory of roles, model permissions and table-filter DAX,
  with an explicit project-only security boundary and no new findings or classification changes.
- Calculation groups and field parameters: first-class inventory, conservative graph traversal, friendly HTML summaries, and explicit evidence for objects reached through these features.
- Report extensions and report-level measures: first-class inventory, visual-reference resolution, structured measure dependency traversal, and developer-friendly HTML summaries.
- Report-to-model binding: resolve explicit local `byPath` targets, support several reports sharing one model, and distinguish remote or missing model definitions without generating cascades of false field errors.
- Power Query M lineage: retain M partition expressions, inventory named expressions, follow static query dependencies transitively, flag unused supporting queries, and identify dynamic expressions requiring review.
- Power Query column lineage: record explicit merge, expand, selection, rename, removal, and type-transform operations as diagnostic evidence without changing semantic usage states.
- Data-source inventory: recognise common M connector families, classify location types without retaining connector arguments, show query associations, and flag non-portable local or network file dependencies.
- Generated-object and relationship review: distinguish annotated Auto Date/Time tables without removing them from analysis; inventory relationship endpoints, cardinality, status and filter direction; and flag bidirectional or many-to-many configurations for review.
- Review outputs and local workflow: accessible searchable HTML, focused semantic-usage CSV exports, timestamped output history, and a lightweight Windows app that opens the latest HTML, CSV, or output folder.

## Deferred: bookmark-captured semantic state

Bookmarks can retain filters, projections, sort state, and alternative visual configurations that reference semantic objects absent from the current `visual.json`. Until this state is analysed, an object used only by a bookmark can be classified as apparently unused.

This is a known accuracy boundary, not a deletion recommendation. It is deferred because bookmark definitions also contain large duplicated and potentially stale snapshots; treating every captured reference as current usage without careful reconciliation would create a different class of misleading result.

Revisit this work when either:

- real-report review shows bookmark-only fields materially affecting unused-object results;
- the primary HTML/CSV review workflow reveals a material bookmark-only usage gap; or
- a supported PBIR schema change makes bookmark-state interpretation more reliable.

## Standing boundaries

- Apparently unused always means no usage found in the analysed scope.
- PBI Assure never removes model or report objects automatically.
- Automated accessibility findings support, but do not replace, manual testing.
- Real or sensitive data and report content must not be committed to this repository.
