# Letting an AI agent create PDFs at runtime

When an application's own LLM agent should produce PDFs, give the agent the
TerraPDF tools. Do not have the model write and execute C#: that needs a
compiler at runtime, cannot be validated, and runs model-written code.

The tools take a declarative JSON document with headings, paragraphs, tables,
lists, key/value blocks, callouts, columns, charts, images, barcodes, and QR
codes. They validate it, render it with TerraPDF, save it, and return the
location and page count. Invalid input writes nothing and returns every error
with a JSON path, such as `$.content[3].rows[1]`, so the model can correct
itself.

| Tool | Purpose |
|---|---|
| `create_pdf(document, fileName)` | Validate, render, and save |
| `get_pdf_document_format()` | Format reference with examples, for the model |

## .NET agents: `TerraPDF.Agents`

```sh
dotnet add package TerraPDF.Agents
```

```csharp
using TerraPDF.Agents.Tools;

var pdfTools = new TerraPdfTools(new TerraPdfToolOptions
{
    OutputDirectory = "/data/pdfs",   // the model chooses file names, never directories
    AssetDirectory = "/data/assets",  // optional; image/font paths cannot escape it
});

// Microsoft Agent Framework
AIAgent agent = chatClient.AsAIAgent(instructions: "...", tools: [.. pdfTools.AsAIFunctions()]);

// Semantic Kernel (functions appear to the model as terrapdf_create_pdf, ...)
kernel.Plugins.AddFromFunctions("terrapdf", pdfTools.AsAIFunctions().Select(f => f.AsKernelFunction()));

// Any IChatClient
var options = new ChatOptions { Tools = [.. pdfTools.AsAIFunctions()] };
```

Use `TerraPdfToolOptions.SaveAsync` to store PDFs somewhere other than disk,
such as blob storage or a chat attachment. `new PdfSpecRenderer().Render(json)`
renders the same JSON without an LLM, for example from stored templates.

## MCP clients and non-.NET frameworks: `TerraPDF.Mcp`

The MCP server hosts the same tools, plus `get_terrapdf_csharp_guide`, which
serves this skill's content to clients that write C#. It works with GitHub
Copilot, Cursor, Claude Code, Claude Desktop, and LangChain/LangGraph through
`langchain-mcp-adapters`:

```json
{ "servers": { "terrapdf": { "type": "stdio", "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"] } } }
```

Settings: `--output-dir` / `TERRAPDF_OUTPUT_DIR` (default `./pdf-output`) and
`--asset-dir` / `TERRAPDF_ASSET_DIR` (default: the working directory).
