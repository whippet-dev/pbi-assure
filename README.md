# PBI Assure

PBI Assure is the provisional name for an internally owned Power BI assurance tool. Its intended purpose is to inspect Power BI Project (PBIP) source without changing it, document the report and semantic model, trace dependencies, and produce evidence-led quality and accessibility findings.

The repository is at foundation stage. The executable can discover the report and semantic-model parts of a PBIP project; parse PBIR pages and visual containers; extract evidence-rich field references from visual projections, filters, sorting, and formatting; inventory TMDL tables, columns, measures, hierarchies, partitions, and relationships; and build an evidence-backed dependency graph across DAX, sort-by columns, hierarchy levels, relationship endpoints, and containing tables.

The graph classifies semantic objects as directly used, indirectly used, structurally required, used only by an unused branch, or apparently unused within the analysed scope. Power Query M dependencies and consumers outside the selected PBIP project are not yet analysed, so “apparently unused” does not mean “safe to delete.” See [usage classification](docs/usage-classification.md) for the exact contract.

The scanner also emits versioned assurance findings with severities, evidence paths, remediation guidance, assessment type, and authoritative references. The initial rules cover unresolved report bindings, Power BI Q&A retirement, alternative text, duplicate or excluded tab-order entries, and explicitly disabled data-visual titles. See the [rule catalog](docs/rule-catalog.md).

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

## Run the initial scanner

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject"
```

Write the JSON inventory to a file:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output inventory.pbiassure.json
```

The scanner is read-only with respect to the selected Power BI project. Supplying `--output` writes only to the output path chosen by the operator.

## Repository structure

```text
src/PbiAssure.Core/       Domain types and analysis logic
src/PbiAssure.Cli/        Thin command-line entry point
tests/                    Automated tests using synthetic files
docs/                     Architecture, assurance, and security decisions
```

Start with [the architecture overview](docs/architecture.md), [the rule catalog](docs/rule-catalog.md), and [the contributor guide](CONTRIBUTING.md).

## Current boundaries

- PBIP/PBIR and TMDL are the initial supported input formats.
- Analysis is metadata-only by default.
- Current dependency analysis covers PBIR, DAX expressions, sort-by columns, hierarchy levels, and relationship endpoints; it does not yet parse Power Query M dependencies.
- A finding is evidence for review, not a declaration of legal or WCAG compliance.
- “Unused” always means “not referenced within the analysed scope,” never automatically “safe to delete.”
