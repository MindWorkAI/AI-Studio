using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what a model reads of a tool result.
/// </summary>
/// <remarks>
/// The result goes into the request as a string, and the request is serialized once more on its way
/// to the provider. Whatever the first serialization escapes therefore reaches the model as the
/// escape itself: a German document would arrive with every umlaut spelled out as six characters,
/// and a piece of code with every angle bracket. That costs tokens, the budget of all tool results
/// counts it, and a model quoting a name from it may quote the escape.
/// </remarks>
[TestFixture]
public sealed class ToolExecutionResultTests
{
    [TestCase("Größe der Übersicht")]
    [TestCase("if (a < b && c > d) return 'x';")]
    [TestCase("日本語のテキスト")]
    public void TextReachesTheModelAsWritten(string text)
    {
        var result = new ToolExecutionResult { JsonContent = new JsonObject { ["text_content"] = text } };
        Assert.That(result.ToModelContent(), Does.Contain(text));
    }

    [Test]
    public void TheResultStaysValidJson()
    {
        const string TEXT = "A \"quoted\" line\nand a backslash \\ at its end.";
        var result = new ToolExecutionResult { JsonContent = new JsonObject { ["text_content"] = TEXT } };

        var readBack = JsonSerializer.Deserialize<JsonObject>(result.ToModelContent());
        Assert.That(readBack?["text_content"]?.GetValue<string>(), Is.EqualTo(TEXT), "What JSON itself has to escape, it still escapes.");
    }
}