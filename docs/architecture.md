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
  -> JSON / HTML / CSV / CI results
```

Each stage has one responsibility:

1. **Discovery** locates supported files and records their format versions.
2. **Parsing** extracts facts without judging them.
3. **Normalisation** gives report and model objects stable internal identities.
4. **Dependency analysis** connects model objects, expressions, filters, pages, and visuals.
5. **Rules** create findings from facts and graph relationships.
6. **Presentation** renders the same result for people or build pipelines.

## Project boundaries

### `PbiAssure.Core`

Contains the domain model, parsers, dependency engine, and rules. It must not depend on a desktop UI, console output, or a specific CI platform.

### `PbiAssure.Cli`

Provides automation and an early user interface. It translates command-line input into calls to the core and serialises results.

### Future projects

- `PbiAssure.Pbir`: detailed PBIR parsing if it becomes large enough to isolate.
- `PbiAssure.Tabular`: TMDL/TOM and XMLA integration.
- `PbiAssure.Desktop`: an accessible graphical interface.
- `PbiAssure.Reporting`: HTML and other human-readable reports.

These should be introduced only when their boundaries are real; empty architectural layers add maintenance cost.

## Dependency graph

The graph will use directed edges. For example, a visual has an edge to the measure it uses, while a measure has edges to the columns and measures referenced by its DAX expression. Starting at user-facing roots and traversing these edges makes usage classification explainable.

Initial usage states will be:

- Directly used.
- Indirectly used.
- Used only by an otherwise unused branch.
- Apparently unused within scope.
- Usage unknown or external to scope.

## Format evolution

PBIR files declare JSON schemas and format versions. Parsers must record the encountered schema and fail with a useful unsupported-version finding rather than silently misinterpreting a newer format. Parser fixtures should cover each supported version.

## Decision records

Important choices and their trade-offs live in `docs/decisions`. This supports future ownership transfer and prevents decisions from surviving only as institutional memory.
