# Desktop entity partition evidence — composite model over a Power BI semantic model

A Power BI Desktop composite model whose remote tables are persisted as `entity` partitions. It is
the smallest Desktop-authored proof of how a DirectQuery connection to a published semantic model
is written to a Power BI Project: no M for the remote tables, only a `source` block naming the shared
expression that connects to the remote model.

## Provenance

- Authored in Power BI Desktop 2.157.1354.0 (August 2026) on 2026-09-18 and saved as a Power BI
  Project; supplied as `samples-local/CompositeEvidence`.
- The remote model is the `Tab Order Test Sample` project from this repository's local samples,
  published to a shared workspace named `pbi-assure`. The excluded local settings record the
  composite-model consent (`userConsent.compositeModel: true`), consistent with the documented
  live-connect → **Make changes to this model** → **Add a local model** path; one Enter data table
  (`localData`) was added locally.
- The definition files are byte-for-byte Desktop output (CRLF as written; Git normalises them on
  commit). Only `.pbi/` (local settings, security-binding signatures, `cache.abf`) is excluded.
- The exact click path and a save/close/reopen round trip were not independently observed by the
  tool that promoted this fixture.

## Persisted evidence

**[verified from persisted files]**

- `tables/testTable.tmdl` — the remote table:
  `partition testTable = entity` / `mode: directQuery` / a bare `source` block with
  `entityName: testTable` and `expressionSource: 'DirectQuery to AS - Tab Order Test Sample'`.
  No `schemaName`. The table and every column carry `sourceLineageTag`; columns keep an ordinary
  `sourceColumn` equal to their own name.
- `tables/LocalDateTable_0f7e2929-….tmdl` — the remote model's auto date/time table is persisted the
  same way (`= entity`, same `expressionSource`), with `showAsVariationsOnly` but **without** the
  `__PBI_LocalDateTable` annotation Desktop writes for a locally generated date table.
- `expressions.tmdl` — the single shared expression:
  `AnalysisServices.Database("powerbi://api.powerbi.com/v1.0/myorg/pbi-assure", "Tab Order Test Sample")`,
  then `Cubes = Table.Combine(Source[Data])` and `Cube = Cubes{[Id="Model", Kind="Cube"]}[Data]`,
  annotated `PBI_IncludeFutureArtifacts = True`.
- `model.tmdl` — the expression is listed in `PBI_QueryOrder` beside the local query; nothing else
  is partition-specific (`defaultPowerBIDataSourceVersion: powerBI_V3`, `valueFilterBehavior`,
  `dataAccessOptions`).
- `relationships.tmdl` — `testTable.Date` → the remote date table (the Date column's `variation`).
- `tables/localData.tmdl` — an ordinary local Enter data `m` partition, so both partition kinds
  coexist in one model.
- The report's single `tableEx` visual projects `testTable[Category]` and `Sum(testTable[Value])`
  and carries matching `filterConfig` entries, so the remote table is bound by the report.

## Product consequence

Before this fixture, the shared expression had no incoming reference and was classified an
orphan (`PBI-QUERY-002`), the remote tables had no Power Query context, and the Analysis Services
source was attributed only to that "unused" expression. An entity partition now depends on the
expression its `expressionSource` names, the expression is a supporting query, and its connector is
attributed to the table. A partition whose `expressionSource` is missing or undefined raises
`PBI-LIMIT-MODEL-PARTITION-SOURCE` instead of being skipped.

The evidence covers DirectQuery to a Power BI semantic model only. Direct Lake models persist the
same partition source type, but no Direct Lake file has been observed here; nothing Direct
Lake-specific is inferred from this fixture.
