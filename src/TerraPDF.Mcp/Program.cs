using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TerraPDF.Agents.Tools;
using TerraPDF.Mcp;

// stdio MCP server. Configuration (command line wins over environment):
//   --output-dir <path>   TERRAPDF_OUTPUT_DIR   where create_pdf writes files (default: ./pdf-output)
//   --asset-dir <path>    TERRAPDF_ASSET_DIR    root for image/font paths      (default: current directory)
//   --no-assets           TERRAPDF_NO_ASSETS=1  disallow file paths; base64 only
HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // Keep the working directory the client launched us in: it is the user's
    // workspace, which is where relative output and asset paths should resolve.
    ContentRootPath = Environment.CurrentDirectory,
});

// stdout carries the MCP protocol; anything else written there corrupts it.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

ConfigurationManager config = builder.Configuration;
bool noAssets = config.GetValue<bool>("no-assets") || Environment.GetEnvironmentVariable("TERRAPDF_NO_ASSETS") == "1";

var options = new TerraPdfToolOptions
{
    OutputDirectory = Path.GetFullPath(config["output-dir"]
        ?? Environment.GetEnvironmentVariable("TERRAPDF_OUTPUT_DIR")
        ?? "pdf-output"),
    AssetDirectory = noAssets
        ? null
        : Path.GetFullPath(config["asset-dir"]
            ?? Environment.GetEnvironmentVariable("TERRAPDF_ASSET_DIR")
            ?? Environment.CurrentDirectory),
};

var pdfTools = new TerraPdfTools(options);

builder.Services
    .AddMcpServer(server =>
    {
        server.ServerInfo = new() { Name = "terrapdf", Version = typeof(TerraPdfTools).Assembly.GetName().Version?.ToString(3) ?? "1.0.0" };
        server.ServerInstructions = ServerText.Instructions;
    })
    .WithStdioServerTransport()
    .WithTools([
        .. pdfTools.AsAIFunctions().Select(f => McpServerTool.Create(f, new McpServerToolCreateOptions
        {
            // create_pdf writes a new file (never overwriting); the format tool only reads.
            ReadOnly = f.Name != "create_pdf",
            Destructive = false,
            Idempotent = f.Name != "create_pdf",
            OpenWorld = false,
        })),
        McpServerTool.Create(CSharpGuide.Get, new McpServerToolCreateOptions
        {
            Name = CSharpGuide.Name,
            Description = CSharpGuide.Description,
            ReadOnly = true,
            Idempotent = true,
        }),
    ]);

await builder.Build().RunAsync().ConfigureAwait(false);
