# Desktop iterator no-home-column fixture

A Power BI Desktop-authored PBIP fixture in which **a measure on a measures-only table iterates another
table and names that table's column unqualified**:

```dax
Iterator Prove = SUMX(Dim, [X])
```

The measure lives on `'Measures (2)'`, which has no column named `X`. Only `Dim` has one. Desktop accepts
the expression, saves it in exactly that form, reopens it, and does not rewrite it to `Dim[X]`.

It exists for one reason: it is the real-Desktop reproducer of a case that was previously only
synthetic — an unqualified column reference whose only possible persisted binding is the column of the
table an evidenced iterator puts in row context, with **no same-named column on the expression's own
table to fall back on**. Before this fixture, PBI Assure retained `[X]` as a NotFound reference, raised
`PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE`, and qualified every absence result in the model.

## Provenance

| | |
|---|---|
| Origin | Authored from scratch in Power BI Desktop as a PBIP project |
| Power BI Desktop release | August 2026 [inferred from the `Fluent2-CY26SU08` base theme the report carries; not separately checked in the About dialog] |
| Date supplied | 2026-09-21 |
| Model mode | Import |
| Compatibility level | 1606 [verified by Power BI Desktop-authored fixture] |
| Data | **Synthetic**: three rows of `Key`/`X` pairs (1/10, 2/20, 3/30) created with **Home → Enter data**; the measures table is a one-column **Enter data** table with no rows |

Everything under the project folder is Desktop output. `.pbi/cache.abf` and `.pbi/localSettings.json`
are excluded by `.gitignore` as for every fixture. The table is named `'Measures (2)'` and its only
column `1` because that is what Desktop's Enter data dialog produced; neither is tidied, because the
value of the fixture is that Desktop wrote it.

## What Desktop persisted

`definition/tables/Measures (2).tmdl` [verified by Power BI Desktop-authored fixture]:

```tmdl
	measure 'Iterator Prove' = SUMX(Dim, [X])
		formatString: 0
		lineageTag: ba56133d-4290-4dfd-8613-12baf1c49fa6

	measure 'Unused Control' = 1
		formatString: 0
		lineageTag: a9ebd43d-33a4-431b-959d-98742423b3b8

	column 1
		dataType: string
```

`definition/tables/Dim.tmdl` declares columns `Key` and `X`. The report has one page with one
`cardVisual` whose single projection is `'Measures (2)'[Iterator Prove]`.

## What this fixture proves

- Desktop persists an unqualified iterator row reference **as written**. The measure's home table
  contributes nothing to its binding; `[X]` can only be `Dim[X]`, because `SUMX` names `Dim` as its
  entire first argument and nothing between the iterator and the reference changes the row.
- PBI Assure's expression reader keeps the iterator's persisted source table and the resolver binds
  the reference against it, so:

  | Object | State | Confidence |
  |---|---|---|
  | `'Measures (2)'[Iterator Prove]` | DirectlyUsed | Established |
  | `Dim[X]` | IndirectlyUsed — referenced by `Iterator Prove` | Established |
  | `'Measures (2)'[Unused Control]` | ApparentlyUnused | Established |
  | `Dim[Key]` | ApparentlyUnused | Established |
  | `'Measures (2)'[1]` | ApparentlyUnused | Established |

  No unresolved reference and no `PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE` remain.

The regression contract is `DesktopIteratorNoHomeColumnFixtureTests`; the synthetic controls for the
forms that must stay unresolved (nested iterators, table expressions and variables as the source,
unaccounted calls between the iterator and the reference, iterators the reader has no evidence for,
unbalanced syntax) are in `DaxUnqualifiedCollisionTests`.

## What this fixture does NOT prove

- **How Desktop resolves `[X]` when the home table also has a column `X`**, or when a measure named
  `X` exists. Neither is in this model; PBI Assure keeps its existing collision handling for both.
- **Nested iterators or iterators over table expressions.** The only iterator here is a single `SUMX`
  over a bare table name.
- **Any iterator other than `SUMX`.** `FILTER` and `SELECTCOLUMNS` share the reader's mechanism, but
  this fixture does not exercise them, and iterators outside that list are not given a row context.
- **Other Desktop versions, DirectQuery or Direct Lake models.**
