# Desktop-authored semantic fixture plan

**Date:** 2026-08-19 · **Against commit:** `dcbde4a` on `master` · **Status: evidence-gathering and planning. No production code, tests or fixtures were created.**
**Repository:** `whippet-dev/pbi-assure`

Standalone — no prior document or chat context is required.

**Purpose.** PBI Assure's semantic-model definition-file registry was built from Microsoft documentation.
This document plans the Power BI Desktop-authored fixture needed to check documented structure against
what Desktop actually emits, and reports what the *existing* repository fixtures already prove.

## Evidence labels used throughout

| Label | Meaning |
|---|---|
| **[verified in repository]** | Checked against code or committed fixtures at `dcbde4a` |
| **[verified by Microsoft primary documentation]** | Stated in current Microsoft Learn documentation |
| **[verified by Power BI Desktop-authored fixture]** | Observed in files a real Desktop emitted |
| **[inferred]** | Reasoned, not directly evidenced |
| **[design decision]** | A proposal |

Documentation is **never** promoted to Desktop-observed. Nothing hand-written is described as
Desktop-authored.

---

## 0. Headline finding — read this first

**Every Power BI Desktop-authored semantic model already produces three analysis limitations, and under
the propagation rule proposed in `unsupported-construct-design.md` that would qualify every object in
every model, always.**

Scanning the committed Desktop-authored fixture `tests/fixtures/tab-order-states` at `dcbde4a` produces
**[verified in repository]**:

```
AnalysisLimitations: 3
  PBI-LIMIT-MODEL-CULTURE    DependencyEffectUnknown   definition/cultures/en-US.tmdl
  PBI-LIMIT-MODEL-DATABASE   DependencyEffectUnknown   definition/database.tmdl
  PBI-LIMIT-MODEL-SETTINGS   DependencyEffectUnknown   definition/model.tmdl
```

All three of those files are emitted by Desktop for **every** model — confirmed across all three
Desktop-authored fixtures in the repository **[verified by Power BI Desktop-authored fixture]**.

The proposed propagation rule qualifies absence states when a model has any limitation whose impact is
`MayCreateDependencies` **or** `DependencyEffectUnknown`. Since all three are `DependencyEffectUnknown`,
**100% of real models would be qualified**.

This is exactly the failure the design named for *property*-level detection — "a limitation mechanism
that fires on essentially every model is worse than the current silence" — but it arises at **file**
level, which the design did not anticipate.

**Consequence:** the propagation slice must not ship before §6 is acted on. The fix is cheap and the
evidence for it is largely already in the repository.

---

## 1. What the existing repository fixtures already prove

Three fixtures are Power BI Desktop-authored: `tab-order-states`, `grouped-tab-order`,
`model-reference-context`. (`privacy-canary` is explicitly synthetic and is excluded from all claims
here.) Each contains exactly these tracked semantic-model files **[verified in repository]**:

```
<name>.SemanticModel/definition.pbism
<name>.SemanticModel/definition/cultures/en-US.tmdl
<name>.SemanticModel/definition/database.tmdl
<name>.SemanticModel/definition/model.tmdl
<name>.SemanticModel/definition/tables/<table>.tmdl
<name>.SemanticModel/.pbi/editorSettings.json
<name>.SemanticModel/.platform
```

**[verified by Power BI Desktop-authored fixture]** from this:

| Claim | Evidence |
|---|---|
| Cultures use the **directory** form `definition/cultures/<culture>.tmdl` | present in all three fixtures — confirms the `dcbde4a` registry correction was right |
| `database.tmdl` and `model.tmdl` are emitted for every model | present in all three |
| `dataSources.tmdl` is **not** emitted for a simple import model | absent from all three |
| `relationships.tmdl` / `expressions.tmdl` are omitted when the model has none | absent from all three; present only in the synthetic fixture |
| `.pbi/cache.abf` is correctly **not** committed | `git ls-files` shows it untracked; `.gitignore` covers it |
| `definition.pbism` reports `"version": "4.2"` | TMDL format, consistent with documented version ≥ 4.0 |

### Emitted content, verbatim

`database.tmdl` — **the entire file**:

```tmdl
database
	compatibilityLevel: 1606
```

`model.tmdl` — **the entire file**:

```tmdl
model Model
	culture: en-US
	defaultPowerBIDataSourceVersion: powerBI_V3
	sourceQueryCulture: en-GB
	valueFilterBehavior: independent
	dataAccessOptions
		legacyRedirects
		returnErrorValuesAsNull

annotation __PBI_TimeIntelligenceEnabled = 1

annotation PBI_QueryOrder = ["testData"]

annotation PBI_ProTooling = ["DevMode"]

ref table testData

ref cultureInfo en-US
```

`cultures/en-US.tmdl` — **the entire file**:

```tmdl
cultureInfo en-US
```

**Analysis.** `model.tmdl` *does* name objects — `ref table testData`, `ref cultureInfo en-US`, and
`annotation PBI_QueryOrder = ["testData"]`. But `ref` declarations exist for **every** object in a
collection regardless of whether anything uses it; Microsoft documents them as preserving collection
ordering on round-trip **[verified by Microsoft primary documentation]**. They therefore carry **no usage
information** — treating them as usage would mark every table in every model as used, which would be
strictly wrong. `database.tmdl` and the default `cultures/en-US.tmdl` contain no object references at
all.

---

## 2. Minimum fixture set

**[design decision]** **Two fixtures from one authoring session**, plus one optional.

| # | Fixture | Covers | Priority |
|---|---|---|---|
| **A** | TMDL-format PBIP | roles, perspective, DAX UDF, model.tmdl, database.tmdl, cultures, tables — everything except TMSL | **Required** |
| **B** | TMSL-format PBIP | `model.bim` | Optional — see §4 |
| **C** | Translated culture | a `cultures/<x>.tmdl` containing actual translations | Optional — only if §6 culture recommendation is contested |

**Why one combined fixture rather than several.** Each construct serialises to its own distinct file, so
combining them cannot make the evidence ambiguous — the question is "what path and content does Desktop
emit for construct X", and each answer lands in a separate file. Splitting would multiply authoring
effort with no gain.

**Where combining would be wrong, and is therefore avoided:** fixture B must be a *separate project*, not
a re-save of A, because Microsoft documents that upgrading a project from TMSL to TMDL **cannot be
reverted** **[verified by Microsoft primary documentation]**.

---

## 3. Authoring paths — what Desktop can and cannot do

Researched against current Microsoft Learn documentation.

| Construct | Authoring path | Status |
|---|---|---|
| **RLS role** | **Modeling → Manage roles** (full GUI, with default and DAX editors) | **[verified by Microsoft primary documentation]** |
| **DAX UDF** | DAX query view, **TMDL view**, or Model view / Model explorer. GA since the June 2026 release; needs compatibility level 1702+ | **[verified by Microsoft primary documentation]** |
| **Perspective** | **TMDL view only — no GUI.** Microsoft states plainly: *"I need to create a perspective… However, I can't create or edit it using the graphical interface of Power BI Desktop. Solution: Open the TMDL view…"* | **[verified by Microsoft primary documentation]** |
| **Culture / translation** | **TMDL view only — no GUI.** Same source names translations among *"other semantic model metadata that lack a graphical interface"* | **[verified by Microsoft primary documentation]** |
| **model.tmdl, database.tmdl, cultures/en-US.tmdl** | Emitted automatically, no authoring needed | **[verified by Power BI Desktop-authored fixture]** |
| **dataSources.tmdl** | No authoring path found. Not emitted by any existing fixture. Probably requires a legacy provider data source rather than an M partition | **[inferred]** — see §8 |
| **model.bim** | Save a project **without** the TMDL format option enabled | **[verified by Microsoft primary documentation]** |

### The distinction that makes TMDL view acceptable evidence

**[design decision]** TMDL view authoring **counts as Desktop serialization**; editing PBIP files in an
external editor does **not**.

In TMDL view you write a script and press **Apply**, which executes it against the semantic model
*inside Desktop*; Desktop then serialises the model to disk on save. The bytes on disk are written by
Desktop. Microsoft documents both this flow and the separate flow of editing PBIP files externally (which
requires restarting Desktop to reload) **[verified by Microsoft primary documentation]**.

So a perspective created via TMDL view yields genuinely Desktop-emitted output. **A perspective file
typed by hand into `definition/perspectives/` does not, and must never be described as such.**

---

## 4. Manual procedure

Follow in order. Record everything §5 asks for as you go. The goal is the smallest possible model, not a
meaningful report.

### Step 0 — Record the environment

1. **Help → About** (or File → Options and settings → Options → Diagnostics). **Record the exact version
   string.** Every claim this fixture supports is version-scoped.
2. **File → Options and settings → Options → Preview features.** Look for *"Store semantic model using
   TMDL format"*. **Record whether it exists and whether it is already enabled.** Microsoft's
   documentation still calls it preview; the repository's existing fixtures are already TMDL, so it may
   have become default. Either answer is useful evidence.

### Step 1 — Build the model (no external data source)

3. **Home → Enter data.** Create a table named **`Sales`**:

   | Region | Amount | Date |
   |---|---|---|
   | West | 500 | 2026-01-15 |
   | East | 300 | 2026-02-20 |
   | West | 450 | 2026-03-10 |

   Use **Enter data** deliberately: it needs no credentials, no gateway and no external source, so the
   fixture is safe to commit and reproducible by anyone.
4. Confirm the column types are Text / Whole number / Date.
5. Add a measure on `Sales`: `Total Amount = SUM(Sales[Amount])`

### Step 2 — Row-level security (GUI)

6. **Modeling → Manage roles → New.** Name it **`RegionalManager`**.
7. Under **Select tables**, choose `Sales`. Select **Switch to DAX editor** and enter exactly:

   ```dax
   [Region] = "West"
   ```

8. **New** again. Name it **`DynamicUser`**, table `Sales`, DAX editor:

   ```dax
   [Region] = USERPRINCIPALNAME()
   ```

9. **Save.**

   Two roles are deliberate: they prove whether Desktop emits **one file per role**, and they capture
   both a static and a dynamic filter serialization.

### Step 3 — Save as a PBIP

10. **File → Save as**, choose the **.pbip** (Power BI project) type, name it
    **`desktop-semantic-constructs`**, and save it somewhere outside the repository for now.
11. If prompted to upgrade to TMDL, accept, and **record that the prompt appeared**.
12. **Record the emitted file tree at this point** before adding anything else.

### Step 4 — Perspective (TMDL view — no GUI exists)

13. **View → TMDL view.** Open a new empty tab and enter exactly this — it is Microsoft's own documented
    example adapted to our table names:

    ```tmdl
    createOrReplace
    	perspective SalesView
    		perspectiveTable Sales
    			perspectiveMeasure 'Total Amount'
    			perspectiveColumn Region
    ```

14. Press **Apply**. Record success or the exact error.

### Step 5 — DAX user-defined function (TMDL view)

15. New TMDL view tab, enter exactly this (Microsoft's documented example verbatim):

    ```tmdl
    createOrReplace
    	/// AddTax takes in amount and returns amount including tax
    	function AddTax = (amount : NUMERIC) => amount * 1.1
    ```

16. Press **Apply**.
17. **If a compatibility-level upgrade prompt appears, record both the current and required levels**,
    then accept. (UDFs are documented as needing level 1702+; the existing fixtures are at 1606, so this
    prompt is likely.)

### Step 6 — Culture / translation (best effort — do not invent syntax)

18. **No documented TMDL script for authoring a translation was found.** Attempt it only using TMDL
    view's built-in autocomplete (`Ctrl+Space`) after typing `createOrReplace` and `cultureInfo`.
19. **If you cannot produce it confidently, skip this step and record that it was skipped.** Do not
    guess at syntax. A default empty `cultures/en-US.tmdl` is emitted regardless and is already captured
    by existing fixtures, so this step only adds the *translated* case.

### Step 7 — Save, capture, reopen

20. **Save.** Close Power BI Desktop completely.
21. **Capture the full file tree** of the `.SemanticModel` folder (see §5).
22. **Reopen the project in Desktop.** Record whether it opens cleanly, with any warning or error text
    verbatim.
23. **Save again without making any change.** Compare the files before and after: record any
    normalisation, reordering or rewriting. This is the same class of behaviour the `tab-order-states`
    fixture README documents for tab order, and it matters for the same reason.

### Step 8 — TMSL fixture (optional, separate project)

24. Only if the TMDL preview toggle can be turned **off**: disable it, create a **new** minimal project
    (one `Enter data` table is enough), save as `.pbip`, and confirm `model.bim` appears with no
    `definition/` folder.
25. **If the toggle cannot be disabled, record that** — it would mean current Desktop can no longer
    produce TMSL projects, which is itself the answer for the `model.bim` registry rule.

---

## 5. Evidence to capture

For each construct, preserve **both** the emitted files and a README recording the context files cannot
carry.

### Files to commit

Commit the whole PBIP **except**:

- `**/.pbi/cache.abf` — **never commit.** It is an Analysis Services backup that can contain **data**.
  Already covered by `.gitignore` and correctly untracked in existing fixtures **[verified in
  repository]**.
- `**/.pbi/localSettings.json` — user- and machine-specific; already gitignored.

Existing fixtures do commit `.pbi/editorSettings.json`, `.platform`, `definition.pbism`, and the whole
`definition/` tree **[verified in repository]** — follow that precedent.

Before committing, confirm no real data or personal identifiers are present. The `Enter data` values
above are synthetic.

### README content

Following `tests/fixtures/tab-order-states/README.md`, which is the established convention:

1. **Power BI Desktop version string**, exact
2. **Date created**
3. **Whether TMDL format was a preview toggle or the default**
4. **Compatibility level** before and after any upgrade prompt
5. **Exact authoring steps**, distinguishing **GUI-authored** (RLS) from **TMDL-view-authored**
   (perspective, UDF) — both are Desktop-serialised, but the distinction matters if Desktop's GUI later
   gains these features
6. **The emitted relative path for each construct**
7. **A short verbatim TMDL excerpt per construct**, especially the `tablePermission` line
8. **Whether reopening succeeded** without warnings
9. **Whether re-saving normalised anything**
10. **What the fixture proves**
11. **What it explicitly does NOT prove** — e.g. one Desktop version, one locale, import mode only, no
    legacy data source, no composite model
12. **A do-not-re-save warning** if step 23 shows re-saving changes the files

---

## 6. Registry implications

Current `DependencyEffectUnknown` entries at `dcbde4a`: `cultures`, `dataSources.tmdl`, `model.tmdl`,
`database.tmdl`.

### Recommended now, on evidence already in the repository

**[design decision]** These three should move from `DependencyEffectUnknown` to
**`NoKnownDependencyEffect`**, keeping classification `SemanticNotYetAnalyzed`:

| Entry | Evidence | Effect |
|---|---|---|
| `definition/database.tmdl` | Desktop emits only `compatibilityLevel`. No object references, across 3 fixtures **[verified by Power BI Desktop-authored fixture]** | Still listed as not analysed; stops qualifying |
| `definition/model.tmdl` | Desktop emits model properties, annotations and `ref` declarations. `ref` exists for every object regardless of usage and is documented as ordering **[verified by fixture + documentation]**, so it carries no usage information | Still listed; stops qualifying |
| `definition/cultures/*` | Desktop emits `cultureInfo en-US` with no content **[verified by fixture]**. Translations describe object captions rather than consuming objects **[inferred]** | Still listed; stops qualifying |

**This is the design's impact axis working as intended.** Classification answers *"is not reading this
worth recording?"* — yes, all three stay recorded and visible. `DependencyImpact` answers *"could not
reading this change a usage conclusion?"* — no. Transparency is fully preserved while the
qualify-everything failure in §0 is removed.

**Why act despite the culture case being partly inferred.** Leaving these `Unknown` guarantees that
propagation qualifies 100% of models, which is certain harm. Being wrong about one of them risks a
specific missed dependency, which is recoverable and would be caught by the confirming fixture. The
asymmetry favours moving them.

### Not recommended yet

| Entry | Why not |
|---|---|
| `definition/dataSources.tmdl` | Never observed. Leave `DependencyEffectUnknown` — it is absent from real models, so it costs nothing |
| `definition/roles/*` | Keep `MayCreateDependencies`. Documentation confirms `TablePermission.FilterExpression` is DAX; the fixture will confirm the emitted shape |
| `definition/perspectives/*` | Keep `MayCreateDependencies` until the fixture shows what a Desktop-emitted perspective contains |
| `definition/functions.tmdl` | Keep `MayCreateDependencies`; `Function.Expression` is documented DAX |
| `model.bim` | Keep `MayCreateDependencies`; it is the whole model |

**No code change is made in this task.** These are recommendations for a separate focused change.

---

## 7. Property-level precondition — resolved for three of four

The blocker recorded in `unsupported-construct-design.md` §6.8 asked: do `lineageTag`, `summarizeBy`,
`dataCategory` or `isKey` reference other model objects, such that ignoring them could invalidate an
`ApparentlyUnused` or `UsedOnlyByUnusedBranch` classification?

| Property | Type per Microsoft API reference | Verdict |
|---|---|---|
| **`summarizeBy`** | `AggregateFunction` **enum** — Default, None, Sum, Min, Max, Count, Average, DistinctCount | **Verified reference-free.** An enum cannot name an object |
| **`isKey`** | **Boolean** | **Verified reference-free** |
| **`dataCategory`** | **String** from a fixed 248-value vocabulary (Regular, ImageURL, Id, …) | **Verified reference-free.** A category label, not an object name |
| **`lineageTag`** | **String.** *"Lineage tags enable stable identification of objects across different semantic models… enables Power BI features such as composite models to maintain their binding to referenced tables or columns, even if the source semantic model object is renamed"* | **Reference-free within a model; still uncertain across models** — see below |

All four **[verified by Microsoft primary documentation]** via the Tabular API reference.

Committed fixtures agree **[verified in repository]**: `lineageTag` always appears as a bare GUID;
`summarizeBy` only ever as `none` or `sum`; `dataCategory` and `isKey` appear **zero** times.

### The `lineageTag` nuance

`lineageTag` is the object's **own identity**, not a pointer to another object. Nothing inside the model
resolves a reference through it. It is consumed by *other* models — composite models binding to this one.

Two consequences:

1. **Within a single semantic model, ignoring `lineageTag` cannot hide a dependency.** It is
   reference-free for PBI Assure's purposes in the ordinary case.
2. **A composite model binding by lineage tag is an external consumer**, which the product already treats
   as outside the analysed scope — an `AnalysisScopeBoundary`, not an `AnalysisLimitation`.

**The one open case:** a single PBIP containing **two semantic models**, one composite over the other.
There the binding would be *inside* the analysed scope. **Still uncertain** — no such fixture exists, and
`lineageTag` does not appear in any report-side PBIR file in the repository (**0 files**, [verified in
repository]), so no evidence either way.

### Conclusion for property-level detection

**The specific blocker is cleared for these four properties.** If property-level detection is built, all
four should carry `NoKnownDependencyEffect` and qualify nothing — which is what makes the mechanism
viable at all, since `lineageTag` and `summarizeBy` appear in essentially every model.

**This does not clear property-level detection generally.** Other unread properties may bear references
and must be assessed individually before being ignored.

---

## 8. Remaining unknowns

| # | Unknown | How it would be settled |
|---|---|---|
| 1 | Emitted path and content for **roles** — one file per role under `definition/roles/`? | Fixture A, steps 2 and 7 |
| 2 | Emitted `tablePermission` serialization, static and dynamic | Fixture A, step 21 |
| 3 | Emitted path and content for a **perspective** | Fixture A, steps 4 and 7 |
| 4 | Emitted path and content for **functions.tmdl** | Fixture A, steps 5 and 7 |
| 5 | Whether **dataSources.tmdl** is ever emitted by current Desktop, and by what kind of source | Not covered. Would need a legacy provider connection — deliberately out of scope to avoid credentials |
| 6 | Whether a **translated** culture file names model objects | Fixture C, or step 6 if it succeeds |
| 7 | Whether current Desktop can still produce **model.bim** | Fixture B, step 24–25 |
| 8 | Whether `lineageTag` binds across two models **inside one PBIP** | A composite-model fixture — not planned |
| 9 | Whether re-saving normalises any of these files | Step 23 |
| 10 | Whether `model.tmdl` in a model *with* roles and perspectives adds anything beyond `ref` declarations | Fixture A — this is what would confirm the §6 `model.tmdl` recommendation |

---

## 9. Fixture repository layout

**[design decision]** Follow the existing convention exactly.

```
tests/fixtures/desktop-semantic-constructs/
    README.md
    desktop-semantic-constructs.pbip
    desktop-semantic-constructs.Report/…
    desktop-semantic-constructs.SemanticModel/
        definition.pbism
        .platform
        .pbi/editorSettings.json
        definition/
            database.tmdl
            model.tmdl
            cultures/en-US.tmdl
            tables/Sales.tmdl
            roles/RegionalManager.tmdl        ← expected, to be confirmed
            roles/DynamicUser.tmdl            ← expected, to be confirmed
            perspectives/SalesView.tmdl       ← expected, to be confirmed
            functions.tmdl                    ← expected, to be confirmed

tests/fixtures/desktop-semantic-constructs-tmsl/     (optional, fixture B)
    README.md
    …/model.bim
```

Naming follows the existing kebab-case fixtures (`tab-order-states`, `grouped-tab-order`,
`model-reference-context`, `privacy-canary`).

**Never regenerate these from synthetic text.** If they are lost, they must be re-authored in Desktop.
The README must say so, in the style of the `tab-order-states` warning.

---

## 10. Recommended next action after the fixture exists

In order. Each step is separately committable.

1. **Commit the fixture and its README.** No code change. Record the actual emitted paths in the README
   and note any that differ from the registry's expectations.
2. **Add one test** asserting that every definition artifact in the Desktop fixture is classified by the
   registry **without reaching the `Unrecognized` fallback**. This is the real-evidence counterpart of the
   existing synthetic `EveryDefinitionArtifactIsClassifiedByTheConstructRegistry` invariant, and it will
   fail immediately if Desktop's paths differ from the documented ones.
3. **Correct any registry path that the fixture contradicts** — a one-line change per rule, plus the
   README note.
4. **Apply the §6 impact changes** — `database.tmdl`, `model.tmdl` and `cultures/*` to
   `NoKnownDependencyEffect`. **This is the prerequisite for propagation**, per §0.
5. **Record the §7 property conclusions** in `unsupported-construct-design.md` so the property-level
   precondition is visibly discharged for those four properties.
6. **Only then** consider the propagation slice, and after that RLS parsing — for which fixture A is
   also the parsing test fixture.

---

## Scope statement

No production code, tests or fixtures were created or modified. The repository is unchanged at
`dcbde4a`. Research used current Microsoft Learn documentation (TMDL overview, PBIP semantic model
folder, TMDL view, RLS, DAX user-defined functions, and the Tabular API reference for `LineageTag`,
`SummarizeBy`, `IsKey` and `DataCategory`) together with the committed Desktop-authored fixtures and one
read-only CLI scan of `tests/fixtures/tab-order-states`. All proposed names, paths and classifications
are recommendations for review.

### Sources

- [Tabular Model Definition Language (TMDL)](https://learn.microsoft.com/en-us/analysis-services/tmdl/tmdl-overview)
- [Power BI Desktop project semantic model folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-dataset)
- [Use TMDL view in Power BI](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-tmdl-view)
- [Row-level security (RLS) with Power BI](https://learn.microsoft.com/en-us/fabric/security/service-admin-row-level-security)
- [Using DAX user-defined functions](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-user-defined-functions-overview)
- [Column.LineageTag](https://learn.microsoft.com/en-us/dotnet/api/microsoft.analysisservices.tabular.column.lineagetag) ·
  [Column.SummarizeBy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.analysisservices.tabular.column.summarizeby) ·
  [Column.IsKey](https://learn.microsoft.com/en-us/dotnet/api/microsoft.analysisservices.tabular.column.iskey) ·
  [Column.DataCategory](https://learn.microsoft.com/en-us/dotnet/api/microsoft.analysisservices.tabular.column.datacategory)
