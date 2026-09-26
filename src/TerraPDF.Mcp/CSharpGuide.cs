using System.ComponentModel;

namespace TerraPDF.Mcp;

/// <summary>
/// Serves the TerraPDF coding-assistant skill (skills/terrapdf) so MCP clients
/// that write C# get the same verified API contract as clients that load the skill.
/// </summary>
internal static class CSharpGuide
{
    internal const string Name = "get_terrapdf_csharp_guide";

    internal const string Description =
        "Returns verified documentation for writing C# code with the TerraPDF library (NuGet: TerraPDF). " +
        "Call before writing or fixing TerraPDF code, and do not use API members that are not documented here. " +
        "Topics: overview (rules and skeleton - start here), api (every public member), recipes (compiling " +
        "invoice, report, chart, font, encryption, label, and ASP.NET Core examples), troubleshooting (compiler " +
        "errors and runtime symptoms).";

    private static readonly string[] s_topics = ["overview", "api", "recipes", "troubleshooting"];

    internal static string Get(
        [Description("overview, api, recipes, or troubleshooting. Defaults to overview.")] string? topic = null)
    {
        string key = string.IsNullOrWhiteSpace(topic) ? "overview" : topic.Trim().ToLowerInvariant();
        if (!s_topics.Contains(key))
            return $"Unknown topic '{topic}'. Available topics: {string.Join(", ", s_topics)}.";

        using Stream stream = typeof(CSharpGuide).Assembly.GetManifestResourceStream($"skill/{key}.md")
            ?? throw new InvalidOperationException($"Guide resource '{key}' is missing from the server build.");
        using var reader = new StreamReader(stream);
        string text = reader.ReadToEnd();

        // Links inside the skill point at sibling files; over MCP they are topics.
        return text
            .Replace("(references/api-reference.md)", " (topic: api)", StringComparison.Ordinal)
            .Replace("(references/recipes.md)", " (topic: recipes)", StringComparison.Ordinal)
            .Replace("(references/troubleshooting.md)", " (topic: troubleshooting)", StringComparison.Ordinal)
            .Replace("(references/agent-tools.md)", " (the create_pdf tool on this server)", StringComparison.Ordinal);
    }
}

internal static class ServerText
{
    internal const string Instructions =
        "TerraPDF server. Two uses: " +
        "(1) To produce a PDF file for the user, call get_pdf_document_format once, then create_pdf with a JSON " +
        "document; if it returns errors, fix exactly the JSON paths listed and call again. " +
        "(2) To write or fix C# code that uses the TerraPDF library, call get_terrapdf_csharp_guide (start with " +
        "topic 'overview') and use only the API members it documents.";
}
