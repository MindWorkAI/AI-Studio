using System.Text.Json.Nodes;

using AIStudio.Provider.OpenAI;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Checks how a tool's parameter schema is translated into the form OpenAI's strict mode requires.
/// </summary>
/// <remarks>
/// Strict mode wants every argument required and says "may be left out" by allowing null instead.
/// The translation has to keep what the schema means: an argument the tool can do without stays
/// one the model can leave empty, and an argument the tool needs stays exactly as it was.
/// </remarks>
[TestFixture]
public sealed class OpenAIStrictToolSchemaTests
{
    [Test]
    public void AnOptionalArgumentMayBeNullInstead()
    {
        var properties = Converted(ToolParameterSchemaBuilder.Create()
            .RequiredString("query", "The search query.")
            .OptionalString("language", "The language.")
            .OptionalInteger("page", "The page."))["properties"]!;

        Assert.Multiple(() =>
        {
            Assert.That(Types(properties["language"]!), Is.EqualTo(new[] { "string", "null" }));
            Assert.That(Types(properties["page"]!), Is.EqualTo(new[] { "integer", "null" }));
        });
    }

    [Test]
    public void AnOptionalChoiceOffersNullAmongItsValues()
    {
        var timeRange = Converted(ToolParameterSchemaBuilder.Create()
            .RequiredString("query", "The search query.")
            .OptionalEnum("time_range", "The time range.", "day", "month", "year"))["properties"]!["time_range"]!;

        Assert.Multiple(() =>
        {
            Assert.That(Types(timeRange), Is.EqualTo(new[] { "string", "null" }));
            Assert.That(timeRange["enum"]!.AsArray().Select(value => value?.GetValue<string>()), Is.EqualTo(new[] { null, "day", "month", "year" }));
        });
    }

    [Test]
    public void AnOptionalListMayBeNullWhileItsEntriesKeepTheirChoice()
    {
        var dataSourceIds = Converted(ToolParameterSchemaBuilder.Create()
            .RequiredString("query", "The search query.")
            .OptionalStringArray("data_source_ids", "The data sources.", "first", "second"))["properties"]!["data_source_ids"]!;

        Assert.Multiple(() =>
        {
            Assert.That(Types(dataSourceIds), Is.EqualTo(["array", "null"]), "Leaving the list out is said by allowing null for the list itself.");
            Assert.That(dataSourceIds["items"]!["enum"]!.AsArray().Select(value => value?.GetValue<string>()), Is.EqualTo(["first", "second"]), "Null is a way to leave the list out, not an entry it may hold.");
            Assert.That(dataSourceIds["enum"], Is.Null, "The list itself names no values of its own.");
        });
    }

    [Test]
    public void ARequiredArgumentStaysAsItIs()
    {
        var query = Converted(ToolParameterSchemaBuilder.Create()
            .RequiredString("query", "The search query.")
            .OptionalString("language", "The language."))["properties"]!["query"]!;

        Assert.That(query["type"]!.GetValue<string>(), Is.EqualTo("string"));
    }

    [Test]
    public void EveryArgumentIsRequiredInTheOrderOfTheProperties()
    {
        //
        // The order has to be stable across requests, because prompt caching depends on it. The
        // optional argument comes first here, so an order taken from the old required list would
        // show.
        //
        var schema = Converted(ToolParameterSchemaBuilder.Create()
            .OptionalString("language", "The language.")
            .RequiredString("query", "The search query."));

        Assert.That(schema["required"]!.AsArray().Select(name => name!.GetValue<string>()), Is.EqualTo(new[] { "language", "query" }));
    }

    [Test]
    public void ExtraArgumentsStayRefused()
    {
        var schema = Converted(ToolParameterSchemaBuilder.Create()
            .RequiredString("query", "The search query.")
            .OptionalString("language", "The language."));

        Assert.That(schema["additionalProperties"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void ASchemaWithoutOptionalArgumentsIsLeftUntouched()
    {
        var parameters = ToolParameterSchemaBuilder.Create()
            .RequiredString("url", "The address of the page.")
            .Build();

        var converted = OpenAIStrictToolSchema.FromToolParameters(parameters);
        Assert.That(JsonNode.DeepEquals(JsonNode.Parse(converted.GetRawText()), JsonNode.Parse(parameters.GetRawText())), Is.True);
    }

    /// <summary>
    /// Builds the schema and returns it the way strict mode receives it.
    /// </summary>
    private static JsonNode Converted(ToolParameterSchemaBuilder builder) => JsonNode.Parse(OpenAIStrictToolSchema.FromToolParameters(builder.Build()).GetRawText())!;

    /// <summary>
    /// Reads the types a property allows, whether it names one or several.
    /// </summary>
    private static string[] Types(JsonNode property) => property["type"] switch
    {
        JsonArray types => types.Select(type => type!.GetValue<string>()).ToArray(),
        var type => [type!.GetValue<string>()],
    };
}