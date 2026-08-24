# KPI and measure Detail Rows dependency evidence

**Date:** 2026-08-24 · **Against commit:** `f7f8528` · **Status:** implemented from a round-tripped
Power BI Desktop fixture.

## Desktop evidence

The external `desktop-kpi-detailrows-evidence-final` PBIP was saved, closed, reopened and saved again.
Its `EvidenceData.tmdl` retained these exact measure-owned forms:

```tmdl
measure 'KPI Base' = SUM(EvidenceData[Amount])
    kpi
        targetExpression = 'EvidenceData'[KPI Target Only]
        statusExpression = 'EvidenceData'[KPI Status Only]
        trendExpression = 'EvidenceData'[KPI Trend Only]

measure 'Detail Rows Base' = SUM(EvidenceData[Amount])
    detailRowsDefinition =
        SELECTCOLUMNS(
            EvidenceData,
            "Detail Rows Only",
            EvidenceData[Detail Rows Only]
        )
```

The two owner measures are each directly used by a report card. The four target objects are absent from
report field wells. `Category`, `Unused Measure Control` and `Unused Column Control` are also absent.
In Desktop, `DETAILROWS([Detail Rows Base])` successfully returned the configured Detail Rows projection.

## Product treatment

PBI Assure now retains only the three measure-KPI expression properties and a measure-owned
`detailRowsDefinition` expression in process. It sends each through the existing DAX reference extractor
with the owning measure as source. The resulting normal `Dax` edges provide ordinary reachability and
the existing **Why: Referenced by …** explanation; no usage state, root or report surface is added.

The expressions are intentionally `JsonIgnore` implementation data. JSON schema remains `0.26`, its
shape is unchanged, and CSV is unchanged. The emitted existing dependency list gains the resolved normal
`Dax` edges, as it does for any other analysed measure expression.

The committed [sanitised fixture](../../tests/fixtures/kpi-detailrows-sanitized/README.md) contains only
the observed semantic shape, direct card bindings and safe placeholder data. It is not untouched Desktop
output.

## Verified result

| Object | Classification |
|---|---|
| `KPI Base` | `DirectlyUsed` |
| `Detail Rows Base` | `DirectlyUsed` |
| `KPI Target Only` | `IndirectlyUsed` |
| `KPI Status Only` | `IndirectlyUsed` |
| `KPI Trend Only` | `IndirectlyUsed` |
| `Detail Rows Only` | `IndirectlyUsed` |
| `Amount` | `IndirectlyUsed` |
| `Category` | `ApparentlyUnused` |
| `Unused Measure Control` | `ApparentlyUnused` |
| `Unused Column Control` | `ApparentlyUnused` |

## Boundary

No table-owned Detail Rows definition was observed. That form remains unsupported and must not be
inferred from this measure-owned evidence. The parser supports both single-line and fenced/multiline
expressions through its existing TMDL expression readers.
