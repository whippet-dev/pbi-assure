# KPI and Detail Rows semantic-dependency investigation

**Date:** 2026-08-24 · **Against commit:** `8c55be3` · **Status:** evidence incomplete; no
production change, fixture or classification contract added.

## Question

Can a measure or column that is referenced only by a measure KPI or a Detail Rows definition be
reported as apparently unused by PBI Assure?

## What the current code does

`TmdlSemanticModelParser.ParseTable` retains a measure's ordinary expression, but does not retain a
`kpi` child block or a table/measure `detailRowsDefinition` block. Its table-child loop skips unknown
blocks without corrupting subsequent declarations. `SemanticMeasureInventory` and
`SemanticTableInventory` therefore expose neither construct to the dependency graph.

`SemanticDependencyAnalyzer` consequently adds no edge for either construct. The scan also has no
block-level limitation detection for these forms, so an absence-state result is currently marked
`Established`, not qualified by a coverage warning.

The later implementation path is contained: `DaxReferenceExtractor` already extracts qualified and
unqualified DAX reference candidates, and `SemanticDependencyAnalyzer.AddDaxDependencies` already
resolves them in an owning-table context and creates a non-root edge from an owning model node. With a
report-used owner, that normal reachability path would give the referenced object `IndirectlyUsed`; it
does not require a new semantic-usage state. Exact Desktop persistence must determine the source node,
DAX context, evidence path and whether a dedicated dependency kind is useful.

## Local discovery, not fixture evidence

The ignored local `IT Spend Analysis Sample` PBIP has a measure-level shape in
`Fact.tmdl`:

```tmdl
measure Actual/Plan = [Actual]
    kpi
        targetExpression = 'Fact'[Plan]
        statusExpression = ```
            var x='Fact'[Actual/Plan]/'Fact'[_Actual/Plan Goal] return ...
            ```
```

This establishes a useful candidate spelling for `kpi`, an explicit qualified target reference, and a
fenced status expression. It does **not** establish the requested behaviour: the project has no
recorded Desktop authoring or save/reopen provenance, contains no `trendExpression` or
`detailRowsDefinition`, and `Plan` is also directly used by a report visual. It must not be copied into
the committed fixtures or presented as Desktop evidence.

The current scan of that sample confirms the parser gap rather than the desired classification case:

| Object | Current result | Why this is not the required proof |
|---|---|---|
| `Fact[Actual/Plan]` | `ApparentlyUnused` | It owns the skipped KPI block but is not report-used. |
| `Fact[Plan]` | `DirectlyUsed` | A visual names it directly, as well as the skipped KPI target expression. |

The emitted dependency list has the ordinary `Actual/Plan` -> `Actual` DAX edge, but no KPI-derived
edge to `Plan` or to the status-expression reference.

No committed TMDL fixture (114 files scanned) and no local sample TMDL beyond this KPI sample (188
files scanned) contains `detailRowsDefinition`. No scanned TMDL contains `trendExpression`.

## Required Desktop evidence fixture

Do not hand-author this fixture. Create one small PBIP in Power BI Desktop, then save, close, reopen and
save again before sanitising a regression copy. Record Desktop version, authoring steps and the exact
persisted blocks.

The model needs only these controlled dependencies:

| Directly report-used owner | Metadata-only dependency | Control |
|---|---|---|
| `KPI Base` | `KPI Target Only` via target expression | `Unused Measure Control` |
| `KPI Base` | `KPI Status Only` via status expression | |
| `KPI Base` | `KPI Trend Only` via trend expression | |
| `Detail Rows Base` | `EvidenceData[Detail Rows Only]` via the Desktop-authored Detail Rows definition | `EvidenceData[Unused Column Control]` |

Put only `KPI Base` and `Detail Rows Base` in ordinary report field wells. Verify every sacrificial
object and both controls are absent from all report field wells, filters, visual formatting expressions
and unrelated model metadata. Exercise every Detail Rows ownership form that the current Desktop UI can
persist (measure and/or table); do not assume its TMDL spelling in advance.

The saved/reopened fixture must establish the exact locations and expression forms for all three KPI
properties and Detail Rows. Only then can a sanitised fixture assert the intended current gap:
`KPI Base` and `Detail Rows Base` directly used; the four metadata-only objects apparently unused today;
both controls apparently unused. If that result holds, implementation is justified as a narrow parser
and graph extension using existing DAX reachability.

## Conclusion

The parser gap is confirmed in the repository, and a local sample makes KPI persistence plausible, but
the necessary Desktop-authored, isolated evidence for classification and for Detail Rows is absent.
Implementation is **not justified yet**.
