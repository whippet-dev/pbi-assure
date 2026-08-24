# KPI and Detail Rows dependency regression fixture

This is a deliberately small, sanitised PBIP regression fixture derived from the external
`desktop-kpi-detailrows-evidence-final` Power BI Desktop project. It is not claimed to be untouched
Desktop output.

## Provenance

**[verified by Desktop evidence]** The external project was saved, closed, reopened and saved again.
Its measure `kpi` block persisted `targetExpression`, `statusExpression` and `trendExpression`; its
measure `detailRowsDefinition` persisted a multiline `SELECTCOLUMNS` table expression. In Desktop,
`DETAILROWS([Detail Rows Base])` successfully returned the configured Detail Rows projection.

The source project's two card visuals directly use only `KPI Base` and `Detail Rows Base`. The metadata
targets, both controls and `Category` do not occur in report field wells.

## Repository form

The fixture preserves only the observed measure-owned expressions, the necessary source columns and two
direct report bindings. It removes lineage tags, local state, cached data, Desktop scripts, themes and
the source project's embedded Enter Data payload. The placeholder M partition has no source connection.

This fixture proves no table-owned Detail Rows form. PBI Assure supports only the measure-owned shape
observed here.
