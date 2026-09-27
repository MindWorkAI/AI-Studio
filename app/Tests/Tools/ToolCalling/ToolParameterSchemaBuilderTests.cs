using System.Text.Json.Nodes;

using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks the plain JSON Schema a tool describes its arguments with.
/// </summary>
/// <remarks>
/// This is the form Anthropic and every host without strict mode receive as written, so it has to
/// mean exactly what the tool expects. The translation for strict mode is checked on its own, see
/// OpenAIStrictToolSchemaTests.
/// </remarks>
[TestFixture]
public sealed class ToolParameterSchemaBuilderTests
{
    [Test]
    public void AListOfChoicesRestrictsEveryEntry()
    {
        var schema = Built(ToolParameterSchemaBuilder.Create().OptionalStringArray("data_source_ids", "The data sources.", "first", "second"));
        var property = schema["properties"]!["data_source_ids"]!;

        Assert.Multiple(() =>
        {
            Assert.That(property["type"]!.GetValue<string>(), Is.EqualTo("array"));
            Assert.That(property["description"]!.GetValue<string>(), Is.EqualTo("The data sources."));
            Assert.That(property["items"]!["type"]!.GetValue<string>(), Is.EqualTo("string"));
            Assert.That(property["items"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()), Is.EqualTo(new[] { "first", "second" }));
        });
    }

    [Test]
    public void AListWithoutChoicesTakesAnyString()
    {
        var items = Built(ToolParameterSchemaBuilder.Create().OptionalStringArray("tags", "Some tags."))["properties"]!["tags"]!["items"]!;

        Assert.That(items["enum"], Is.Null, "An empty enum would allow no entry at all rather than any.");
    }

    [Test]
    public void AnOptionalListIsNotRequired()
    {
        var schema = Built(ToolParameterSchemaBuilder.Create()
            .RequiredString("query", "The search query.")
            .OptionalStringArray("data_source_ids", "The data sources.", "first"));

        Assert.That(schema["required"]!.AsArray().Select(name => name!.GetValue<string>()), Is.EqualTo(new[] { "query" }));
    }

    private static JsonNode Built(ToolParameterSchemaBuilder builder) => JsonNode.Parse(builder.Build().GetRawText())!;
}