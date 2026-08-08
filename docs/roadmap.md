# Product roadmap

PBI Assure is being developed in vertical slices that produce reviewable value while protecting the accuracy of unused-object classifications. Priorities may change after each real-report review.

## Current priority: usable assurance results

1. Self-contained, accessible HTML report generated from the normalized inventory.
2. Search and filtering for findings and semantic usage states.
3. CSV exports for analysts who need to sort, annotate, or combine results.
4. A stable operator workflow for selecting a PBIP project and an output location.

## Next core-analysis priorities

1. Report-level measures and report extensions.
2. Power Query M lineage and data-source inventory.
3. Cross-artifact and external-consumer evidence where it can be obtained safely.

## Completed core-analysis slices

- Calculation groups and field parameters: first-class inventory, conservative graph traversal, friendly HTML summaries, and explicit evidence for objects reached through these features.

## Deferred: bookmark-captured semantic state

Bookmarks can retain filters, projections, sort state, and alternative visual configurations that reference semantic objects absent from the current `visual.json`. Until this state is analysed, an object used only by a bookmark can be classified as apparently unused.

This is a known accuracy boundary, not a deletion recommendation. It is deferred because bookmark definitions also contain large duplicated and potentially stale snapshots; treating every captured reference as current usage without careful reconciliation would create a different class of misleading result.

Revisit this work when either:

- real-report review shows bookmark-only fields materially affecting unused-object results;
- the primary HTML/CSV review workflow is usable; or
- a supported PBIR schema change makes bookmark-state interpretation more reliable.

## Standing boundaries

- Apparently unused always means no usage found in the analysed scope.
- PBI Assure never removes model or report objects automatically.
- Automated accessibility findings support, but do not replace, manual testing.
- Departmental data and report content must not be committed to this repository.
