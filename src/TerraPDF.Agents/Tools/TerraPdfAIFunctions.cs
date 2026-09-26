using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace TerraPDF.Agents.Tools;

/// <summary>
/// Exposes <see cref="TerraPdfTools"/> as <see cref="AIFunction"/>s, the tool type
/// shared by the Microsoft Agent Framework, Semantic Kernel
/// (<c>AIFunction.AsKernelFunction()</c>), and every <see cref="IChatClient"/>.
/// </summary>
public static class TerraPdfAIFunctions
{
    /// <summary>Returns <c>create_pdf</c> and <c>get_pdf_document_format</c> as AI functions.</summary>
    public static IList<AIFunction> AsAIFunctions(this TerraPdfTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return
        [
            AIFunctionFactory.Create(TerraPdfTools.CreatePdfMethod, tools, new AIFunctionFactoryOptions
            {
                Name = TerraPdfTools.CreatePdfName,
                Description = TerraPdfTools.CreatePdfDescription,
                JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions { TransformSchemaNode = DeclareDocumentAsObject },
            }),
            AIFunctionFactory.Create(TerraPdfTools.GetFormatMethod, target: null, new AIFunctionFactoryOptions
            {
                Name = TerraPdfTools.GetFormatName,
                Description = TerraPdfTools.GetFormatDescription,
            }),
        ];
    }

    // The document parameter is a JsonElement so a JSON-encoded string is also
    // accepted, which leaves its schema untyped. Several providers reject
    // untyped parameters, and "object" steers models to send one.
    private static JsonNode DeclareDocumentAsObject(AIJsonSchemaCreateContext context, JsonNode schema)
    {
        if (context.TypeInfo.Type == typeof(JsonElement) && schema is JsonObject obj && !obj.ContainsKey("type"))
            obj["type"] = "object";
        return schema;
    }

    /// <summary>Creates the tools with <paramref name="options"/> and returns them as AI functions.</summary>
    public static IList<AIFunction> Create(TerraPdfToolOptions? options = null) =>
        new TerraPdfTools(options).AsAIFunctions();
}
