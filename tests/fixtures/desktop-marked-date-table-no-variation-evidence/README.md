# Desktop marked date table: no column variation

This small synthetic PBIP fixture is persistence evidence for how Power BI Desktop treats a **user-authored
marked date table** with an explicit hierarchy. It contains no private, organisational or production data.

## The question it settles

When a fact-table date column relates to a user-authored date table that is marked as the model's date
table and contains an explicit hierarchy, does Desktop persist a TMDL `variation` block on the fact date
column whose `defaultHierarchy` points at that user-authored hierarchy?

**It does not.** See the conclusion below.

This was deliberately not assumed from Auto Date/Time behaviour. Every other `variation` block in this
repository comes from Auto Date/Time and points at a generated `LocalDateTable_*` hierarchy.

## Provenance and confidence

- Initially **schema-authored** in this repository, not captured from a Desktop-created report. The report
  side was derived from the published PBIR schemas and from the binding shape a real Microsoft-published
  report uses for a user-authored hierarchy (`microsoft/BCApps`, Sales app).
- Power BI Desktop **opened** it with no blocking PBIR error and no non-blocking auto-fix prompt. Visible
  top-level windows were enumerated at every stage and never exceeded the main window.
- A PBIP folder carries no cached data, so a **refresh** was required. After refreshing, every "table
  contains no data" state cleared with no refresh error.
- The report **rendered** correctly: chart title "Total Value by Year", category `2026`, value `1245`,
  which is `100 + 250 + 400 + 175 + 320`. The hierarchy binding, the relationship and the measure all
  resolved.
- Desktop then **saved, closed and reopened** the project successfully — closing produced no
  unsaved-changes prompt, and on reopen the chart rendered `1245` again.

## Controlled model

Both tables use literal inline M — `List.Dates` and `Table.FromRows` — so no external connection or
credential is required and the data is deterministic.

`DimDate` is **user-authored** and marked as the model's date table through `dataCategory: Time` on the
table and `isKey` on its `Date` column:

```tmdl
table DimDate
	lineageTag: 8c41a0d6-3e57-4b92-9f10-5a2d7e6b3001
	dataCategory: Time

	column Date
		dataType: dateTime
		isKey
```

That pair is the shape observed in Desktop-authored output — `microsoft/PowerBI-LogAnalytics-Template-Reports`
`Calendar.tmdl` carries exactly it. Microsoft's prose documentation describes the *Mark as date table*
dialog, not the metadata, so the representation rests on real Desktop output rather than on documentation.

`DimDate` covers all 365 days of 2026, a contiguous unique null-free range, because Desktop validates a
marked date table for exactly those properties. It carries an explicit hierarchy `Date Hierarchy` with
levels `Year`, `Quarter`, `Month`, `Day`.

**Auto Date/Time is disabled** via `annotation __PBI_TimeIntelligenceEnabled = 0` in `model.tmdl`. That
value is attested in real Desktop-authored PBIP models including `microsoft/PowerBI-LogAnalytics-Template-Reports`,
`microsoft/finops-toolkit` and `RuiRomano/pbip-demo`; every other fixture here carries the same annotation
with value `1`, the enabled state, so both values are attested from Desktop output. Disabling it is what
makes the experiment valid: with Auto Date/Time on, Desktop would generate a `LocalDateTable_*` for
`Fact[Date]` and attach a variation to that instead, confounding the result.

`Fact` has `Date` and `Value`, five rows spread across all four quarters of 2026, and a measure
`Total Value = SUM ( Fact[Value] )`. It has a normal **active** many-to-one relationship to the date table:

```tmdl
relationship 5f2c8d31-7b46-4e09-a2d5-61c8f0937a01
	fromColumn: Fact.Date
	toColumn: DimDate.Date
```

Cardinality and active state are TMDL defaults, so they are omitted exactly as Desktop omits them.

`compatibilityLevel` is `1606`.

## Report

One page, one `clusteredColumnChart`. The `Category` role carries all four levels of the explicit
user-authored `DimDate[Date Hierarchy]`; the `Y` role carries `Fact[Total Value]`. The hierarchy is
therefore genuinely used, giving Desktop every reason to materialise whatever metadata a used hierarchy
requires.

The binding is the ordinary direct form — `HierarchyLevel` → `Hierarchy` → `SourceRef: DimDate` — not the
`PropertyVariationSource` form that Auto Date/Time hierarchies are reached through.

## No variation was hand-authored

`Fact[Date]` carried **no** `variation` block before the round trip, and none was authored anywhere in the
model. That was the point of the experiment: nothing in the model required one, the report bound correctly
without one, and Desktop was left entirely free to decide.

## What Desktop did on the round trip

**It created no variation.** After save, close and reopen:

- `Fact[Date]` still has no `variation` block. The only occurrence of the string "variation" anywhere in
  the semantic model is the project's own name inside `.platform`.
- No `PropertyVariationSource` appears anywhere in the report.
- No `LocalDateTable_*` or `DateTableTemplate_*` table was created. `definition/tables/` still contains
  only `DimDate.tmdl` and `Fact.tmdl`.
- `dataCategory: Time`, `isKey`, the hierarchy with all four levels and their `lineageTag` values, and the
  relationship were all retained verbatim. Desktop added no explicit cardinality or `isActive`.
- `__PBI_TimeIntelligenceEnabled = 0` was retained, not rewritten to `1`.
- The report kept the ordinary `SourceRef` hierarchy binding. Desktop did not rewrite it to
  `PropertyVariationSource`.

A byte-level diff against the pre-open snapshot shows every semantic-model file identical apart from LF to
CRLF conversion and one additional trailing blank line each.

The **only material report normalization** was `RoleProjection` drill state: Desktop added `"active": true`
to the `Year` level projection and `"active": false` to `Quarter`, `Month` and `Day`. That is the schema's
`active` property, "used as part of drill operations". Everything else in the report is identical apart
from line endings.

## Evidence conclusion

For this Desktop-round-tripped marked-date-table configuration, the user-authored hierarchy is persisted
and consumed through ordinary hierarchy and relationship metadata rather than an Auto Date/Time-style
column variation.

### Caveat

This is Desktop round-trip evidence, not proof that every possible UI-authored marked-date-table workflow
can never produce a variation. In particular it does not cover marking a date table through the Desktop UI
on a model that started unmarked, or a model where Auto Date/Time was enabled at some point.

## Repository form

The repository retains the PBIP launcher, report definition, semantic-model definition, the `.platform`
files Desktop generated, and the required base theme. Desktop-local `.pbi` cache and settings directories
are excluded. Internal project artifact names remain as saved by Desktop, so the evidence-bearing project
files are not needlessly rewritten.

`StaticResources/SharedResources/BaseThemes/Fluent2-CY26SU08.json` is copied verbatim from
`desktop-incremental-refresh-evidence`; `report.json` requires `themeCollection` and the base theme is
referenced through `resourcePackages`, so the file must be present for the reference to resolve.
