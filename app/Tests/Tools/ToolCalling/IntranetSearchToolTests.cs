using System.Web;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.Web;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks the parts of the intranet search which decide what it asks and what it hands the model.
/// </summary>
/// <remarks>
/// The model supplies nothing but words, and everything around them comes from here: the query the
/// words end up in, which redirects stay within the intranet, and which hits of the answer reach the
/// model. The answers below follow the documented format of the search REST API of SharePoint
/// Server; the request itself needs a real SharePoint and is left to a manual test.
/// </remarks>
[TestFixture]
public sealed class IntranetSearchToolTests
{
    private static readonly Uri INTRANET = new("https://intranet.example.org/");

    private const string ATOM_ANSWER = """
        <?xml version="1.0" encoding="utf-8"?>
        <d:query xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices" xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata" m:type="Microsoft.Office.Server.Search.REST.SearchResult">
          <d:PrimaryQueryResult m:type="Microsoft.Office.Server.Search.REST.QueryResult">
            <d:RelevantResults m:type="Microsoft.Office.Server.Search.REST.RelevantResults">
              <d:Table m:type="SP.SimpleDataTable">
                <d:Rows>
                  <d:element m:type="SP.SimpleDataRow">
                    <d:Cells>
                      <d:element m:type="SP.KeyValue"><d:Key>Title</d:Key><d:Value>Travel guideline</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                      <d:element m:type="SP.KeyValue"><d:Key>Path</d:Key><d:Value>https://intranet.example.org/sites/hr/Documents/Travel%20guideline.pdf</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                      <d:element m:type="SP.KeyValue"><d:Key>HitHighlightedSummary</d:Key><d:Value>Every &lt;c0&gt;business trip&lt;/c0&gt; needs approval&lt;ddd/&gt;</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                      <d:element m:type="SP.KeyValue"><d:Key>FileType</d:Key><d:Value>pdf</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                      <d:element m:type="SP.KeyValue"><d:Key>SiteTitle</d:Key><d:Value>Human Resources</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                    </d:Cells>
                  </d:element>
                  <d:element m:type="SP.SimpleDataRow">
                    <d:Cells>
                      <d:element m:type="SP.KeyValue"><d:Key>Title</d:Key><d:Value>Old share</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                      <d:element m:type="SP.KeyValue"><d:Key>Path</d:Key><d:Value>file://fileserver/hr/old.docx</d:Value><d:ValueType>Edm.String</d:ValueType></d:element>
                    </d:Cells>
                  </d:element>
                </d:Rows>
              </d:Table>
            </d:RelevantResults>
          </d:PrimaryQueryResult>
        </d:query>
        """;

    private const string VERBOSE_JSON_ANSWER = """
        {"d":{"query":{"PrimaryQueryResult":{"RelevantResults":{"Table":{"Rows":{"results":[
          {"Cells":{"results":[{"Key":"Title","Value":"Travel","ValueType":"Edm.String"},{"Key":"Path","Value":"https://intranet.example.org/sites/hr/Pages/Travel.aspx","ValueType":"Edm.String"},{"Key":"FileType","Value":"aspx","ValueType":"Edm.String"}]}}
        ]}}}},"SecondaryQueryResults":{"results":[]}}}}
        """;

    private const string PLAIN_JSON_ANSWER = """
        {"PrimaryQueryResult":{"RelevantResults":{"Table":{"Rows":[
          {"Cells":[{"Key":"Title","Value":"Forms"},{"Key":"Path","Value":"https://intranet.example.org/forms"}]}
        ]}}}}
        """;

    [Test]
    public void AnAtomAnswerTurnsIntoHits()
    {
        Assert.That(IntranetSearchTool.TryParseResults(ATOM_ANSWER, out var hits), Is.True);
        Assert.That(hits, Has.Count.EqualTo(1), "A hit without an HTTP address, such as one of a file share, cannot be opened and is left out.");

        var hit = hits[0];
        Assert.Multiple(() =>
        {
            Assert.That(hit.Title, Is.EqualTo("Travel guideline"));
            Assert.That(hit.Url, Is.EqualTo(new Uri("https://intranet.example.org/sites/hr/Documents/Travel%20guideline.pdf")), "The hit links the document directly, which Read Web Page opens.");
            Assert.That(hit.Summary, Is.EqualTo("Every business trip needs approval …"), "The marks of the words found and of the gaps are SharePoint's, not text.");
            Assert.That(hit.FileType, Is.EqualTo("pdf"));
            Assert.That(hit.Site, Is.EqualTo("Human Resources"));
        });
    }

    [Test]
    public void AnAddressWithSpacesReachesTheModelWhole()
    {
        // SharePoint writes its paths as they are named, spaces included:
        const string ANSWER = """{"PrimaryQueryResult":{"RelevantResults":{"Table":{"Rows":[{"Cells":[{"Key":"Title","Value":"Jobticket"},{"Key":"Path","Value":"https://intranet.example.org/sites/hr/Shared Documents/Jobticket Antrag.pdf"}]}]}}}}""";
        Assert.That(IntranetSearchTool.TryParseResults(ANSWER, out var hits), Is.True);

        var url = IntranetSearchTool.BuildResults(hits)[0]!["url"]!.GetValue<string>();

        Assert.Multiple(() =>
        {
            Assert.That(url, Is.EqualTo("https://intranet.example.org/sites/hr/Shared%20Documents/Jobticket%20Antrag.pdf"));
            Assert.That(WebAddresses.Find(url), Does.Contain(url), "An address cut at a space would not count as given, and Read Web Page would refuse the hit while the free address choice is off.");
        });
    }

    [Test]
    public void AHitReadAlongCarriesItsContent()
    {
        Assert.That(IntranetSearchTool.TryParseResults(PLAIN_JSON_ANSWER, out var hits), Is.True);
        var content = new SearchHitContent(hits[0].Url, hits[0].Url, "Forms", "All forms of the intranet.", IsPartial: true, Failure: null);

        var result = IntranetSearchTool.BuildResults(hits, new Dictionary<int, SearchHitContent> { [0] = content })[0]!;

        Assert.Multiple(() =>
        {
            Assert.That(result["text_content"]!.GetValue<string>(), Is.EqualTo("All forms of the intranet."));
            Assert.That(result["content_status"]!.GetValue<string>(), Is.EqualTo("partial"), "The model learns that read_web_page has more of it.");
        });
    }

    [Test]
    public void AVerboseJsonAnswerTurnsIntoHits()
    {
        Assert.That(IntranetSearchTool.TryParseResults(VERBOSE_JSON_ANSWER, out var hits), Is.True);
        Assert.That(hits.Select(hit => hit.Title), Is.EqualTo(new[] { "Travel" }));
    }

    [Test]
    public void APlainJsonAnswerTurnsIntoHits()
    {
        Assert.That(IntranetSearchTool.TryParseResults(PLAIN_JSON_ANSWER, out var hits), Is.True);
        Assert.That(hits.Select(hit => hit.Url.ToString()), Is.EqualTo(new[] { "https://intranet.example.org/forms" }));
    }

    [Test]
    public void AnAnswerWithoutHitsIsStillASearchResult()
    {
        const string ANSWER = """{"PrimaryQueryResult":{"RelevantResults":{"Table":{"Rows":[]}}}}""";

        Assert.That(IntranetSearchTool.TryParseResults(ANSWER, out var hits), Is.True, "Nothing found is an answer, not a failure.");
        Assert.That(hits, Is.Empty);
    }

    [TestCase("<!DOCTYPE html><html><body><form action=\"/_login\">Sign in</form></body></html>")]
    [TestCase("{\"error\":{\"message\":\"Access denied\"}}")]
    [TestCase("plain text")]
    [TestCase("<broken")]
    public void AnythingElseIsNoSearchResult(string body)
    {
        Assert.That(IntranetSearchTool.TryParseResults(body, out _), Is.False, "A sign-in page or an error must not reach the model as a search without hits.");
    }

    [TestCase("travel guideline", "travel guideline")]
    [TestCase("path:https://evil.example/ secrets", "path https //evil.example/ secrets")]
    [TestCase("\"exact phrase\" (a OR b)", "exact phrase a OR b")]
    [TestCase("size>1000", "size 1000")]
    public void TheQueryKeepsNoSearchSyntax(string query, string expected)
    {
        Assert.That(IntranetSearchTool.ToPlainWords(query), Is.EqualTo(expected), "A model must not turn the search into another query.");
    }

    [Test]
    public void AQuoteCannotLeaveTheQueryString()
    {
        var searchUrl = IntranetSearchTool.BuildSearchUrl(INTRANET, "O'Brien travel", 10);
        var parameters = HttpUtility.ParseQueryString(searchUrl.Query);

        Assert.Multiple(() =>
        {
            Assert.That(searchUrl.AbsolutePath, Is.EqualTo("/_api/search/query"));
            Assert.That(parameters["querytext"], Is.EqualTo("'O''Brien travel'"), "A single quote is written twice inside the string of the REST API.");
            Assert.That(parameters["rowlimit"], Is.EqualTo("10"));
            Assert.That(IntranetSearchTool.IsWithinIntranet(INTRANET, searchUrl), Is.True);
        });
    }

    [TestCase("https://intranet.example.org/sites/hr/Documents/form.docx", true)]
    [TestCase("https://INTRANET.example.org/", true)]
    [TestCase("https://login.example.org/adfs/ls/", false)]
    [TestCase("http://intranet.example.org/", false)]
    [TestCase("https://intranet.example.org.evil.example/", false)]
    public void OnlyTheConfiguredIntranetIsWithinIt(string url, bool expected)
    {
        Assert.That(IntranetSearchTool.IsWithinIntranet(INTRANET, new Uri(url)), Is.EqualTo(expected));
    }

    [Test]
    public void ASearchWithHitsKeepsTheChatWithTrustedProvidersAndRestrictsIt()
    {
        var (confidence, restriction) = IntranetSearchTool.GetRequirements(2, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES);

        Assert.Multiple(() =>
        {
            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.HIGH));
            Assert.That(restriction.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), "The level is the one of internal pages.");
            Assert.That(restriction.Origin, Is.EqualTo(OutboundDataOrigin.INTRANET));
        });
    }

    [Test]
    public void ASearchWithoutHitsDemandsNothing()
    {
        var (confidence, restriction) = IntranetSearchTool.GetRequirements(0, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES);

        Assert.Multiple(() =>
        {
            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.NONE));
            Assert.That(restriction, Is.EqualTo(OutboundDataRequirement.NONE), "Nothing of the intranet reached the chat.");
        });
    }

    [TestCase("https://intranet.example.org/sites/HR/Shared%20Documents/form.docx", true)]
    [TestCase("https://intranet.example.org/sites/hr/Pages/start.aspx", true)]
    [TestCase("https://intranet.example.org/sites/it/Pages/start.aspx", false)]
    public void ASitePathIsComparedRegardlessOfCase(string url, bool expected)
    {
        var site = new Uri("https://intranet.example.org/Sites/HR/");
        Assert.That(IntranetSearchTool.IsWithinIntranet(site, new Uri(url)), Is.EqualTo(expected), "SharePoint does not tell the case of a site path apart.");
    }
}