# TerraPDF.Mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server for
[TerraPDF](https://www.nuget.org/packages/TerraPDF). It gives any MCP client
two capabilities:

| Tool | Use it to |
|---|---|
| `create_pdf` | Produce a PDF file from a JSON document description. The document is validated first, and errors come back with JSON paths. |
| `get_pdf_document_format` | Read the JSON document format and examples. |
| `get_terrapdf_csharp_guide` | Look up the verified TerraPDF C# API, recipes, and troubleshooting while writing code. |

## Run

Requires the .NET 8 SDK or later. With the .NET 10 SDK, no install is needed:

```sh
dnx TerraPDF.Mcp --yes
```

Or install it as a global tool:

```sh
dotnet tool install -g TerraPDF.Mcp
terrapdf-mcp
```

| Setting | Argument | Environment variable | Default |
|---|---|---|---|
| Output directory | `--output-dir <path>` | `TERRAPDF_OUTPUT_DIR` | `./pdf-output` |
| Asset directory (image and font paths) | `--asset-dir <path>` | `TERRAPDF_ASSET_DIR` | working directory |
| Disallow file paths (base64 only) | `--no-assets true` | `TERRAPDF_NO_ASSETS=1` | off |

Image and font paths cannot escape the asset directory, and URLs are never
fetched.

## Client configuration

**VS Code / GitHub Copilot** (`.vscode/mcp.json`):

```json
{
  "servers": {
    "terrapdf": { "type": "stdio", "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"] }
  }
}
```

**Cursor** (`.cursor/mcp.json`) and **Claude Desktop** (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "terrapdf": { "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"] }
  }
}
```

**Claude Code**:

```sh
claude mcp add terrapdf -- dnx TerraPDF.Mcp --yes
```

**LangChain / LangGraph (Python)** via `langchain-mcp-adapters`:

```python
from langchain.agents import create_agent
from langchain_mcp_adapters.client import MultiServerMCPClient

client = MultiServerMCPClient({
    "terrapdf": {"transport": "stdio", "command": "dnx", "args": ["TerraPDF.Mcp", "--yes"],
                 "env": {"TERRAPDF_OUTPUT_DIR": "./reports"}},
})
tools = await client.get_tools()
agent = create_agent(model, tools)   # model: any LangChain chat model with tool calling
await agent.ainvoke({"messages": [{"role": "user", "content": "Create a one-page PDF summary of Q3 sales: ..."}]})
```

**LangChain.js** uses the same server through `@langchain/mcp-adapters`.

For in-process .NET agents (Microsoft Agent Framework, Semantic Kernel), use
the `TerraPDF.Agents` package directly instead of this server.
