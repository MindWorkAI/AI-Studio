using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks when a tool reaches a Chat Completions host in strict mode, and when as written.
/// </summary>
/// <remarks>
/// Strict mode only holds where the host binds the model's calls to the schema. Anywhere else, the
/// model just reads a schema which calls every argument required: Groq then rejects each call that
/// leaves one out, and other models fill the gap with a placeholder the tool has to refuse. Such a
/// host therefore gets the schema the way the tool wrote it.
/// </remarks>
[TestFixture]
public sealed class ProviderToolAdaptersTests
{
    [Test]
    public void AHostWhichDoesNotEnforceStrictModeGetsTheSchemaAsWritten()
    {
        var definition = WebSearchLike();
        var function = Sent(definition, hostEnforcesStrict: false);

        Assert.Multiple(() =>
        {
            Assert.That(function["strict"]!.GetValue<bool>(), Is.False);
            Assert.That(JsonNode.DeepEquals(function["parameters"], JsonNode.Parse(definition.Function.Parameters.GetRawText())), Is.True);
        });
    }

    [Test]
    public void AHostWhichEnforcesStrictModeGetsTheConvertedSchema()
    {
        var function = Sent(WebSearchLike(), hostEnforcesStrict: true);

        Assert.Multiple(() =>
        {
            Assert.That(function["strict"]!.GetValue<bool>(), Is.True);
            Assert.That(function["parameters"]!["required"]!.AsArray().Select(name => name!.GetValue<string>()), Is.EqualTo(new[] { "query", "page" }));
        });
    }

    [Test]
    public void AToolWhichOptsOutStaysOutOfStrictModeEverywhere()
    {
        var definition = WebSearchLike(isStrict: false);
        var function = Sent(definition, hostEnforcesStrict: true);

        Assert.Multiple(() =>
        {
            Assert.That(function["strict"]!.GetValue<bool>(), Is.False);
            Assert.That(JsonNode.DeepEquals(function["parameters"], JsonNode.Parse(definition.Function.Parameters.GetRawText())), Is.True);
        });
    }

    /// <summary>
    /// A tool with one required and one optional argument, the shape the web search has.
    /// </summary>
    private static ToolDefinition WebSearchLike(bool isStrict = true) => new()
    {
        Id = "web_search",
        Function = new()
        {
            Name = "web_search",
            DescriptionForLLM = "Search the web.",
            Strict = isStrict,
            Parameters = ToolParameterSchemaBuilder.Create()
                .RequiredString("query", "The search query.")
                .OptionalInteger("page", "The page, starting at 1.")
                .Build(),
        },
    };

    /// <summary>
    /// Returns the function object of the tool as it goes over the wire.
    /// </summary>
    private static JsonNode Sent(ToolDefinition definition, bool hostEnforcesStrict)
    {
        var tool = ProviderToolAdapters.ToChatCompletionTool(definition, hostEnforcesStrict);
        return JsonNode.Parse(JsonSerializer.Serialize(tool, ProviderJsonOptions.OPTIONS))!["function"]!;
    }
}