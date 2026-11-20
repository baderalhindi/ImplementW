# Portfolio Dashboard Designer — Map and Text box in the gallery (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`). The user (20 Nov 2026): "add here [Add a visual] map and text box".

## Map (frontend only)

A **Map** tile in the gallery (after Gauge, before Container), icon a map pin over a folded map. It places the existing `PROJECT_LOCATIONS`
section (the Projects by location map, drawn from the dashboard's filters) as a visual: by click at the first free place, by drag at the drop
cell (as every tile), 12 × 8 by default, titled as the section is. The map is once per design (sections are unique): while it is on the
design the tile is disabled and says "The map is already on the dashboard" (its tooltip and a visually hidden description).

## Text box

`PortfolioVisualType` gains `Text = 11` (`TEXT`): a visual of the designer's own words — a heading, a note, an explanation.

```jsonc
"text": { "ar": "…", "en": "…", "size": "NORMAL", "align": "START", "bold": false }   // only on a TEXT visual
```

- `ar`, `en`: each 1–2000 characters after trimming, line breaks kept (`\n`); the visual-title text rules otherwise (R-17: no control
  characters except `\n`, the Arabic and English script checks as titles have). `size`: `SMALL` | `NORMAL` | `LARGE` | `HEADING`; `align`:
  `START` | `CENTER` | `END`; `bold`: boolean.
- A TEXT visual has no measures, values, columns, groupBy (`NONE`), filters, axes, section, widget code, children; its `title` stays (both
  languages, the visual's name in the designer and its accessible name) and defaults to "Text box" / "مربع نص". It may be a container's child.
- Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`): `visuals[i].text` REQUIRED on TEXT, NOT_ALLOWED elsewhere; `text.ar|en` REQUIRED /
  OUTSIDE_RANGE; anything else on a TEXT visual NOT_ALLOWED.
- Storage: nullable `jsonb` column `text_box` on `project.portfolio_visual` (camelCase as the API); CHECKs: `visual_type` includes `TEXT`,
  `(visual_type = 'TEXT') = (text_box IS NOT NULL)`, and the measures CHECK lets a TEXT visual through with zero measures. Migration
  `TASK-112_AddPortfolioVisualText` (DROPs in Up only via `migrationBuilder.Sql(... -- EXPAND-THEN-CONTRACT-REVIEWED: ...)`; Down deletes
  TEXT visuals before restoring the older checks). Results echo `text`; groups `[]`.

### Frontend

- Gallery tile **Text box** (icon "T" with lines) after Map. A new text box is 6 × 2, its text "Text" / "نص" until edited, selected.
- Settings: titles (its name), **Text (Arabic)** and **Text (English)** as textareas (dir rtl/ltr), **Size** select, **Alignment** (start /
  centre / end), **Bold** tick, size fields; no Data panel.
- Drawing (designer and dashboard): the text in the page's language (the other language when that one is empty — never, as both are
  required), line breaks kept, sizes ≈ 0.875 / 1 / 1.25 / 1.5rem (heading 700 weight), alignment logical (start/end mirror in Arabic); on the
  dashboard no title bar, no card chrome beyond the visual's frame — a text box reads as text on the dashboard (transparent background, no
  border or shadow); in the designer it keeps its handle, pencil and a light dashed outline so it can be found and moved. Not fitted to box;
  it scrolls if longer than its visual.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

Frontend type contract: `VisualType` adds `'TEXT'`; `TextBox = { ar: string; en: string; size: 'SMALL' | 'NORMAL' | 'LARGE' | 'HEADING';
align: 'START' | 'CENTER' | 'END'; bold: boolean }`; `Visual.text?: TextBox | null`.
