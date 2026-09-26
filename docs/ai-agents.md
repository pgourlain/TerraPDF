# TerraPDF for AI Agents

TerraPDF ships three pieces for AI-assisted development. Pick by what the AI
is doing:

| The AI is... | Use | What it provides |
|---|---|---|
| **Writing C# code** that uses TerraPDF (Copilot, Cursor, Claude Code, Codex) | The **TerraPDF skill** (`skills/terrapdf`) | Verified API reference, rules that prevent the common compile errors, compiling recipes, troubleshooting |
| **Creating PDFs at runtime** inside your .NET agent (Microsoft Agent Framework, Semantic Kernel, `IChatClient`) | **`TerraPDF.Agents`** NuGet package | `create_pdf` / `get_pdf_document_format` tools taking a validated JSON document |
| **Either, through MCP** (Copilot, Cursor, Claude, LangChain/LangGraph, any MCP client) | **`TerraPDF.Mcp`** server | The runtime tools plus a `get_terrapdf_csharp_guide` tool serving the skill |

---

## 1. The skill, for coding assistants

The skill is a folder in the [Agent Skills](https://agentskills.io) format: a
`SKILL.md` with rules and a skeleton, and a `references/` folder that the
assistant loads only when needed. Copy it into your project:

```sh
# GitHub Copilot (VS Code agent mode, Copilot coding agent)
cp -r skills/terrapdf .github/skills/terrapdf

# Claude Code
cp -r skills/terrapdf .claude/skills/terrapdf
```

For assistants without skill support, reference it from your rules file
(`AGENTS.md`, `.github/copilot-instructions.md`, `.cursor/rules/*.mdc`,
`CLAUDE.md`):

```markdown
## PDF generation
This project generates PDFs with TerraPDF (NuGet: TerraPDF). Before writing or
editing PDF code, read skills/terrapdf/SKILL.md and use only API members listed
in skills/terrapdf/references/api-reference.md.
```

Or connect the MCP server (section 3), whose `get_terrapdf_csharp_guide` tool
serves the same content.

The skill covers:

- **Rules** that prevent the failures agents hit most: single-child containers,
  `PublishPdf` as the only output call, the uneven `Color` palette, tuple page
  sizes, WinAnsi-only standard fonts, and canvas coordinates.
- **API reference**: every public member with its signature and defaults.
- **Recipes**: invoice, report with TOC, reusable components, bar and pie
  charts, Unicode fonts, encryption, shipping label, spanning tables, and an
  ASP.NET Core endpoint. All of them compile.
- **Troubleshooting**: compiler errors mapped to fixes, and runtime symptoms
  mapped to causes.

---

## 2. `TerraPDF.Agents`, for .NET agents

```sh
dotnet add package TerraPDF.Agents
```

The model describes the document as JSON and calls `create_pdf`:

```json
{
  "title": "Q3 Sales Report",
  "content": [
    { "type": "heading", "level": 1, "text": "Q3 Sales Report" },
    { "type": "paragraph", "text": "Revenue grew **18%** quarter over quarter." },
    { "type": "chart", "chartType": "bar", "labels": ["NA", "EMEA", "APAC"], "values": [430, 455, 240] },
    { "type": "table", "columns": [ { "header": "Region", "width": 3 }, { "header": "Revenue", "align": "right" } ],
      "rows": [ ["NA", "$430k"], ["EMEA", "$455k"], ["APAC", "$240k"] ] }
  ]
}
```

Block types: `heading`, `paragraph` (with `**bold**`/`*italic*` or styled
`spans`), `list`, `table` (with `footerRows` for totals), `keyValue`,
`callout`, `columns` (nestable), `image`, `chart` (bar, line, or pie; one or
more series), `barcode`, `qrCode`, `divider`, `spacer`, and `pageBreak`. The
document also supports headers, footers, page numbers, a table of contents,
custom fonts, a theme, and AES-256 encryption. The complete reference is
[document-format.md](../src/TerraPDF.Agents/Resources/document-format.md),
which is also what `get_pdf_document_format` returns to the model.

### Wiring it up

```csharp
using TerraPDF.Agents.Tools;

var pdfTools = new TerraPdfTools(new TerraPdfToolOptions { OutputDirectory = "out" });

// Microsoft Agent Framework
AIAgent agent = chatClient.AsAIAgent(
    instructions: "You produce polished PDF reports. Use create_pdf.",
    tools: [.. pdfTools.AsAIFunctions()]);

// Semantic Kernel
kernel.Plugins.AddFromFunctions("terrapdf", pdfTools.AsAIFunctions().Select(f => f.AsKernelFunction()));

// Microsoft.Extensions.AI IChatClient
IChatClient client = new ChatClientBuilder(inner).UseFunctionInvocation().Build();
var response = await client.GetResponseAsync(prompt, new ChatOptions { Tools = [.. pdfTools.AsAIFunctions()] });
```

### Designed for models

- **Self-correcting.** Invalid input writes nothing and returns all errors at
  once, each with a JSON path and a fix, such as
  `$.content[2].rows[1]: Row has 5 cells but the table defines 4 columns.`
- **Strict where drift loses content.** A misspelt property such as
  `"colour"` is an error, not silently ignored.
- **Lenient where drift is harmless.** It accepts any property casing,
  comments, trailing commas, numbers in table cells, `#RGB` colours, common
  aliases (`"type": "h2"`, `"bullets"`, `"text"`), and the document sent as a
  JSON string.
- **Honest about fonts.** It warns when text contains characters the built-in
  fonts cannot draw, before they render as `?`.

### Security

Tool arguments are model output, so treat them as untrusted:

- The model chooses a file name only. Paths are stripped, and existing files
  are never overwritten unless `AllowOverwrite` is set.
- Image and font paths resolve inside `AssetDirectory` and cannot escape it.
  With no asset directory (the default), only base64 data is accepted and no
  file is read.
- URLs are never fetched. Links allow only http, https, and mailto.
- Asset size, block count, nesting depth, and chart size are capped.

### Options

| Option | Default | Purpose |
|---|---|---|
| `OutputDirectory` | `%TEMP%/terrapdf` | Where PDFs are written |
| `AssetDirectory` | `null` | Root for image and font paths; `null` means base64 only |
| `AllowOverwrite` | `false` | Replace existing files instead of adding a `-2` suffix |
| `MaxAssetBytes` | 20 MB | Per image or font |
| `FontFamilies` | empty | Families you registered via `FontFamily.Register` that documents may use |
| `SaveAsync` | `null` | Custom storage (blob, S3, chat attachment); returns the location shown to the model |

Without an LLM, `new PdfSpecRenderer().Render(json)` turns the same JSON into
PDF bytes, which is useful for template-driven documents.

---

## 3. `TerraPDF.Mcp`, for MCP clients

```sh
dnx TerraPDF.Mcp --yes                 # .NET 10 SDK, no install
dotnet tool install -g TerraPDF.Mcp    # or install terrapdf-mcp globally
```

| Client | Configuration |
|---|---|
| VS Code / Copilot (`.vscode/mcp.json`) | `{ "servers": { "terrapdf": { "type": "stdio", "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"] } } }` |
| Cursor (`.cursor/mcp.json`), Claude Desktop | `{ "mcpServers": { "terrapdf": { "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"] } } }` |
| Claude Code | `claude mcp add terrapdf -- dnx TerraPDF.Mcp --yes` |
| LangChain / LangGraph | `MultiServerMCPClient({"terrapdf": {"transport": "stdio", "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"]}})` |

Tools: `create_pdf`, `get_pdf_document_format`, and `get_terrapdf_csharp_guide`
(topics: `overview`, `api`, `recipes`, `troubleshooting`). PDFs are written to
`./pdf-output` by default (`--output-dir`, `TERRAPDF_OUTPUT_DIR`). Image and
font paths resolve against the working directory (`--asset-dir`,
`TERRAPDF_ASSET_DIR`), or pass `--no-assets true` to allow base64 only.
