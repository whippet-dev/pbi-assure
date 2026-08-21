# Aggregation `alternateOf` regression fixture

This is a deliberately small, synthetic/sanitised PBIP fixture. It contains no connection, workspace,
Fabric or organisational metadata.

## Provenance

**[verified by Desktop-authored fixture]** Power BI Desktop's **Manage aggregations** UI persisted the
same `alternateOf` syntax in a separate project that was saved, closed, reopened and saved again. That
external evidence project used a real Fabric Warehouse connection and is intentionally not committed.

**[verified in repository]** this safe fixture reproduces only the observed semantic shape:

- `AggSales[DateKey]` and `[ProductKey]` have `baseColumn` only;
- `AggSales[Amount]` has `summarization: sum` and `baseColumn`;
- `AggSales[ControlUnused]` has no `alternateOf` block;
- the report uses only `FactSales[DateKey]` and `[Amount]`.

The fixture is a parser/dependency regression fixture, not an untouched byte-for-byte Desktop project.
