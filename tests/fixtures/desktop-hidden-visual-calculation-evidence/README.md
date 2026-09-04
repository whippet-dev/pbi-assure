# Desktop hidden visual-calculation projection evidence

This small synthetic PBIP fixture is persistence evidence for a *hidden supporting projection* consumed
by a visual calculation. It contains no private, organisational or production data.

## Provenance and confidence

- Initially **schema-authored** in this repository, not captured from a Desktop-created report. The report
  side was derived from `semanticQuery/1.4.0` and `visualConfiguration/2.3.0`, because
  `RoleProjection.field` is typed as a `QueryExpressionContainer` (which carries
  `NativeVisualCalculation`) and no other schema slot for a visual calculation exists.
- Opened in Power BI Desktop on 2026-09-04 **without correction or error** — no blocking PBIR error and no
  non-blocking auto-fix prompt.
- **Refresh rendered the visual calculation correctly.** A PBIP folder carries no cached data, so a
  refresh was required first. The table then rendered `Adjusted Value` as 360, 90 and 225 against
  `Visible Measure` values of 400, 100 and 250 — exactly `[Visible Measure] - [HiddenSupportValue]` for
  the three rows.
- Desktop then **saved, closed and reopened** the project successfully. On reopen the calculation and the
  hidden field were both still present and the visual rendered the same values.
- **`hidden: true` and the `NativeVisualCalculation` projection survived Desktop re-serialization
  unchanged.** Desktop rewrote every file on save, and a byte-level diff against the pre-open snapshot
  shows the report definition is identical apart from LF to CRLF conversion. The TMDL files differ only by
  one additional trailing blank line each. `Name`, `queryRef`, `nativeQueryRef`, `DataType` and projection
  order are exactly as authored, and the declared `visualContainer/2.12.0` schema version was retained.

### Evidence tier — read this before relying on the fixture

This is **Desktop round-trip persistence evidence**. It establishes that Desktop accepts this shape,
re-serialises it through its own object model, and preserves it across a save/close/reopen cycle — so the
shape is within Desktop's serialization vocabulary rather than merely tolerated on read.

It is **not** proof of the exact byte shape Desktop would originate if a visual calculation were authored
manually through the UI. Property ordering, and optional properties Desktop might add of its own accord,
are not established here. Settling that would require creating a visual calculation through the Desktop UI
and diffing the result.

## What it establishes

`HiddenSupportValue` is genuinely required by the visual and genuinely not presented to the report reader.
Both halves are demonstrated rather than asserted: the calculation could not evaluate to 360/90/225 unless
the field were on the visual matrix, and the rendered accessibility tree exposes only `Row Selection`,
`Category`, `Visible Measure` and `Adjusted Value` as columns.

Microsoft documents this behaviour: a hidden field "still appears on the visual matrix but isn't shown on
the resulting visual", and hidden fields "only appear in underlying data exports".

## Why this fixture exists

**UserFacing classification, not semantic dependency discovery.**

The dependency question is closed: a visual calculation can only reference fields already on the visual, so
it cannot introduce a model dependency PBI Assure could not otherwise see. This fixture is about the other
half of that finding.

PBI Assure's regression contract for this fixture is:

| | Classification | Assessment |
| --- | --- | --- |
| Semantic usage | `DirectlyUsed` | Correct. The visual really does require the field. |
| UserFacing | `No` | Correct for this projection-only control. The persisted projection is hidden. |

PBI Assure retains the literal role-projection `hidden` flag on that specific field reference. It does not
infer the state from visual-calculation DAX and does not conflate it with visual/container or semantic-model
object visibility.

## Controlled model

One table, `Fact`, from a literal inline `Table.FromRows` M source — no external connection or credential.

- `Category` (string) — visible projection, the table's grouping column.
- `VisibleValue` (int64, `summarizeBy: sum`) — source of the visible measure. Not projected directly.
- `HiddenSupportValue` (int64, `summarizeBy: none`) — the controlled object. `summarizeBy: none` keeps it a
  plain `Column` projection rather than an implicit `Aggregation`, so the control stays legible.
- `Visible Measure` = `SUM ( Fact[VisibleValue] )` — visible projection.

`compatibilityLevel` is `1606`.

## Report

One page, one `tableEx`, one role (`Values`), four projections in the order Desktop preserved: visible
`Category`, visible `Visible Measure`, hidden `HiddenSupportValue`, then the `Adjusted Value` visual
calculation. `HiddenSupportValue` has no other reference anywhere in the report — no second projection, no
filter, no sort, no formatting reference, no bookmark.

Desktop added **no** automatic `filterConfig` on this round trip, consistent with the documented rule that
automatic filters persist only after the filter pane has been expanded at least once while editing.

## Repository form

The repository retains the PBIP launcher, report definition, semantic-model definition, the `.platform`
files Desktop generated, and the required base theme. Desktop-local `.pbi` cache and settings directories
are excluded. Internal project artifact names remain as saved by Desktop, so the evidence-bearing project
files are not needlessly rewritten.

`StaticResources/SharedResources/BaseThemes/Fluent2-CY26SU08.json` is copied verbatim from
`desktop-incremental-refresh-evidence`; `report.json` requires `themeCollection` and the base theme is
referenced through `resourcePackages`, so the file must be present for the reference to resolve.
