# PBI Assure

PBI Assure is the provisional name for an internally owned Power BI assurance tool. Its intended purpose is to inspect Power BI Project (PBIP) source without changing it, document the report and semantic model, trace dependencies, and produce evidence-led quality and accessibility findings.

The repository is at foundation stage. The executable can discover the report and semantic-model parts of a PBIP project; resolve each report's local semantic-model connection from `definition.pbir`; distinguish local, remote, and missing model targets; parse PBIR pages, page roles, report and page filters, drillthrough bindings, visual interactions, report-tooltip page bindings, visual containers, bookmarks, visual actions, report extensions, and report-level measures; extract evidence-rich field references from report, page, and visual scopes; inventory TMDL tables, columns, measures, hierarchies, partitions, relationships, field parameters, calculation groups, and calculation items; and build an evidence-backed dependency graph across DAX, report-measure references, parameter choices, calculation items, sort-by columns, hierarchy levels, relationship endpoints, and containing tables.

The semantic graph classifies model objects as directly used, indirectly used, structurally required, used only by an unused branch, or apparently unused within the analysed scope. A separate Power Query graph classifies loaded table queries, supporting named expressions, and named expressions with no static consumer. Recognised connector calls are summarised by family and location category without copying their arguments into connector records. Dynamic M and consumers outside the selected PBIP project remain analysis boundaries, so “apparently unused” does not mean “safe to delete.” See [usage classification](docs/usage-classification.md) for the exact contract.

The scanner also emits versioned assurance findings with severities, evidence paths, remediation guidance, assessment type, and authoritative references. The initial rules cover unresolved report bindings, Power BI Q&A retirement, alternative text, duplicate or excluded tab-order entries, explicitly disabled data-visual titles, broken or incomplete bookmark and page navigation, drillthrough configuration, visual-interaction endpoints, and report-tooltip targets. See the [rule catalog](docs/rule-catalog.md).

## Why a command-line tool first?

The analysis engine needs to work in several environments: on a developer workstation, in a build pipeline, and eventually behind a desktop interface. Starting with a command-line interface keeps the behaviour testable and prevents user-interface decisions from becoming coupled to the analysis logic.

## Prerequisites

- Windows, macOS, or Linux for the current source-only scanner. Power BI Desktop integration will later require Windows.
- .NET 10 SDK. The repository pins the supported feature band in `global.json`.
- Git.
- A PBIP project for real-world testing. Do not commit departmental reports or data to this repository.

## Build and test

```powershell
dotnet restore PbiAssure.slnx
dotnet build PbiAssure.slnx --no-restore
dotnet test PbiAssure.slnx --no-build
```

## Generate assurance results

For real-world local testing, place a PBIP/PBIR project under `samples-local/<report name>/`. This folder is intentionally ignored by Git so departmental reports and generated outputs stay local.

Scan a project and save an accessible, self-contained HTML report automatically:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "samples-local\Columns Usage"
```

The output is saved beside the selected project, for example:

```text
samples-local/Columns Usage/outputs/2026-08-09_09-55-32-123/assurance.pbiassure.html
```

The folder name uses the machine's local time in sortable `yyyy-MM-dd_HH-mm-ss-fff` form. Each run creates a separate timestamped output folder. The HTML report itself records the source project path and its UTC scan timestamp. Keep deterministic synthetic fixtures in `tests/`; do not commit reports or generated outputs in `samples-local/`.

Write to a specific location when needed:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output assurance.pbiassure.html
```

Write the machine-readable JSON inventory:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output inventory.pbiassure.json
```

The output format is inferred from a `.html` extension and otherwise defaults to JSON when `--output` is supplied. Without `--output`, HTML is the default; use `--format json` to create a timestamped JSON inventory instead. The scanner is read-only with respect to the selected Power BI project; it writes only the selected output file.

The HTML report is organised as expandable review cards rather than long data tables. Each report page contains collapsible visual summaries showing the visible title or on-canvas label, friendly visual type, approximate page position, referenced columns and measures, behaviour, accessibility metadata, and related findings. Findings and semantic-model tables are separately searchable and collapsible. Internal PBIR identifiers remain available only inside technical details.

## Repository structure

```text
src/PbiAssure.Core/       Domain types and analysis logic
src/PbiAssure.Reporting/  Accessible human-readable report rendering
src/PbiAssure.Cli/        Thin command-line entry point
tests/                    Automated tests using synthetic files
docs/                     Architecture, assurance, and security decisions
samples-local/            Ignored local PBIP/PBIR projects and timestamped scan outputs
```

Start with [the architecture overview](docs/architecture.md), [the product roadmap](docs/roadmap.md), [the rule catalog](docs/rule-catalog.md), and [the contributor guide](CONTRIBUTING.md).

## Current boundaries

- PBIP/PBIR and TMDL are the initial supported input formats.
- Explicit `byPath` report connections are resolved to local semantic models even when report and model names differ or several reports share one model. `byConnection` reports are identified as remote and excluded from local unused-object conclusions.
- Analysis is metadata-only by default.
- Current dependency analysis covers report, page, and visual PBIR references, report-level measure references, DAX expressions, field-parameter choices, calculation groups and items, sort-by columns, hierarchy levels, relationship endpoints, static Power Query references, and a privacy-minimised inventory of common M connector families. It does not yet inspect bookmark-captured semantic state, complete dynamic M, or detailed external data-source lineage.
- A finding is evidence for review, not a declaration of legal or WCAG compliance.
- “Unused” always means “not referenced within the analysed scope,” never automatically “safe to delete.”
