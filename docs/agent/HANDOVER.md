# Handover

Tactical entry point for an incoming coding agent. Read this first, then
[CURRENT_STATE.md](CURRENT_STATE.md), then [DECISIONS.md](DECISIONS.md).

## What just happened

Slice 1 of unsupported-construct detection was implemented and then corrected against current
Microsoft-documented TMDL/PBIP structure. PBI Assure now records semantic-model definition artifacts it
does not analyse, instead of skipping them silently. It does **not** yet change any usage classification
because of them.

## State

- **HEAD:** `dcbde4a` on `master`
- **Working tree:** expected clean apart from untracked local review documents; no tracked modifications
- **Verified at this HEAD:** build succeeded with 0 warnings; **227 core + 2 privacy tests passed**; CI green
- **Known exception:** `dotnet format --verify-no-changes` fails with 24 pre-existing whitespace errors
  in two Theme Review files. Unrelated to current work, deliberately not fixed. See
  [CURRENT_STATE.md](CURRENT_STATE.md).

## Immediate next task — not a coding task

**Create the Power BI Desktop-authored semantic fixture defined in `DESKTOP_SEMANTIC_FIXTURE_PLAN.md`.**

This is a manual authoring task performed by a person in Power BI Desktop. It is currently in progress.
An agent cannot complete it, and **must not substitute hand-written TMDL for it** — the entire point is
to observe what Desktop actually emits.

An agent may help by reviewing the produced fixture, drafting its README from captured evidence, or
preparing the follow-up code changes once the fixture exists.

## Do not do yet

- Uncertainty propagation or `ClassificationConfidence`
- Row-level security / `tablePermission` parsing
- Block-level or property-level limitation detection
- Registry classification or impact changes
- Malformed-TMDL recovery
- HTML/CSV limitation surfaces
- Changes to `PBI-ACCESS-001`
- The pre-existing `dotnet format` whitespace cleanup

Reasons are recorded in [CURRENT_STATE.md](CURRENT_STATE.md) and [DECISIONS.md](DECISIONS.md). Two are
blocking rather than merely sequenced:

1. **Propagation is blocked.** Every Desktop-authored model emits `model.tmdl`, `database.tmdl` and a
   culture file, all currently carrying `DependencyEffectUnknown`. Propagating today would caveat every
   object in every model.
2. **`PBI-ACCESS-001` volume is unmeasured.** The false-positive concern is inferred, never measured
   against a real report. Do not change the rule on that inference alone.

## Missing evidence

- Desktop-emitted paths and content for roles, perspectives and DAX user-defined functions
- Whether `dataSources.tmdl` is ever emitted by current Desktop
- Whether re-saving normalises any semantic-model file
- Real-report measurement of `PBI-ACCESS-001` finding volume

## Reading order

1. This file
2. [CURRENT_STATE.md](CURRENT_STATE.md) — what is true now
3. [DECISIONS.md](DECISIONS.md) — what not to reopen
4. `DESKTOP_SEMANTIC_FIXTURE_PLAN.md` — the current task
5. `UNSUPPORTED_CONSTRUCT_SLICE1_REGISTRY_CORRECTION.md` — what slice 1 does and why the registry looks
   as it does
6. `UNSUPPORTED_CONSTRUCT_DESIGN_V2.md` — only if deeper architectural context is needed

Items 4–6 are **supplied separately and are not stored in this repository**; see
[CURRENT_STATE.md](CURRENT_STATE.md) → *Reference documents*. Do not read every historical audit before
starting a small task.

## Before you finish a task

1. Run the build and the tests appropriate to the change; for anything touching scanning or output, run
   the whole suite including the privacy end-to-end tests.
2. Commit logical changes separately, with a message explaining the reasoning, not just the diff.
3. Update [CURRENT_STATE.md](CURRENT_STATE.md) if the factual state changed.
4. Update this file with the next task.
5. Update [DECISIONS.md](DECISIONS.md) **only** when a durable decision or established semantic actually
   changed — not for transient observations.
6. Write a task-specific document only when the task genuinely merits one.
7. Never leave a decision recorded only in chat history. Chat is disposable; this repository is the
   project memory.
