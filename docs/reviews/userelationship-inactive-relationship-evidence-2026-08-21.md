# Inactive relationship / USERELATIONSHIP evidence review

Date: 2026-08-21
Starting revision: `4e187aada22ca49285d9fe797792ac9f58ee9ca7`

Evidence labels: **[verified in repository]** checked in source or tests · **[verified from persisted
files]** checked in the Desktop-authored fixture · **[manually verified in Power BI Desktop]** observed
by the fixture author in Desktop · **[inferred]** reasoned from the evidence · **[design decision]** a
proposed safe product boundary.

## Conclusion

The Desktop fixture establishes a useful report-review question, but the current DAX dependency stream
cannot answer it safely. PBI Assure extracts flat column, measure, table and declared-UDF references. It
does not retain the built-in `USERELATIONSHIP` call, argument boundaries, argument order or the fact that
two column references form one endpoint pair. A renderer-only or relationship-inventory-only change
would therefore reconstruct semantics from generic co-occurrence and could associate the wrong
relationship. No activation feature is implemented in this slice. **[verified in repository]**

## Desktop evidence

The committed `tests/fixtures/desktop-userelationship-evidence` project contains four relationships:

| Sales endpoint | Customers endpoint | Active | Activating measure | Used by report |
|---|---|---:|---|---:|
| `BillingCustomerID` | `CustomerID` | Yes | none required | Yes, through normal model filtering |
| `ShippingCustomerID` | `CustomerID` | No | `Sales by Shipping Customer` | Yes |
| `ReferralCustomerID` | `CustomerID` | No | `Sales by Referral Customer` | No |
| `LegacyCustomerID` | `CustomerID` | No | none | No |

The visual uses customer name, total sales and the shipping measure. Both activating measures and all
four relationship states survived save, close, reopen and save. **[verified from persisted files]**

The author observed Alice 300 / 800, Ben 300 / 500, Chloe 400 / 800 and Daniel 1100 / blank for billing
and shipping totals. **[manually verified in Power BI Desktop]** Codex did not independently execute
Desktop.

## Current code path and exact missing layer

1. `TmdlSemanticModelParser` retains measure expressions and relationship endpoints/state.
2. `DaxReferenceExtractor` scans expressions into independent `DaxReference` tokens.
3. A built-in identifier followed by `(` is skipped as a function name; only declared DAX UDF calls
   receive function-reference identity.
4. `SemanticDependencyAnalyzer` turns the remaining flat references into graph edges and publishes
   per-node `SemanticNodeReachability`.
5. `SemanticRelationshipInventory` retains endpoints and active state, but no activation evidence.

The smallest safe missing layer is a bounded structured-call extractor for `USERELATIONSHIP`. It must
retain the source calculation and exactly two explicit, qualified column arguments as one call. It must
ignore text in comments/strings, reject expressions that are not two simple column references, and
preserve ambiguity/no-match outcomes rather than guessing. This can reuse the existing model resolver and
reachability after extraction; it does not require a general DAX AST. **[inferred]**

## Resolution and presentation design

An extracted pair can match a relationship only when both table/column endpoints match one relationship
exactly. Reversed argument order must match the same unordered endpoint pair. Zero or multiple matches
must remain unresolved/ambiguous. **[design decision]**

`SemanticNodeReachability` already distinguishes the report-used shipping measure from the unused
referral measure, including paths through non-user-facing nodes. That is sufficient to distinguish a
detected call in a live report calculation from a call in an unused branch after structured extraction.
**[verified in repository]**

A future additive relationship review can show detected activation calculations and their reachability.
It must not alter semantic object usage states: relationship endpoint columns are already structural
model dependencies, while ordinary DAX dependencies already classify the shipping/referral columns.
**[design decision]**

No normal Finding is justified merely because an inactive relationship has no detected activation. A
neutral review state may say **No USERELATIONSHIP call found in the analysed DAX**. It must not say
**unused** or recommend deletion: calculation groups, external/thin reports and unanalysed consumers
remain real boundaries. **[design decision]**

## Options considered

- **Renderer-only inference:** rejected. The renderer lacks structured call evidence.
- **Infer from flat DAX references:** rejected. Co-occurring endpoint columns do not prove a paired
  `USERELATIONSHIP` call.
- **Mark inactive relationships as used when endpoint columns are reachable:** rejected. Column usage and
  relationship activation are different facts.
- **Bounded structured extraction followed by exact resolution:** preferred future implementation.
- **Relationship Finding:** rejected. The available evidence supports inventory/review, not a defect.

## Current scan control

The fixture scan retains one active and three inactive relationships. Current semantic usage is:

- `Customers[CustomerName]`, `Sales[Total Sales]` and `Sales[Sales by Shipping Customer]` — directly used
- `Sales[Amount]`, `Sales[ShippingCustomerID]` and `Customers[CustomerID]` — indirectly used
- `Sales[BillingCustomerID]`, `Sales[ReferralCustomerID]` and `Sales[LegacyCustomerID]` — structurally
  required as relationship endpoints
- `Sales[Sales by Referral Customer]`, `Sales[SaleID]` and `Sales[ControlUnused]` — apparently unused

The shipping measure is report-reachable; the referral measure is not. No relationship activation state
is currently serialized or displayed. **[verified in repository]**

## Decision and next task

Implementation should wait for the bounded structured-call extractor. The Desktop evidence is now banked,
so the next ranked product-value investigation is the Desktop incremental-refresh policy fixture. That
task is not started here.
