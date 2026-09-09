# PBI Assure

**Understand your Power BI project: how it is built, what is being used, and what may be worth investigating.**

[Open PBI Assure](https://pbiassure.pages.dev/) in desktop Edge or Chrome. No installation or account is needed.

PBI Assure gives you a read-only snapshot of the tables, fields, calculations, queries and report pages in a Power BI project. Explore where objects are used and what depends on them, with evidence behind the results. Your source project is not changed.

## Start with your project

1. Save a Power BI Project (**PBIP**) using the structured **PBIR** report and **TMDL** model formats. See [preparation guidance](docs/preparing-power-bi-project.md) if you currently have a PBIX.
2. Open the tool and choose the folder containing the `.pbip` file and its report and model folders.
3. Select **Run analysis**, then start with the interactive HTML report.
4. Export a catalogue or usage mapping when you want to work with the metadata in a spreadsheet.

After saving changes in Power BI Desktop, use **Analyse again** to read the current folder, including added, changed and deleted files. If you used the alternate folder picker, choose the folder again: that picker supplies a snapshot that cannot be refreshed in place.

See [using PBI Assure](docs/usage.md) for browser limits, exports and local command-line/Windows options.

## What you can investigate

- Where columns and measures appear in report pages, visuals, filters, sorting and supported formatting expressions.
- Dependencies through calculations and model structure, including relationships, security metadata, perspectives and calculation groups.
- How Power Query queries feed the model and one another, recognised parameters and connectors, and bounded static column-lineage evidence.
- Report structure and navigation, including bookmarks, drillthrough, tooltips and hidden items.
- Selected accessibility settings and narrow theme comparisons that support manual review.

Custom visual instance bindings are read from ordinary report metadata. Recognised visual packages are packaging, not automatically a reason to qualify every unused result; their executable code and runtime behaviour are not analysed.

The tool reads metadata. It does not run DAX or M, contact data sources, reproduce Power BI rendering, or assess refresh performance. Power Query column lineage follows supported static transformations; missing lineage is not proof of no use. Accessibility review is not WCAG certification. Theme Review reports saved values and limited comparisons, not manual editing history or final rendered appearance.

See [what PBI Assure analyses](https://pbiassure.pages.dev/coverage) and the [rule catalog](docs/rule-catalog.md).

## Results and exports

| Output | Use it for |
| --- | --- |
| **Interactive HTML report** | Explore model usage, dependencies, Power Query, relationships, report structure, findings and Analysis Coverage. Search and filter the evidence; open it in the browser or download a self-contained report. |
| **Data Catalogue CSV** | One row per eligible developer-authored column or measure, including objects with no detected usage. Choose metadata such as usage, confidence, counts, user-facing evidence and descriptions. |
| **Usage Mapping CSV** | One row per logical direct report usage: where and how an object is referenced, with optional technical provenance. This is not a transitive dependency export. |
| **Semantic Usage CSV** | The existing fixed technical export, including semantic usage, Power Query column evidence, review flags, `ClassificationConfidence` and `QualifyingLimitations`. |

Data Catalogue and Usage Mapping offer selectable columns in the browser and Windows Export Builder. The command-line tool also supports a JSON inventory.

## Used, unused and incomplete evidence

Usage has five states: **Directly used**, **Indirectly used**, **Structurally required**, **Only used by unused items**, and **Apparently unused**.

Confidence is separate. **Established** means this scan identified no limitation that qualifies that object's state within the analysed scope. **QualifiedByLimitation**, presented as an incomplete usage check, means missing or partly understood evidence could affect the conclusion. The usage state itself does not change. **Analysis Coverage** explains the causes for the affected model.

**Apparently unused is a review candidate, never permission to delete.** Other reports, external consumers and runtime behaviour may use an object that this project does not mention. A CSV review flag does not override its confidence. See [usage classification](docs/usage-classification.md).

## Local and read-only

Your project is processed locally in your browser. Application code does not upload selected project files or generated results. Loading the site still requests application assets from its static host; opening the report loads a same-origin viewer shell and transfers report content locally.

Generated outputs can contain sensitive project metadata. In particular, the detailed HTML includes full Power Query expressions, which may contain paths, server names or hard-coded values. Review exports before sharing them.

Read [Privacy](PRIVACY.md) for the exact boundary and verification steps, and [Security](SECURITY.md) for vulnerability reporting.

## Build or contribute

The same Core and Reporting libraries serve the browser, command-line and Windows applications. The repository includes synthetic regression projects and documented Desktop persistence fixtures.

Start with [Contributing](CONTRIBUTING.md), [architecture](docs/development/architecture.md), [testing](docs/development/testing.md) and [hosting](docs/development/hosting.md). Local builds require the SDK pinned in `global.json`; browser users do not need it.

## Licence

PBI Assure is open-source software licensed under the [MIT License](LICENSE).
