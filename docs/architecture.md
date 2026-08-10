# Architecture overview

## Objective

PBI Assure will turn Power BI report and semantic-model metadata into a normalised inventory and dependency graph. Versioned rule packs will evaluate that graph for maintainability, performance, governance, and accessibility concerns.

## Processing flow

```text
PBIP project
  -> source discovery
  -> PBIR and TMDL parsers
  -> normalised artifact inventory
  -> dependency graph
  -> versioned assurance rules
  -> JSON inventory / HTML report / semantic-usage CSV / CI results
```

Each stage has one responsibility:

1. **Discovery** locates supported files and records their format versions.
2. **Parsing** extracts facts without judging them, including pages, visuals, actions, bookmarks, semantic objects, and expressions.
3. **Normalisation** gives report and model objects stable internal identities and binds reports to local models using their explicit PBIR dataset references.
4. **Dependency analysis** connects model objects, expressions, filters, pages, and visuals.
5. **Rules** create findings from facts and graph relationships.
6. **Presentation** renders the same result for people or build pipelines.

## Project boundaries

### `PbiAssure.Core`

Contains the domain model, parsers, dependency engine, and rules. It must not depend on a desktop UI, console output, or a specific CI platform.

### `PbiAssure.Cli`

Provides automation, default output naming, and file writing. A default scan creates a paired HTML report and semantic-usage CSV beside the source project; explicit output paths create only the requested output.

### `PbiAssure.Reporting`

Renders normalized inventories into accessible HTML and focused semantic-usage CSV outputs. It depends on the core domain model but contains no scanning or Power BI parsing logic.

### `PbiAssure.Desktop`

Provides the lightweight Windows workflow over the shared scanner and output writers. It selects a local PBIP/PBIR project, runs assurance, and opens the latest HTML report, semantic CSV, or output folder without embedding a report viewer.

### Future projects

- `PbiAssure.Pbir`: detailed PBIR parsing if it becomes large enough to isolate.
- `PbiAssure.Tabular`: TMDL/TOM and XMLA integration.

These should be introduced only when their boundaries are real; empty architectural layers add maintenance cost.

## Dependency graph

Report-to-model binding happens before field reconciliation. An explicit `byPath` reference is matched by normalized project-relative model path rather than by folder-name similarity. A `byConnection` reference is recorded as remote and is not compared with local model objects. Name matching remains only as a compatibility fallback for incomplete or older fixtures with no explicit dataset reference. If an explicit local target is missing, PBI Assure emits one model-level finding and suppresses cascading field-resolution errors until the model is available.

The semantic graph uses directed edges. A report visual provides direct roots, while a measure has edges to the columns, measures, and tables referenced by its DAX expression. A field-parameter table links to every statically declared choice. A used calculation-group table links to all of its calculation items, whose expressions then link to their explicit semantic dependencies. Sort-by columns, hierarchy levels, relationship endpoints, and containing tables provide structural edges. Starting at report-facing and structural roots and traversing these edges makes usage classification explainable.

Power Query lineage is a separate directed graph over M-backed table partitions and named expressions. Loaded partitions are roots. Static references to known query names are traversed transitively, while strings, comments, and local step names are excluded. A focused column-level pass records explicit static operations such as merge keys, expanded columns, selections, renames, removals, and type transformations. This separation avoids treating query execution as evidence that every semantic column or measure in the loaded table is report-facing usage.

Connector extraction runs over the same M expressions but emits a minimised inventory: connector family, connector function, coarse location category, query identity, and source artifact path. Literal connector arguments are used transiently only to distinguish local, network, relative, web, named-server, or dynamic locations and are not retained in connector records.

Initial usage states are:

- Directly used.
- Indirectly used.
- Structurally required.
- Used only by an otherwise unused branch.
- Apparently unused within scope.

References that cannot be resolved are emitted separately with their evidence and reason. See [usage classification](usage-classification.md) for state precedence and current analysis boundaries.

Report filters, page filters, drillthrough parameters, and visual references all provide direct roots for the semantic dependency graph. Evidence locations use nullable page and visual identifiers plus an artifact path, allowing report-, page-, and visual-scoped references to share one explainable usage model without placeholder object names. Automatic date-hierarchy variations are normalized to their underlying model column because their generated levels are not standalone TMDL hierarchy objects.

Bookmark and action reconciliation is kept alongside, rather than inside, the semantic dependency graph. It compares enabled typed actions with bookmark definitions and report pages, and compares bookmark state with page and visual inventories. Disabled action configuration remains visible in inventory but does not create a broken-target finding. Bookmark-captured semantic state is not yet treated as a dependency root.

Page visual interactions are reconciled against the visuals on their own page. Report-tooltip page bindings are parsed only when PBIR provides a `section` expression or explicitly requests a canvas tooltip; built-in default tooltip settings and visual-header text tooltips are not page bindings. Disabled bindings remain in inventory without producing stale-target findings, while dynamic targets require human review.

## Format evolution

PBIR files declare JSON schemas and format versions. Parsers must record the encountered schema and fail with a useful unsupported-version finding rather than silently misinterpreting a newer format. Parser fixtures should cover each supported version.

## Decision records

Important choices and their trade-offs live in `docs/decisions`. This supports long-term maintenance and keeps project knowledge available to future contributors.
