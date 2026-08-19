# Desktop UDF measure-consumer fixture

A Power BI Desktop-authored PBIP fixture in which **an ordinary semantic-model measure calls a DAX
user-defined function**, and a report visual consumes that measure.

It exists for one reason: it is the smallest real model in which **two different dependency branches
reach the same column, and only one of them explains that column's usage state.** That makes it the
regression guard for usage-reason selection.

## Provenance

| | |
|---|---|
| Origin | A copy of the `desktop-udf-references` fixture project, reopened in Power BI Desktop and extended |
| Power BI Desktop release | July 2026, `2.156.951.0 (26.07)` — inherited from `desktop-udf-references`; both carry the `Fluent2-CY26SU07` base theme [inferred from the copy, not separately re-checked in the About dialog] |
| Date authored | 2026-08-19 |
| Model mode | Import |
| Compatibility level | 1702 [verified by Power BI Desktop-authored fixture] |
| Data | **Synthetic**, inherited unchanged from `desktop-udf-references`: three rows of region/amount pairs created with **Home → Enter data** |

**The folders are still named `desktop-udf-references.*`** — the project was copied rather than renamed,
so Desktop kept the original artifact names and the scanned semantic model is called
`desktop-udf-references`, not `desktop-udf-measure-consumer`. That is genuine Desktop output and is
deliberately **not** tidied: renaming would mean hand-editing files whose whole value is that Desktop
wrote them.

## What was added

Two changes on top of `desktop-udf-references`, both made in Power BI Desktop:

1. A new measure on `Sales`, written in the DAX formula bar as `UDF Result = Doubled()`.
2. A **Card** visual bound to `UDF Result`.

Desktop accepted the measure and emitted it into `Sales.tmdl` in ordinary measure form
[verified by Power BI Desktop-authored fixture]:

```tmdl
	measure 'UDF Result' = Doubled()
		formatString: 0
		lineageTag: 5f5c59e4-d2c2-4c02-b920-78a4f4594516
```

There is nothing special about the serialization: a measure calling a user-defined function looks
exactly like any other measure. The `lineageTag` is Desktop-assigned.

The Card is at
`definition/pages/fedb54562442fb31d8fd/visuals/87d1f06d9f569954dc41/visual.json`, a `cardVisual` whose
single projection is `"queryRef": "Sales.UDF Result"`.

`functions.tmdl` is unchanged from `desktop-udf-references`, still holding all five functions.

## The point of this fixture: two branches, one explanation

`Sales[Amount]` has **two** incoming DAX dependencies [verified in repository]:

```
LIVE   Card ─▶ Sales[UDF Result] ─▶ Doubled() ─▶ Sales[Total Amount] ─▶ Sales[Amount]

DEAD   TotalOf() ─▶ Sales[Amount]
```

Both edges are real. `TotalOf = () => SUM(Sales[Amount])` genuinely references `Amount`.

But **`TotalOf` is never called by anything**. It is an uncalled function definition, so it sits on an
unused branch and is not why `Amount` is `IndirectlyUsed`. Only the live branch supports that state.

This is what makes the fixture useful: an implementation that picks "some incoming reference" looks
correct on every simpler model and is wrong here. Before the reason-selection fix, PBI Assure rendered
`Amount` as *Indirectly used* with **"Why: Referenced by [TotalOf]"** — a true statement about the graph
and a misleading explanation of the classification [verified by rendered HTML].

Note also that the live path runs **through a function node**. `Doubled` is not a `SemanticObjectUsage`
row and has no usage state of its own, so a reason-selection rule based only on the usage states of
public objects cannot follow this chain. Reachability has to come from the graph.

## Expected scan result

[verified in repository]

| Object | State |
|---|---|
| `Sales[UDF Result]` | `DirectlyUsed` — the Card references it |
| `Sales[Total Amount]` | `IndirectlyUsed` — reached through `Doubled()` |
| `Sales[Amount]` | `IndirectlyUsed` — reached through `Total Amount` |
| `Sales[Region]` | `ApparentlyUnused`, with the coverage marker |

The four `AnalysisLimitations` are the ones every model of this shape produces: culture, database,
model definition, and functions.

**These states were identical before and after the reason-selection fix.** That fix changed which
evidence is *shown*, never what was *classified*.

## What this fixture proves

- An ordinary semantic-model measure **can call a DAX user-defined function**, and Desktop serialises it
  as a plain measure expression
- PBI Assure already followed that consumer path correctly before this fixture existed — the classifier
  was never the problem
- A model object can be reached by **both a live and a dead branch at once**
- A live dependency path can run **through a function node that has no usage row**

## What this fixture does NOT prove

- Anything about **report-level measures or visual calculations** calling a UDF. Those remain the unread
  UDF-consumer gap; this fixture contains neither
- Anything about other Desktop versions, locales, or DirectQuery/Direct Lake models
- That the Desktop version is exactly the one recorded above — it is inherited from the source project
  rather than re-verified in the About dialog

## Do not regenerate this fixture from hand-written TMDL

If these files are lost they must be **re-authored in Power BI Desktop**, by copying
`desktop-udf-references`, adding `UDF Result = Doubled()` in the formula bar, and placing a Card on it. A
hand-written PBIP proves nothing about what Desktop emits.

Deliberately not committed: `.pbi/cache.abf`, `.pbi/localSettings.json` (both the report's and the
semantic model's), and the `before-reopen.SemanticModel` snapshot, which was moved out of the project
folder before the scan that produced this evidence.

## One difference from Desktop's bytes: line endings

Desktop wrote these files with CRLF. The repository's `.gitattributes` sets `* text=auto eol=lf`, so
they are stored and checked out with LF. Content is otherwise preserved exactly — no reformatting, no
GUID rewriting, no whitespace tidying. This matches the other Desktop-authored fixtures.
