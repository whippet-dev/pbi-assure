# Using PBI Assure

## Browser workflow

[Open the tool](https://pbiassure.pages.dev/) in current desktop Edge or Chrome. Choose the folder that directly contains one `.pbip` file, with its report and semantic-model folders. If starting with a PBIX, follow [preparation guidance](preparing-power-bi-project.md). A local TMSL `model.bim` model is rejected before output; a remote `byConnection` model is identified as remote, not analysed as a local model.

Select **Run analysis**, then **Open investigation** to explore model objects, lineage, Power Query,
relationships, security, report pages and findings. **Review apparently unused in Model Objects**
opens the focused review in that same viewer; **Review analysis scope** opens its qualifications and
limitations. Each opens a new tab with a fixed snapshot. Return to project focuses the originating
tab where available; refreshing either tab does not restore a project session. Hidden or supporting
report references can be used without being user-facing.

Save changes in Power BI Desktop before selecting **Analyse again**. With the primary folder picker, PBI Assure enumerates the folder again and reads current files, including additions and deletions. The alternate picker supplies a snapshot and requires folder reselection for another analysis. A failed refresh does not replace the previous completed result with partial results.

If the primary picker is blocked, use **Having trouble selecting a folder?** for the alternate picker. Firefox and Safari are best effort with the alternate picker; mobile and older enterprise browsers are not supported. Organisation policies can also block folder access, WebAssembly or downloads.

Browser limits are 10,000 visited entries, 5,000 accepted metadata files, 25 MiB per file, 100 MiB total accepted metadata and 64 directory levels. These are operability limits, not Power BI format limits.

## Choosing an output

- **Investigation:** the primary interactive web experience. It consumes the analysis snapshot; it is not the portable HTML layout.
- **Portable HTML downloads:** under collapsed Downloads & exports, Download report saves the full existing report layout; Download apparently unused review saves `<Project>.apparently-unused.html`. Both are self-contained files for sharing or offline review. Apparently unused is not a deletion recommendation; Power Query preparation evidence may still exist.
- **Data Catalogue CSV:** one row per eligible developer-authored column or measure, including zero-usage objects. Select columns for usage, confidence, user-facing evidence, counts, contexts and optional Description.
- **Usage Mapping CSV:** one row per logical direct report usage, grouped by location and context/role. Several technical evidence paths can support one row. Optional fields retain those paths; this is not a map of every transitive dependency.
- **Semantic Usage CSV:** the fixed technical export with semantic usage, report locations, Power Query column evidence and review flags. `ClassificationConfidence` is `Established` or `QualifiedByLimitation`; `QualifyingLimitations` lists the relevant limitation identifiers. `ReviewCandidate=Yes` is not permission to delete, regardless of confidence.

`UserFacing` is separate from usage: Yes means qualifying presentation or intentional-interaction evidence was found; No means none was found, not that users can never encounter the object; Unclear means the retained direct context could not be classified confidently.

Power Query evidence does not change semantic usage states. A missing lineage row or `PowerQueryUsed=No` does not prove that a column is irrelevant to data preparation. See [usage classification](usage-classification.md).

Outputs contain project metadata. The HTML includes full M expressions; optional CSV descriptions and provenance may also be sensitive. Review files before sharing. See [Privacy](../PRIVACY.md).

## Command line

Install the SDK pinned in `global.json` to run from a checkout:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject"
```

A default scan writes timestamped HTML and Semantic Usage CSV files under the project's `outputs/`, and updates `latest.pbiassure.html` and `latest.semantic-usage.csv`. Historical filenames use local time; the inventory records the UTC scan timestamp. Source project metadata is not changed.

Explicit output paths create only the requested file:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output assurance.pbiassure.html
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output semantic-usage.csv
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\YourProject" --output inventory.pbiassure.json
```

With `--output`, `.html` and `.csv` infer their formats; other extensions default to JSON. Without an explicit path, `--format json` or `--format csv` creates that timestamped output type alone.

## Windows application

```powershell
dotnet run --project src/PbiAssure.Desktop
```

The Windows application retains its saved-HTML workflow, not the hosted investigation viewer. It uses the same scanner and renderers. It writes the default HTML/CSV outputs locally and offers Data Catalogue and Usage Mapping through Export Builder. See [Contributing](../CONTRIBUTING.md) for build requirements.
