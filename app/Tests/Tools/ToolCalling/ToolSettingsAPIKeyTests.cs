using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how the web search treats the API keys of its search services.
/// </summary>
/// <remarks>
/// The settings dialog checks a key while the user enters it, so the fields of the keys have to say
/// that they hold one. The settings validation must not refuse such a key, though: it decides whether
/// the web search is offered at all, and one key stored before AI Studio checked keys would switch
/// off every search service, those which work included. Such a key fails when its service is asked,
/// before any request, so that the dispatcher reports it and asks the other services.
/// </remarks>
[TestFixture]
public sealed class ToolSettingsAPIKeyTests
{
    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();

    private static IEnumerable<TestCaseData> Backends()
    {
        yield return new TestCaseData(new TavilySearchBackend()).SetArgDisplayNames("Tavily");
        yield return new TestCaseData(new StaanSearchBackend()).SetArgDisplayNames("Staan");
    }

    [Test]
    public void AnAPIKeyIsASecret()
    {
        var field = ToolSettingsSchemaBuilder.Create().OptionalAPIKey("service.apiKey").Build().Properties["service.apiKey"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.Secret, Is.True);
            Assert.That(field.IsAPIKey, Is.True);
        }
    }

    [TestCaseSource(nameof(Backends))]
    public void EverySecretOfASearchServiceIsAnAPIKey(IWebSearchBackend backend)
    {
        var secrets = SecretFields(backend);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(secrets, Is.Not.Empty);
            Assert.That(secrets.Where(field => !field.Value.IsAPIKey).Select(field => field.Key), Is.Empty);
        }
    }

    [TestCaseSource(nameof(Backends))]
    public void AKeyWhichCannotBeSentKeepsTheSearchServiceConfigured(IWebSearchBackend backend)
    {
        var apiKeyField = SecretFields(backend).Single().Key;
        var settings = new Dictionary<string, string> { [apiKeyField] = $"key{ZERO_WIDTH_SPACE}AbC123" };
        Assert.That(backend.TryValidateConfiguration(settings, out _), Is.True);
    }

    [TestCaseSource(nameof(Backends))]
    public async Task AStoredKeyWhichCannotBeSentFailsBeforeAnyRequest(IWebSearchBackend backend)
    {
        var apiKeyField = SecretFields(backend).Single().Key;
        var settings = new Dictionary<string, string> { [apiKeyField] = $"key{ZERO_WIDTH_SPACE}AbC123" };
        var query = new WebSearchQuery("weather", null, null, null, null, 5, 10);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.SearchAsync(query, settings));
        Assert.That(exception?.Message, Does.Contain(backend.Backend.ToString()).IgnoreCase.And.Not.Contain("AbC123"));
    }

    private static List<KeyValuePair<string, ToolSettingsFieldDefinition>> SecretFields(IWebSearchBackend backend)
    {
        var builder = ToolSettingsSchemaBuilder.Create();
        backend.DeclareSettings(builder);
        return builder.Build().Properties.Where(field => field.Value.Secret).ToList();
    }
}