# AGENTS.md

Guidance for AI coding agents working in this repository. For writing code
that *uses* TerraPDF, load the skill in [skills/terrapdf/](skills/terrapdf/SKILL.md).

## What this repository is

TerraPDF is a zero-dependency, pure C# PDF 1.7 generator (NuGet `TerraPDF`)
targeting .NET 8, 9, and 10. It also ships AI-agent tooling on top of it.

| Path | Contents |
|---|---|
| `src/TerraPDF/Core/` | Public fluent API: `Document`, descriptors, `ContainerExtensions`, `VectorCanvas` |
| `src/TerraPDF/Elements/` | Internal layout tree (measure/draw): `Column`, `Row`, `Table`, `TextBlock`, decorators |
| `src/TerraPDF/Drawing/` | PDF writing: objects, fonts, encryption, image decoders, TrueType subsetting |
| `src/TerraPDF/Helpers/`, `Infra/` | Public helpers (`Color`, `PageSize`, `Unit`, `TextStyle`, `FontFamily`) and interfaces |
| `src/TerraPDF.Agents/` | JSON document spec, validator, renderer, and `AIFunction` tools for LLM agents |
| `src/TerraPDF.Mcp/` | MCP server (dotnet tool) hosting the agent tools and the C# guide |
| `skills/terrapdf/` | Agent Skill for coding assistants: rules, API reference, recipes, troubleshooting |
| `tests/` | xUnit v3 suites for the library and the agent tools |
| `samples/TerraPDF.Sample/` | Eighteen runnable sample documents |
| `docs/` | User documentation, one guide per feature |

## Build and test

```sh
dotnet build -c Release
```

`dotnet test` cannot drive the xUnit v3 projects on the .NET 10 SDK: it
reports "Zero tests ran". Run the test executables directly after building:

```sh
./tests/TerraPDF.Tests/bin/Release/net10.0/TerraPDF.Tests
./tests/TerraPDF.Agents.Tests/bin/Release/net10.0/TerraPDF.Agents.Tests
```

(On Windows, add `.exe`.) CI runs every target framework: net8.0, net9.0, and net10.0.

## Rules

- **The `TerraPDF` project has zero runtime dependencies.** Never add a
  `PackageReference` to it other than build-only ones (`PrivateAssets="All"`).
  Dependencies belong in `TerraPDF.Agents` or `TerraPDF.Mcp`.
- **Warnings are errors**, analyzers run at `latest-recommended`, and
  `.editorconfig` style is enforced in the build. Use file-scoped namespaces,
  `_camelCase` private fields, and no `var` for built-in types.
- **Every public member needs an XML doc comment** and must validate its
  arguments with `ArgumentNullException.ThrowIfNull`,
  `ArgumentException.ThrowIfNullOrWhiteSpace`, or
  `ArgumentOutOfRangeException.ThrowIf*`. `ValidationTests.cs` covers these
  guards.
- **Tests assert on PDF output.** Content streams are Flate-compressed, so use
  `PdfTestUtils.InflatedText(bytes)` before searching for operators or text.
- **Keep the agent-facing contract in sync.** When a public API member is
  added, changed, or removed, update all of these:
  1. `docs/<feature>.md`
  2. `skills/terrapdf/references/api-reference.md` (and `recipes.md` if a recipe uses it)
  3. `CHANGELOG.md` under `[Unreleased]`
  4. The website's `/docs/ai-agents/` contract (terrapdf.com repository), which
     `api-reference.md` was derived from

  The recipes compile against the real API. If you edit one, compile it.
- **Agent tool changes** (`src/TerraPDF.Agents`): a new block type or property
  needs validation with a JSON path in `PdfSpecValidator`, rendering in
  `PdfSpecRenderer`, an entry in `Resources/document-format.md`, and a test.
  `SpecRenderingTests` renders every JSON example in `document-format.md`, so
  examples there must be valid.
- **Security invariants of the agent tools:** the model never chooses an
  output directory (`SanitizeFileName`), asset paths never escape
  `AssetDirectory`, URLs are never fetched, and links allow only
  http/https/mailto. Do not weaken these without an explicit request.

## Fixed after 2.2.0 (unreleased)

The skill and website still document the 2.2.0 workarounds for these. When the
fix ships, update the version wording in `skills/terrapdf/` and on the website.

- `VectorCanvas.Grid()` now records a command that is sized at draw time. It
  used to draw nothing, because `Canvas(height, draw)` runs `draw` before layout.
- `ShowIf(false)` now returns a detached container. It used to be overwritten
  by the element chained after it.
