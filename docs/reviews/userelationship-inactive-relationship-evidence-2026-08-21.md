# Inactive relationship / USERELATIONSHIP evidence review

Date: 2026-08-21
Starting revision: `4e187aada22ca49285d9fe797792ac9f58ee9ca7`

Evidence labels: **[verified in repository]** checked in source or tests · **[verified from persisted
files]** checked in the Desktop-authored fixture · **[manually verified in Power BI Desktop]** observed
by the fixture author in Desktop · **[inferred]** reasoned from the evidence · **[design decision]** a
proposed safe product boundary.

## Conclusion

The Desktop fixture establishes a useful report-review question. PBI Assure now retains the built-in
`USERELATIONSHIP` call only when it contains exactly two explicit, qualified column arguments, then
resolves that pair against one exact relationship. A renderer-only or relationship-inventory-only change
would still reconstruct semantics from generic co-occurrence and could associate the wrong relationship.
**[verified in repository]**

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
5. A separate bounded extractor preserves eligible `USERELATIONSHIP` endpoint pairs without changing the
   ordinary flat-reference stream. It resolves a pair only when one relationship has the exact endpoints,
   in either argument order, then uses the source node's existing reachability.
6. `SemanticRelationshipInventory` additively retains inactive-relationship activation state and the
   calculation sources that established it.

The bounded extractor retains the source calculation and exactly two explicit, qualified column arguments
as one call. It ignores text in comments/strings, rejects expressions that are not two simple column
references, and preserves ambiguity/no-match outcomes by not classifying them. It reuses the existing
model resolver and reachability after extraction; it is not a general DAX AST. **[verified in repository]**

## Resolution and presentation design

An extracted pair can match a relationship only when both table/column endpoints match one relationship
exactly. Reversed argument order must match the same unordered endpoint pair. Zero or multiple matches
must remain unresolved/ambiguous. **[design decision]**

`SemanticNodeReachability` already distinguishes the report-used shipping measure from the unused
referral measure, including paths through non-user-facing nodes. That is sufficient to distinguish a
detected call in a live report calculation from a call in an unused branch after structured extraction.
**[verified in repository]**

The relationship review shows detected activation calculations and their reachability. It does not alter
semantic object usage states: relationship endpoint columns are already structural model dependencies,
while ordinary DAX dependencies already classify the shipping/referral columns. **[verified in repository]**

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

The fixture scan retains one active and three inactive relationships. Shipping is **Activated by
report-used DAX**, Referral is **Referenced only by unused DAX**, and Legacy has **No USERELATIONSHIP call
found in analysed DAX**. Billing remains visually normal because it is active. Calls in comments/strings,
malformed calls, non-simple arguments, unresolved endpoints and non-unique relationship matches remain
unclassified. **[verified in repository]**

Semantic usage remains:

- `Customers[CustomerName]`, `Sales[Total Sales]` and `Sales[Sales by Shipping Customer]` — directly used
- `Sales[Amount]`, `Sales[ShippingCustomerID]` and `Customers[CustomerID]` — indirectly used
- `Sales[BillingCustomerID]`, `Sales[ReferralCustomerID]` and `Sales[LegacyCustomerID]` — structurally
  required as relationship endpoints
- `Sales[Sales by Referral Customer]`, `Sales[SaleID]` and `Sales[ControlUnused]` — apparently unused

The shipping measure is report-reachable; the referral measure is not. No relationship activation state
is currently serialized or displayed. **[verified in repository]**

## Decision and next task

The bounded extractor is complete. Its relationship-review metadata is additive in JSON schema `0.25`;
CSV and Findings remain unchanged. The next recommended task is the report-level measure DAX dependency
gap, using the already documented Desktop evidence process.
