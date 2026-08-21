# Desktop USERELATIONSHIP evidence

This synthetic Power BI Desktop PBIP fixture records inactive relationships and DAX measures that call
`USERELATIONSHIP`. It contains no private, organisational or production data.

## Provenance

- Source path: `C:\Users\morty\Downloads\PBI Assure Testing\desktop-userelationship-evidence`
- The project was created and saved in Power BI Desktop, then closed, reopened and saved again.
- Local `.pbi` cache/settings, generated outputs and `diagramLayout.json` are excluded from the repository
  copy. The Desktop version was not retained and is not inferred.

## Controlled model

`Customers` has `CustomerID` and `CustomerName`. `Sales` has four customer-key columns, `Amount`, and a
deliberately unused control column.

Desktop retained these relationships after the round trip:

- active: `Sales[BillingCustomerID]` to `Customers[CustomerID]`
- inactive: `Sales[ShippingCustomerID]` to `Customers[CustomerID]`
- inactive: `Sales[ReferralCustomerID]` to `Customers[CustomerID]`
- inactive: `Sales[LegacyCustomerID]` to `Customers[CustomerID]`

The model contains:

- `Sales[Total Sales]`
- `Sales[Sales by Shipping Customer]`, which calls `USERELATIONSHIP` for the shipping relationship
- `Sales[Sales by Referral Customer]`, which calls `USERELATIONSHIP` for the referral relationship

The only visual uses `Customers[CustomerName]`, `Sales[Total Sales]` and
`Sales[Sales by Shipping Customer]`. The referral measure is deliberately not used, and no measure calls
the legacy relationship.

## Evidence boundary

**[verified from persisted files]** Desktop retained the four relationship definitions, their active
states, both `USERELATIONSHIP` expressions and the visual's three current fields after save, close,
reopen and save.

**[manually verified in Power BI Desktop]** the table displayed Alice as 300 billing / 800 shipping,
Ben as 300 / 500, Chloe as 400 / 800 and Daniel as 1100 / blank. Codex did not independently perform
those Desktop interactions.

**[verified in repository]** PBI Assure currently retains the relationships, measure expressions and
ordinary semantic reachability correctly. It does not yet retain the structured `USERELATIONSHIP` call
or the paired arguments needed to identify relationship activation safely. See
`docs/reviews/userelationship-inactive-relationship-evidence-2026-08-21.md`.
