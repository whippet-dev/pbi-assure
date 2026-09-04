# Desktop measure dynamic format-string evidence

This small synthetic PBIP fixture is persistence evidence for a measure-owned
`formatStringDefinition`. It contains no private, organisational or production data.

## Provenance and confidence

- Initially hand-authored in this repository, then opened, saved, fully closed, reopened successfully,
  saved and closed again in Power BI Desktop on 2026-09-04.
- The current `Fact.tmdl` is authoritative evidence that Desktop accepted and retained the measure-owned
  multiline `formatStringDefinition` form. No first-save byte snapshot was retained, so this fixture does
  not claim byte-for-byte stability between saves.
- Structure, file set, indentation, line endings and trailing-newline conventions follow the existing
  Desktop-authored fixtures (`desktop-tmsl-model-bim-evidence-tmdl` for the overall skeleton,
  `desktop-userelationship-evidence` for the multi-line measure expression form).
- `StaticResources/SharedResources/BaseThemes/Fluent2-CY26SU08.json` is copied verbatim from
  `desktop-incremental-refresh-evidence`. `report.json` requires `themeCollection`, and the Desktop base
  theme is referenced through `resourcePackages`, so the file has to be present for the reference to
  resolve.
- Contains no private, organisational or production data.

## What it establishes

A measure-owned `formatStringDefinition` can carry its own dependencies. `Dynamic Amount` has an ordinary
DAX expression that references `[Base Amount]`, and a separate multi-line format-string expression that
references **both** `FormatLookup[FormatString]` (a column on another table) and `[Base Amount]` (a
measure). The persisted definition establishes that it is a second dependency-bearing expression on the
same object, distinct from the ordinary measure expression.

Desktop retained the definition as an indented child block after the measure's `lineageTag`, using an
unfenced multiline assignment. `Dynamic Amount` has no literal `formatString`; the separate `Base Amount`
control retains its ordinary `formatString: #,0`.

## Controlled model

`FormatLookup` has `FormatKey` and `FormatString`, loaded from an inline `Table.FromRows` M source with two
rows carrying different format strings (`$#,0.00` and `0.0%`). `Fact` has a single numeric `Amount` column
from an equivalent inline source. Both sources are literal, so no external connection or credential is
required.

There is no relationship between the two tables. `SELECTEDVALUE` over `FormatLookup[FormatString]` does not
need one, and leaving it out keeps `relationships.tmdl` out of the round trip.

`compatibilityLevel` is `1606`. MS-SSAS-T requires 1601 or higher for `FormatStringDefinition` on a Measure;
1606 is the level every Desktop-authored fixture in this repository carries, so Desktop should not need to
raise it on save.

## Report

One page with one card visual bound to `Dynamic Amount`. No filters, bookmarks or other report behaviour.

## Repository form

The repository retains the PBIP launcher, report definition, semantic-model definition and required base
theme. Desktop-local `.pbi` cache and settings directories are excluded. The fixture directory was
promoted to `desktop-dynamic-format-string-evidence`; internal project artifact names remain as saved by
Desktop so the evidence-bearing project files are not needlessly rewritten.
