# Incremental-refresh policy evidence review

Date: 2026-08-21

Evidence labels: **[verified in repository]** checked in source or tests · **[verified from persisted
files]** checked in the Desktop-authored fixtures · **[manually verified in Power BI Desktop]** observed
by the fixture author · **[inferred]** reasoned from evidence · **[design decision]** a bounded product
choice.

## Question and fixture

The paired Desktop projects ask whether PBI Assure can distinguish an authored incremental-refresh policy
from ordinary Power Query use of the reserved `RangeStart` and `RangeEnd` parameters. Both tables in both
projects have the same synthetic rows, DateTime parameters and `[EventDate] >= RangeStart` / `< RangeEnd`
filter. Only `FactEvents_Policy` in the after-state was configured through Desktop. **[verified from
persisted files]**

The source paths were:

- `C:\Users\morty\Downloads\PBI Assure Testing\desktop-incremental-refresh-evidence-baseline`
- `C:\Users\morty\Downloads\PBI Assure Testing\desktop-incremental-refresh-evidence`

## Exact persisted difference

The only changed semantic-definition file is
`definition/tables/FactEvents_Policy.tmdl`. Desktop added a table-owned `refreshPolicy` object before the
columns and partition. It contains:

| Persisted field | Value | Bounded meaning |
|---|---|---|
| `policyType` | `basic` | a basic refresh policy is configured |
| `rollingWindowGranularity` | `year` | archive/rolling-window unit |
| `rollingWindowPeriods` | `2` | two periods retained |
| `incrementalGranularity` | `day` | refresh-window unit |
| `incrementalPeriods` | `30` | 30 periods refreshed |
| `incrementalPeriodsOffset` | `-1` | one complete refresh period is excluded from the window head |
| `pollingExpression` | `List.Max(FactEvents_Policy[LastModified])` with null handling | change-detection polling expression |
| `sourceExpression` | retained filtered M | source M used for new policy partitions |
| `mode` | absent | no explicit persisted mode claim is made |

`FactEvents_FilterOnly` retains the same parameter filter and no `refreshPolicy`. RangeStart/RangeEnd
usage alone therefore does not prove policy configuration. **[verified from persisted files]**

After save, close, reopen and save, the author confirmed the two-year archive, 30-day refresh, complete
days, `LastModified` change detection and real-time DirectQuery off. **[manually verified in Power BI
Desktop]** The persisted `-1` offset is presented as complete periods for this day-granularity policy;
the raw offset remains inventory evidence. Because `mode` is omitted, PBI Assure does not display a
real-time state for this fixture rather than guessing from a default. **[design decision]**

## Query-folding boundary

Desktop warned that it could not confirm whether the inline `#table` query could fold. **[manually
verified in Power BI Desktop]** The policy block proves authored configuration only. It does not prove
query folding, source support, refresh efficiency, successful service processing, historical partitions,
hybrid partitions or working change detection. PBI Assure therefore adds no folding or refresh-health
Finding. **[design decision]**

## Implementation

`SemanticTableInventory.RefreshPolicy` additively retains the explicit policy fields. The parser reads
only a `refreshPolicy` object owned by the table; it never derives one from named expressions or M text.
The generated HTML adds a compact **Incremental refresh** feature block to the affected semantic table,
showing archive window, refresh window, complete periods and change detection, followed by the evidence
boundary. The filter-only table has no block. **[verified in repository]**

The polling expression is retained verbatim. A structural column dependency is created only when the
expression contains exactly one explicit qualified reference to the owning table. The Desktop expression
therefore makes `FactEvents_Policy[LastModified]` structurally required independently of Auto Date/Time.
A custom polling query or ambiguous expression remains retained without inventing a model-column edge.
**[design decision]**

The public JSON inventory is additively versioned from `0.23` to `0.24`. CSV is unchanged. No Finding or
severity is added. **[verified in repository]**

## Remaining limitations

- No query-folding assessment.
- No Power BI Service refresh, partition or effective-policy observation.
- No claim about real-time mode when the TMDL property is absent.
- Custom polling M is retained but not interpreted as a model-column dependency.
- Only the exact Desktop-authored basic-policy shape is fixture-backed; other persisted policy forms need
  their own evidence before richer interpretation.

## Next task

Return to the banked inactive-relationship evidence and scope the bounded structured
`USERELATIONSHIP` call extractor. Do not infer activation from flat endpoint-column references.
