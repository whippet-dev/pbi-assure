# Desktop TMSL `model.bim` evidence

This small synthetic PBIP project was authored in Power BI Desktop with **Store semantic model using
TMDL format** disabled. Its report uses the enhanced PBIR format, while its local semantic model is
stored as `desktop-tmsl-model-bim-evidence.SemanticModel/model.bim` with no `definition/` folder.

The project contains a `Sales` table, `ID` and `Amount` columns, and `Total Amount =
SUM(Sales[Amount])`. A Card consumes `Total Amount`.

The project was saved, closed, reopened and saved again in Power BI Desktop before it was supplied. It
remained a TMSL `model.bim` project. No Desktop version was retained, so this fixture does not claim one.

## What it proves

Current Power BI Desktop can retain a valid PBIP with a local TMSL semantic model. PBI Assure does not
parse TMSL in this release and must stop before normal analysis, rules or output generation, rather than
presenting an incomplete inventory or a false unresolved-model reference.

## Repository form

The committed files are the report and semantic-model metadata required for the test. Local `.pbi`
cache/settings and static theme resources are intentionally excluded. The paired upgraded fixture is
[`../desktop-tmsl-model-bim-evidence-tmdl`](../desktop-tmsl-model-bim-evidence-tmdl).
