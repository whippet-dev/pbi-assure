# Desktop UDF references fixture

A Power BI Desktop-authored PBIP fixture preserving how Desktop serialises **DAX user-defined functions
whose bodies reference model objects**.

It exists to close the single gap `desktop-semantic-constructs` left open. That fixture proved functions
serialise into `definition/functions.tmdl`, but its one function used only its own parameter, so it could
not show how a reference to a table, a column or a measure is actually written — and that was the last
qualifying cause on its usage classifications.

## Provenance

| | |
|---|---|
| Power BI Desktop release | July 2026 |
| Product version | `2.156.951.0 (26.07)` — the same release and machine as `desktop-semantic-constructs`; both carry the `Fluent2-CY26SU07` base theme |
| Date authored | 2026-08-19 |
| Model mode | Import |
| Compatibility level | 1702, as first written — no upgrade occurred here |
| Data | **Synthetic.** Created with **Home → Enter data**; no external source, no credentials, no gateway |

The embedded partition decodes to three rows of region/amount pairs (`West/500`, `East/300`,
`West/450`). Nothing here is real organisation data.

## Model

One table, `Sales`, with columns `Region` and `Amount` and one measure:

```dax
Total Amount = SUM(Sales[Amount])
```

Deliberately no `Date` column, so Power BI generated **no** Auto Date/Time tables and no relationships.
`desktop-semantic-constructs` already covers generated date artefacts; keeping them out here leaves the
function dependencies as the only thing that can reach an object, which is what makes the usage
assertions in `FunctionDependencyTests` meaningful.

Also absent: roles, perspectives, and any second table. This fixture is deliberately narrow.

## How the functions were authored — TMDL-View-authored

Power BI Desktop has no GUI for DAX user-defined functions at this version, so all five were written in
the **TMDL view**, one per script tab, each applied with `createOrReplace`. The five scripts are
preserved under `TMDLScripts/` exactly as Desktop saved them:

| Script | Body | What it is evidence for |
|---|---|---|
| `Script 1` | `function TotalOf = () => SUM(Sales[Amount])` | a **qualified column** reference |
| `Script 2` | `function Doubled = () => [Total Amount] * 2` | an **unqualified** reference, which is a measure |
| `Script 3` | `function RowCount = () => COUNTROWS(Sales)` | a **bare table** reference |
| `Script 4` | `function ShadowAmount = (Amount : NUMERIC) => Amount * 2` | a **parameter that shadows a column name** |
| `Script 5` | `function Quadrupled = () => Doubled() * 2` | a **function calling another function**, declared earlier |

`Script 4` is the sharp one. The model has `Sales[Amount]`, and the function takes a parameter also
called `Amount`. A parser that treated the body as ordinary DAX would invent a dependency on the column
that does not exist.

`Script 5` also pins declaration order: `Quadrupled` is written after `Doubled`, so anything that
resolves callees in file order happens to work here — which is why `FunctionDependencyTests` includes a
reversed-order case built synthetically.

## Emitted content

All five functions land in **one** `definition/functions.tmdl`, in authoring order, each with a
`lineageTag` Desktop assigned:

```tmdl
function TotalOf = () => SUM(Sales[Amount])
	lineageTag: 6a4797bc-df7d-436a-b429-c185ad039f1c

function Doubled = () => [Total Amount] * 2
	lineageTag: 0c9c168e-815c-4245-b154-3a9155efb9fe

function RowCount = () => COUNTROWS(Sales)
	lineageTag: 1e26aedc-b2ed-4a0c-b548-0e598735f34f

function ShadowAmount = (Amount : NUMERIC) => Amount * 2
	lineageTag: 0aafd538-75de-4400-9cea-684b91a82d3a

function Quadrupled = () => Doubled() * 2
	lineageTag: 0c7ea8c5-524e-493f-a163-00e5f8772010
```

Three things to note, because each one shaped the parser:

1. **The declaration is `function <Name> = (<params>) => <body>`.** The name is the declared identifier;
   the parameter list belongs to the expression, not to the declaration line. Splitting on the first `=`
   yields a body that still starts with `(...)=>`.
2. **A parameter carries a type hint** in `name : TYPE` form. `NUMERIC` here; other hints exist and are
   not exercised.
3. **No function was given a `ref` line in `model.tmdl`.** That file lists `ref table Sales` and
   `ref cultureInfo en-US` and nothing else. Functions are discovered by the file existing, not by being
   referenced from the model root — do not build anything on a `ref function` line.

`database.tmdl` is the usual two lines:

```tmdl
database
	compatibilityLevel: 1702
```

## TMDL view editor artefacts

Authoring through the TMDL view produced `TMDLScripts/Script 1.tmdl` … `Script 5.tmdl` and
`TMDLScripts/.pbi/tmdlScripts.json` (tab order plus `"defaultTab": "Script 5"`). These are **editor
state, not model definition**. They carry the `.tmdl` extension inside the semantic-model folder, so
they are enumerated as candidate definition artifacts and the registry classifies them as packaging —
`DesktopUdfReferencesFixtureTests` asserts they produce no limitation.

## Reopen and save stability

The project was closed completely, reopened with no warning or error, saved without any intentional
change, and closed again. A snapshot taken before reopening was compared file by file.

**Every semantic-model definition file, every TMDL view script, `.platform`, `definition.pbism` and
`.pbi/editorSettings.json` was byte-for-byte unchanged.** `functions.tmdl` in particular survived
untouched: no reordering, no re-tagging, no whitespace normalisation.

Exactly one file differed — `.pbi/localSettings.json`, whose encrypted `securityBindingsSignature`
changed — and it is not committed here.

This fixture therefore has **no pre-save state to protect**, unlike `tab-order-states`. Reopening it in
Desktop is not known to be destructive. Even so, prefer working on a copy.

## What this fixture proves

- A UDF body writes a **qualified column** reference in ordinary `Table[Column]` form
- An **unqualified** `[Name]` inside a UDF is a measure reference; a function has no owning table for it
  to resolve against
- A **bare identifier matching a table** is a table reference
- A **parameter may share a name with a model column**, so parameter names are local symbols that must
  shadow model objects rather than resolve to them
- A UDF may **call another UDF**, and the call is written as a bare `Name()` with no qualification
- All functions in a model serialise into **one** `definition/functions.tmdl`, in authoring order
- Desktop assigns each function a `lineageTag`
- `model.tmdl` has **no `ref function` line**
- Reopening and saving normalises nothing, at this Desktop version

## What this fixture does NOT prove

- **Where a UDF is called from outside the model definition.** Microsoft documents that visual
  calculations and report-level measures can call one. Neither is read by PBI Assure, and neither
  appears here, which is why `definition/functions.tmdl` remains a **partially analysed** construct that
  still records a limitation. A function that looks unreferenced may be called from metadata nobody read
- **Whether a UDF name can be namespaced with dots.** Every name here is a single identifier. A dotted
  name would not tokenise as one identifier in the current reference extractor
- **What other parameter type hints look like.** Only `NUMERIC` appears, on one parameter
- **Multi-parameter or multi-line function bodies.** Every function here is one line, and only
  `ShadowAmount` takes a parameter at all
- **Return type annotations, optional parameters, or `VAR`/`RETURN` inside a function body**
- **Anything about other Desktop versions, locales, or DirectQuery/Direct Lake models.** Import only,
  one version, one machine

## Do not regenerate this fixture from hand-written TMDL

If these files are lost, they must be **re-authored in Power BI Desktop**. A hand-written PBIP proves
nothing about what Desktop emits, which is this fixture's entire purpose, and describing hand-written
files as Desktop-authored would corrupt the evidence base other decisions rest on.

Deliberately not committed: `.pbi/cache.abf` (an Analysis Services backup that can contain data),
`.pbi/localSettings.json` (machine-specific, contains an encrypted signature), and the
`before-reopen.SemanticModel` comparison snapshot.

## One difference from Desktop's bytes: line endings

Power BI Desktop wrote these files with CRLF line endings. The repository's `.gitattributes` sets
`* text=auto eol=lf`, so they are stored and checked out with LF. **Content is otherwise preserved
exactly** — no reformatting, no GUID rewriting, no whitespace tidying.

This matches how the existing Desktop-authored fixtures are already stored. If a future question depends
on Desktop's exact line endings, this fixture cannot answer it; compare against a freshly authored
project instead.
