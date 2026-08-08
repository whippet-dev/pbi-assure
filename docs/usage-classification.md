# Usage classification

PBI Assure classifies semantic-model objects by traversing an evidence-backed dependency graph. These classifications describe only the selected PBIP project and the dependency types that the current scanner understands.

## Evidence currently included

- PBIR report filters, page filters, drillthrough parameters, and visual projections, filters, sorting, and formatting references.
- DAX references from measures, calculated columns, and calculated-table partition expressions.
- Standard field-parameter choices declared through `NAMEOF(...)` references in a calculated table.
- Calculation items, their DAX and format-string expressions, and calculation-group selection expressions.
- Sort-by column links.
- Hierarchy levels and their backing columns.
- Active and inactive relationship endpoint columns.
- The table containing each column, measure, or hierarchy level.

Every edge records its source and target identities, dependency kind, source file, and the relevant expression or reference text.

## State precedence

An object receives the first applicable state in this order:

1. `DirectlyUsed`: referenced directly by report-, page-, or visual-level PBIR metadata.
2. `IndirectlyUsed`: reachable from a directly used object through one or more dependency edges.
3. `StructurallyRequired`: reachable from a relationship endpoint but not from a direct report root.
4. `UsedOnlyByUnusedBranch`: referenced by an object that is itself outside all direct and structural paths.
5. `ApparentlyUnused`: not reached or referenced by any dependency represented in the graph.

A table is directly used when it contains a directly used object. Otherwise, its state is derived from graph reachability and the states of its contained objects.

When a report uses a field-parameter table, every statically declared `NAMEOF(...)` choice is treated as reachable because the saved PBIR metadata does not prove which choices a reader may select at runtime. Numeric what-if parameters based on expressions such as `GENERATESERIES(...)` are retained as ordinary calculated tables and are not labelled as field parameters.

Report-level measures are kept separate from semantic-model measures. A visual that uses a report measure makes that report measure a dependency root; PBI Assure then follows the structured measure references stored in `reportExtensions.json`. Referenced model measures are classified as indirectly used, and references to other report measures are followed transitively. If Power BI marks a report measure as containing unrecognized references, the inventory preserves that warning for manual review.

When a report uses a calculation-group table, every calculation item is treated as reachable. Explicit object references in calculation-item DAX and format-string expressions then participate in normal dependency traversal. Functions such as `SELECTEDMEASURE()` are contextual and do not create an invented dependency to every measure in the model; the report's explicit measure references remain the evidence for those base measures.

## Unresolved evidence

An unresolved report reference or semantic dependency is retained as a separate record. PBI Assure does not silently correct names or invent a target because doing so would make removal recommendations unsafe. For example, a visual reference to `Sales[Dates]` remains unresolved when the model contains only `Sales[Date]`. Each report reference records its report, optional page and visual, artifact path, JSON evidence path, and usage context.

## Current limits

The graph does not yet include bookmark-captured semantic state, Power Query M dependencies, external tools, thin reports outside the selected project, XMLA clients, Analyze in Excel, or other external consumers. Dynamic references assembled as text can also be impossible to prove statically.

For those reasons, `ApparentlyUnused` means “no usage found within the analysed scope.” It is a review candidate, never automatic permission to delete an object.
