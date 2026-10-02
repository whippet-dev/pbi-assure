# Desktop field-parameter persistence evidence

This small PBIP project was created in current Power BI Desktop using **Modeling > New parameter > Fields**. It contains synthetic entered data, with no private or production data.

## Provenance

The user created the field parameter in Desktop, saved the project as PBIP, closed and reopened it, changed the parameter selection, and saved again. Codex did not independently perform those Desktop interactions. The exact Desktop build was not recorded.

The supplied model and report definition files are preserved unchanged. Desktop cache and local settings files are excluded from Git by the repository's existing ignore rules.

## Persisted observations

- `Metric[Metric]` has `sortByColumn: 'Metric Order'` and a `relatedColumnDetails` block with `groupByColumn: 'Metric Fields'`.
- `Metric[Metric Fields]` is hidden, owns the `ParameterMetadata` extended property (`version: 3`, `kind: 2`), and sorts by `Metric Order`.
- `Metric[Metric Order]` is hidden.
- The calculated `Metric` partition contains the two entries `("Total Amount", NAMEOF('Sales'[Total Amount]), 0)` and `("Total Qty", NAMEOF('Sales'[Total Qty]), 1)`.
- The clustered-column-chart PBIR stores `Metric[Metric]` in `fieldParameters.parameterExpr` and materialises both `Sales[Total Amount]` and `Sales[Total Qty]` as actual Y projections.
- The slicer directly references `Metric[Metric]`. The chart also uses `Sales[Region]` as its category.

## Regression boundary

[The fixture tests](../../PbiAssure.Core.Tests/DesktopFieldParameterEvidenceFixtureTests.cs) verify these persisted structures and the current analysis:

| Objects | Usage state |
| --- | --- |
| Metric label; Sales Total Amount and Total Qty | DirectlyUsed |
| Metric Fields | StructurallyRequired |
| Metric Order; Sales Amount and Qty | IndirectlyUsed |
| Sales Cost | UsedOnlyByUnusedBranch |
| Sales Unused Control and Notes | ApparentlyUnused |

The tested classifications have `Established` confidence, with no unresolved semantic references or qualifying limitations. `Metric Fields` is excluded from the Apparently Unused review; the two absence controls remain included. Informational coverage items may remain visible.

This is Desktop-authored persistence evidence for this workflow and selected targets. It does not prove that every parameter configuration materialises every possible target in PBIR or establish runtime rendering behaviour independently of the user's Desktop observations.
