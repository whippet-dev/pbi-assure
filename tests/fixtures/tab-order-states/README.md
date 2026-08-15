# Tab-order states fixture

This is a Power BI Desktop-authored PBIP fixture used to preserve confirmed tab-order behaviour.

Its baseline page had four cards with explicit PBIR values: Card A `3000`, Card B `2000`, Card C `1000`, and Card D `0`.

Removing Card B from the Power BI Desktop **Tab order** pane changed only its value to `-9999000`. Any negative value is therefore an explicit exclusion marker; its magnitude is not stable.

To reproduce a state seen in older or converted PBIR reports, the `tabOrder` property was then manually removed from Card C while Power BI Desktop was closed. When reopened without saving, Desktop kept Card C in the Tab order pane, placed it first, and keyboard navigation reached it first. Opening the project did not recreate the property.

After saving, Desktop normalised the values to Card C `2000`, Card A `1000`, Card D `0`, and Card B `-1`.

The fixture deliberately preserves the meaningful pre-save input state:

- Card A: explicit non-negative rank (`3000`)
- Card B: explicit negative exclusion (`-9999000`)
- Card C: `tabOrder` absent, included using Power BI's default order
- Card D: explicit zero rank (`0`)

Do not open and save this fixture in Power BI Desktop, because that would normalise Card C back to an explicit value.
