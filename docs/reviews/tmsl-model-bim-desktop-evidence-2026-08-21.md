# TMSL `model.bim` Desktop evidence and supported-input treatment

**Date:** 2026-08-21
**Scope:** current Power BI Desktop local PBIP semantic-model storage and PBI Assure's safe response.

Evidence labels: **[verified by Desktop-authored fixture]** means retained source bytes from a project
saved by Power BI Desktop. **[design decision]** means a product boundary selected from that evidence.

## Evidence

`tests/fixtures/desktop-tmsl-model-bim-evidence` is a small synthetic PBIP authored with **Store
semantic model using TMDL format** disabled. It has a PBIR report, a local semantic-model folder containing
`definition.pbism` and `model.bim`, and no `definition/` folder. It contains a `Sales` table with `ID`,
`Amount`, `Total Amount = SUM(Sales[Amount])`, and one Card using the measure. The source was saved,
closed, reopened and saved again in Desktop before capture. [verified by Desktop-authored fixture]

Enabling TMDL did not silently migrate the source. Desktop prompted to upgrade on save; choosing not to
upgrade retained TMSL. The paired `desktop-tmsl-model-bim-evidence-tmdl` fixture records the upgraded
TMDL output with the same small report/model intent. No Desktop version was retained. [reported manual
observation and verified retained fixture shape]

## Previous unsafe result

The TMSL project could enter the normal pipeline despite PBI Assure not parsing its whole semantic model.
That produced an empty local model inventory and could emit `PBI-MODEL-001` for a report measure that
exists in the valid TMSL model. This was a PBI Assure false positive, not evidence that the Desktop project
was broken. [verified before implementation]

## Adopted treatment

PBI Assure does not implement a TMSL parser in this slice. Instead, the shared scanner stops before
parsing, rules or output generation when it finds a local `.SemanticModel/model.bim` without a TMDL
`definition/` folder. A project containing both forms also stops because its local model layout is
ambiguous. [design decision]

The command line returns a non-zero result and writes no HTML, JSON or CSV. The Windows app clears prior
output state and shows the same specific explanation. The browser shows it inline and leaves report/CSV
controls unavailable. Remote `byConnection` report models are not local `model.bim` projects and retain
their existing bounded treatment. [verified in code/tests]

## User guidance

Keep a backup, enable **Store semantic model using TMDL format** in Power BI Desktop and choose
**Upgrade** when saving. The upgrade cannot be undone. PBI Assure makes no claim that it can convert,
translate or safely analyse TMSL itself. [design decision]
