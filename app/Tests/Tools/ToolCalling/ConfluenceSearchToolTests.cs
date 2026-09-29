using System.Web;

using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks the parts of the Confluence search which decide where a request may go and what it asks.
/// </summary>
/// <remarks>
/// The model supplies nothing but words, and everything around them comes from here: the wiki
/// address a search may use, the CQL the words end up in, which redirects stay within the wiki,
/// and when an answer is the login page rather than a search. A mistake in any of them sends the
/// user's query or sign-in somewhere else, or lets a model change what is searched. The request
/// itself needs a real Confluence and is left to a manual test.
/// </remarks>
[TestFixture]
public sealed class ConfluenceSearchToolTests
{
    private const string BASE_URL = "https://wiki.example.org/confluence/";

    private static readonly Uri WIKI = new(BASE_URL);

    [TestCase("https://wiki.example.org/confluence", "https://wiki.example.org/confluence/")]
    [TestCase("https://wiki.example.org/confluence/", "https://wiki.example.org/confluence/")]
    [TestCase("  https://wiki.example.org/confluence//  ", "https://wiki.example.org/confluence/")]
    [TestCase("https://wiki.example.org", "https://wiki.example.org/")]
    public void ABaseUrlEndsWithExactlyOneSlash(string value, string expected)
    {
        Assert.That(ParseBaseUrl(value).AbsoluteUri, Is.EqualTo(expected), "Without the slash, resolving the search page against the base URL would replace its last segment, and with more than one, no page of the wiki would count as within it.");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("wiki.example.org/confluence/")]
    [TestCase("http://wiki.example.org/confluence/")]
    [TestCase("https://user:secret@wiki.example.org/confluence/")]
    [TestCase("https://wiki.example.org/confluence/?os_authType=basic")]
    [TestCase("https://wiki.example.org/confluence/#search")]
    public void ABaseUrlWhichIsNotAPlainHttpsAddressIsRefused(string? value)
    {
        Assert.That(ConfluenceSearchTool.TryParseBaseUrl(value, out var baseUrl), Is.False, "Plain HTTP would expose the sign-in, credentials in the address would travel with every request, and a query or fragment has no place in the root of a wiki.");
        Assert.That(baseUrl, Is.Null);
    }

    [Test]
    public void TheSearchGoesToTheSearchPageOfTheWiki()
    {
        var searchUrl = ConfluenceSearchTool.BuildSearchUrl(ParseBaseUrl("https://wiki.example.org/confluence"), "release plan", null);
        var parameters = HttpUtility.ParseQueryString(searchUrl.Query);

        Assert.That(searchUrl.GetLeftPart(UriPartial.Path), Is.EqualTo("https://wiki.example.org/confluence/dosearchsite.action"), "The search page lies below the context path of the wiki, even when the configured address lacks the final slash.");
        Assert.That(parameters["cql"], Is.EqualTo("text ~ \"release plan\""));
        Assert.That(parameters["queryString"], Is.EqualTo("release plan"), "Confluence shows these words in its search field, so the page reads like a search the user made.");
        Assert.That(ConfluenceSearchTool.IsWithinWiki(WIKI, searchUrl), Is.True);
    }

    [TestCase(@"plan"" or space = ""HR", @"text ~ ""plan\"" or space = \""HR""")]
    [TestCase(@"C:\temp\", @"text ~ ""C:\\temp\\""")]
    [TestCase(@"plan\"" or space = \""HR", @"text ~ ""plan\\\"" or space = \\\""HR""")]
    public void TheQueryCannotLeaveItsCqlString(string query, string expectedCql)
    {
        //
        // The words land inside a quoted CQL string. Were a quote to end it, or a trailing backslash
        // to turn the closing quote into a literal one, a model could append conditions of its own,
        // such as one which widens the search to spaces the user never asked for:
        //
        Assert.That(Cql(ConfluenceSearchTool.BuildSearchUrl(WIKI, query, null)), Is.EqualTo(expectedCql), "Backslashes are escaped first and quotes second, so neither can end the string.");
    }

    [Test]
    public void ASpaceKeyRestrictsTheSearchToThatSpace()
    {
        Assert.That(Cql(ConfluenceSearchTool.BuildSearchUrl(WIKI, "release plan", "DEV")), Is.EqualTo("text ~ \"release plan\" and space=\"DEV\""));
    }

    [Test]
    public void ASpaceKeyCannotLeaveItsCqlStringEither()
    {
        Assert.That(Cql(ConfluenceSearchTool.BuildSearchUrl(WIKI, "release plan", @"DEV"" or space = ""HR")), Is.EqualTo(@"text ~ ""release plan"" and space=""DEV\"" or space = \""HR"""), "The space key comes from the model as well, so it gets the same escaping as the words.");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void WithoutASpaceKeyTheWholeWikiIsSearched(string? spaceKey)
    {
        Assert.That(Cql(ConfluenceSearchTool.BuildSearchUrl(WIKI, "release plan", spaceKey)), Is.EqualTo("text ~ \"release plan\""), "An empty space key would otherwise ask for a space which does not exist and find nothing.");
    }

    [Test]
    public void TheQueryStaysInsideItsParameters()
    {
        // Characters which mean something in a URL must neither end a parameter, start a fragment, nor change the path:
        const string QUERY = "R&D #1 ../../admin?x=1";
        var searchUrl = ConfluenceSearchTool.BuildSearchUrl(WIKI, QUERY, null);
        var parameters = HttpUtility.ParseQueryString(searchUrl.Query);

        Assert.That(searchUrl.AbsolutePath, Is.EqualTo("/confluence/dosearchsite.action"));
        Assert.That(searchUrl.Fragment, Is.Empty);
        Assert.That(parameters.AllKeys, Is.EquivalentTo(new[] { "cql", "queryString" }), "No word of the query may become a parameter of its own.");
        Assert.That(parameters["queryString"], Is.EqualTo(QUERY));
    }

    [TestCase("https://wiki.example.org/confluence/dosearchsite.action?cql=x")]
    [TestCase("https://wiki.example.org/confluence/display/DEV/Release+Plan")]
    [TestCase("https://WIKI.example.org:443/confluence/pages/viewpage.action?pageId=1")]
    [TestCase("https://wiki.example.org./confluence/display/DEV/")]
    public void AnAddressBelowTheBaseUrlIsWithinTheWiki(string url)
    {
        Assert.That(ConfluenceSearchTool.IsWithinWiki(WIKI, new Uri(url)), Is.True, "The case of the host, the default port written out, and a trailing dot all name the same wiki.");
    }

    [TestCase("https://wiki.example.org/confluence-evil/dosearchsite.action")]
    [TestCase("https://wiki.example.org/confluence/../admin/")]
    [TestCase("https://wiki.example.org/")]
    [TestCase("https://wiki.example.org:8443/confluence/")]
    [TestCase("http://wiki.example.org/confluence/")]
    [TestCase("https://wiki.example.org.evil.example/confluence/")]
    [TestCase("https://id.atlassian.com/login")]
    public void AnAddressOutsideTheBaseUrlIsNotWithinTheWiki(string url)
    {
        Assert.That(ConfluenceSearchTool.IsWithinWiki(WIKI, new Uri(url)), Is.False, "A neighbouring path, a path which climbs out, another port, plain HTTP, or another host is not the configured wiki, however similar it looks.");
    }

    [TestCase("https://wiki.example.org/confluence/login.action")]
    [TestCase("https://wiki.example.org/confluence/login.action?os_destination=%2Fdosearchsite.action%3Fcql%3Dx")]
    [TestCase("https://wiki.example.org/confluence/LOGIN.ACTION")]
    [TestCase("https://wiki.example.org/confluence/signin?os_destination=%2Fdosearchsite.action")]
    public void TheLoginPageIsRecognized(string url)
    {
        Assert.That(ConfluenceSearchTool.IsLoginPage(new Uri(url)), Is.True, "Confluence answers a search without a valid session with its login page, or with another page that carries the search as the destination to return to.");
    }

    [Test]
    public void ASearchForTheWordsOfTheLoginPageIsNoLoginPage()
    {
        var searchUrl = ConfluenceSearchTool.BuildSearchUrl(WIKI, "login.action os_destination=", null);

        Assert.That(ConfluenceSearchTool.IsLoginPage(searchUrl), Is.False, "Somebody looking up the login page in the wiki gets search results, not a message saying that the sign-in failed.");
    }

    private static Uri ParseBaseUrl(string value) => ConfluenceSearchTool.TryParseBaseUrl(value, out var baseUrl)
        ? baseUrl
        : throw new AssertionException($"'{value}' should be a valid base URL.");

    private static string? Cql(Uri searchUrl) => HttpUtility.ParseQueryString(searchUrl.Query)["cql"];
}