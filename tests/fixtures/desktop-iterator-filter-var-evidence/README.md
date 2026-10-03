# Desktop filtered-iterator and table-VAR evidence

This controlled project was created in current Power BI Desktop, saved as PBIP, closed, reopened and saved again. The user supplied the persisted project and reported the round trip; Codex did not independently perform the Desktop interactions. The exact Desktop build was not recorded.

The supplied fixture files are preserved unchanged. The measures table is `_Measures`: the user reported that Desktop did not permit `Measures` as its name in this model.

## Persisted controls

- `Direct Iterator` uses `SUMX(Dim, [X])`.
- `Filtered Iterator` uses `SUMX(FILTER(Dim, Dim[Key] > 0), [X])`.
- `Filtered Iterator Via VAR` assigns that filtered table to `t`, then uses `SUMX(t, [X])`.
- Three report cards reference those measures; no `Dim` columns are directly placed on visuals.
- `Dim[Notes]`, `_Measures[Unused Measure]` and the dummy Enter Data column `_Measures[Column1]` are absence controls. `Unused Measure` references `Dim[Key]`.

## Evidence boundary

[The fixture tests](../../PbiAssure.Core.Tests/DesktopIteratorFilterVarEvidenceFixtureTests.cs) verify each iterator measure's dependency on `Dim[X]` independently, and the two filtered measures' dependencies on `Dim[Key]`. The three report measures are `DirectlyUsed`; `X` and `Key` are `IndirectlyUsed`; the three absence controls are `ApparentlyUnused / Established`.

Report-format schema observations remain informational. This fixture establishes these persisted single-base-table FILTER/VAR shapes; it does not establish arbitrary table transformations, projected virtual-column lineage or every iterator function.
