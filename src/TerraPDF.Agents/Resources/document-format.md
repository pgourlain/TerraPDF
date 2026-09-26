# TerraPDF document format (for create_pdf)

Pass `create_pdf` a JSON object describing the document. Only `content` is
required. Unknown properties are errors, so a typo is reported rather than
silently ignored. Colours are hex strings (`"#1A4A8A"`). Sizes are PDF points
(72 pt = 1 inch, about 28.35 pt = 1 cm).

## Top level

| Property | Type | Default | Notes |
|---|---|---|---|
| `title`, `author`, `subject`, `keywords` | string | | PDF metadata. `title` also names the file if `fileName` is empty. |
| `page` | object | A4 portrait, margin 50 | `size`: A0-A6, Letter, Legal, Tabloid, Executive. `orientation`: portrait or landscape. `margin`: points. `width`/`height`: custom size in points. `backgroundColor`. |
| `theme` | object | | `accentColor` (#1A4A8A; headings, table headers, charts), `textColor` (#212121), `mutedColor` (#6C757D), `fontFamily` (Helvetica, Times, Courier, or a registered font), `fontSize` (10). |
| `header` | block[] | | Repeated at the top of every page. No `heading` or `pageBreak` blocks. |
| `footer` | block[] | | Repeated at the bottom of every page. |
| `pageNumbers` | bool | true | Adds "Page X of Y" to the footer. |
| `headerOnFirstPageOnly` | bool | false | |
| `tableOfContents` | bool | false | Inserts a contents page built from `heading` blocks. |
| `fonts` | object[] | | `{ "family", "regular", "bold", "italic", "boldItalic" }`: each a `.ttf` path under the asset directory or base64. Needed for text beyond Western European characters. |
| `encryption` | object | | `{ "userPassword", "ownerPassword", "allowPrinting": true, "allowCopying": true, "allowEditing": false }`. AES-256. |
| `content` | block[] | | The body, flowed top to bottom across as many pages as needed. |

## Blocks

Every block is an object with a `type`. Content paginates automatically; long
tables repeat their header row on each page.

| type | Properties |
|---|---|
| `heading` | `text` (required), `level` 1-6 (2), `color`, `align`, `fontSize`, `link` |
| `paragraph` | `text` or `spans`; `align` (left, center, right, justify), `fontSize`, `color`, `bold`, `italic`, `link` |
| `list` | `items` (string[], required), `ordered` (false), `fontSize`, `color` |
| `table` | `columns` (required): `[{ "header", "width" (relative weight, 1), "align" }]`; `rows`: string[][]; `footerRows`: string[][] drawn bold (totals); `striped` (true); `color` (header background); `fontSize` |
| `keyValue` | `entries`: `[{ "label", "value" }]`; aligned label/value pairs, e.g. invoice details |
| `callout` | `text` (required), `title`, `color`: a tinted box with a coloured left border |
| `columns` | `columns`: `[{ "width" (relative weight), "content": [blocks] }]`, `spacing` (16). Up to 6 side-by-side columns, nestable. |
| `image` | `source` (required), `width` (points; default natural size, capped to available width), `align`, `link` |
| `chart` | `chartType`: bar, line, or pie; `labels` (required); `values` (one series) **or** `series`: `[{ "name", "values", "color" }]`; `title`; `height` (220); `color` |
| `barcode` | `data` (printable ASCII), `width` (220), `height` (40), `showCaption` (true), `align`, `color` |
| `qrCode` | `data`, `size` (100), `errorCorrection`: L, M, Q, H (M), `align`, `color` |
| `divider` | `thickness` (1), `color` |
| `spacer` | `height` (required, points) |
| `pageBreak` | Starts a new page. |

### Text formatting

- `text` in `paragraph`, `list` items, `callout`, `keyValue` values, and table
  cells supports `**bold**` and `*italic*`. An unmatched `*` stays literal and
  `\*` escapes one. A newline (`\n`) starts a new line.
- For colour or size changes inside a paragraph, use `spans`:
  `[{ "text": "Status: " }, { "text": "Overdue", "bold": true, "color": "#C62828" }]`.
  Span properties: `text`, `bold`, `italic`, `underline`, `strikethrough`, `color`, `fontSize`.
- `link` makes the block clickable. Only http, https, and mailto URLs are allowed.

### Images and fonts

`source` (and font faces) accept, in order of preference:

1. A `data:` URI: `"data:image/png;base64,iVBORw0KGgo..."`.
2. Raw base64 data.
3. A file path relative to the asset directory, if the host configured one.

Only PNG and JPEG images and TrueType (`.ttf`) fonts are supported. URLs are not fetched.

### Characters

The built-in fonts (Helvetica, Times, Courier) cover Western European text
(Windows-1252). Other characters, such as Cyrillic, Greek, Devanagari, CJK,
arrows, and emoji, render as `?` unless you register a font that contains them in `fonts`
and set `theme.fontFamily` to it. `create_pdf` warns when this will happen.
Typographic quotes, en/em dashes, the bullet, and the euro sign are fine.

### Limits

At most 10,000 blocks, nesting 6 levels deep, 500 chart points, 10 chart series,
and 6 columns per `columns` block. Images and fonts are capped by the host (20 MB by default).

## Example: report

```json
{
  "title": "Q3 Sales Report",
  "author": "Analytics Team",
  "theme": { "accentColor": "#0B5394" },
  "header": [ { "type": "paragraph", "text": "**Acme Corp** - Q3 Sales Report", "color": "#6C757D", "fontSize": 8 } ],
  "tableOfContents": false,
  "content": [
    { "type": "heading", "level": 1, "text": "Q3 Sales Report" },
    { "type": "paragraph", "text": "Revenue grew **18%** quarter over quarter, led by the *Widgets* line." },
    { "type": "callout", "title": "Key takeaway", "text": "EMEA overtook North America for the first time." },
    { "type": "heading", "level": 2, "text": "Revenue by region" },
    { "type": "chart", "chartType": "bar", "title": "Revenue (USD)",
      "labels": ["NA", "EMEA", "APAC", "LATAM"],
      "series": [ { "name": "Q2", "values": [410000, 380000, 190000, 60000] },
                  { "name": "Q3", "values": [430000, 455000, 240000, 75000] } ] },
    { "type": "table",
      "columns": [ { "header": "Region", "width": 3 }, { "header": "Q2", "align": "right" },
                   { "header": "Q3", "align": "right" }, { "header": "Change", "align": "right" } ],
      "rows": [ ["North America", "$410,000", "$430,000", "+4.9%"],
                ["EMEA", "$380,000", "$455,000", "+19.7%"],
                ["APAC", "$190,000", "$240,000", "+26.3%"],
                ["LATAM", "$60,000", "$75,000", "+25.0%"] ],
      "footerRows": [ ["Total", "$1,040,000", "$1,200,000", "+15.4%"] ] },
    { "type": "heading", "level": 2, "text": "Next steps" },
    { "type": "list", "ordered": true, "items": ["Expand the APAC channel team", "Launch **Widgets Pro** in EMEA"] }
  ]
}
```

## Example: invoice

```json
{
  "title": "Invoice INV-2026-0042",
  "page": { "size": "Letter", "margin": 48 },
  "pageNumbers": false,
  "content": [
    { "type": "columns", "columns": [
      { "width": 2, "content": [
        { "type": "paragraph", "text": "**Northwind Traders**\n42 Harbour St\nSeattle, WA 98101" } ] },
      { "width": 1, "content": [
        { "type": "heading", "level": 1, "text": "INVOICE", "align": "right" },
        { "type": "keyValue", "entries": [ { "label": "Invoice #", "value": "INV-2026-0042" },
                                           { "label": "Date", "value": "2026-09-26" },
                                           { "label": "Due", "value": "**2026-10-26**" } ] } ] } ] },
    { "type": "divider", "thickness": 2 },
    { "type": "keyValue", "entries": [ { "label": "Bill to", "value": "Contoso Ltd.\n1 Market Rd, Boston, MA" } ] },
    { "type": "table",
      "columns": [ { "header": "Description", "width": 5 }, { "header": "Qty", "align": "right" },
                   { "header": "Unit price", "width": 2, "align": "right" }, { "header": "Amount", "width": 2, "align": "right" } ],
      "rows": [ ["Consulting (hours)", 12, "$150.00", "$1,800.00"],
                ["Annual support plan", 1, "$1,200.00", "$1,200.00"] ],
      "footerRows": [ ["", "", "Subtotal", "$3,000.00"], ["", "", "Tax (10%)", "$300.00"], ["", "", "Total", "$3,300.00"] ] },
    { "type": "callout", "title": "Payment", "text": "Bank transfer to IBAN GB00 NWBK 0000 0000 0000 00 within 30 days.", "color": "#2E7D32" },
    { "type": "qrCode", "data": "https://pay.example.com/INV-2026-0042", "size": 80, "align": "right" }
  ]
}
```

## Result

`create_pdf` returns `{ "success", "location", "fileName", "pageCount", "sizeBytes", "warnings", "errors", "hint" }`.
On failure nothing is written. Each error names a JSON path such as
`$.content[3].rows[1]`, so fix exactly those and call again.
