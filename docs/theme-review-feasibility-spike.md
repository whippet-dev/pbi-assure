# PBI Assure Theme Review feasibility spike

Research date: 12 August 2026

Status: research only. This document does not define production behaviour or assurance rules.

## 1. Executive recommendation

A dedicated Theme Review capability is technically viable, but it should be built in layers and should not initially claim to reproduce Power BI's formatting engine.

The reliable starting point is:

- identify the report's base-theme and custom-theme references;
- resolve the active local theme resources where they exist;
- inventory theme colours, text classes and `visualStyles`;
- inventory formatting persisted in `visual.objects`, `visualContainerObjects`, page objects and report objects;
- compare a supported literal visual value with a supported, locally available theme rule;
- describe the result as a persisted-value comparison, not proof of the developer's intent or editing history.

The unsafe starting point would be to label every serialized visual property as a manual override, or every colour outside `dataColors` as an accessibility failure. The local samples demonstrate why: an older report with a custom theme serializes formatting for almost every visual, including many values that exactly match the theme. PBIR preserves the value, but not a reliable provenance trail explaining whether it was applied by theme import, copied with a visual, explicitly edited, or retained from an older save format.

Recommended product boundary:

> PBI Assure can reliably inventory active theme metadata and can prove some saved-value/theme-value matches and differences. It cannot yet reconstruct every effective rendered property or reliably distinguish every manual edit from formatting materialized by Power BI.

A dedicated **Theme Review** tab remains the right design. Consistency observations are high-volume, contextual and rarely defects by themselves. Only a later, deliberately small set of high-confidence accessibility failures should be candidates for the general Findings surface.

Before production comparison work, create a small Power BI Desktop-authored before/after fixture pack. Artificially editing JSON would test the parser, but would not answer the central question of what Desktop writes when a property is applied, reset, inherited, conditionally formatted or retained as stale metadata.

## 2. PBIR theme discovery findings

### 2.1 Authoritative metadata

PBIR stores report theme selection in `definition/report.json` under `themeCollection`. The report schema URI in the current samples is:

```text
https://developer.microsoft.com/json-schemas/fabric/item/report/definition/report/3.3.0/schema.json
```

The representative current report contains:

```json
"themeCollection": {
  "baseTheme": {
    "name": "CY19SU06",
    "reportVersionAtImport": {
      "visual": "1.8.39",
      "report": "2.0.39",
      "page": "1.3.39"
    },
    "type": "SharedResources"
  },
  "customTheme": {
    "name": "Music_Charts3284044798188317.json",
    "reportVersionAtImport": {
      "visual": "1.8.40",
      "report": "2.0.40",
      "page": "1.3.40"
    },
    "type": "RegisteredResources"
  }
}
```

The same `report.json` contains resource-package items that map those logical references to files:

```text
SharedResources / BaseTheme   / CY19SU06
  -> BaseThemes/CY19SU06.json

RegisteredResources / CustomTheme / Music_Charts3284044798188317.json
  -> Music_Charts3284044798188317.json
```

The files are held outside `definition`:

```text
<report>.Report/
  StaticResources/
    SharedResources/BaseThemes/CY19SU06.json
    RegisteredResources/Music_Charts3284044798188317.json
```

Microsoft documents `RegisteredResources` as report-specific files loaded by the user, including custom themes, and states that each registered resource has a corresponding entry in `report.json`. Microsoft also documents `themeCollection.baseTheme` in PBIR report metadata. See [Power BI Desktop project report folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report).

### 2.2 Safe active-resource resolution

For PBIR, the safe resolution sequence is:

1. Read `themeCollection` from `definition/report.json`.
2. Treat `baseTheme` and `customTheme` as separate ordered layers.
3. Match each reference by package/type/name against the corresponding `resourcePackages` item.
4. Resolve the item's path under the named `StaticResources` package.
5. Validate that the file exists and has a theme-like JSON shape.
6. Record missing, duplicate or malformed resolutions rather than guessing.

Do not select an active theme merely because a JSON file in `RegisteredResources` contains `dataColors` or `visualStyles`. That identifies a theme-like resource, not the active resource. An unreferenced file should be inventoried as an unused or unselected resource, with conservative wording because resource-retention behaviour still needs a Desktop-authored replacement/removal fixture.

### 2.3 What can be classified

| Question | Reliable answer |
|---|---|
| Is there a base theme reference? | Yes, when `themeCollection.baseTheme` exists. |
| Is there an active custom-theme layer? | Yes, when `themeCollection.customTheme` exists and can be resolved. |
| Which local custom theme file is active? | Deterministic when `themeCollection`, `resourcePackages` and the file agree. |
| Is the report using no theme? | No. Microsoft states every report has a base theme. The safe label is **base theme only** or **base theme metadata unavailable**, not **no theme**. |
| Is a custom theme imported, built in, organisational, or created through Customize current theme? | Not reliably from the serialized custom-theme resource alone. These paths can all produce a custom layer. |
| Is the active custom theme a modified version of another theme? | Not without a supplied reference/original theme or retained provenance metadata. The file content can be inventoried, but its ancestry cannot be proved. |
| Is an extra registered theme file stale? | It can be proved to be unselected by the current `themeCollection`; why it remains and whether it is safe to remove need a controlled save-history fixture. |

### 2.4 Missing and implicit base themes

Most current PBIR samples reference a local base theme, for example `CY26SU07` under `SharedResources/BaseThemes`. However, `samples-local/IT Spend Analysis Sample/.../definition/report.json` has an empty `themeCollection` and no theme resource package. Microsoft states that every report still has a base theme. This is evidence that PBI Assure must support an **implicit or unavailable built-in base** state rather than assuming the full base JSON is always locally available.

The absence of a local base JSON prevents complete value reconstruction. PBI Assure may report the metadata that is present, but it must not manufacture a current Power BI default and apply it retrospectively to an older report.

### 2.5 PBIR-Legacy note

The legacy Sales & Returns sample stores `themeCollection` inside the JSON string held in top-level `config`, using numeric resource types and `version` rather than the current PBIR shape. The same active custom theme can still be identified, but this is a distinct parser path. The first Theme Review phase should target the current PBIR `definition` format. Legacy support can be added explicitly rather than silently sharing assumptions.

## 3. Theme JSON structure findings

Microsoft's custom-theme documentation says `name` is the only required field; other settings are optional and fall back to the base theme. See [Create custom report themes](https://learn.microsoft.com/en-us/power-bi/create-reports/report-themes-create-custom) and the authoritative [Microsoft Report Theme JSON Schema repository](https://github.com/microsoft/powerbi-desktop-samples/tree/main/Report%20Theme%20JSON%20Schema).

The schema is versioned with Power BI Desktop and continues to grow. At research time, the repository lists `reportThemeSchema-2.156.json` for July 2026. It is approximately 1.17 MB. A future implementation must retain the PBIR/theme version as evidence and avoid treating a hand-written property list as permanently complete.

### 3.1 Representative local themes

| Theme | Source | Top-level coverage | Palette | `visualStyles` |
|---|---|---|---:|---:|
| `CY26SU07` | Shared/base | structural and semantic colours, four primary text classes, visual styles | 41 entries / 40 distinct | 36 visual keys, about 51 formatting-card names |
| `CY19SU06` | Shared/base | similar older structure | 41 entries / 40 distinct | 25 visual keys, about 16 formatting-card names |
| `Music Charts` | Registered/custom | `name`, `dataColors`, `visualStyles` only | 480 entries / 468 distinct | `barChart` and `columnChart` only |

The active Music Charts theme therefore does not replace every base setting. Its two chart-type rule sets and palette layer over `CY19SU06`; omitted structural colours, text classes and other visual types fall back to the base theme.

### 3.2 Available areas

The sampled base themes expose:

- `name`;
- `dataColors`;
- semantic/sentiment colours such as `good`, `neutral`, `bad`, `maximum`, `center`, `minimum` and `null`;
- structural colours such as `foreground`, `foregroundNeutralSecondary`, `background`, `backgroundLight`, `backgroundNeutral` and `tableAccent`;
- hyperlink and visited-hyperlink colours;
- `textClasses` including `callout`, `title`, `header` and `label`;
- `visualStyles` with global wildcard rules, visual-type-specific rules, formatting-card rules and page rules.

Current documentation also uses preferred aliases such as `firstLevelElements`, `secondLevelElements`, `thirdLevelElements`, `fourthLevelElements` and `secondaryBackground`. Older saved themes use the corresponding legacy names. A later parser should normalize aliases without discarding their original name and evidence path.

### 3.3 Text-class inheritance

The primary text classes carry font family, size and colour. Microsoft documents secondary classes derived from the primary classes, including large, light, bold and small variants. Those derived values can be implicit. Therefore:

- an explicitly present text-class value is inventory-grade evidence;
- a documented derivation may be modeled later, with version awareness;
- an omitted value must not be assigned a hard-coded universal default unless the applicable base theme or documented derivation is known.

There is no WCAG minimum font-size success criterion that makes a small theme font an automatic failure. Font size and font family are useful review information, not deterministic accessibility findings by themselves.

### 3.4 `visualStyles`

The public shape is:

```text
visualStyles
  -> visual type or "*"
    -> style preset name or "*"
      -> formatting card name or "*"
        -> array of property bags
```

The default preset is `*`. Named presets inherit from the default preset. Formatting cards can include discriminators such as `$id`, and the property-bag array is meaningful; it must not be flattened without retaining those selectors/discriminators.

The documentation explicitly warns that PBIR `objects` and theme JSON have different wrappers, even when their card and property names translate. For example, PBIR uses an `expr/Literal/Value` wrapper where the theme uses a typed scalar. A comparison engine therefore needs normalization rather than raw JSON equality.

### 3.5 Page and report settings

The base themes include a `page` visual-style entry. Current reports also persist report-level `objects` and page-level `objects`, including backgrounds, wallpaper/outspace and filter-pane settings. Theme Review should eventually inventory these layers, but the first visual-consistency comparison can safely begin with a small whitelist of well-understood visual/container properties.

`tableAccent` and the structural colour classes affect multiple contexts; they are not proof that any arbitrary table border or text/background pair is rendered with those exact values.

## 4. Visual formatting structure findings

PBIR visual files in the current samples use visual-container schema `2.11.0`. Formatting is primarily under:

- `visual.objects`: visual-specific formatting cards such as axes, labels, values, data points and line styles;
- `visual.visualContainerObjects`: common container formatting such as title, background, border and visual header;
- selectors attached to an object instance;
- expression nodes inside property values.

Not everything under these nodes is a theme-comparison candidate. Actions, tooltips, navigation and data-bound configurations can use similar wrappers but describe behaviour rather than appearance.

### 4.1 Serialization varies materially by report history/version

| Sample | Visuals | With `visual.objects` | With `visualContainerObjects` | With neither |
|---|---:|---:|---:|---:|
| Columns Usage (`CY26SU07`, no custom theme) | 10 | 5 | 1 | 5 |
| Tab Order Test (`CY26SU07`, no custom theme) | 5 | 0 | 4 | 1 |
| Sales & Returns (`CY19SU06` + Music Charts) | 166 | 163 | 161 | 0 |

The Sales & Returns report originated from an older report generation and contains extensive persisted formatting. The newer samples are much sparser. This makes raw property count unsuitable as an override count.

### 4.2 Exact theme/visual comparison example

For the Sales & Returns bar chart at:

```text
definition/pages/ReportSection4b3fbaa7dd7908d906d9/
  visuals/3a28c5fee26bd29ff352/visual.json
```

the active Music Charts `barChart/*` rules compare as follows after normalizing PBIR literal wrappers:

| Property | Custom-theme value | Persisted visual value | Comparison |
|---|---:|---:|---|
| `categoryAxis.labelColor` | `#73738E` | `#73738E` | equal |
| `categoryAxis.fontSize` | `10` | `10` | equal |
| `categoryAxis.titleFontSize` | `10` | `10` | equal |
| `legend.fontSize` | `10` | `10` | equal |
| `labels.fontSize` | `9` | `10` | different |
| `title.fontSize` | `12` | `12` | equal |

This proves that PBI Assure can compare supported values. It does **not** prove which equal values were manually set, applied when the theme was imported, copied from another visual, or materialized during format conversion.

### 4.3 Expression types

The same bar chart contains both:

- `ThemeDataColor` for one data-point colour; and
- a `Conditional` expression with a `dataViewWildcard` selector for another.

These are not equivalent to literal colour overrides:

- `ThemeDataColor` is theme-linked by index/variant and should be classified separately from a literal colour;
- `Conditional` depends on data and filter context, so its final colour cannot be established statically;
- a DAX field-value expression may return a literal hex value or a named theme colour at runtime, which remains unknown without evaluation.

### 4.4 Selectors

The controlled stale-reference samples demonstrate at least these selector shapes:

- a `scopeId` expression identifying a particular series/category member;
- `dataViewWildcard` applying formatting over changing data members;
- `metadata` naming a bound field/aggregation;
- `id: "default"` for a default state.

Selector identity is part of the formatting key. A per-series line style must not be compared as if it were the visual-wide line style. Stale selector metadata must also be reconciled with the current query/field bindings before it contributes to deviation counts.

### 4.5 Custom visuals

The Sales & Returns sample includes custom visual type identifiers such as `PBI_CV_...`. Their formatting cards are visual-defined and do not automatically map to the native report-theme schema. Some custom visuals consume theme colours or expose themeable properties; others do not. The default position must be **unsupported/conservative**, unless a future adapter is built for a known custom visual and version.

## 5. Effective-value resolution limitations

Microsoft documents the broad precedence chain:

1. visual-specific formatting takes precedence;
2. the custom theme layers over the base theme;
3. omitted custom-theme settings fall back to the base theme;
4. remaining behaviour is supplied by Power BI defaults/rendering.

See [Use report themes in Power BI](https://learn.microsoft.com/en-in/power-bi/create-reports/desktop-report-themes) and [Visual defaults in Power BI reports](https://learn.microsoft.com/en-us/power-bi/create-reports/power-bi-reports-visual-defaults).

PBI Assure can only reconstruct a subset of this chain.

### 5.1 Strongly available

- active base/custom references and locally available theme JSON;
- explicit theme rules and typed theme values;
- persisted visual/container/page/report formatting nodes;
- literal values after type normalization;
- expression kind, such as literal, theme colour reference or conditional;
- selector structure and evidence path;
- comparison between a supported literal persisted value and a resolved supported theme candidate.

### 5.2 Partly available

- wildcard, visual-specific and preset layering, once tested against controlled fixtures;
- structural/text-class inheritance when a local base theme and version-correct rule are available;
- `ThemeDataColor` linkage to a palette entry, although shade/variant rendering and generated colours need testing;
- page/background composition when every contributing literal colour, visibility and transparency value is known.

### 5.3 Not generally reconstructable

- why a persisted value exists;
- every built-in default when no full base theme is packaged;
- renderer defaults omitted by both base and custom JSON;
- final values of conditional, field-value or measure-driven formatting;
- series/category values and order under runtime filter context;
- all selector/preset merge semantics across PBIR and Power BI versions;
- custom visual rendering;
- the final pixel background behind translucent, layered or image-backed content;
- interactive/highlight/selection/focus state without the relevant rendered context.

### 5.4 Safe language boundary

Use:

- **No saved local value; applicable theme rule found**
- **Saved value matches the theme rule**
- **Saved value differs from the theme rule**
- **Theme-linked colour reference**
- **Dynamic or conditional value**
- **Theme value unavailable**
- **Unsupported or ambiguous mapping**

Avoid initially:

- **Developer manually overrode this**
- **This visual is definitely aligned with the rendered theme**
- **This is the effective value** when a fallback or renderer default is missing
- **This colour is wrong** solely because it is outside `dataColors`

## 6. Proposed deviation taxonomy

Use two dimensions rather than a single severity-like label.

### 6.1 Formatting source/state

| State | Meaning |
|---|---|
| `NoPersistedValue` | No property is stored at the supported visual scope. A theme candidate may apply. |
| `PersistedLiteral` | A scalar/colour/font value is stored. Its provenance is unknown. |
| `ThemeReference` | The value is linked to a theme colour or structural theme token. |
| `DynamicExpression` | Conditional, field/measure or other runtime expression. |
| `SelectorScoped` | Value is limited to a series/category/state selector. Can combine with the above. |
| `Unsupported` | The value or context is not safely normalized. |

### 6.2 Comparison result

| Result | Required evidence |
|---|---|
| `ThemeCandidateOnly` | No persisted supported value; a supported theme rule is known. |
| `MatchesThemeCandidate` | Normalized persisted literal equals the resolved theme candidate. |
| `DiffersFromThemeCandidate` | Normalized persisted literal differs from the resolved theme candidate. |
| `ThemeLinked` | Expression refers to the active theme rather than a fixed literal. |
| `DynamicNotComparable` | Final value depends on runtime state. |
| `ThemeUnavailable` | Applicable custom/base/built-in value cannot be resolved. |
| `AmbiguousMapping` | Property, preset, selector or custom visual mapping is not established. |

Each result should also carry confidence (`Strong`, `Conservative`, `Unsupported`) and evidence paths for both sides.

### 6.3 Meaningful aggregation

Do not turn each difference into a finding. Aggregate supported comparisons by:

- property family: font family, font size, text colour, background, border, data colour;
- exact property: for example visual title font size;
- visual type;
- page;
- theme-linked versus fixed literal versus dynamic;
- theme candidate value and most common alternative value;
- selector scope;
- peer-group outlier status.

Useful statements include:

- `42 of 58 supported visual titles store a font family different from the theme candidate`;
- `18 titles store 14 pt while the theme candidate is 16 pt`;
- `7 supported literal colours are outside the active palette; review whether this is intentional`;
- `Page X has a higher mapped-difference density than the report median`;
- `One column chart differs from every other comparable column chart`.

Percentages must use an explicit eligible denominator. Unsupported properties, dynamic values and unknown theme values should not silently count as aligned or different.

## 7. Accessibility checks

### 7.1 Deterministic / strong

These are strong only when the exact rendered context is represented by a tested mapping:

| Candidate check | Conditions for strong classification |
|---|---|
| Text contrast | Literal foreground and literal background are both known for the same visible text context; opacity/compositing is known; large-text status is known if using the 3:1 exception. |
| Table/matrix text contrast | A tested mapping resolves the exact text role and its opaque cell/background state. |
| Hyperlink contrast | Exact hyperlink text colour and actual background for the same context are known. |
| Non-text control/graphic contrast | The meaningful graphical/UI component colour and its adjacent colour are both known for a tested state. |
| Invalid colour value | Theme value cannot be parsed/validated. This is a theme integrity issue, not automatically an accessibility failure. |

WCAG 2.2 SC 1.4.3 requires 4.5:1 for normal text and 3:1 for large-scale text. SC 1.4.11 requires 3:1 for visual information needed to identify UI components and meaningful graphics. See W3C's [Contrast (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum) and [Non-text Contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast).

Microsoft's Power BI accessibility checklist asks authors to ensure at least 4.5:1 between title, axis-label and data-label text and their backgrounds. See [Design Power BI reports for accessibility](https://learn.microsoft.com/en-us/power-bi/create-reports/desktop-accessibility-creating-reports).

Top-level `foreground` and `background` alone should initially produce a structural-theme contrast observation, not a guaranteed failure for every visual. Microsoft documents those structural colours as feeding many different contexts; some are text, some lines, fills or states.

### 7.2 Heuristic / Review only

- small default text sizes;
- unusual, narrow or potentially unavailable font families;
- incomplete dark-theme structural colour definitions;
- transparent visual backgrounds whose page/wallpaper composition is uncertain;
- selection, highlight or focus colours without the rendered adjacent colour/state;
- literal colours outside `dataColors`;
- duplicate or near-duplicate palette entries;
- low luminance separation between palette entries;
- red/green or other commonly difficult hue combinations;
- simulated colour-vision-deficiency similarity;
- many local differences from a theme;
- inconsistent fonts, sizes or colours across peer visuals.

These are prompts for developer review. WCAG does not say that a particular font family, font size, palette membership or pairwise palette distance is automatically a failure.

### 7.3 Not reliably possible from theme metadata alone

- whether a chart uses colour as the only means of conveying information;
- whether labels, markers, patterns, alt text or a data table provide an alternative;
- actual series order and adjacency after filtering;
- final conditional-formatting colours;
- contrast over images, gradients or unknown/translucent layers;
- custom visual rendering and focus behaviour;
- browser/host high-contrast substitution;
- font loading, glyph shape, anti-aliasing and final pixels;
- whole-report WCAG conformance.

WCAG SC 1.4.1 requires that colour not be the only visual means of conveying information. A palette cannot establish whether the report supplies text, shape, markers or another cue. See W3C's [Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color).

## 8. Palette accessibility analysis options

### 8.1 Safe inventory observations

- palette length and distinct-colour count;
- exact duplicates and their positions;
- malformed or non-literal entries;
- theme-linked versus fixed literal colours used by comparable visuals;
- literal colours that are/are not exact palette entries;
- named semantic colours (`good`, `bad`, `neutral`, hyperlinks, divergent endpoints).

The sampled `CY26SU07` and `CY19SU06` palettes each contain 41 entries with 40 distinct values. The Music Charts custom theme contains 480 entries with 468 distinct values. Exact duplication is deterministic, but it is not automatically an accessibility problem: repeated entries may never be used together.

### 8.2 Review heuristics

- pairwise relative-luminance contrast;
- adjacent-entry contrast in declared palette order;
- perceptual colour distance, using a documented colour space/metric;
- colour-vision-deficiency simulation and post-simulation distance;
- clusters of near-duplicate colours;
- common red/green reliance indicators.

These results should be worded as `potentially difficult to distinguish` and show the method/threshold. Thresholds for perceptual similarity are not WCAG pass/fail thresholds and can vary by display, size, adjacency and the graphical encoding.

### 8.3 Important limitations

Microsoft documents that series colours are allocated by current series order, which can change for dynamic series and between visuals. Power BI can also generate further colours when a palette is exhausted. A static palette cannot prove which colours are adjacent, simultaneously visible, semantically important or supplemented by labels/markers.

Therefore palette checks belong on Theme Review as Review observations, not Errors. Even exact duplicates should normally be a neutral observation or Review item unless a later visual-level analysis proves that indistinguishable colours encode separate simultaneously visible data without another cue.

## 9. Controlled fixture findings

No existing sample was modified. The current corpus covers enough structure for the inventory recommendation, but not enough Power BI Desktop editing history to validate a production deviation resolver.

| Case | Existing evidence | Coverage |
|---|---|---|
| Base theme only | `samples-local/Columns Usage` and `Tab Order Test Sample` | Active shared base resource; sparse visual formatting; visuals with no saved formatting. |
| Implicit/unavailable base | `samples-local/IT Spend Analysis Sample` | Empty `themeCollection`; no local theme resource. |
| Custom theme | `samples-local/Sales & Returns Sampe v201912` | Active registered custom theme layered over local base. |
| Custom `visualStyles` | Music Charts registered resource | Bar/column defaults, fonts, sizes, colours and 480-entry palette. |
| Theme/visual exact match | Sales & Returns bar chart `3a28...` | Multiple normalized equality examples. |
| Theme/visual difference | Same bar chart | `labels.fontSize` theme 9 versus saved 10. |
| Theme colour expression | Same bar chart | `ThemeDataColor`. |
| Conditional formatting | Same chart and `StaleMetadata_table_IconFormat` | `Conditional`, wildcard and metadata selector. |
| Per-series/category selector | `StaleMetadata_Line_01_Formatted` | `scopeId` selector on `lineStyles`. |
| Custom visual | Sales & Returns | Visual-defined formatting objects with no safe native mapping. |
| Page/report formatting | Columns Usage and Sales & Returns | Report, page background/outspace and filter-pane objects. |
| PBIR-Legacy theme | `SamplePBIPLegacy` | Active theme in stringified `config`; separate format. |

### 9.1 Required Desktop-authored fixture pack before Phase 2

Create one small PBIR report and save a copy after each single change:

1. base theme only, untouched visual;
2. simple custom theme with `name`, structural colours and text classes;
3. custom theme with wildcard and visual-type-specific `visualStyles`;
4. untouched visual inheriting a known property;
5. set one visual property to a different value;
6. set the property explicitly to the same value as the theme;
7. reset that property and save again;
8. conditional/field-value formatting;
9. one static-series and one dynamic-series selector;
10. named style preset selected and reset;
11. custom font family;
12. opaque known poor-contrast text/background pair;
13. opaque known high-contrast pair;
14. multiple registered theme resources with only one active;
15. replace/remove a custom theme and inspect retained resources;
16. change the base theme version;
17. one representative custom visual.

For every step, retain `report.json`, the active theme files and the affected `visual.json`/`page.json`. A manifest should record Desktop version, exact UI action, expected property and whether the report was converted from legacy format. This fixture pack is more valuable than broad implementation at this stage.

## 10. Proposed architecture

Do not put theme parsing directly into assurance rules. Theme data is report inventory and analysis data used by reporting, accessibility review and a future supplied-reference comparison.

### 10.1 Small internal model

```text
ThemeInventory
  BaseSource
  CustomSource?
  ActiveLayers
  RegisteredThemeResources
  Properties / Rules
  ResolutionIssues

ThemeSource
  Kind: BaseShared | CustomRegistered | ImplicitBuiltIn |
        ReferencedButUnavailable | Unknown
  ReferenceName / ResourcePath / ThemeName
  ReportVersionAtImport / SchemaEvidence

ThemeProperty
  Layer
  Scope: top-level | text class | visual style | page
  VisualType / Preset / Card / Discriminator / Property
  NormalizedValue
  EvidencePath

PersistedFormattingValue
  Report / Page / Visual
  Storage: objects | visualContainerObjects | page | report
  Card / Instance / Property
  SelectorKind / ExpressionKind
  NormalizedValue?
  EvidencePath

ThemePropertyMatch
  FormattingState
  ComparisonResult
  Confidence
  ThemeCandidate?
  Evidence

ThemeDeviationAggregate
  Property / VisualType / Page
  Eligible / Match / Difference / Dynamic / Unknown counts
  Common values / Outliers

ThemeAccessibilityObservation
  Strength: Strong | Review | Unsupported
  Context / Values / Ratio or heuristic
  Explanation / Evidence
```

`PersistedFormattingValue` is preferable to `VisualFormattingOverride` internally until provenance is proved. The UI can use the familiar word override in explanatory text only if it clearly means a saved value taking precedence, not necessarily a deliberate manual action.

### 10.2 Module placement

- `PbirThemeParser`: read `themeCollection`, resource packages and theme JSON through `IProjectFileSource`; normalize theme structure.
- `PbirVisualFormattingParser`: extract only formatting nodes, expression kinds and selectors from visual/page/report JSON.
- `ThemeAnalyzer`: index active theme layers, resolve supported candidates, compare values and aggregate observations.
- `ThemeAccessibilityAnalyzer`: apply a small context whitelist and produce strong/review observations separately.
- inventory records: carry normalized results to CLI, Desktop and browser without host-specific work.
- `HtmlReportRenderer`: render the later dedicated tab.
- assurance adapter, later and optional: promote only explicitly approved high-confidence observations to normal Findings.

`PbirReportParser` should orchestrate the two PBIR parsers because it already owns report/page/visual file discovery, but it should not contain the theme merge/comparison logic. `ProjectScanner` can call `ThemeAnalyzer` after report parsing, alongside existing semantic and Power Query analyzers.

### 10.3 Resolver design

Pre-index theme rules by a normalized key such as:

```text
(layer, visualType, preset, card, discriminator, property)
```

Retain wildcard rules separately and apply a tested precedence order. Preserve the original JSON path and value. Normalize PBIR literals (`'10'`, `33D`, quoted strings, colour objects) into typed values while retaining raw evidence. Unknown expressions must remain unknown, not collapse to `null` or a default.

## 11. Proposed Theme Review tab UX

### Theme summary

- active base theme and whether its JSON is available;
- active custom theme and resolved file;
- clear source wording: base only, custom layer, or metadata unavailable;
- theme name, imported report-version metadata and resource path in technical details;
- fonts/text classes;
- compact palette swatches and named structural/semantic colours;
- count of visual-style rules and supported visual types;
- resource-resolution issues.

### Consistency

- comparable visual/property count and coverage percentage;
- saved matches, saved differences, theme-linked, dynamic and unknown counts;
- top property-level patterns;
- top alternative values;
- per-page and per-visual-type breakdown;
- outliers;
- expandable visual details with user-facing page name, visual type/title/location and concise property comparison;
- technical paths/IDs only inside technical details.

The tab must explain that a saved difference can be intentional and that a saved match does not prove no manual formatting occurred.

### Accessibility

- strong known-context contrast failures, if any;
- Review observations for incomplete contrast context, typography and palette;
- palette swatches with duplicate/similarity indicators;
- explicit unknown/not-assessed coverage;
- guidance links to Microsoft Power BI accessibility and WCAG/W3C.

### Presentation principles

- summary first, details collapsed;
- patterns and outliers before raw property lists;
- counts always show the eligible denominator;
- separate strong results from Review observations;
- do not use red/yellow severity colours for neutral theme deviations;
- no giant raw JSON viewer as the primary interface;
- provide raw evidence paths only in technical details;
- keep the generated HTML bounded by aggregating repeated matches and lazy-expanding visual details.

## 12. Findings integration recommendation

Default principle:

- theme consistency stays on Theme Review;
- theme inventory/resolution issues stay on Theme Review unless they prevent scan integrity;
- palette and typography heuristics stay as Review observations on Theme Review;
- dynamic/conditional/unknown cases show as not assessed, not failures;
- only a small allowlist of proven, same-context accessibility failures may later become normal Findings.

An example future candidate for Findings is a literal visible text colour against a literal opaque background, with a tested mapping to the same rendered context and a ratio below the applicable threshold. A top-level foreground/background comparison without contextual mapping is not enough.

## 13. Risks and false-positive risks

| Risk | Impact | Mitigation |
|---|---|---|
| Intentional differences | Brand accents or emphasis appear as noise. | Aggregate and describe; do not warn per difference. |
| Persisted-value provenance | Theme-materialized values are mislabelled manual overrides. | Use saved-value language; validate with before/after fixtures. |
| Missing built-in defaults | False differences or false alignment. | `ThemeUnavailable`; never substitute today's defaults for older reports. |
| Theme/schema version changes | Property maps and defaults drift. | Record schema/import version; parser is tolerant; test versioned fixtures. |
| Custom visuals | Native mappings do not apply. | Unsupported by default; versioned adapters only. |
| Dynamic/conditional formatting | Static value is unknowable. | Separate classification; exclude from simple difference counts. |
| Per-series/category selectors | One series is mistaken for a visual-wide setting. | Include selector in key; reconcile current bindings. |
| Stale formatting metadata | Deleted series/fields create false deviations. | Reuse stale-reference classification and exclude stale selectors from headline counts. |
| Named style presets | Wrong candidate rule is selected. | Capture selected preset and fixture-test default/preset inheritance. |
| Multiple/unselected resources | Old theme is mistaken for active. | Resolve only through `themeCollection` and package entry. |
| Incomplete colour context | False WCAG failures. | Strong checks require both values for the same context and known composition. |
| Images/transparency/layers | Computed contrast does not match pixels. | Review/unknown unless composition is fully known. |
| Power BI-generated palette colours | Theme palette analysis misses runtime colours. | State scope; do not claim complete rendered palette. |
| Report conversion history | Old reports serialize much more formatting. | Benchmark and compare by eligible mappings, not raw object density. |

The biggest implementation risk is overclaiming provenance/effective values, not parsing JSON. Active theme discovery is straightforward; faithful reimplementation of Power BI's formatting resolution is not.

## 14. Performance considerations

Let:

- `T` be normalized active theme rules;
- `P` be persisted formatting properties;
- `S` be selector instances;
- `C` be palette colours.

With pre-indexing, theme comparison should be approximately `O(T + P + S)` with constant-time candidate lookup. Do not scan raw theme JSON separately for every visual/property.

Pairwise palette analysis is `O(C²)`. This is trivial for common palettes near 40 entries. The 480-entry Music Charts sample produces about 115,000 unordered pairs, still feasible, but future browser/WASM work should cap expensive simulations, report truncation clearly, or use nearest-neighbour strategies for very large palettes.

Large-report controls:

- parse each JSON file once;
- normalize only formatting candidates, not every behavioural property;
- intern repeated keys/values where useful;
- retain evidence paths rather than full raw JSON subtrees;
- aggregate matches and common differences during analysis;
- store per-visual detail only where needed for expansion;
- exclude unsupported/custom-visual property explosions from headline analysis;
- benchmark native and browser/WASM paths;
- render summary/aggregates first and avoid one HTML row per matching property.

The service's documented PBIR limits include up to 1,000 pages, 1,000 visuals per page, 1,000 resource files and 300 MB of report/resource files. PBI Assure should not assume typical report sizes in parser design, even if practical reports are much smaller.

## 15. Regression-test matrix

| Test | Expected result |
|---|---|
| Active custom theme detection | `customTheme` resolves through the correct package item and file. |
| Base-theme reference | Shared base name/version/path is inventoried. |
| Implicit/missing base JSON | Report is classified unavailable/implicit; no fabricated defaults. |
| Unused registered theme file | File is listed as unselected and never used as active comparison source. |
| Missing referenced theme file | Resolution issue; no fallback to another similarly named JSON file. |
| Exact normalized match | PBIR literal wrapper and typed theme value compare equal. |
| Explicit differing value | Difference is recorded with both evidence paths. |
| Explicit value equal to theme | Match is recorded; not labelled proof of inheritance. |
| Visual with no stored property | Theme candidate may be shown; no false local difference. |
| Theme property absent | Falls back only to a known local base rule; otherwise unavailable. |
| Wildcard versus visual-specific rule | Tested precedence selects the Desktop-observed candidate. |
| Named preset | Preset/default inheritance matches Desktop output. |
| Dynamic formatting | Classified dynamic and excluded from literal comparison. |
| `ThemeDataColor` | Classified theme-linked, not outside-palette literal. |
| Per-series selector | Separate scoped observation, not visual-wide. |
| Conditional wildcard | Dynamic selector retained; no simple deviation. |
| Unsupported property | Ambiguous/unsupported, never assumed aligned. |
| Custom visual | Conservative unsupported unless an adapter fixture exists. |
| Stale selector | Does not enter headline deviation counts. |
| Poor known text/background contrast | Strong observation only for whitelisted same-context opaque pair. |
| Unknown background | No strong contrast failure. |
| Transparent background | Review/unknown unless full composition is resolved. |
| Exact palette duplicates | Deterministic inventory observation, not automatic error. |
| Schema/version fixture | Older and current theme/property aliases normalize without losing evidence. |
| Large synthetic report | Bounded time/memory and HTML size; no quadratic visual comparison. |
| Browser/native parity | Same theme inventory and aggregate results through `IProjectFileSource`. |

Avoid full-document HTML snapshots. Test parsers, resolver outcomes, aggregate counts and focused renderer fragments independently.

## 16. Files/classes likely to change later

Likely new Core files:

```text
src/PbiAssure.Core/Scanning/PbirThemeParser.cs
src/PbiAssure.Core/Scanning/PbirVisualFormattingParser.cs
src/PbiAssure.Core/Scanning/ThemeAnalyzer.cs
src/PbiAssure.Core/Scanning/ThemeAccessibilityAnalyzer.cs
src/PbiAssure.Core/Inventory/ThemeInventory.cs
src/PbiAssure.Core/Inventory/ThemeProperty.cs
src/PbiAssure.Core/Inventory/PersistedFormattingValue.cs
src/PbiAssure.Core/Inventory/ThemePropertyMatch.cs
src/PbiAssure.Core/Inventory/ThemeAccessibilityObservation.cs
```

Likely existing files:

```text
src/PbiAssure.Core/Scanning/PbirReportParser.cs
src/PbiAssure.Core/Scanning/ProjectScanner.cs
src/PbiAssure.Core/Inventory/ReportInventory.cs
src/PbiAssure.Core/Inventory/VisualInventory.cs
src/PbiAssure.Core/Inventory/ProjectInventory.cs
src/PbiAssure.Reporting/HtmlReportRenderer.cs
tests/PbiAssure.Core.Tests/...
```

The exact inventory attachment point should be decided after the fixture pack. Adding raw formatting data to the public serialized inventory could materially increase JSON and HTML size, so prefer normalized observations/aggregates and explicitly selected detail.

CLI, Desktop and Web should not need separate analysis implementations. They already share Core scanning and Reporting; browser file selection must simply include the referenced `StaticResources` files.

## 17. Recommended phased implementation sequence

### Phase 0 — resolver evidence pack

- create the Desktop-authored controlled fixtures;
- record exact before/after JSON;
- establish normalization and precedence contracts;
- decide the initial supported native property whitelist;
- benchmark representative large formatting payloads.

This is required before claiming visual deviation accuracy.

### Phase 1 — active theme inventory and Theme Review summary

- parse base/custom references and resource packages;
- resolve local theme files;
- inventory source, name, versions, text classes, colours, palette and rule counts;
- identify unselected theme-like resources conservatively;
- show availability/coverage, with no general Findings integration.

This phase is high-confidence and useful on its own.

### Phase 2 — supported persisted-format comparisons

- parse formatting values/expressions/selectors;
- compare a fixture-backed whitelist of native properties;
- separate match, difference, theme-linked, dynamic and unavailable;
- aggregate patterns/outliers by property, visual type and page;
- do not create a finding for each difference.

### Phase 3 — high-confidence accessibility contexts

- implement WCAG relative-luminance/contrast calculation;
- support only tested same-context foreground/background mappings;
- treat unknown composition as Review/unsupported;
- decide separately whether any strong result is promoted to Findings.

### Phase 4 — palette and typography heuristics

- duplicates, distance, luminance and colour-vision simulation;
- typography and incomplete dark-theme observations;
- Review wording, documented thresholds and explicit limitations.

### Phase 5 — supplied reference-theme comparison

- accept an expected theme through a future configuration/UI design;
- parse it through the same `ThemeInventory`/normalization model;
- compare active theme layers and supported visual values;
- distinguish active-theme drift from local visual differences;
- summarize compliance without assuming every difference is undesirable.

## 18. Explicit out-of-scope items

This spike does not implement or decide:

- production theme parsing;
- changes to the inventory JSON schema;
- assurance rules or severities;
- Findings entries;
- HTML tabs or renderer changes;
- CSV changes;
- CLI/Desktop/Web controls;
- reference-theme configuration;
- full PBIR-Legacy support;
- Power BI's full formatting/preset/selector engine;
- DAX or data evaluation for conditional formatting;
- custom visual rendering adapters;
- complete WCAG conformance assessment;
- screenshot/pixel-level analysis;
- automatic theme rewriting or visual-format reset;
- deletion of unselected registered resources.

## Final decision summary

- **Technically viable:** yes, if scoped to inventory, supported comparisons and conservative accessibility observations.
- **Confident now:** active PBIR theme discovery; local resource resolution; theme JSON inventory; literal value normalization; supported match/difference evidence; expression/selector classification.
- **Not reliably reconstructable now:** editing provenance, every built-in/renderer default, all preset/selector merges, runtime conditional values, custom visual rendering and contrast where the actual background/composition is unknown.
- **Biggest risk:** presenting persisted metadata as proof of manual override or final rendered appearance, producing convincing but false precision.
- **Recommended first implementation:** Phase 0 fixtures followed by Phase 1 active-theme inventory and a read-only Theme Review summary.
- **Product design:** a dedicated Theme Review tab is strongly recommended. It gives patterns, coverage and uncertainty enough space without flooding normal Findings.

## Primary references

- [Power BI Desktop project report folder and PBIR structure](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report)
- [Use report themes in Power BI](https://learn.microsoft.com/en-in/power-bi/create-reports/desktop-report-themes)
- [Create custom report themes in Power BI Desktop](https://learn.microsoft.com/en-us/power-bi/create-reports/report-themes-create-custom)
- [Visual defaults in Power BI reports](https://learn.microsoft.com/en-us/power-bi/create-reports/power-bi-reports-visual-defaults)
- [Microsoft Power BI Report Theme JSON Schema repository](https://github.com/microsoft/powerbi-desktop-samples/tree/main/Report%20Theme%20JSON%20Schema)
- [Design Power BI reports for accessibility](https://learn.microsoft.com/en-us/power-bi/create-reports/desktop-accessibility-creating-reports)
- [WCAG 2.2 Understanding SC 1.4.1: Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color)
- [WCAG 2.2 Understanding SC 1.4.3: Contrast (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum)
- [WCAG 2.2 Understanding SC 1.4.11: Non-text Contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast)
