# Handover

Tactical entry point for an incoming coding agent. Read this first, then
[CURRENT_STATE.md](CURRENT_STATE.md), then [DECISIONS.md](DECISIONS.md).

## What just happened

DAX user-defined function dependencies are analysed, backed by a second Desktop-authored fixture,
`desktop-udf-references`, authored specifically to show how a function body writes a reference.

A function is a **dependency node, not a root**. Nothing in the model requires a definition to exist, so
what a function references becomes reachable only when something reachable calls it, and an uncalled
function's references land on `UsedOnlyByUnusedBranch`. That is what separates a function from a role
filter or a perspective member, both of which are roots.

**The expected result did not happen, and that is the important finding.** The previous handover
predicted this would take the Desktop fixture's qualified count to zero. It did not move at all:
`desktop-semantic-constructs` is still 21 of 27 objects `QualifiedByLimitation`, before and after. The
unread part of `functions.tmdl` was never the definitions — it is where a function is *called from*.
Microsoft documents that visual calculations and report-level measures can call one, and PBI Assure
parses neither, so the impact stays `MayCreateDependencies`. Do not treat that as unfinished work to be
tidied away; it is the honest state.

## State

- **Last verified product state:** `659b895` on `master`. Later commits may be documentation-only; run
  `git log --oneline` to see whether anything after it touched behaviour.
- **Working tree:** expected clean apart from untracked local review documents; no tracked modifications
- **Verified at that commit:** build succeeded with 0 warnings; **361 core + 2 privacy tests passed**; CI green
- **Known exception:** `dotnet format --verify-no-changes` fails with 24 pre-existing whitespace errors
  in two Theme Review files. Unrelated to current work, deliberately not fixed. See
  [CURRENT_STATE.md](CURRENT_STATE.md).

## Immediate next task

**Design how limitations and qualified confidence should appear to a user.**

The previous handover deferred this on the grounds that most objects were about to stop being caveated.
That reasoning is now measured and wrong — the caveat is not about to disappear, because the unread UDF
consumers are report-side metadata that is a separate piece of work. Designing presentation while
objects are qualified is therefore designing for the real situation, not a transitional one.

Nothing surfaces in HTML, CSV or the browser app today, so a user cannot see that a conclusion was
qualified. See [../design/unsupported-construct-design.md](../design/unsupported-construct-design.md) §5
for the shape already proposed, which has not been reviewed against the implemented behaviour.

The alternative is to read report-level measure expressions as DAX, which would close one of the two
unread UDF consumers. That needs a Desktop fixture with a report-level measure calling a UDF.

## Do not do yet

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

1. **The culture impact value rests on a design decision**, not an observation — translations are treated
   as describing objects rather than consuming them. Everything built on qualification inherits that.
2. **`PBI-ACCESS-001` volume is unmeasured.** The false-positive concern is inferred, never measured
   against a real report. Do not change the rule on that inference alone.

## Missing evidence

- RLS forms beyond the two the Desktop fixture proves — cross-table filters, column permissions (OLS)
- **Where a UDF is called from outside the model definition** — visual calculations and report-level
  measures, neither parsed. This is now the reason functions still qualify
- Whether a *translated* culture file names model objects, and whether Q&A synonyms constitute usage
- Whether `dataSources.tmdl` is ever emitted by current Desktop
- Real-report measurement of `PBI-ACCESS-001` finding volume

## Reading order

1. This file
2. [CURRENT_STATE.md](CURRENT_STATE.md) — what is true now
3. [DECISIONS.md](DECISIONS.md) — what not to reopen
4. `../../tests/fixtures/desktop-semantic-constructs/README.md` and
   `../../tests/fixtures/desktop-udf-references/README.md` — the Desktop evidence, and its limits
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
