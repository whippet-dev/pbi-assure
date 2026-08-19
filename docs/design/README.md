# Design documents

Proposed and approved architecture, and the evidence plans that must be satisfied before an
implementation slice proceeds. Forward-looking: these describe behaviour that is intended or partly
built.

Completed work and point-in-time verification live in [`../reviews/`](../reviews/). Architectural
decision records live in [`../decisions/`](../decisions/). Standing invariants an agent must not reopen
are summarised in [`../agent/DECISIONS.md`](../agent/DECISIONS.md).

| Document | Purpose |
|---|---|
| [unsupported-construct-design.md](unsupported-construct-design.md) | How PBI Assure should behave when it encounters metadata it recognises but does not analyse. Defines `AnalysisLimitation`, the construct registry, and the uncertainty-propagation rule. Revision 2 |
| [desktop-semantic-fixture-plan.md](desktop-semantic-fixture-plan.md) | The Power BI Desktop-authored fixture needed before further unsupported-construct work, and the manual procedure to create it. **The current task** |
| [proposed-rules.md](proposed-rules.md) | Candidate assurance rules, each with the analysis that already exists and how it could produce a false positive. Nothing here is committed to |

A design document is not a promise. Where one records a `[design decision]`, that is a proposal for
review unless a review document or the code says it shipped.
