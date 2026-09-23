using System.Text.Json;
using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

namespace AIStudio.Tests.Tools.ToolCalling;

[TestFixture]
public sealed class ConfluenceSearchToolTests
{
    private static readonly Uri BASE_URL = new("https://wiki.example.org/confluence/");

    [Test]
    public void SearchRequestKeepsTheQueryInsideOneCqlPhrase()
    {
        var url = ConfluenceSearchTool.BuildSearchUrl(BASE_URL, "policy\" OR type=user", 8);
        var cql = Uri.UnescapeDataString(url.Query.Split('&')[0][5..]);

        Assert.Multiple(() =>
        {
            Assert.That(url.AbsolutePath, Is.EqualTo("/confluence/rest/api/search"));
            Assert.That(cql, Is.EqualTo("siteSearch ~ \"policy\\\" OR type=user\" AND type = page"));
            Assert.That(url.Query, Does.Contain("limit=8"));
        });
    }

    [Test]
    public void SearchResponseReturnsOnlyPagesInsideTheConfiguredWiki()
    {
        const string response = """
                                {
                                  "results": [
                                    { "title": "Policy", "excerpt": "A <strong>highlight</strong> &amp; a note", "url": "/confluence/display/TEAM/Policy" },
                                    { "title": "Outside", "excerpt": "Skip", "url": "https://other.example.org/secret" },
                                    { "title": "Other context", "excerpt": "Skip", "url": "https://wiki.example.org/other/display/TEAM/Policy" }
                                  ]
                                }
                                """;

        var results = ConfluenceSearchTool.ParseResults(response, BASE_URL, 8);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Title, Is.EqualTo("Policy"));
            Assert.That(results[0].Excerpt, Is.EqualTo("A highlight & a note"));
            Assert.That(results[0].Url, Is.EqualTo("https://wiki.example.org/confluence/display/TEAM/Policy"));
        });
    }

    [TestCase("http://wiki.example.org/confluence/")]
    [TestCase("https://wiki.example.org/confluence/?token=secret")]
    [TestCase("https://user:password@wiki.example.org/confluence/")]
    public void InvalidBaseUrlsCannotCarryCredentials(string baseUrl)
    {
        Assert.That(ConfluenceSearchTool.TryParseBaseUrl(baseUrl, out _), Is.False);
    }

    [Test]
    public void LowConfidenceProviderIsBlockedBeforeContactingTheWiki()
    {
        var tool = new ConfluenceSearchTool(null!);
        using var arguments = JsonDocument.Parse("""{"query":"internal policy"}""");
        var context = new ToolExecutionContext
        {
            Definition = tool.GetDefinition(),
            SettingsManager = null!,
            SettingsValues = new Dictionary<string, string> { ["baseUrl"] = BASE_URL.ToString() },
            ProviderConfidence = ConfidenceLevel.LOW,
        };

        Assert.ThrowsAsync<ToolExecutionBlockedException>(async () => await tool.ExecuteAsync(arguments.RootElement, context));
    }
}
