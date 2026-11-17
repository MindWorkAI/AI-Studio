using AIStudio.Tests.Models.Generation;

using Microsoft.CodeAnalysis;

using SourceCodeRules.UsageAnalyzers;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks that every way into JSON contracts built by reflection is refused while compiling.
/// </summary>
/// <remarks>
/// The pre-release v26.10.1 failed because a published build lacked what reflection needed for the JSON of
/// AI Studio. Since then, the options of AI Studio name a JsonSerializerContext, whose contracts are generated
/// while compiling. A single call without options brings reflection back, and only a published build shows
/// it, so MWAIS0015 turns such a call into a build error. These tests say which code it refuses and which it
/// leaves alone.
/// </remarks>
[TestFixture]
public sealed class JsonSourceGenerationAnalyzerTests
{
    [TestCase("JsonSerializer.Serialize(meeting)", "JsonSerializer.Serialize gets no options", TestName = "Writing without options")]
    [TestCase("JsonSerializer.Serialize(meeting, options: null)", "JsonSerializer.Serialize gets no options", TestName = "Writing with options which are null")]
    [TestCase("JsonSerializer.Deserialize<Meeting>(json)", "JsonSerializer.Deserialize gets no options", TestName = "Reading without options")]
    [TestCase("element.Deserialize<Meeting>()", "JsonSerializer.Deserialize gets no options", TestName = "Reading a JSON element without options")]
    [TestCase("client.GetFromJsonAsync<Meeting>(\"/meeting\")", "HttpClientJsonExtensions.GetFromJsonAsync gets no options", TestName = "Reading a web answer without options")]
    [TestCase("client.PostAsJsonAsync(\"/meeting\", meeting)", "HttpClientJsonExtensions.PostAsJsonAsync gets no options", TestName = "Sending a web request without options")]
    [TestCase("content.ReadFromJsonAsync<Meeting>()", "HttpContentJsonExtensions.ReadFromJsonAsync gets no options", TestName = "Reading web content without options")]
    [TestCase("JsonContent.Create(meeting)", "JsonContent.Create gets no options", TestName = "Creating web content without options")]
    [TestCase("JsonSerializer.Serialize(meeting, JsonSerializerOptions.Default)", "JsonSerializerOptions.Default builds every contract by reflection", TestName = "The default options")]
    [TestCase("JsonSerializerOptions.Web", "JsonSerializerOptions.Web builds every contract by reflection", TestName = "The options for the web")]
    [TestCase("new DefaultJsonTypeInfoResolver()", "DefaultJsonTypeInfoResolver builds contracts by reflection", TestName = "The resolver which uses reflection")]
    [TestCase("new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }", "DefaultJsonTypeInfoResolver builds contracts by reflection", TestName = "Options with the resolver which uses reflection")]
    [TestCase("new JsonSerializerOptions { WriteIndented = true }", "These options name no TypeInfoResolver", TestName = "Options without a resolver")]
    [TestCase("new JsonSerializerOptions(JsonSerializerDefaults.Web)", "These options name no TypeInfoResolver", TestName = "Options for the web without a resolver")]
    [TestCase("new JsonSerializerOptions { TypeInfoResolver = null }", "These options name no TypeInfoResolver", TestName = "Options with a resolver which is null")]
    public async Task AWayIntoReflectionIsRefused(string expression, string expectedMessage)
    {
        var reported = await AnalyzeAsync(expression);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0015"));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.StartWith(expectedMessage));
        }
    }

    [Test]
    public async Task OptionsCreatedByATargetTypedNewWithoutAResolverAreRefused()
    {
        var reported = await AnalyzeAsync("OTHER_OPTIONS", "private static readonly JsonSerializerOptions OTHER_OPTIONS = new() { WriteIndented = true };");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.StartWith("These options name no TypeInfoResolver"));
        }
    }

    [TestCase("JsonSerializer.Serialize(meeting, OPTIONS)", TestName = "Writing with options")]
    [TestCase("JsonSerializer.Serialize(meeting, contract)", TestName = "Writing with the contract")]
    [TestCase("element.Deserialize(contract)", TestName = "Reading a JSON element with the contract")]
    [TestCase("client.GetFromJsonAsync<Meeting>(\"/meeting\", OPTIONS)", TestName = "Reading a web answer with options")]
    [TestCase("content.ReadFromJsonAsync(contract)", TestName = "Reading web content with the contract")]
    [TestCase("JsonContent.Create(meeting, contract)", TestName = "Creating web content with the contract")]
    [TestCase("new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = resolver }", TestName = "Options with a resolver")]
    [TestCase("new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver }", TestName = "Options for the web with a resolver")]
    [TestCase("JsonNode.Parse(json)", TestName = "A tree of JSON nodes")]
    public async Task JsonWithGeneratedContractsIsLeftAlone(string expression)
    {
        var reported = await AnalyzeAsync(expression);

        Assert.That(reported, Is.Empty);
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string expression, string otherMembers = "")
    {
        var source =
            $$"""
              using System.Net.Http;
              using System.Net.Http.Json;
              using System.Text.Json;
              using System.Text.Json.Nodes;
              using System.Text.Json.Serialization.Metadata;

              namespace Sample;

              public sealed class Meeting
              {
                  public string Location { get; set; } = "Cologne";
              }

              public static class MeetingJson
              {
                  private static readonly JsonSerializerOptions OPTIONS = new() { TypeInfoResolver = JsonTypeInfoResolver.Combine() };

                  {{otherMembers}}

                  public static object? Use(Meeting meeting, string json, JsonElement element, HttpClient client, HttpContent content, JsonTypeInfo<Meeting> contract, IJsonTypeInfoResolver resolver) => {{expression}};
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new JsonSourceGenerationAnalyzer());
    }
}