# PBI Assure Theme Review fixture analysis

Research date: 12 August 2026

Status: research only. This document is intentionally untracked. It does not define production behaviour, change assurance logic, or modify the fixture pack.

## Executive conclusion

The Desktop-authored fixtures strengthen the original feasibility recommendation: implement active-theme inventory first, then persisted-formatting classification, and only then add a very small comparison whitelist backed by direct theme-to-PBIR mappings.

The pack proves that PBI Assure can truthfully identify:

- the active base and custom theme resources;
- no persisted value versus a persisted literal;
- removal of a saved value by Reset;
- a theme-linked colour expression;
- a field/measure-driven expression;
- wildcard and series/category-scoped selectors.

It does **not** preserve authoring provenance. A saved value can remain even when the author deliberately chooses a value that looks like the current theme value, but PBIR does not say why it was saved. The controlled title-font fixture also exposes a mapping gap: the saved `18D` value is not the active custom theme's primary `textClasses.title.fontSize` value of `19`, and the active theme has no `visualStyles` title font-size rule. PBI Assure must not compare those paths as if they were equivalent.

The contrast pair is not a clean two-colour control. The poor fixture stores two literals, but the good fixture changes both the text colour and the background representation. The pack therefore supports a **Review-only** contrast observation today, not a general deterministic accessibility rule.

Recommended next product phase:

1. implement Phase 1 active-theme/resource inventory;
2. implement persisted-value expression and selector classification without theme comparison;
3. add one repaired direct-`visualStyles` fixture and one repaired opaque contrast pair;
4. only then enable the first guarded Phase 2 comparison property.

## Evidence method and boundaries

The analysis compared each report with its immediate predecessor and then checked the relevant JSON nodes directly. File hashes were used to separate controlled changes from unrelated content. The following were treated as evidence:

- `definition/report.json` theme references and resource-package mappings;
- the actual files under `StaticResources/SharedResources` and `StaticResources/RegisteredResources`;
- the stable test page and visual JSON;
- the supplied source theme JSON files under `samples-local/CustomThemeStore`;
- the exact PBIR expression and selector shapes.

The following were not inferred from PBIR:

- an editing action beyond the intended action supplied by the fixture name/context;
- the reason a persisted value exists;
- a final runtime value for a measure-driven expression;
- the final rendered value where a theme reference, opacity, fallback, or renderer default is unresolved.

The only incidental save churn observed was a `.platform` change at `00 -> 01`. It is not part of theme analysis. Page JSON was byte-for-byte stable across all 13 fixtures.

## Common fixture inventory

All fixtures use the same project and report:

- project: `ThemeTesting.pbip`;
- report folder: `ThemeTesting.Report`;
- semantic model folder: `ThemeTesting.SemanticModel`;
- PBIR definition version: `definition/version.json` = `2.0.0`;
- report definition-properties version: `definition.pbir` = `4.0`;
- report schema: `report/3.3.0`;
- page schema: `page/2.1.0`;
- visual-container schema: `visualContainer/2.11.0`;
- page ID: `c325e96894e74824cb75`;
- page display name: `Page 1`;
- visual ID: `c2b7dc665a046304d05a`;
- visual type: `clusteredColumnChart`;
- explicit saved visual title text: none;
- test visual path: `ThemeTesting.Report/definition/pages/c325e96894e74824cb75/visuals/c2b7dc665a046304d05a/visual.json`;
- report path: `ThemeTesting.Report/definition/report.json`;
- page path: `ThemeTesting.Report/definition/pages/c325e96894e74824cb75/page.json`.

The page ID, visual ID, visual type, project name, report name, schema versions, query's Category/Y bindings, and page JSON remain stable unless a transition below explicitly says otherwise. That makes the visual suitable for pairwise comparison.

## Fixture manifest

| Fixture | Parent | Intended action | Active theme/resource state | Observed evidence | Confidence / unexpected behaviour |
|---|---|---|---|---|---|
| `00_Base` | none | Save baseline with the built-in/base theme | Base `CY26SU07`; only `SharedResources/BaseThemes/CY26SU07.json` | No custom-theme reference, no registered resource, no saved test property | High |
| `01_CustomTheme` | `00` | Import `PBI Assure Theme Fixture.json` | Base `CY26SU07`; custom `PBI_Assure_Theme_Fixture4988638439890827.json` | Registered package/file added; visual unchanged; three supplied palette colours expanded to 480 | High |
| `02_CustomizedCurrentTheme` | `01` | Customize current theme through Desktop | Base unchanged; custom replaced by `PBI_Assure_Theme_Fixture5240220954484197.json` | Title text class `16/#222222 -> 19/#FF0000`; visual unchanged; no `visualStyles` created | High |
| `03_DifferentValue` | `02` | Set visual title font size to a different value | Theme state unchanged | Saved literal `30D` under `visualContainerObjects.title`; no unrelated visual formatting materialized | High |
| `04_ManualSameValue` | `03` | Manually set the property to the UI's theme-equivalent value, without Reset | Theme state unchanged | Saved literal remains and changes `30D -> 18D` | High for storage; medium for exact theme equivalence because the active theme contains `19`, not a direct `18` title rule |
| `05_Reset` | `04` | Reset the same property | Theme state unchanged | Property and enclosing title card disappear; visual is byte-for-byte identical to `02` | High |
| `06_Conditional` | `05` | Apply field/measure conditional formatting | Theme state unchanged | `dataPoint.fill` becomes a `Measure` expression bound to `TestData[CF Colour]` with a data-view wildcard selector | High |
| `07_SeriesFormatting` | `06` | Add a series field and format member `X` | Theme state unchanged | `TestData[Series]` projection added; `dataPoint.fill` is `ThemeDataColor(4,-0.5)` with a `scopeId` comparison for `Series = 'X'` | High; this is a category/legend series member, not a second measure |
| `08_CustomFont` | `07` | Change a global/custom-theme font | Base unchanged; custom replaced by `PBI_Assure_Theme_Fixture45630101221061925.json` | `textClasses.label.fontFace` changes to a normalized font stack; no visual font values are saved | High for theme/font evidence; series binding/formatting was also removed, so that cleanup is not attributable to the font action alone |
| `09_PoorContrast` | `08` | Set a poor title text/background pair | Reverts to the `02` custom resource | Literal title text `#B0B0B0` and literal title background `#FFFFFF` saved in the same title card | High for saved values; opacity is not explicitly stored |
| `10_GoodContrast` | `09` | Change to a good title text colour while retaining the title background | Theme state unchanged | Text becomes literal `#111111`, but background also changes from literal `#FFFFFF` to `ThemeDataColor(0,0)` | High; persisted result contradicts the intended one-property-only control |
| `11_ReplacedTheme` | `10` | Apply the distinct replacement custom theme | Custom becomes `PBI_Assure_Theme_Fixture_-_Rep14294056349226703.json` | Prior custom file/package entry is absent; replacement palette is expanded to 480; prior title formatting disappears | High for files; medium for attributing visual cleanup solely to theme replacement |
| `12_ThemeRemoved` | `11` | Replace/remove custom theme in favour of a built-in/base theme | Base becomes `CY18SU07`; no custom theme or registered package | Replacement registered file is absent; only `SharedResources/BaseThemes/CY18SU07.json`; visual is unchanged from `11` | High; this does not test survival of an arbitrary override because `11` already has none |

### Theme resource matrix

| Fixtures | Base reference | Active custom reference | Registered theme files present |
|---|---|---|---|
| `00` | `CY26SU07` | none | none |
| `01` | `CY26SU07` | `PBI_Assure_Theme_Fixture4988638439890827.json` | exactly that file |
| `02`-`07` | `CY26SU07` | `PBI_Assure_Theme_Fixture5240220954484197.json` | exactly that file |
| `08` | `CY26SU07` | `PBI_Assure_Theme_Fixture45630101221061925.json` | exactly that file |
| `09`-`10` | `CY26SU07` | `PBI_Assure_Theme_Fixture5240220954484197.json` | exactly that file |
| `11` | `CY26SU07` | `PBI_Assure_Theme_Fixture_-_Rep14294056349226703.json` | exactly that file |
| `12` | `CY18SU07` | none | none |

`CY26SU07` imports with visual/report/page versions `2.11.0 / 3.4.0 / 2.3.1`. `CY18SU07` in fixture `12` records older import versions `1.8.23 / 2.0.23 / 1.3.23`.

## Normalized pairwise findings

Paths below are JSON paths within the named file. “Absent” means the property/card/package was not present, not that its rendered value was proved.

### `00 -> 01`: import custom theme

| File / path | Before | After | Interpretation | Confidence |
|---|---|---|---|---|
| `report.json $.themeCollection.customTheme` | absent | registered resource `PBI_Assure_Theme_Fixture4988638439890827.json` | Deterministic active custom-theme reference | High |
| `report.json $.resourcePackages[RegisteredResources]` | absent | one `CustomTheme` item whose name/path is that filename | Deterministic reference-to-file mapping | High |
| `StaticResources/RegisteredResources/...json` | absent | Desktop-saved custom theme | Resource was copied/normalized into the report | High |
| custom theme `$.dataColors` | supplied 3 colours | 480 colours, first three preserved | Desktop expanded the palette | High |
| test `visual.json` | baseline hash | same hash | Import alone did not materialize formatting into this visual | High |

The source theme has only `name`, three `dataColors`, and `textClasses.title/label`. Desktop preserves those sections and expands the palette; it does not generate `visualStyles` here.

### `01 -> 02`: customize current theme in Desktop

| File / path | Before | After | Interpretation | Confidence |
|---|---|---|---|---|
| `report.json $.themeCollection.customTheme.name` | `...4988638439890827.json` | `...5240220954484197.json` | Desktop created/referenced a replacement resource | High |
| registered-resource set | old file | new file only | Old resource is not retained in this saved fixture | High |
| theme `$.textClasses.title.fontSize` | `16` | `19` | Global title class changed | High |
| theme `$.textClasses.title.color` | `#222222` | `#FF0000` | Global title class changed | High |
| theme `$.textClasses.label` | Arial, 11, `#444444` | unchanged | Customization was targeted | High |
| theme `$.visualStyles` | absent | absent | Customize current theme did not generate visual-style rules in this case | High |
| test `visual.json` and page JSON | unchanged | unchanged | Existing visual acquired no saved values merely because the theme changed | High |

### `02 -> 03`: manual different value

One meaningful visual change appears:

```text
$.visual.visualContainerObjects.title[0].properties.fontSize.expr.Literal.Value
absent -> "30D"
```

The property is in `visualContainerObjects`, not `visual.objects`. The wrapper is a decimal literal encoded as a string. The title card contains only this property, so Desktop did not materialize unrelated title values. A persisted value is a higher/local layer for analysis, but the fixture alone does not render or prove every part of Power BI's precedence engine.

### `03 -> 04`: manual same-looking value

```text
$.visual.visualContainerObjects.title[0].properties.fontSize.expr.Literal.Value
"30D" -> "18D"
```

The property remains explicitly serialized. There is no origin, “manually set,” or “same as theme” marker.

Important limitation: the active custom theme stores `textClasses.title.fontSize = 19`; its `visualStyles` is absent. The base theme stores `textClasses.title.fontSize = 12` and only `titleWrap` in the global title visual style. The separate source file `PBI Assure Theme Fixture - Visual Styles.json` contains `columnChart/*/title.fontSize = 18`, but that source file is not active in any fixture. Therefore the action context says the author chose the UI's theme-equivalent value, while the saved files do **not** establish a direct `18D <-> active theme rule` mapping.

Truthful conclusion:

- PBI Assure can distinguish “no saved value” from “a saved value exists.”
- A same-looking value can remain saved when Reset was not used.
- PBI Assure cannot determine why it exists.
- This pack does not yet prove that `visual title fontSize` should be compared directly with `textClasses.title.fontSize`.

### `04 -> 05`: Reset

The entire `visualContainerObjects.title` card disappears because `fontSize` was its only property. No reset marker remains. Fixture `05`'s visual is byte-for-byte identical to `02`, the earlier theme-controlled state.

This is strong evidence that `NoPersistedValue` is a meaningful storage classification. It means “no supported value is saved at this scope”; it does not, by itself, prove the final rendered value.

### `05 -> 06`: conditional formatting

The new expression is:

```text
$.visual.objects.dataPoint[0].properties.fill.solid.color.expr.Measure
  Expression.SourceRef.Entity = "TestData"
  Property = "CF Colour"

$.visual.objects.dataPoint[0].selector.data[0].dataViewWildcard.matchingOption = 1
```

No literal fallback and no `ThemeDataColor` remain on this property. The final colour depends on the measure and runtime context. This supports `DynamicExpression` plus `DynamicNotComparable`; DAX evaluation is out of scope.

### `06 -> 07`: selector-scoped series formatting

The visual gains a `Series` query projection for `TestData[Series]` and a matching categorical filter entry. The conditional expression is replaced by:

```text
$.visual.objects.dataPoint[0].properties.fill.solid.color.expr.ThemeDataColor
  ColorId = 4
  Percent = -0.5

$.visual.objects.dataPoint[0].selector.data[0].scopeId.Comparison
  ComparisonKind = 0
  Left = TestData[Series]
  Right = 'X'
```

There is no separate visual-wide `dataPoint.fill` value. Only member `X` is represented. The observation is both `ThemeReference` and `SelectorScoped`; those classifications are dimensions, not mutually exclusive statuses.

The current stale-reference implementation already:

- extracts selector field identities in `PbirFieldReferenceExtractor`;
- distinguishes `ScopeId`, `Metadata`, `Wildcard`, `Total`, and `Id` selectors;
- builds active query/projection identities;
- reconciles a selector with current bindings in `PbirVisualReferenceClassifier`;
- classifies active, high-confidence persisted, and ambiguous selector references.

A future formatting parser should reuse or extract the canonical selector/binding-index portion of that logic. It should not force all formatting through the stale-reference result: Theme Review also needs the selector's member predicate and normalized formatting value.

### `07 -> 08`: custom/global font

The active custom resource is replaced again. In its JSON:

```text
$.textClasses.label.fontFace
"Arial" -> "'Segoe UI Bold', wf_segoe-ui_bold, helvetica, arial, sans-serif"
```

`textClasses.title` remains Arial, 19, red. No `visualStyles` is created and no visual-level font property is materialized. Desktop stores the selected font as a normalized CSS-like fallback stack rather than only the friendly font name. It changes the chosen text class, not every text class automatically.

The transition also removes the Series projection, its categorical filter, and the scoped `dataPoint` object, returning the visual to the baseline hash. That is setup cleanup alongside the font experiment; do not attribute it to font serialization without the exact UI action log.

### `08 -> 09`: poor contrast setup

The earlier non-font custom resource becomes active again. The test visual gains:

```text
$.visual.visualContainerObjects.title[0].properties.fontColor.solid.color.expr.Literal.Value
  '#B0B0B0'

$.visual.visualContainerObjects.title[0].properties.background.solid.color.expr.Literal.Value
  '#FFFFFF'
```

Both are literals in the same title instance. No explicit background transparency/opacity property is saved in that node.

### `09 -> 10`: intended good contrast

Two persisted changes occur, not one:

```text
title.fontColor:  Literal '#B0B0B0' -> Literal '#111111'
title.background: Literal '#FFFFFF' -> ThemeDataColor { ColorId: 0, Percent: 0 }
```

This is unexpected Desktop behaviour relative to the intended controlled action. It must be preserved as evidence rather than normalized away.

### `10 -> 11`: replace custom theme

The active custom-theme reference and registered package item point only to the replacement file. The previous custom file is absent. Desktop expands the replacement's supplied three-colour palette to 480 entries while retaining its text classes and top-level `background`, `foreground`, and `tableAccent`.

The test visual's entire title formatting card also disappears, returning it to the baseline hash. The files prove that the formatting is absent after the transition. They do not prove that applying any replacement theme universally deletes visual overrides; an unrecorded Reset or a Desktop-specific interaction remains possible.

There is no coexisting unselected theme resource in this fixture, so the pack cannot test how retained but unselected resources should be described beyond the metadata rule: only `themeCollection.customTheme` is active.

### `11 -> 12`: remove/replace custom theme with built-in/base

```text
$.themeCollection.baseTheme.name: CY26SU07 -> CY18SU07
$.themeCollection.customTheme: replacement resource -> absent
RegisteredResources package: present -> absent
registered replacement file: present -> absent
shared base file: CY26SU07.json -> CY18SU07.json
```

The visual is unchanged from `11`. This supports deterministic base-only state resolution. It does not prove that an arbitrary saved visual override survives theme removal because the preceding fixture already had no title override.

## Contrast assessment

WCAG relative-luminance calculations for the observed/intended pairs are:

| Pair | Ratio | Evidence status |
|---|---:|---|
| saved poor: `#B0B0B0` on `#FFFFFF` | `2.1687:1` | Both literal values are saved in the same title card; opacity is omitted |
| intended good: `#111111` on `#FFFFFF` | `18.8831:1` | This pair is not what fixture `10` saved |
| saved good, **if** `ThemeDataColor(0,0)` resolves to active palette entry 0 `#1F77B4` | `3.9169:1` | Conditional calculation; palette resolution and final composition are not proved by this pair |

The poor literal pair is mathematically below both the WCAG 4.5:1 normal-text and 3:1 large-text thresholds. However, a strong product observation also needs a validated rule that the two stored properties are visible, opaque, and compose exactly as assumed. The fixture has no explicit opacity value.

Overall classification: **B. Review-only contrast observation**.

Why not A yet:

- the good fixture did not retain the literal white background;
- the background became a theme reference, so the pair is not literal-to-literal;
- no explicit opacity/transparency is present;
- the pack does not independently prove the `ThemeDataColor` palette/shade resolution contract;
- a clean control should vary only the foreground.

Repair recommendation: recreate `10` from `09`, retain the exact literal `#FFFFFF` background, change only title text to `#111111`, explicitly verify the title background is enabled and fully opaque in Desktop, and record the exact UI actions. Keep typography review separate; font family and size are not deterministic WCAG failures.

## Cross-fixture answers

1. **Can active custom-theme discovery be deterministic?** Yes. Resolve `themeCollection.customTheme` through the matching `resourcePackages` item and package path; all custom fixtures agree.
2. **Can an unselected theme resource be distinguished without guessing?** Conceptually yes: a present theme-like resource not selected by `themeCollection` is unselected. This pack has no active-plus-unselected coexistence case, so it does not prove retention semantics or deletability.
3. **Does Power BI retain replaced/removed custom resources?** Not in this sequence. Each replacement/removal leaves only the current resource, or none. Do not generalize that Desktop never retains them.
4. **Does changing a global/theme setting serialize formatting into existing visuals?** No in `01 -> 02` and `07 -> 08`; the change is in the custom resource and the visual gains no formatting from it.
5. **Does a manual visual change create an explicit value?** Yes: title font size becomes a saved `Literal`.
6. **If a manual value equals the UI's theme candidate, does it remain?** Yes in the controlled action: `18D` remains saved. Exact mapping to the active theme JSON is unresolved.
7. **Does Reset remove it?** Yes. Property and otherwise-empty card disappear; the visual matches the earlier inherited-state bytes.
8. **Can the proposed storage classifications be distinguished?** Yes for `NoPersistedValue`, `PersistedLiteral`, `ThemeReference`, `DynamicExpression`, and `SelectorScoped`. `ThemeReference` and `SelectorScoped` can overlap.
9. **Can PBI Assure determine a deliberate manual override?** No. PBIR contains state, not editing intent/provenance.
10. **Can it safely say a saved value differs from/matches a theme candidate?** Only when a version-appropriate, tested mapping resolves a candidate at the same scope. That mapping is not established for this fixture's title font size.
11. **Are conditional values distinct from literals?** Yes. The `Measure` expression shape is unambiguous and its bound measure is recoverable.
12. **Are per-series values distinct from visual-wide values?** Yes. The `scopeId` comparison and Series binding identify member `X`; selector identity must remain in the key.
13. **Is same-context contrast deterministic here?** Not as a clean pair. Fixture `09` is promising but omits opacity; fixture `10` changes the background representation. Use Review-only until repaired.
14. **Imported theme versus Customize current theme?** Import expands the supplied palette and creates a registered custom resource. Customize current theme replaces that resource and updates `textClasses`; it did not generate `visualStyles` or materialize visual formatting. The font UI saved a normalized font stack.
15. **Anything contradict the original spike?** The main recommendation is strengthened. Two cautions are sharper: this sequence replaces/removes old resources rather than accumulating them, and the controlled “same”/contrast actions do not yield the assumed direct JSON mappings. The latter reinforces conservative wording and fixture-backed rules.

## Theme resolution model

### Proven by these fixtures

- A shared base theme is selected through `themeCollection.baseTheme` and its `SharedResources` package entry.
- A custom layer is selected through `themeCollection.customTheme` and its `RegisteredResources` package entry.
- Desktop can replace the registered custom resource filename rather than modify a file in place.
- Imported/customized theme data can live entirely in the theme resource without being copied into an existing visual.
- An explicit visual value has a separate persisted node.
- Reset can remove that node and the otherwise-empty formatting card.
- Literal, `ThemeDataColor`, and `Measure` expression kinds are structurally distinct.
- Wildcard and `scopeId` selector scopes are structurally distinct.
- Theme/resource absence in `12` is a deterministic base-only state.

### Documented by Microsoft but not proven by these fixtures

- the broader precedence of visual-specific formatting over custom theme, custom theme over base theme, and renderer defaults after those layers;
- wildcard, visual-type-specific, style-preset, card, and discriminator merge precedence;
- secondary text-class derivation and the exact class used by every visual property;
- the full `ThemeDataColor` palette/shade resolution rules;
- built-in defaults when the full base theme is unavailable;
- final rendered composition for transparency, wallpaper, images, interaction, and selection states.

Relevant references are the [PBIR report-folder documentation](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report), [Power BI report themes](https://learn.microsoft.com/en-in/power-bi/create-reports/desktop-report-themes), [custom theme documentation](https://learn.microsoft.com/en-us/power-bi/create-reports/report-themes-create-custom), the [Microsoft report-theme schema repository](https://github.com/microsoft/powerbi-desktop-samples/tree/main/Report%20Theme%20JSON%20Schema), and [WCAG contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum).

### Still unknown

- why any persisted value was created;
- the direct candidate for `visualContainerObjects.title.fontSize = 18D` in this active theme;
- whether `columnChart` rules cover this `clusteredColumnChart` in the exact saved schema/version;
- whether applying a replacement theme alone caused the `10 -> 11` title-card removal;
- whether arbitrary saved visual formatting survives custom-theme removal;
- whether Desktop retains old registered themes in other histories;
- the omitted title-background opacity/default in these fixtures;
- final rendering of the good contrast background;
- named presets, discriminators, custom visuals, and legacy PBIR in this controlled pack.

## Validated and refined product wording

Use wording that describes saved evidence rather than intent:

- **No saved local value; supported active-theme candidate available**
- **Saved value matches the supported active-theme candidate**
- **Saved value differs from the supported active-theme candidate**
- **Theme-linked colour reference; final rendered colour not fully resolved**
- **Dynamic or conditional value; final value depends on report data**
- **Theme value unavailable**
- **Unsupported or ambiguous mapping**
- **Scoped to series/category: X** when a selector identity is understood

“Supported” is important: a theme value merely existing in `textClasses` is not enough to assert that it controls a particular visual property. Avoid “manually overridden,” “effective value,” or “safe to reset/remove.”

## Initial property whitelist recommendation

### Extraction/classification whitelist now

These paths are sufficiently evidenced for storage classification, not necessarily theme comparison:

| PBIR property | Supported evidence now | Exclusions |
|---|---|---|
| `visualContainerObjects.title.fontSize` | absent vs decimal `Literal`; reset removal | Do not map directly to `textClasses.title.fontSize` yet |
| `visualContainerObjects.title.fontColor` | literal colour | Contrast requires known background/composition |
| `visualContainerObjects.title.background` | literal or `ThemeDataColor` | Theme reference is not a resolved literal; opacity is unknown here |
| `objects.dataPoint.fill` | `Measure` or `ThemeDataColor`; wildcard/scope selector | No static theme-difference claim for dynamic/scoped values |

### Strict Phase 2 comparison whitelist

From this controlled pack alone, the honest automatic match/difference whitelist is **empty**. The pack proves storage and classification but does not contain an active direct `visualStyles` rule that can be mapped to the changed title property without assuming text-class or visual-type inheritance.

The first candidate should be only:

```text
visualContainerObjects.title.fontSize
```

and only when all of these are true:

- the active theme contains an explicit, direct, fixture-validated `visualStyles` rule for the resolved visual type/preset/card/property;
- the selector/discriminator scope matches;
- the PBIR value is a literal decimal;
- no dynamic expression is involved;
- both raw evidence paths are retained.

Before enabling it, import the existing `PBI Assure Theme Fixture - Visual Styles.json` into a new controlled fixture, confirm that Desktop saves it as the active resource, record the exact rule used for `clusteredColumnChart`, and repeat untouched/different/same/reset states. Title colour/background can follow only after the repaired opaque contrast pair. Font family should wait for tested name/stack normalization. Label properties should wait for a direct active rule and known selector scope.

## Implementation and performance notes

Useful normalized keys:

```text
Theme rule:
(layer, visualType-or-wildcard, preset-or-wildcard, card,
 discriminator, property)

Persisted formatting:
(report, page, visual, storage collection, card, instance,
 canonical selector identity, property, expression kind)
```

Selector identity should preserve:

- selector kind;
- bound table/object identities;
- query/projection reference where available;
- comparison operator/kind;
- normalized member literal, for example `Series = 'X'`;
- current-binding relevance: active, high-confidence persisted/stale, or ambiguous.

Indexing strategy:

- resolve base/custom resource paths once per report;
- parse each theme file once;
- pre-index theme rules by normalized key, with wildcard/preset layers separate;
- build the visual binding index once per visual;
- parse each formatting instance once and retain compact normalized values plus evidence paths;
- perform constant-time candidate lookup for whitelisted properties;
- aggregate while scanning rather than carrying entire raw subtrees into inventory/report output.

Payload risks:

- Desktop expanded each three-colour custom palette to 480 entries;
- older reports may persist formatting for almost every visual;
- selector-scoped formatting can create many instances per card;
- retaining raw JSON for every value would inflate native, browser/WASM, serialized inventory, and HTML output.

Reuse recommendation:

- reuse `PbirFieldReferenceExtractor`'s field-identity extraction;
- extract/share the binding-index and selector-kind/canonicalization logic from `PbirVisualReferenceClassifier` when implementation begins;
- reuse its active/high-confidence-persisted/ambiguous reconciliation so stale selectors do not enter headline comparison counts;
- keep Theme Review's expression/value parser separate because a selector can be active while its value is literal, theme-linked, or dynamic.

Complexity should remain approximately `O(T + P + S)` for theme rules, persisted properties, and selectors after indexing. Do not rescan the full theme for every visual property. Contrast/palette pair analysis should be separately bounded because 480-colour Desktop-expanded palettes already create 114,960 unordered pairs.

## Regression implications

When implementation is authorized, use focused assertions rather than full-report snapshots:

| Fixture(s) | Regression contract |
|---|---|
| `00`, `01`, `02`, `11`, `12` | Active base/custom resolution follows `themeCollection` and the exact package item/file |
| `01` | Palette expansion is inventoried without treating generated colours as user-authored provenance |
| `01 -> 02` | Customize current theme replaces the resource; no visual value is invented |
| `02`, `03`, `04`, `05` | No value, differing literal, saved same-looking literal, and Reset are distinct; `05` matches `02` |
| `06` | Measure expression is dynamic, bound to `TestData[CF Colour]`, wildcard-scoped, and not compared |
| `07` | `ThemeDataColor` and `scopeId Series = 'X'` are both retained; selector reconciles to the active Series projection |
| `08` | Font stack is preserved/normalized as evidence; no visual font is fabricated |
| `09`, `10` | Test asserts the background changed representation as well as the text colour; no false literal-pair claim |
| `11` | Only replacement custom resource is active/present in this fixture; no deletion advice is emitted |
| `12` | Base-only state resolves to `CY18SU07`; no stale custom layer is guessed |

Additional fixtures required before broader Phase 2 assertions:

- active direct `visualStyles` rule with untouched/different/same/reset states;
- repaired poor/good opaque literal contrast pair;
- active plus retained-unselected custom resources;
- replacement/removal while an unrelated explicit visual value is known to exist;
- named preset and discriminator cases;
- custom visual and PBIR-Legacy cases only when those scopes are deliberately added.

## Effect on the original feasibility recommendation

The recommendation remains **technically viable, phased, and conservative**.

Evidence now upgrades these areas from proposed to fixture-proven:

- active resource discovery for current PBIR;
- import/customize replacement behaviour in this sequence;
- `NoPersistedValue` versus `PersistedLiteral`;
- Reset removal;
- dynamic versus literal expression classification;
- theme reference and selector scope classification;
- no visual materialization from the observed theme/global changes.

Evidence keeps these areas out of automatic conclusions:

- authoring intent/provenance;
- broad effective-value resolution;
- title font mapping through text classes;
- deterministic contrast for this pair;
- general resource-retention/deletion advice;
- general visual-override survival across theme replacement/removal.

Phase 1 active-theme inventory should proceed next. Phase 2 should initially parse and classify the small extraction whitelist, but automatic match/difference output should wait for the one missing direct-`visualStyles` fixture. This produces useful Theme Review evidence without claiming to reproduce Power BI's formatter.

## Safety and repository note

This analysis did not modify the 13 fixtures, production code, tests, HTML, CSV, or schemas. The document itself is deliberately left untracked and should not be staged or committed without an explicit later request.
