# Benchmark analysis — 2026-09-26

First run of the `TerraPDF.Benchmarks` suite (see [docs/benchmarks.md](../docs/benchmarks.md)),
the hotspots it revealed, their root causes in the code, and a prioritised plan to fix them.

## Environment

- Commit: `61f5c50` (branch `users/pgo/improvements`)
- BenchmarkDotNet v0.15.8, macOS 27.2, Apple M5 (10 cores), .NET 10.0.12 Arm64 RyuJIT
- Job: `ShortRun` (3 iterations, 3 warmups, 1 launch) — indicative only; the
  invoice results have a very wide error margin and should be re-measured with the default job.

## Baseline results

### End to end

| Benchmark | Mean | Allocated |
|---|---:|---:|
| HelloWorld | 10.5 µs | 90 KB |
| Invoice30Lines | 0.81 ms | 1.9 MB |
| Invoice300Lines | 6.9 ms | 13.8 MB |

### Text (built-in fonts)

| Benchmark | Pages | Mean | Allocated |
|---|---:|---:|---:|
| LongText | 10 | 6.8 ms | 19.2 MB |
| LongText | 100 | 90.2 ms | 237 MB |
| LongText | 500 | 422 ms | 1,186 MB |
| RichSpans | 10 | 8.7 ms | 23.1 MB |
| RichSpans | 100 | 109 ms | 283 MB |
| RichSpans | 500 | 560 ms | 1,415 MB |
| UnicodeWithBuiltInFont* | 10 | 2.8 ms | 7.5 MB |
| UnicodeWithBuiltInFont* | 100 | 23.2 ms | 60 MB |
| UnicodeWithBuiltInFont* | 500 | 140 ms | 372 MB |

\* Uses a shorter paragraph than `LongText`, so it is not directly comparable.

### Tables

| Benchmark | Rows | Mean | Allocated |
|---|---:|---:|---:|
| PlainTable | 100 | 2.4 ms | 6.5 MB |
| PlainTable | 1,000 | 20.7 ms | 48.4 MB |
| PlainTable | 10,000 | 283 ms | 645 MB |
| TableWithSpans | 100 | 1.6 ms | 4.7 MB |
| TableWithSpans | 1,000 | 13.2 ms | 33.9 MB |
| TableWithSpans | 10,000 | 187 ms | 461 MB |

### Fonts

| Benchmark | Mean | Allocated |
|---|---:|---:|
| ParseLato | 18.7 µs | 131 KB |
| ParseDevanagari | 5.9 µs | 43 KB |
| SubsetLato | 115 µs | 938 KB (mostly LOH) |
| LatinDocument10Pages (Lato) | 15.1 ms | 28.5 MB |
| DevanagariDocument10Pages | 5.7 ms | 15.7 MB |

### Images

| Benchmark | Count | Mean | Allocated |
|---|---:|---:|---:|
| DecodePng (header logo) | – | 2.1 ms | 8.1 MB |
| DecodeAlphaPng | – | 0.27 ms | 1.1 MB |
| PngDocument | 1 | 12.1 ms | 32.4 MB |
| PngDocument | 40 | 93.9 ms | 323 MB |
| AlphaPngDocument | 1 | 2.8 ms | 4.5 MB |
| AlphaPngDocument | 40 | 14.9 ms | 43.6 MB |
| JpegDocument | 1 | 18 µs | 95 KB |
| JpegDocument | 40 | 98 µs | 174 KB |

### Encryption (20-page document)

| Benchmark | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| Unencrypted | 13.8 ms | 38.3 MB | 1.00 |
| Aes128 | 14.3 ms | 38.4 MB | 1.03 |
| Aes256 | 15.5 ms | 41.2 MB | 1.12 |

### Barcodes and canvas

| Benchmark | Mean | Allocated |
|---|---:|---:|
| QrShort (L / H) | 63 / 109 µs | 13 / 27 KB |
| QrLong (L / H) | 1.7 / 4.4 ms | 149 / 449 KB |
| Code128 | 0.33 µs | 1.9 KB |
| Document100QrCodes | 25.0 ms | 9.2 MB |
| DenseCanvas ×10 | 2.5 ms | 2.2 MB |
| DenseCanvas ×100 | 26.2 ms | 21.0 MB |

## Findings

| Area | Signal | Root cause |
|---|---|---|
| PNG images | 40× the same PNG = 323 MB / 94 ms, exactly 10× one placement. JPEG is cheap (174 KB for 40). | Every `ImageElement` decodes its PNG in its constructor; document-wide deduplication only happens at write time, after all decodes. Dedup key is a SHA-256 over the *decoded* pixels. |
| Text | ~2.4 MB allocated per page of plain paragraphs. | Word widths recomputed 4–5× per token; line layout recomputed on every `Measure` and on `Draw`; per-token string/`StringBuilder` allocations when emitting PDF operators. |
| Tables | ~64 KB per row; time grows ×8.8 (100→1k rows) then ×13.7 (1k→10k rows). | Row heights computed up to 3×; `DrawRows` scans *all* cells of the table for every page slice (quadratic in rows × pages). |
| Custom fonts | Lato document 2× slower and 50% more allocation than built-in font. `SubsetLato` allocates 938 KB, mostly LOH. | `CustomFontVariant.MeasureWidth` always runs Devanagari reordering + conjunct mapping (list allocations), multiplied by the repeated width measurement. Subsetting grows buffers instead of sizing them. |
| QR codes | ~250 µs / 94 KB per QR inside a document vs ~62 µs to generate. | One rectangle operator per module. |
| Layout passes | (not directly benchmarked yet) | The initial page-count hint is 99, so any document with 1–9 or ≥100 pages is laid out a second time — even without page-number spans. |
| Encryption | +3% (AES-128), +12% (AES-256). | Acceptable; no action needed. |

## Improvement plan

### Phase 0 — Reliable baseline (½ day)

- Re-run the full suite with the default job and store the JSON under `perf/baseline/`.
- Add `LongText` variants at **5** and **150** pages to expose the double-layout issue (item 2).
- Add an output-equivalence check (content streams decompressed, ignoring `/ID` and dates, or
  `pdftoppm` rendering diff) so optimisations can be proven not to change output.

### Phase 1 — Quick wins (low risk, high impact)

| # | Problem | Fix | Where | Expected gain |
|---|---|---|---|---|
| 1 | PNG decoded once per placement; SHA-256 over decoded pixels per page | Document-level decode cache keyed by the input bytes; lazy decode (layout only needs the IHDR size); hash the source bytes instead of pixels | `src/TerraPDF/Elements/ImageElement.cs:62`, `src/TerraPDF/Drawing/PdfDocument.cs:141` | `PngDocument(40)` 323 MB → ~10 MB |
| 2 | Double layout for 1–9 or ≥100 pages | Track whether page-number / total-pages spans are used; skip the stabilisation re-layout when they are not | `src/TerraPDF/Core/DocumentComposer.cs:824`, `src/TerraPDF/Elements/Element.cs:12` | ~2× on typical 1–9 page invoices |
| 3 | Token widths computed 4–5× (`BuildLines`, `Measure` Max+Sum, `DrawLines` Sum + advance, `DrawToken`) plus a font lookup each time | Compute width and resolved font once in `Tokenize`, store them on `TextToken` | `src/TerraPDF/Elements/TextBlock.cs:100` | text −30–50% time |
| 4 | `LayoutLines` re-run on every `Measure` and `Draw`; `Row` measures auto items twice; `Table.GetRowHeights` runs 3× | Memoise the last layout per `(width, style, hint)` on `TextBlock` and `Table` | `src/TerraPDF/Elements/TextBlock.cs:228`, `src/TerraPDF/Elements/Table.cs:115` | tables / nested layouts −40%+ |
| 5 | `F()` / `C()` format through `ToString("F2")`; `EscapeForPdfString` allocates a `StringBuilder` per token; `PdfColor.FromHex` per token | Format numbers directly into the content buffer (`ISpanFormattable`); escape straight into `_ops`; parse colours once per style | `src/TerraPDF/Drawing/PdfPage.cs:734`, `src/TerraPDF/Drawing/PdfPage.cs:768` | fewer allocations on every document |
| 6 | `PngDecoder` allocates a new row buffer per scanline, concatenates IDAT chunks via `List<byte[]>`, decompresses via `MemoryStream.ToArray()` | Reuse two row buffers; decompress into a pre-sized buffer (`height × stride`) | `src/TerraPDF/Drawing/PngDecoder.cs:125` | `DecodePng` 8.1 MB → ~3 MB |

### Phase 2 — Structural (medium risk)

7. **PNG passthrough**: for non-alpha RGB / gray / indexed PNGs, embed the IDAT stream as-is with
   `/DecodeParms << /Predictor 15 /Colors n /BitsPerComponent 8 /Columns w >>` (and an `/Indexed`
   colour space for palettes). No decode and no recompression. `PngDocument(1)` 12 ms → < 0.5 ms.
   Alpha images keep the decode path.
8. **Table slice drawing is quadratic**: `DrawRows` iterates every cell for every slice and builds a
   `Dictionary` each time. Index cells by row once so each slice only visits its own rows
   (`src/TerraPDF/Elements/Table.cs:215`).
9. **One `Tj` per line instead of per word**: every word currently emits its own `Td` + `Tj`. Merge
   consecutive same-style tokens; use `Tw` or `TJ` for justification. Content streams 2–3× smaller,
   so compression and encryption get faster too.
10. **Custom-font fast path**: skip Devanagari reordering and conjunct mapping when the text has no
    U+0900–U+097F characters (`src/TerraPDF/Drawing/TrueType/CustomFontVariant.cs:47`).
11. **Subsetting buffers**: size output buffers exactly or rent them from `ArrayPool` to stay off the LOH.
12. **Content stream copies**: `StringBuilder` → `string` → `byte[]` → `Compress` → `ToArray` makes
    three full copies; encode and compress directly into a pooled buffer.
13. **QR codes**: merge horizontal module runs into single rectangles; cache the generated QR matrix
    on the element.

### Phase 3 — Optional

- Parallel per-page content building and compression after layout (requires auditing shared state:
  image alias counter, custom-font glyph usage merging).
- Stream PDF objects to the output instead of accumulating `binaryObjects` (lower peak memory on
  large documents).
- Expose a compression-level option (`Optimal` today everywhere; `Fastest` for bulk generation).

## Suggested order

1. Items 2, 1, 3, 4 — biggest gains for the least code.
2. Items 8, 5.
3. Items 7, 9 — change PDF output, so they need the Phase 0 equivalence check plus a visual diff.
4. Items 10–13.
5. Phase 3 only if benchmarks still justify it.

Each change should be its own PR, with a before/after table from the relevant benchmark filter
(e.g. `--filter '*TableBenchmarks*'`), the full test suite green, and output equivalence verified
(except for items 7 and 9).
