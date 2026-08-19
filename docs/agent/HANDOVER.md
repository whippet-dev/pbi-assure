# Handover

Tactical entry point for an incoming coding agent. Read this first, then
[CURRENT_STATE.md](CURRENT_STATE.md), then [DECISIONS.md](DECISIONS.md).

## What just happened

A Power BI Desktop-authored fixture was added at `tests/fixtures/desktop-semantic-constructs/`,
confirming that the definition-file registry's paths match what Desktop actually emits. No path needed
correcting. The always-present `model.tmdl`, `database.tmdl` and culture files were then changed from
`DependencyEffectUnknown` to `NoKnownDependencyEffect`, which clears the blocker that would have made
propagation caveat every model.

## State

- **Last verified product state:** `d2ecbcf` on `master`. Later commits may be documentation-only; run
  `git log --oneline` to see whether anything after it touched behaviour.
- **Working tree:** expected clean apart from untracked local review documents; no tracked modifications
- **Verified at that commit:** build succeeded with 0 warnings; **245 core + 2 privacy tests passed**; CI green
- **Known exception:** `dotnet format --verify-no-changes` fails with 24 pre-existing whitespace errors
  in two Theme Review files. Unrelated to current work, deliberately not fixed. See
  [CURRENT_STATE.md](CURRENT_STATE.md).

## Immediate next task

**Review the fixture evidence and the dependency-impact corrections before propagation is built.**

Start with `tests/fixtures/desktop-semantic-constructs/README.md`, which is the authoritative record of
what the fixture proves and — importantly — what it does not. Then read the impact reasoning in
`SemanticDefinitionFileRegistry`, particularly the culture rule, which rests on a design decision rather
than an observation.

Propagation (`ClassificationConfidence` and the qualification rule) is now unblocked but deliberately
still unimplemented, pending that review.

## Do not do yet

- Uncertainty propagation or `ClassificationConfidence`
- Row-level security / `tablePermission` parsing
- Block-level or property-level limitation detection
- Further registry classification or impact changes without new evidence — the always-present files were
  corrected on the evidence recorded in `SemanticDefinitionFileRegistry`; `dataSources.tmdl` stays
  `DependencyEffectUnknown` until it is actually observed
- Malformed-TMDL recovery
- HTML/CSV limitation surfaces
- Changes to `PBI-ACCESS-001`
- The pre-existing `dotnet format` whitespace cleanup

Reasons are recorded in [CURRENT_STATE.md](CURRENT_STATE.md) and [DECISIONS.md](DECISIONS.md). Two are
blocking rather than merely sequenced:

1. **Propagation is unblocked but unreviewed.** The always-present-file problem is fixed, but the
   culture impact value rests on a design decision. Review it before building on it.
2. **`PBI-ACCESS-001` volume is unmeasured.** The false-positive concern is inferred, never measured
   against a real report. Do not change the rule on that inference alone.

## Missing evidence

- Whether a *translated* culture file names model objects, and whether Q&A synonyms constitute usage
- How a DAX user-defined function that references a model object serialises
- Whether `dataSources.tmdl` is ever emitted by current Desktop
- Real-report measurement of `PBI-ACCESS-001` finding volume

## Reading order

1. This file
2. [CURRENT_STATE.md](CURRENT_STATE.md) — what is true now
3. [DECISIONS.md](DECISIONS.md) — what not to reopen
4. `../../tests/fixtures/desktop-semantic-constructs/README.md` — the Desktop evidence, and its limits
5. [../reviews/unsupported-construct-slice1-registry-correction.md](../reviews/unsupported-construct-slice1-registry-correction.md)
   — what slice 1 does and why the registry looks as it does
6. [../design/unsupported-construct-design.md](../design/unsupported-construct-design.md) — only if
   deeper architectural context is needed

Everything needed is in this repository; no external document is required. Do not read every historical
audit before starting a small task — [../reviews/](../reviews/) is there when you need it, not before.

## Before you finish a task

1. Run the build and the tests appropriate to the change; for anything touching scanning or output, run
   the whole suite including the privacy end-to-end tests.
2. Commit logical changes separately, with a message explaining the reasoning, not just the diff.
3. Update [CURRENT_STATE.md](CURRENT_STATE.md) if the factual state changed — build, tests, CI, what is
   implemented, or an evidence gap. A documentation-only commit does not require an update.
4. Update this file with the next task.
5. Update [DECISIONS.md](DECISIONS.md) **only** when a durable decision or established semantic actually
   changed — not for transient observations.
6. Write a task-specific document only when the task genuinely merits one.
7. Never leave a decision recorded only in chat history. Chat is disposable; this repository is the
   project memory.
