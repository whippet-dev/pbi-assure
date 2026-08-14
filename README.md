# PBI Assure

PBI Assure is a general-purpose, read-only Power BI pre-flight checker. It inspects Power BI Project (PBIP) source, documents the report and semantic model, traces dependencies, and produces evidence-led quality and accessibility findings.

The shared scanner discovers the report and semantic-model parts of a PBIP project; resolves each report's local semantic-model connection from `definition.pbir`; distinguishes local, remote, and missing model targets; parses PBIR pages, page roles, filters, drillthrough bindings, visual interactions, report tooltips, bookmarks, visual actions, report extensions, and report-level measures; inventories TMDL tables, columns, measures, hierarchies, partitions, relationships, field parameters, calculation groups, and calculation items; and builds an evidence-backed dependency graph across report and model metadata.

The semantic graph classifies model objects as directly used, indirectly used, structurally required, used only by an unused branch, or apparently unused within the analysed scope. Power BI-generated Auto Date/Time tables remain in that analysis but are identified separately from developer-authored objects. A relationship inventory shows endpoints, cardinality, active state and cross-filter direction, with review findings for bidirectional and many-to-many configurations. Power Query analysis separately traces query-level dependencies between M-backed table partitions and named expressions, plus explicit column-level merge, expand, select, rename, remove, and type-transform evidence. Recognised connector calls are summarised by family and location category without copying their arguments into connector records. Dynamic M and consumers outside the selected PBIP project remain analysis boundaries, so “apparently unused” does not mean “safe to delete.” See [usage classification](docs/usage-classification.md) for the exact contract.

The scanner also emits versioned assurance findings with severities, evidence paths, remediation guidance, assessment type, and authoritative references. Current rules cover unresolved report bindings, relationship configurations worth reviewing, Power BI Q&A retirement, alternative text, duplicate or excluded tab-order entries, explicitly disabled data-visual titles, broken or incomplete bookmark and page navigation, drillthrough configuration, visual-interaction endpoints, and report-tooltip targets. See the [rule catalog](docs/rule-catalog.md).

The generated HTML also includes an informational Theme Review. It identifies the active PBIR base and custom theme resources, summarizes bounded theme metadata, and classifies saved evidence for a deliberately small set of title and data-colour formatting properties. This first phase does not assess theme alignment, infer manual changes, resolve final rendered colours, or produce accessibility findings.

## Ways to run PBI Assure

The same analysis and reporting libraries support three frontends:

- a local browser/WebAssembly application that processes selected project files without uploading them;
- a command-line tool for developer and automation workflows; and
- a lightweight Windows desktop application.

## Privacy and security

Your Power BI project is processed locally in your browser. PBI Assure's project-processing code does
not upload selected project files, their contents or generated results. Loading the browser application
still creates normal requests for static application files from Cloudflare Pages.

See [Privacy](PRIVACY.md) for the exact boundary and independent verification steps. Report potential
security or privacy vulnerabilities using the private route described in [Security](SECURITY.md).

## Prerequisites

- Windows, macOS, or Linux for the command-line scanner and browser application. The desktop application requires Windows.
- .NET 10 SDK. The repository pins the supported feature band in `global.json`.
- Git.
- A PBIP project for real-world testing. Do not commit real reports or data to this repository.

## Build and test

```powershell
dotnet restore PbiAssure.slnx
dotnet build PbiAssure.slnx --no-restore
dotnet test PbiAssure.slnx --no-build
```

## Generate assurance results

For real-world local testing, place a PBIP/PBIR project under `samples-local/<report name>/`. This folder is intentionally ignored by Git so real reports and generated outputs stay local.

Scan a project and save an accessible, self-contained HTML report automatically:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "samples-local\Columns Usage"
```

The output is saved beside the selected project, for example:

```text
samples-local/Columns Usage/outputs/latest.pbiassure.html
samples-local/Columns Usage/outputs/latest.semantic-usage.csv
samples-local/Columns Usage/outputs/assurance_2026-08-09_09-55-32.pbiassure.html
samples-local/Columns Usage/outputs/assurance_2026-08-09_09-55-32.semantic-usage.csv
```

Historical filenames use the machine's local time in sortable `yyyy-MM-dd_HH-mm-ss` form. Each default run retains timestamped HTML and semantic-usage CSV files, then updates `latest.pbiassure.html` and `latest.semantic-usage.csv` for quick reopening. The CSV has one row per developer-authored semantic object, including semantic usage, friendly report locations, concise static Power Query column-lineage evidence, and a cautious review flag; it deliberately excludes M expressions, connection values, and source paths. The HTML report itself records the source project path and its UTC scan timestamp. Keep deterministic synthetic fixtures in `tests/`; do not commit reports or generated outputs in `samples-local/`.

Write to a specific location when needed:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output assurance.pbiassure.html
```

Write the machine-readable JSON inventory:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output inventory.pbiassure.json
```

Write only a semantic-usage CSV when needed:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output semantic-usage.csv
```

The output format is inferred from `.html` and `.csv` extensions and otherwise defaults to JSON when `--output` is supplied. Without `--output`, HTML is the default and its companion semantic-usage CSV is created automatically; use `--format json` or `--format csv` to create only that timestamped output type instead. Explicit output paths continue to create only the requested file. The scanner is read-only with respect to the selected Power BI project; it writes only generated output files.

## Desktop app (Windows)

For a simple local developer workflow, run the lightweight desktop app:

```powershell
dotnet run --project src/PbiAssure.Desktop
```

Choose the folder containing the PBIP/PBIR project, select **Run assurance**, then use **Open latest report**, **Open semantic CSV**, or **Open output folder**. The app uses the same scanner and renderers as the command-line tool. It saves timestamped HTML and CSV outputs plus stable latest copies in the selected project's `outputs/` folder, and does not change the Power BI project itself.

After user-facing desktop changes, refresh the Windows publish output with:

```powershell
dotnet publish src/PbiAssure.Desktop -c Release -o artifacts/desktop
```

The HTML report is organised as expandable review cards rather than long data tables. Each report page contains collapsible visual summaries showing the visible title or on-canvas label, friendly visual type, approximate page position, referenced columns and measures, behaviour, accessibility metadata, and related findings. Findings, report pages, Power Query, relationships, and semantic-model objects provide context-specific search and filtering. Internal PBIR identifiers remain available only inside technical details.

## Browser application

`PbiAssure.Web` is a standalone Blazor WebAssembly frontend. It lets a user select one PBIP project folder, runs the shared scanner locally in the browser, shows a concise assurance summary, and creates local downloads of the existing self-contained HTML report and semantic-usage CSV. It has no project-processing backend or upload endpoint.

For full assurance, prepare a PBIP using PBIR and TMDL, then select the project root folder containing the `.pbip`, `.Report`, and `.SemanticModel` items. See [Prepare a Power BI project for PBI Assure](docs/preparing-power-bi-project.md) for the PBIX-to-PBIP steps and current Microsoft guidance.

```powershell
.\scripts\Publish-Web.cmd
```

Serve `artifacts/web/wwwroot` with a normal static web server. Current desktop Edge and Chrome are the supported first-beta browsers; Firefox and Safari are best effort only after fallback testing, and mobile or older enterprise browsers are not initially supported.

Choose the folder that directly contains one `.pbip` file. Browser ingestion is bounded to 10,000 visited entries, 5,000 accepted metadata files, 25 MiB per file, 100 MiB total and 64 directory levels. The HTML export contains detailed project metadata and full M expressions and should be reviewed before sharing.

See [the browser application guide](docs/browser-app.md), [browser privacy model](docs/browser-privacy.md), and [static-hosting requirements](docs/browser-hosting.md).

## Repository structure

```text
src/PbiAssure.Core/       Domain types and analysis logic
src/PbiAssure.Reporting/  Accessible human-readable report rendering
src/PbiAssure.Cli/        Thin command-line entry point
src/PbiAssure.Desktop/     Lightweight Windows desktop workflow
src/PbiAssure.Web/         Local standalone browser frontend
tests/                    Automated tests using synthetic files
docs/                     Architecture, assurance, and security decisions
samples-local/            Ignored local PBIP/PBIR projects and scan outputs
```

Start with [the architecture overview](docs/architecture.md), [the product roadmap](docs/roadmap.md), [the rule catalog](docs/rule-catalog.md), and [the contributor guide](CONTRIBUTING.md).

## Current boundaries

- PBIP/PBIR and TMDL are the initial supported input formats.
- Explicit `byPath` report connections are resolved to local semantic models even when report and model names differ or several reports share one model. `byConnection` reports are identified as remote and excluded from local unused-object conclusions.
- Analysis is metadata-only by default.
- Current dependency analysis covers report, page, and visual PBIR references, report-level measure references, DAX expressions, field-parameter choices, calculation groups and items, sort-by columns, hierarchy levels, relationship endpoints, static Power Query references, and a privacy-minimised inventory of common M connector families. It does not yet inspect bookmark-captured semantic state, complete dynamic M, or detailed external data-source lineage.
- A finding is evidence for review, not a declaration of legal or WCAG compliance.
- “Unused” always means “not referenced within the analysed scope,” never automatically “safe to delete.”

## Security and licensing

See [security and data handling](docs/security-and-data-handling.md) for the local-processing and output-sensitivity boundaries. The source is publicly visible, but no open-source licence has been selected; see [ownership and licensing](docs/ownership-and-licensing.md) before copying, modifying, or redistributing it.
