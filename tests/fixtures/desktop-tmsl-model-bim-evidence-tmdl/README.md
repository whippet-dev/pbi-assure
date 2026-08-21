# Desktop TMDL upgrade companion evidence

This synthetic PBIP is the Desktop-upgraded companion to
[`../desktop-tmsl-model-bim-evidence`](../desktop-tmsl-model-bim-evidence). Power BI Desktop was asked
to enable **Store semantic model using TMDL format** and upgrade the local semantic model on save.

It retains the same small report/model intent: the `Sales` table, `ID`, `Amount`, `Total Amount =
SUM(Sales[Amount])`, and a Card using the measure. Its semantic model is now stored in `definition/`
TMDL files and contains no `model.bim` file.

## What it proves

TMSL and TMDL are distinct local input layouts. The current PBI Assure scanner can analyse the upgraded
TMDL companion normally; it must not guess or partially analyse the TMSL source project.

## Repository form

The committed files are the report and semantic-model metadata needed for scanner tests. Local `.pbi`
cache/settings and static theme resources are intentionally excluded. No Desktop version was retained,
so this fixture does not claim one.
