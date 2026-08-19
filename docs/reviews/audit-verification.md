# Audit verification and first fixes

**Date:** 2026-08-19 · **Baseline commit:** `42f2c42` · **Ends at:** `b84d487`
**Companion to** `architecture-review.md`, `object-coverage-review.md` and `../design/proposed-rules.md`.

This document does two things: it records which of the audits' claims survive contact with the current
repository, and it records the two changes made as a result. Unlike the three companion documents, this
one is **not** read-only — it covers work that was committed and pushed to `master`.

Findings are marked **[verified]** where something was executed or mechanically checked, and
**[inferred]** where the conclusion comes from reading code without running it. Where a claim in one of
the companion audits turned out to be wrong or overstated, it is called out explicitly rather than
quietly worked around.

---

## 1. Baseline

| | |
|---|---|
| Branch / commit | `master` @ `42f2c42` "Clarify semantic object usage presentation" |
| Working tree | Clean except the three review documents and four `docs/` spikes, all untracked |
| Build | `dotnet build PbiAssure.slnx` → **succeeded, 0 warnings, 0 errors** (`TreatWarningsAsErrors=true`) |
| Tests | `dotnet test PbiAssure.slnx --no-build` → **187 passed, 0 failed, 0 skipped**, 420 ms |

The architecture review's stated baseline reproduced exactly [verified].

---

## 2. P0 / P1 cross-check against current code

Every finding in `architecture-review.md` §14 was re-checked against the working tree before
any change was made. **All seven P0/P1 findings were still live at `42f2c42`.**

| # | Finding | Status at baseline | Evidence |
|---|---|---|---|
| P0-1 | `PbiAssure.Privacy.E2E` absent from solution and CI | **Live** — now fixed | `grep -c Privacy.E2E PbiAssure.slnx` → 0; `grep -c Privacy .github/workflows/ci.yml` → 0; test run printed *"A total of 1 test files matched"* [verified] |
| P0-2 | `PBI-ACCESS-001` lacks decorative-type exclusion | **Live** — untouched | `MissingAltTextRule` filters only `IsInTabOrder && !HasAltText`; `VisualExcludedFromTabOrderRule` holds `DecorativeCandidateTypes = { basicShape, image, textbox }` [verified] |
| P0-3 | No dedicated `SemanticDependencyAnalyzer` tests | **Live** — now fixed, but see §3 | No test file referenced the type [verified] |
| P1-4 | Schema version captured but never validated | **Live** — untouched | `SchemaVersion: "0.21"` hardcoded at `ProjectScanner.cs:43`; consumed only by two `HtmlReportRenderer` display lines; no unsupported-version rule exists [verified] |
| P1-5 | Group participation inconsistent across accessibility rules | **Live** — untouched | `VisualRuleContexts.Read` enumerates `page.Visuals` only → ACCESS-001/003 never see groups. `DuplicateTabOrderRule` uses `VisualGroupHierarchyResolver.Resolve(page)`, which concatenates `page.VisualGroups` **and** `page.Visuals` → ACCESS-002 does [verified] |
| P1-6 | `PBI-NAV-003 / 005 / 006` untested | **Live** — untouched | Each id appears in `AssuranceRuleCatalog.cs` and `NavigationAssuranceRule.cs`, in no test file [verified] |
| P1-7 | No save/reopen tab-order normalisation fixture pair | **Live** — untouched | Six fixtures; `tab-order-states` documents the `-9999000` → `-1` transition in prose only [verified] |

### A caveat on P0-2

The *asymmetry* between the two rules is verified. The **volume** claim — that this is the dominant
false-positive source on real reports — remains `[inferred]`, in the original audit and here. No
measurement against a real report has been taken by anyone. The audit's own §16 step 2 says to measure
before changing the rule; that advice still stands and was followed by not touching it.

---

## 3. Corrections to the companion audits

These are the places where the audits are wrong or overstated. They matter because acting on them
verbatim would have produced duplicated or misdirected work.

### 3.1 P0-3 is overstated — the recommended coverage largely already exists

"No dedicated test file" is literally true. "Exercised only incidentally" undersells reality.

The architecture review's §16 step 3 recommends writing tests for *"the five-state precedence, auto
date-table structural requirement, field-parameter reachability, and calculation-group traversal."*
**All four already had assertions** [verified]:

| Recommended target | Already asserted at |
|---|---|
| Five-state precedence | `ProjectScannerTests.cs:367-379` — all five states in one test |
| Auto date-table structural requirement | `ProjectScannerTests.cs:2195` — `LocalDateTable_generated[Date]` → `StructurallyRequired` |
| Field-parameter reachability | `ScanClassifiesObjectsUsedThroughFieldParametersAndCalculationGroups` (`:405`) |
| Calculation-group traversal | same test |

Also already covered: DAX extraction edge cases (strings, comments, hierarchy suffixes), unresolved-DAX
retention, report-measure propagation, sort-by, hierarchy-level and relationship-endpoint edges.

The real gap was narrower and different:

1. **Attribution** — every assertion ran through `ProjectScanner.Scan`, so an analyzer regression
   reported a scanner failure, and a parser regression could mask an analyzer one.
2. **Granularity** — assertions were incidental lines inside large multi-purpose tests. The five-state
   one spans roughly 150 lines and also asserts parsing, counts and evidence.
3. **Genuinely untested analyzer-specific invariants** — precedence *ordering* under conflict, cycle
   termination in `Traverse`, `NodeKey` model scoping, and the `ModelLookup` ambiguity rules.

Item 3 is where the value was, and is what the new test file covers.

### 3.2 P3-12 is not a defect

The architecture review §8 raises cross-model identity as a risk: *"Two models with the same name in one
project would collide. Unlikely, unverified."*

`NodeKey` already joins the model name into the key. Cross-model scoping works correctly [verified by
mutation — see §5]. It is now pinned by a test.

### 3.3 `object-coverage-review.md` is wrong that `variation` is unparsed

The coverage review lists `variation NOT parsed`. That is true only model-side. **Report-side variation
resolution exists** — `PbirFieldReferenceExtractor.cs:105` reads `PropertyVariationSource` and resolves
it to the underlying table and column [verified].

What is genuinely missing is the model-side TMDL `variation` block. The distinction matters: the report
half of the feature works today, so the gap is smaller than the review implies.

### 3.4 The other Tier-1 absences hold

Independently re-checked with a reference count across all of `src/PbiAssure.Core`. Each is **zero
references** [verified]: `tablePermission`, `refreshPolicy`, `RangeStart`, `kpi`, `detailRows`,
`alternateOf`, `dataCategory`, `lineageTag`, `summarizeBy`, `perspective`, `objectTranslation`, `isKey`,
`queryGroup`.

---

## 4. Changes made

Two commits, each self-contained, each pushed to `master` with CI green.

### 4.1 `1afd075` — Run privacy end-to-end tests in the normal suite

Addresses P0-1 and nothing else.

**Before editing anything, the privacy tests were run unchanged: 2 passed, 32 s** [verified]. There was
no product defect and no broken infrastructure — the project was simply never wired in. That made this a
pure wiring change, with no temptation to relax an assertion to reach green.

| File | Change |
|---|---|
| `PbiAssure.slnx` | Added the `PbiAssure.Privacy.E2E` project under `/tests/` |
| `.github/workflows/ci.yml` | Added `actions/setup-node` and a `playwright.ps1 install chromium` step, mirroring the command the existing `scripts/Test-Privacy-E2E.ps1` already used |
| `README.md`, `CONTRIBUTING.md` | Recorded the Node.js and Chromium prerequisite for the normal test command |
| `docs/browser-privacy.md` | Removed the sentence *"The heavier browser tests are deliberately not part of the normal solution test run"*, which this change made false |
| `tests/PbiAssure.Privacy.E2E/PrivacyE2EFixture.cs` | Three lines of whitespace only, so the newly covered project satisfies `dotnet format` |

**No privacy assertion was weakened, skipped or relaxed.** `PrivacyWorkflowTests.cs`,
`PrivacyNetworkMonitor.cs`, `PrivacyCanaries.cs` and `PrivacyTestHost.cs` are byte-identical to their
state at `42f2c42` [verified].

Result: `dotnet test PbiAssure.slnx` runs **187 core + 2 privacy** tests, confirmed in Debug and in the
Release shape CI uses. CI run `32224598007` succeeded in 3m16s, up from a 1m36s baseline — the increase
is the Chromium install and the clean web publish the fixture performs.

### 4.2 `b84d487` — Pin dependency analyzer graph semantics

Addresses P0-3, informed by §3.1 above.

Adds `tests/PbiAssure.Core.Tests/SemanticDependencyAnalyzerTests.cs` — 20 tests calling
`SemanticDependencyAnalyzer.Analyze` directly with small hand-built model inventories, so a failure names
the dependency engine rather than the scanner. `InternalsVisibleTo("PbiAssure.Core.Tests")` already
existed, so no production visibility change was needed.

Coverage chosen deliberately to **avoid duplicating** what `ProjectScannerTests` already asserts:

- **State precedence under conflict** — five tests, each placing one object in two states at once and
  pinning which wins. These are the cases a parser-driven test cannot construct on demand.
- **Traversal safety** — mutually referencing measures and a self-referencing measure both terminate.
- **Model scoping** — use in one model does not classify same-named objects in another; edges carry the
  model they were observed in.
- **Resolution refusals** — the exact conditions under which the analyzer declines to guess: unqualified
  reference matching both a measure and a local column; qualified reference matching both a column and a
  measure; missing sort-by column; missing relationship endpoint. Each asserts the unresolved record is
  retained *and* that no node was invented.
- **Edge emission** — containing-table edges for every object, distinct edge kinds for sort-by and
  hierarchy levels, deduplication and deterministic ordering.
- **Table classification** — directly used, structurally required and apparently unused tables, plus a
  table with no columns or measures.

**Drift guard.** The hand-built usage helper mirrors the reconciler's object enumeration. A dedicated
test, `HandBuiltUsagesMatchTheReconciler`, asserts the helper produces the same object set as
`SemanticUsageReconciler.Reconcile` for a representative model, so these fixtures cannot silently drift
away from the usage set the product actually analyses.

No product code was changed. `git diff` against `src/` was empty at commit time [verified].

---

## 5. Mutation evidence

A test suite that passes on first run proves nothing about its own sensitivity. Each claim below was
checked by deliberately breaking the analyzer and observing which tests noticed. The analyzer was
restored to a byte-identical copy afterwards [verified].

| Mutation applied to `SemanticDependencyAnalyzer` | Pre-existing 187 | New 20 |
|---|---|---|
| Swap `IndirectlyUsed` / `StructurallyRequired` precedence order | **2 fail** | 1 fails |
| Strip the model from `NodeKey` | **all pass** | 1 fails |
| Weaken `hasColumn ^ hasMeasure` to `hasColumn \|\| hasMeasure` | **all pass** | 1 fails |

Two genuine coverage gaps closed. The precedence mutation was **already** caught by the existing suite —
recorded here because an earlier assumption that it was not turned out to be wrong on checking, and the
correction is more useful than the assumption.

---

## 6. Demonstrated behaviour: a column used only by RLS

The coverage review ranks RLS as the highest-risk absence, reasoning that a column referenced only by a
`tablePermission` filter would classify as `ApparentlyUnused`. That reasoning was **confirmed by
execution rather than left inferred** [verified].

A synthetic PBIP was scanned with the real CLI. The model contained `Sales[Amount]`, `Sales[Region]`, a
measure over `Amount`, and a `definition/roles.tmdl` holding:

```
role RegionalManager
	modelPermission: read

	tablePermission Sales = Sales[Region] = USERNAME()
```

Result:

```
Sales[Amount]        Column  -> UsedOnlyByUnusedBranch
Sales[Region]        Column  -> ApparentlyUnused
Sales[Total Amount]  Measure -> ApparentlyUnused

UnresolvedSemanticDependencies: []
AssuranceFindings:              (none)
```

Two observations, the second of which is not in the coverage review:

1. The security-filtering column lands in the state the product's own documentation describes as a
   review candidate for removal. Confirmed as predicted.
2. **`roles.tmdl` is consumed in total silence.** No unresolved dependency, no finding, no signal of any
   kind that a construct was encountered and skipped. This is arguably the sharper problem. The
   misclassification is recoverable once RLS is parsed; the silence conflicts directly with the
   product's stated posture of preferring `Unknown` / `ReviewRequired` over a confident claim, and the
   same silence applies to `kpi`, `detailRows`, `refreshPolicy` and `alternateOf`.

**Recommendation:** decide whether an "unparsed construct encountered" record belongs in the design
*before* building RLS support. The answer generalises to every Tier-1 absence, and would turn each of
them from an invention into an application of an existing pattern.

---

## 7. Remaining plan — not implemented

### Pass 2 — characterization tests for unsupported constructs

Pin today's behaviour for RLS-only, `refreshPolicy`, `kpi`, `detailRows` and `alternateOf` columns, named
so they read as known-wrong-and-recorded rather than as endorsement, for example
`RlsOnlyColumnIsNotYetRecognisedAndClassifiesApparentlyUnused`.

*Value:* the eventual fix appears as an intentional test diff rather than a silent behaviour change.
*Risk:* a committed test asserting `ApparentlyUnused` for a security-relevant column can be misread as
approval of that outcome. Worth a deliberate decision, which is why it was not done unilaterally.

These are all plain TMDL text and can be written synthetically with confidence.

### Pass 3 — cases where only a Desktop-authored fixture will do

A hand-written fixture here would pin an assumption about the file format rather than Power BI's actual
behaviour, which is the failure mode the `tab-order-states` fixture exists to prevent.

| Case | Why synthetic is insufficient |
|---|---|
| `refreshPolicy` / incremental refresh | Desktop generates `RangeStart` / `RangeEnd` parameters *and* policy-driven partitions; the partition and expression shape cannot be guessed reliably |
| `alternateOf` / aggregation mappings | Written by Desktop's Manage aggregations UI |
| Model-side `variation` | The `variation` / `navigationProperty` / `defaultHierarchy` block and its relationship to the auto date table |
| Tab-order save/reopen pair (P1-7) | Only Desktop can produce the post-save half of the pair |

### Deliberately not touched

`PBI-ACCESS-001` (P0-2), RLS support, the proposed new rules, and HTML output. The `ClassifyTables`
O(tables × objects) issue (P2-9) is real but should wait for a failing performance test rather than a
speculative refactor.

---

## 8. Open decisions

1. **Pass 2 characterization tests** — proceed, or leave the unsupported constructs entirely unpinned?
2. **The unparsed-construct signal** (§6) — design question with reach well beyond RLS; worth settling
   before any Tier-1 work begins.
3. **`dotnet format --verify-no-changes` fails on `master`** — 24 pre-existing whitespace violations in
   `src/PbiAssure.Reporting/HtmlReportRenderer.ThemeReview.cs`, unrelated to any change here and absent
   from all three companion audits [verified]. `CONTRIBUTING.md` instructs contributors to run this
   command, so it fails on a clean checkout. Small isolated fix, not bundled into either commit above.

---

## Scope statement

Two commits were made and pushed to `master`: `1afd075` and `b84d487`. Both were verified locally and in
CI before and after pushing. No product code under `src/` was modified in either. The three companion
review documents were read but not edited. The analyzer mutations described in §5 were reverted and the
file confirmed byte-identical to its committed state.

**Verification commands used throughout:**

```powershell
dotnet build PbiAssure.slnx --no-restore
dotnet test PbiAssure.slnx --no-build
dotnet format PbiAssure.slnx --verify-no-changes
```

Final state: **207 core + 2 privacy tests passing**, build clean at 0 warnings, CI green on both commits.
