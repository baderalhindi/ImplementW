# Portfolio Dashboard Designer — text box formatting and a usable Map tile (contract, 20 Nov 2026)

Extends `dashboard-designer-map-text.md`. The user (20 Nov 2026): more text box options "like Power BI", a white background, a text colour of the
user's choice; and "why can't I use the map".

## Text box — added to `text` (all optional; absent = the default in brackets; existing saved text boxes read unchanged)

| Property | Values | Meaning |
| --- | --- | --- |
| `fontSize` | integer 8–96, or null [null] | px; null = the `size` preset (SMALL 14, NORMAL 16, LARGE 20, HEADING 24) |
| `italic`, `underline` | boolean [false] | as `bold` |
| `color` | `#RRGGBB` or null [null] | the text colour; null = the theme's text colour |
| `background` | `CARD`, `NONE`, or `#RRGGBB` [`NONE` for boxes saved before; `CARD` for new ones] | `CARD` = the visuals' card colour (white in light, the dark card in dark); `NONE` = transparent |
| `border` | boolean [false] | a 1px border |
| `borderColor` | `#RRGGBB` or null [null] | null = the theme's border colour |
| `verticalAlign` | `TOP` / `MIDDLE` / `BOTTOM` [`TOP`] | the text within the box |

Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`, `visuals[i].text.<prop>`): `fontSize` outside 8–96 or not whole → OUTSIDE_RANGE; a colour not
`^#[0-9A-Fa-f]{6}$` (or for `background` not CARD/NONE/hex) → NOT_ALLOWED; unknown `verticalAlign` → NOT_ALLOWED. Stored in the existing
`text_box` jsonb (no migration); colours stored upper-case. Echoed in results.

### Frontend

- Settings, grouped like Power BI's format pane: **Text** (Arabic, English), **Font** (size presets + a custom size field 8–96, Bold, Italic,
  Underline as toggle buttons, **Text colour**), **Alignment** (horizontal start/center/end, vertical top/middle/bottom), **Background**
  (Card colour / None / a colour), **Border** (on/off, colour). Colour choosers: a row of swatches from the app's chart and status palette (with
  names for screen readers) + "Custom…" (`<input type="color">`) + "Default"; each colour labelled.
- A new text box from the gallery starts with `background: 'CARD'` (the user: "background to be white").
- Drawing (designer and dashboard): font size, styles, colour, background, border and vertical alignment applied; on the dashboard a box with
  `CARD` background reads as a card (the visuals' radius, no shadow), `NONE` as plain text; text colours keep at least 3:1 contrast against the
  background in both themes (if a chosen colour fails in the current theme, draw it anyway — it is the designer's choice — but the settings
  show a small "low contrast" note under the colour).

## Map tile — always usable

While the map is on the design the tile no longer is disabled: a **click** selects the map and scrolls it into view; a **drag** moves the
existing map to the drop cell (or into the container under it), as a moved visual (`moved`). Its tooltip says "Go to the map" / "Drag to move
the map" while placed. When no map is placed it adds one as now.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/api/openapi.v1.json` |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

Frontend type contract: `TextBox` gains `fontSize?: number | null; italic?: boolean; underline?: boolean; color?: string | null;
background?: 'CARD' | 'NONE' | string; border?: boolean; borderColor?: string | null; verticalAlign?: 'TOP' | 'MIDDLE' | 'BOTTOM'`.
