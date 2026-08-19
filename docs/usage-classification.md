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
- Row-level security table permission filter expressions. References inside a filter resolve against the table named by the permission, because Power BI Desktop serialises same-table column references unqualified.
- Perspective members: the tables, columns, measures and hierarchies a perspective exposes.
- DAX user-defined function bodies: what a function references, and which functions call one another. A function is a dependency node rather than a root, so an uncalled function does not keep what it references alive.
- The table containing each column, measure, or hierarchy level.

Every edge records its source and target identities, dependency kind, source file, and the relevant expression or reference text.

## Power BI-generated objects

Power BI-generated Auto Date/Time tables remain full participants in dependency and structural analysis. PBI Assure identifies them only when TMDL contains the explicit `__PBI_LocalDateTable = true` or `__PBI_TemplateDateTable = true` annotation. A hidden table, a matching-looking name, or an unused object is not sufficient evidence.

Developer-facing cleanup counts exclude objects owned by these generated tables and report their total separately. The HTML semantic-model view defaults to developer-authored objects, with a filter for all or Power BI-generated objects. This changes presentation and review emphasis only; it does not change any object's usage state.

Power Query uses a separate lineage graph because query execution dependencies are not the same as report-facing semantic-object usage. Every M table partition is classified as `LoadedToModel`. A named expression reachable from a loaded partition through static query references is `SupportingQuery`; an unreachable named expression is `ApparentlyUnused`. Known query names inside text literals, comments, or local step declarations are excluded. Expressions using dynamic mechanisms such as `Expression.Evaluate`, `#shared`, or `Record.Field` are marked for manual review.

Power Query column-level lineage is additional diagnostic evidence, not a semantic-usage root. The scanner records explicit static merge keys, expanded columns, selections, renames, removals, and type transformations. This evidence can explain why an apparently unused semantic column still matters during data preparation, but it does not change that column's semantic usage classification.

## State precedence

An object receives the first applicable state in this order:

1. `DirectlyUsed`: referenced directly by report-, page-, or visual-level PBIR metadata.
2. `IndirectlyUsed`: reachable from a directly used object through one or more dependency edges.
3. `StructurallyRequired`: reachable from a model-structure root but not from a direct report root. Model-structure roots are the parts of the model that require an object regardless of any report: relationship endpoint columns, field-parameter metadata columns, objects referenced by row-level security table permission filters, and objects exposed by a perspective.
4. `UsedOnlyByUnusedBranch`: referenced by an object that is itself outside all direct and structural paths.
5. `ApparentlyUnused`: not reached or referenced by any dependency represented in the graph.

A table is directly used when it contains a directly used object. Otherwise, its state is derived from graph reachability and the states of its contained objects.

When a report uses a field-parameter table, every statically declared `NAMEOF(...)` choice is treated as reachable because the saved PBIR metadata does not prove which choices a reader may select at runtime. Numeric what-if parameters based on expressions such as `GENERATESERIES(...)` are retained as ordinary calculated tables and are not labelled as field parameters.

Report-level measures are kept separate from semantic-model measures. A visual that uses a report measure makes that report measure a dependency root; PBI Assure then follows the structured measure references stored in `reportExtensions.json`. Referenced model measures are classified as indirectly used, and references to other report measures are followed transitively. If Power BI marks a report measure as containing unrecognized references, the inventory preserves that warning for manual review.

When a report uses a calculation-group table, every calculation item is treated as reachable. Explicit object references in calculation-item DAX and format-string expressions then participate in normal dependency traversal. Functions such as `SELECTEDMEASURE()` are contextual and do not create an invented dependency to every measure in the model; the report's explicit measure references remain the evidence for those base measures.

## Security metadata

A column referenced only by a role's table permission filter is required to enforce that filter, so it is not a deletion candidate. Such objects are `StructurallyRequired`: the model requires them, but no report references them, which is exactly what that state means. They are deliberately not `DirectlyUsed`, because that state means report metadata references the object.

Only table permission filters are analysed. A role also carries column permissions, which name columns for object-level security and are not read, so roles remain a partially analysed construct and continue to record an analysis limitation. Role membership is held in the Power BI service and never appears in a project, so it is outside the analysed scope entirely rather than an unanalysed construct.

## Perspectives

A perspective is a curated subset of the model that an author deliberately exposed, and it drives the Personalize visuals experience: a report reader can add any of its members to a visual at run time. Saved report metadata cannot prove which members a reader picks, so every exposed object is treated as reachable — the same reasoning already applied to field-parameter choices. An object a perspective exposes is `StructurallyRequired`: the model exposes it regardless of what any saved visual references.

Membership is exactly what the perspective lists. Naming a table does not expose its columns or measures; Microsoft documents that each member must be added individually unless `includeAll` is set, which includes every column, hierarchy and measure of that table. Treating a listed table as exposing all its fields would be a large source of false "used" conclusions.

This is a statement about intent recorded in the model, not a claim that any consumer has used the perspective. PBI Assure cannot observe that.

## Classification confidence

Every classification also carries a confidence, and it is deliberately a separate axis rather than a
sixth state. The state says what PBI Assure found. The confidence says how complete the evidence behind
that answer is.

- **Established**: nothing PBI Assure skipped in this model could change the object's state.
- **Usage check incomplete**: this model contains metadata PBI Assure did not fully check, and it could
  bear on the object's state. The state itself is unchanged and remains the best answer available.

An incomplete usage check is not the same as low confidence. PBI Assure may hold strong positive
evidence and simply not have read one more possible source of references. The marker is context about
the check, not a defect in the object, and it never means the object is definitely used — that is not
known either.

Because an unanalysed construct can only *add* references, it cannot retract evidence already collected.
Qualification therefore applies to the two states that assert an absence of usage, `ApparentlyUnused`
and `UsedOnlyByUnusedBranch`. The states resting on positive evidence keep their confidence. That is a
conservative product rule about the constructs known today, not a permanent guarantee: a future
construct that changes how existing evidence should be *read*, rather than only adding to it, would
qualify positive states too.

The HTML report shows this in two places. An **Analysis coverage** section states, per semantic model,
what PBI Assure could not fully check and whether that could change any used or unused result. Each
affected object then carries a small **Usage check incomplete** marker beside its status, linking to
that model's entry. The cause is explained once at model scope rather than repeated beside every
affected object, because one unchecked construct can affect most of a model.

PBI Assure gains coverage for more Power BI metadata over time, so a limitation describes what the
current version reads rather than a problem with the analysed project.

## Why an object has its state

Each classification is shown with a short explanation of the evidence behind it. That explanation is
chosen to be **compatible with the state it sits beside**: an object that is indirectly used is
explained by a predecessor that is itself reached from a report, and an object referenced only by an
unused branch is explained by one of those unused referrers.

This matters because an incoming reference and the evidence for a classification are not the same thing.
An uncalled DAX function may genuinely reference a column without being the reason that column is in
use. Both facts are kept: the reference stays in the dependency graph, and the explanation names
something that actually supports the answer.

Where several predecessors would each be a valid explanation, one is shown. The wording names a
reference rather than claiming to be the only one.

## Unresolved evidence

An unresolved report reference or semantic dependency is retained as a separate record. PBI Assure does not silently correct names or invent a target because doing so would make removal recommendations unsafe. For example, a visual reference to `Sales[Dates]` remains unresolved when the model contains only `Sales[Date]`. Each report reference records its report, optional page and visual, artifact path, JSON evidence path, and usage context.

## Current limits

The graphs do not yet include bookmark-captured semantic state, full connector- and data-source-level lineage, external tools, thin reports outside the selected project, XMLA clients, Analyze in Excel, or other external consumers. Dynamic references assembled as text can also be impossible to prove statically; recognised dynamic M mechanisms are flagged but cannot be resolved automatically.

For those reasons, `ApparentlyUnused` means “no usage found within the analysed scope.” It is a review candidate, never automatic permission to delete an object.
