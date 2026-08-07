# PBI Assure

PBI Assure is the provisional name for an internally owned Power BI assurance tool. Its intended purpose is to inspect Power BI Project (PBIP) source without changing it, document the report and semantic model, trace dependencies, and produce evidence-led quality and accessibility findings.

The repository is at foundation stage. The executable can discover the report and semantic-model parts of a PBIP project; parse PBIR pages and visual containers; extract evidence-rich field references from visual projections, filters, sorting, and formatting; inventory TMDL tables, columns, measures, hierarchies, partitions, and relationships; and reconcile direct report references with those model objects. DAX dependency analysis is not yet implemented, so an object that is not directly referenced by the report must not yet be treated as safe to remove.

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

Start with [the architecture overview](docs/architecture.md) and [the contributor guide](CONTRIBUTING.md).

## Current boundaries

- PBIP/PBIR and TMDL are the initial supported input formats.
- Analysis is metadata-only by default.
- A finding is evidence for review, not a declaration of legal or WCAG compliance.
- “Unused” always means “not referenced within the analysed scope,” never automatically “safe to delete.”
