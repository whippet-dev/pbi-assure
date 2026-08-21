# Aggregation `alternateOf` Desktop evidence

**Date:** 2026-08-21

## Evidence labels

- **[verified by Desktop-authored fixture]** observed in the external, current Power BI Desktop project.
- **[verified in repository]** covered by the committed sanitised regression fixture and automated tests.
- **[design decision]** the bounded product treatment adopted from that evidence.

## Desktop observation

**[verified by Desktop-authored fixture]** a DirectQuery `FactSales` table and an Import `AggSales` table
were configured through **Manage aggregations**. Desktop accepted the mappings without warning and
automatically hid `AggSales`. After save, close, reopen and save again, the mappings remained.

Desktop persisted the mapping only on the aggregation-side column:

```tmdl
column DateKey
    alternateOf
        baseColumn: FactSales.DateKey

column Amount
    alternateOf
        summarization: sum
        baseColumn: FactSales.Amount
```

The GroupBy-style mappings had `baseColumn` only; `summarization` was absent. `AggSales[ControlUnused]`
had no `alternateOf` block. `FactSales.tmdl` contained no reciprocal aggregation metadata.

Before this feature PBI Assure found two directly used columns, zero structurally required columns and
six apparently unused columns. It could not distinguish the three explicitly mapped `AggSales` columns
from the unmapped control, or `FactSales[ProductKey]` from the unrelated unused `FactSales[SaleID]`.

## Adopted treatment

**[design decision]** a resolved, explicit `alternateOf` mapping is model-structure evidence. PBI Assure
creates an `AggregationMapping` edge from the aggregation-side column to the exact resolved detail
column and seeds the aggregation-side column as a structural root. Both endpoints are therefore protected
from an `ApparentlyUnused` classification while ordinary report usage remains higher precedence.

This proves configured metadata only. It does **not** establish a runtime aggregation hit, query
acceleration, successful refresh or Power BI Service aggregation behaviour.

## Regression-fixture provenance

**[verified in repository]** `tests/fixtures/aggregation-alternateof-sanitized` reproduces the observed
TMDL shape and report-use controls without copying the Desktop project's Fabric Warehouse connection or
other environment-specific metadata. It is explicitly synthetic/sanitised, not an untouched copy of the
Desktop-authored bytes.
