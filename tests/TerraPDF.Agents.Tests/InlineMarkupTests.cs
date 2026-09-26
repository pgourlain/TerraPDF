using TerraPDF.Agents.Rendering;

namespace TerraPDF.Agents.Tests;

public class InlineMarkupTests
{
    private static string Describe(string text) =>
        string.Join("|", SpecText.ParseInline(text).Select(r => (r.Bold ? "B:" : "") + (r.Italic ? "I:" : "") + r.Text));

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a **bold** b", "a |B:bold| b")]
    [InlineData("a *it* b", "a |I:it| b")]
    [InlineData("a _it_ b", "a |I:it| b")]
    [InlineData("**_both_**", "B:I:both")]
    [InlineData("5 * 3 = 15", "5 * 3 = 15")]
    [InlineData("price: 5*3", "price: 5*3")]
    [InlineData("snake_case_name", "snake_case_name")]
    [InlineData("unclosed **bold", "unclosed **bold")]
    [InlineData(@"literal \*star\*", "literal *star*")]
    [InlineData("****", "****")]
    public void ParsesMarkup(string input, string expected) => Assert.Equal(expected, Describe(input));
}
