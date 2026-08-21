# Desktop incremental-refresh baseline

This is the pre-policy control for `../desktop-incremental-refresh-evidence/`.

## Provenance

- Source path:
  `C:\Users\morty\Downloads\PBI Assure Testing\desktop-incremental-refresh-evidence-baseline`
- The folder was copied after `RangeStart` and `RangeEnd` and both parameter-filtered tables existed, but
  before any incremental-refresh policy was configured.
- Local `.pbi` cache/settings and `diagramLayout.json` are excluded. The Desktop version was not retained.

## Control purpose

`FactEvents_Policy` and `FactEvents_FilterOnly` both contain:

```powerquery
[EventDate] >= RangeStart and [EventDate] < RangeEnd
```

Neither table has a `refreshPolicy` block. Comparing this fixture with the policy fixture proves that
reserved parameters and parameter-based filtering are prerequisites, not evidence that a policy has
actually been configured.

**[verified from persisted files]** the only semantic-definition file changed by the later policy
configuration is `definition/tables/FactEvents_Policy.tmdl`, where Desktop added the explicit policy
block. All model objects, M filters and report fields in this baseline remain the comparison control.
