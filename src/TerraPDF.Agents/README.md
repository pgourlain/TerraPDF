# TerraPDF.Agents

PDF-generation tools for AI agents, built on [TerraPDF](https://www.nuget.org/packages/TerraPDF).

An LLM describes the document as JSON: headings, paragraphs, tables, lists,
key/value blocks, callouts, columns, charts, images, barcodes, and QR codes.
It then calls `create_pdf`. The tool validates the document, renders it with
TerraPDF, saves it, and returns the location and page count. When the input
is invalid, nothing is written and every error comes back with a JSON path,
so the model can correct itself on the next call.

```sh
dotnet add package TerraPDF.Agents
```

## Tools

| Tool | Purpose |
|---|---|
| `create_pdf(document, fileName)` | Validate, render, and save a PDF |
| `get_pdf_document_format()` | Full format reference with examples, for the model to read first |

## Microsoft Agent Framework

```csharp
using Microsoft.Agents.AI;
using TerraPDF.Agents.Tools;

var pdfTools = new TerraPdfTools(new TerraPdfToolOptions { OutputDirectory = "out" });

AIAgent agent = chatClient.AsAIAgent(
    instructions: "You produce polished PDF reports. Use create_pdf.",
    tools: [.. pdfTools.AsAIFunctions()]);

Console.WriteLine(await agent.RunAsync("Make a one-page PDF summarising our Q3 numbers: ..."));
```

## Semantic Kernel

```csharp
using Microsoft.SemanticKernel;
using TerraPDF.Agents.Tools;

kernel.Plugins.AddFromFunctions("terrapdf",
    new TerraPdfTools().AsAIFunctions().Select(f => f.AsKernelFunction()));

var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
var answer = await kernel.InvokePromptAsync("Create an invoice PDF for ...", new(settings));
```

## Any IChatClient (Microsoft.Extensions.AI)

```csharp
IChatClient client = new ChatClientBuilder(innerClient).UseFunctionInvocation().Build();
var options = new ChatOptions { Tools = [.. TerraPdfAIFunctions.Create()] };
var response = await client.GetResponseAsync("Create a PDF ...", options);
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `OutputDirectory` | `%TEMP%/terrapdf` | Where PDFs are written. The model picks the file name only. |
| `AssetDirectory` | `null` | Directory for image and font paths. When null, assets must be base64, and no file is read. |
| `AllowOverwrite` | `false` | When false, an existing file gets a `-2` style suffix instead of being replaced. |
| `MaxAssetBytes` | 20 MB | Cap per image or font. |
| `FontFamilies` | empty | Families you registered with `FontFamily.Register` that documents may use. |
| `SaveAsync` | `null` | Custom storage, such as blob upload or a chat attachment. Returns the location reported to the model. |

## Without an LLM

The spec renderer is usable directly, for example to render JSON templates:

```csharp
var result = new PdfSpecRenderer().Render(File.ReadAllText("invoice.json"));
if (result.Success) File.WriteAllBytes("invoice.pdf", result.Pdf!);
else foreach (var e in result.Errors) Console.WriteLine(e);
```

For MCP clients (GitHub Copilot, Cursor, Claude Code, LangChain), use the
`TerraPDF.Mcp` server, which hosts these same tools.
