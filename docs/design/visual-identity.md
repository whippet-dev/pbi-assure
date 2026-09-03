# PBI Assure visual identity

The design system shared by the browser application and the generated HTML report. It exists so the
two surfaces read as one product rather than as a web tool that happens to emit a corporate report.

## Where it lives

| File | Owns |
| --- | --- |
| `src/PbiAssure.Web/wwwroot/css/core.css` | Tokens and the primitives both surfaces use |
| `src/PbiAssure.Reporting/Styles/report.css` | The generated report's presentation layer |
| `src/PbiAssure.Web/wwwroot/css/app.css` | The application's layout and emphasis |
| `src/PbiAssure.Reporting/DesignSystem.cs` | Generated. The first two files as compiled text |
| `src/PbiAssure.Reporting/BrandIdentity.cs` | The mark, as inline SVG and as a favicon data URI |

The application links the stylesheets. A generated report travels as one self-contained document, so
it inlines the same bytes. After editing either stylesheet run:

```bash
node scripts/Sync-DesignTokens.mjs
```

`DesignSystemSourceTests` fails the build if the copies drift.

## The idea

PBI Assure is an instrument, not a dashboard and not a compliance document. It reads a Power BI
project and reports evidence, and the one thing it has that a general-purpose tool does not is a
vocabulary for *how confident it is*: nine classifications running from an error a reader should act
on to a model object with no detected usage at all.

That vocabulary is the identity. Every classification renders the same way in both surfaces:

**glyph + label + hue.** The glyph and the label carry the meaning on their own. The hue only makes
scanning faster. Outline style carries a second signal — solid means evidence of use was found,
dashed means none was.

## Colour

Indigo (`--pa-accent`) means "you can act on this": links, primary actions, the focus ring, the
active navigation item. It is never a status, so an accent can never be misread as a finding.

Status colours are two families that never appear in the same list.

| Severity | Token | Glyph |
| --- | --- | --- |
| Error | `--pa-error` | cross in a disc |
| Warning | `--pa-warning` | triangle |
| Review required | `--pa-review` | eye |
| Informational | `--pa-info` | i in a disc |

| Usage | Token | Glyph | Outline |
| --- | --- | --- | --- |
| Directly used | `--pa-used` | filled disc | solid |
| Indirectly used | `--pa-indirect` | disc inside a ring | solid |
| Structurally required | `--pa-structural` | square | solid |
| Only used by unused items | `--pa-branch` | half-filled disc | dashed |
| Apparently unused | `--pa-unused` | hollow ring | dashed |

The usage family is a ramp from evidenced use to no evidence of use, so Directly used and Indirectly
used sit close together on purpose: both mean "used", and the glyph carries the distinction. Every
other pair within a family is clearly separated in hue.

Structurally required is deliberately the least chromatic of the usage states. It is a fact about the
model's machinery rather than a judgement about a person's report.

Every foreground/background pair the design renders clears WCAG AA in both themes.

## Themes

Light, Dark and System, from one token set.

Dark is designed rather than inverted. It has its own canvas, its own surface steps, lower border
contrast than light, and status hues re-picked at a lightness that reads on a dark ground without
glowing.

`:root` carries light. `:root[data-theme="dark"]` carries the explicit choice.
`@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) }` carries System. A stored
choice is applied before the first paint — by `wwwroot/appearance.js` in the application, and by an
inline head script in the report — so nobody sees the wrong theme flash past.

The preference is the only value PBI Assure writes to browser storage. See `PRIVACY.md`.

## Typography

System stacks, led by Segoe UI Variable, because that is where most Power BI developers work and
because the application's content security policy allows no external fonts.

Headings use the display face with tight tracking. Identifiers, DAX, M and paths use Cascadia Mono.
Every count uses `font-variant-numeric: tabular-nums` so figures in a rail line up.

The report sets a 14px base and the application 15px: the report is the denser, more tabular
expression of the same system.

## Surfaces

Mostly flat. Hierarchy comes from type, spacing, alignment and hairlines.

A card means a discrete object a reader can open — a finding, a model table, a page, a visual, a
query, a relationship, a security role. Sections are not cards. Counts are not cards: they print on a
hairline-divided rail, which takes roughly half the vertical space that bordered tiles did and lets
the eye read a row of numbers instead of a row of boxes.

Where objects are listed inside a card that already has a border, they are laid out as a hairline
matrix rather than as smaller cards, so the result reads as one table.

Geometry is crisp: 3/5/8px radii, 1px borders, shadows only where something genuinely floats.

## Motion

110ms for hover and state changes, 170ms for anything larger, on one easing curve. Disclosure markers
are chevrons that rotate. Everything is disabled under `prefers-reduced-motion`.

## The mark

Three nodes joined into an "A": one above, two below, with a crossbar. It reads as the initial of the
product name and as the thing the product does — following a dependency from one object to the
objects beneath it. One stroke weight, so it survives at favicon size, and drawn in `currentColor` so
it inherits the theme.

The lockup is the mark plus "PBI Assure". In the report a `/ Report` qualifier follows it.

## How the two surfaces differ

They share the mark, the tokens, the type principles, the status vocabulary, the controls and the
focus treatment. They differ in density and in emphasis:

- The report is denser, uses a two-column workspace with a sticky navigation rail, and treats plain
  `button` as a utility control.
- The application is a step more spacious, uses a single centred column under a sticky app bar, and
  leads with a filled primary action.
