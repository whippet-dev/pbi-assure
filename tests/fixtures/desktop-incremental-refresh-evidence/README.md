# Desktop incremental-refresh policy evidence

This synthetic Power BI Desktop PBIP fixture records an explicit table-owned incremental-refresh policy.
It contains no private, organisational or production data.

## Provenance

- Source path: `C:\Users\morty\Downloads\PBI Assure Testing\desktop-incremental-refresh-evidence`
- The project was saved in Power BI Desktop, fully closed, reopened, checked and saved again.
- Local `.pbi` cache/settings, generated outputs and `diagramLayout.json` are excluded from the repository
  copy. The Desktop version was not retained and is not inferred.
- The pre-policy control is retained separately at `../desktop-incremental-refresh-evidence-baseline/`.

## Controlled project

The model contains DateTime parameters `RangeStart = #datetime(2026, 1, 1, 0, 0, 0)` and
`RangeEnd = #datetime(2026, 2, 1, 0, 0, 0)`. `FactEvents_Policy` and
`FactEvents_FilterOnly` contain the same six synthetic rows and the same M filter:

```powerquery
[EventDate] >= RangeStart and [EventDate] < RangeEnd
```

Both tables contain `EventID`, `EventDate`, `LastModified` and `Amount`. The report visual uses only
`FactEvents_Policy[EventID]`, `[EventDate]` and `[Amount]`.

Only `FactEvents_Policy` has an explicit `refreshPolicy` block. Desktop retained:

- `policyType: basic`
- a two-year rolling/archive window
- a 30-day incremental refresh window
- `incrementalPeriodsOffset: -1`
- a polling expression using `FactEvents_Policy[LastModified]`
- the policy source M expression
- no explicit `mode` property

## Evidence boundary

**[verified from persisted files]** the policy block is the only changed semantic-definition content
between the baseline and policy projects. The filter-only table still uses both parameters but has no
policy block.

**[manually verified in Power BI Desktop]** after the round trip, incremental refresh remained on with
two years archived, 30 days refreshed, complete days and change detection enabled, `LastModified`
selected, and real-time DirectQuery off. Desktop warned that it could not confirm query folding because
the synthetic source uses inline `#table` data. Codex did not independently perform those Desktop UI
interactions.

**[verified in repository]** PBI Assure treats only the explicit `refreshPolicy` block as policy
evidence. It retains the authored settings, presents them on the semantic table, and treats an explicitly
qualified polling column as a structural dependency. It does not infer policy from parameters or M
filters and does not claim folding, service refresh or generated partition behaviour.
