# Reviews and evidence

Point-in-time audits, verification passes and implementation handovers. Each records what was true at a
stated commit, with its evidence labelled. They are **historical records** — later documents correct
earlier ones rather than earlier ones being edited.

Forward-looking design lives in [`../design/`](../design/).

| Document | Records |
|---|---|
| [architecture-review.md](architecture-review.md) | Independent read-only audit at `42f2c42`. Ranked P0–P3 findings, false-positive risk, and a list of things not to rewrite |
| [object-coverage-review.md](object-coverage-review.md) | Which Power BI object types the analysis does not consider, ranked by whether the absence could make an object look unused |
| [audit-verification.md](audit-verification.md) | Verification of the two reviews above against the working tree, plus the first two fixes. **Corrects several claims in them** — see its §3 |
| [unsupported-construct-slice1-implementation.md](unsupported-construct-slice1-implementation.md) | What slice 1 of unsupported-construct detection implemented, at `06eaec2` |
| [unsupported-construct-slice1-registry-correction.md](unsupported-construct-slice1-registry-correction.md) | Correction of the slice-1 registry against documented TMDL/PBIP structure, at `dcbde4a` |
| [encountered-pbir-schema-compatibility-policy.md](encountered-pbir-schema-compatibility-policy.md) | Inventory of report-side PBIR schema evidence and the conservative policy for exact, unverified, unknown, missing and malformed schema metadata |

Earlier feasibility spikes remain directly under [`../`](../) — `pbix-ingestion-feasibility.md`,
`theme-review-feasibility-spike.md`, `theme-review-fixture-analysis.md` and
`stale-visual-reference-classification-spike.md` — following the naming convention already used there.

## Reading these safely

- **Later corrects earlier.** `audit-verification.md` §3 corrects the architecture and coverage reviews
  in three material places. Do not act on the earlier documents without checking it.
- **Evidence labels are load-bearing.** `[verified]`, `[inferred]` and `[design decision]` are not
  interchangeable. Several findings are explicitly inferred and unmeasured.
- **Not everything here was acted on.** A finding appearing in a review does not mean it was accepted;
  check [`../agent/CURRENT_STATE.md`](../agent/CURRENT_STATE.md) for what is actually implemented.

## Deliberately not retained

An earlier revision of the unsupported-construct design was superseded by
[`../design/unsupported-construct-design.md`](../design/unsupported-construct-design.md), which is
standalone and restates every conclusion it changed along with the reasoning. The earlier revision holds
no evidence that the current one lacks, so it is not kept as project memory.
