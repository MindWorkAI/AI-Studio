using System.Net;
using System.Net.Sockets;
using System.Text;

using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks where a chat may still send data once Read Web Page brought an internal page into it.
/// </summary>
/// <remarks>
/// A page in the intranet or behind a VPN is internal to the organization, and so is every page
/// of its wiki, whatever its address. A web search or an address the model writes itself would
/// carry it to a third party, so such a page restricts the chat as a mailbox does. Both kinds
/// count alike, and only the page as it was read in the end decides. A public page demands nothing.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ReadWebPageInternalPageOutboundDataRestrictionTests : ToolRegistryTestBase
{
    private const string INTRANET_HOST = "intranet.example.org";
    private const string INTRANET_PAGE = "https://intranet.example.org/handbook/travel";
    private const string PUBLIC_PAGE = "https://example.org/newsletter/2026-10";
    private const string WIKI_PAGE = "https://wiki.example.org/confluence/display/TEAM/Budget";

    private static readonly Uri WIKI = new("https://wiki.example.org/confluence/");

    [Test]
    public void APublicPageDemandsNothing()
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(PUBLIC_PAGE), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, WIKI);
        Assert.That(requirement, Is.EqualTo(OutboundDataRequirement.NONE));
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void AnIntranetPageRestrictsTheChatAndNamesItsHost(OutboundDataRestriction level)
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(INTRANET_PAGE), reachedNonPublicAddress: true, level, WIKI);
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(level, OutboundDataOrigin.INTERNAL_WEB_PAGE, INTRANET_HOST)));
    }

    [Test]
    public void APublicAddressWhichRedirectsIntoTheIntranetRestrictsTheChat()
    {
        //
        // The model asked for a public address, and the server sent it on to the intranet. What
        // the model reads came from there, which the retrieval reports for the page as read:
        //
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(INTRANET_PAGE), reachedNonPublicAddress: true, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, wiki: null);
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.INTERNAL_WEB_PAGE, INTRANET_HOST)));
    }

    [Test]
    public void AnIntranetAddressWhichRedirectsToAPublicPageDemandsNothing()
    {
        // A sign-in page outside the intranet, say. Its content is public:
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(PUBLIC_PAGE), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, wiki: null);
        Assert.That(requirement, Is.EqualTo(OutboundDataRequirement.NONE), "Only the page as it was read in the end decides.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AWikiPageCountsAsAnInternalPageWhateverItsAddress(bool reachedNonPublicAddress)
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(WIKI_PAGE), reachedNonPublicAddress, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, WIKI);
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.INTERNAL_WEB_PAGE, WIKI.Host)), "The wiki holds internal content whatever its address, and it is restricted like any other internal page.");
    }

    [Test]
    public void APageBesideTheWikiOnItsPublicHostDemandsNothing()
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri("https://wiki.example.org/blog/"), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, WIKI);
        Assert.That(requirement, Is.EqualTo(OutboundDataRequirement.NONE), "Only the path below the base URL is the wiki.");
    }

    [TestCase(null, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase("", OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(nameof(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase(nameof(OutboundDataRestriction.UNRESTRICTED), OutboundDataRestriction.UNRESTRICTED)]
    [TestCase("ONLY_LINKS", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase("2", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase("UNRESTRICTED, ONLY_LINKS_FROM_CHAT", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void TheLevelOfInternalPagesIsReadFromItsSetting(string? configuredValue, OutboundDataRestriction expected)
    {
        Assert.That(ReadWebPageTool.ReadInternalPageOutboundDataRestriction(configuredValue), Is.EqualTo(expected), "A value which names no level counts as the strictest, because a level guessed too loosely would let internal content out.");
    }

    [Test]
    public async Task AValueWhichNamesNoLevelIsReported()
    {
        var tool = new ReadWebPageTool(null!, null!, null!, NullLogger<ReadWebPageTool>.Instance);
        var state = await tool.ValidateConfigurationAsync(tool.GetDefinition(), new Dictionary<string, string> { ["internalPageOutboundDataRestriction"] = "ONLY_LINKS" });

        Assert.That(state?.IsConfigured, Is.False, "Guessing would decide on the organization's behalf where internal content may go.");
    }

    [Test]
    public async Task TheRetrievalReportsANonPublicAddressOfThePageAsRead()
    {
        //
        // The loopback address stands in for the intranet: it is the one non-public address a test
        // can serve from. A redirect between a public address and it needs a public server, which
        // a test cannot rely on; the service judges the last request the same way, whatever came
        // before, and this one follows a redirect on the way:
        //
        using var server = new LoopbackServer();
        var service = new WebPageRetrievalService(new HTMLParser());

        var retrievedPage = await service.RetrieveAsync(new Uri($"http://127.0.0.1:{server.Port}/redirect"), new WebPageRetrievalOptions
        {
            TimeoutSeconds = 10,
            TargetChosenByUser = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(retrievedPage.Page.FinalUrl.AbsolutePath, Is.EqualTo("/page"), "The redirect was followed.");
            Assert.That(retrievedPage.ReachedNonPublicAddress, Is.True);
        });
    }

    /// <summary>
    /// A minimal HTTP server on the loopback address: /redirect sends the client on to /page, and
    /// /page answers with a small HTML page.
    /// </summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();

        public LoopbackServer()
        {
            this.listener.Start();
            _ = this.ServeAsync();
        }

        public int Port => ((IPEndPoint)this.listener.LocalEndpoint).Port;

        private async Task ServeAsync()
        {
            try
            {
                while (!this.stop.IsCancellationRequested)
                {
                    using var client = await this.listener.AcceptTcpClientAsync(this.stop.Token);
                    await using var stream = client.GetStream();
                    var request = await ReadRequestHeadAsync(stream, this.stop.Token);
                    var response = request.StartsWith("GET /redirect ", StringComparison.Ordinal)
                        ? $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{this.Port}/page\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : Page("<html><head><title>Travel</title></head><body><main><p>The travel policy of the intranet.</p></main></body></html>");

                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response), this.stop.Token);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                // The test is over.
            }
        }

        private static string Page(string html) => $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(html)}\r\nConnection: close\r\n\r\n{html}";

        private static async Task<string> ReadRequestHeadAsync(NetworkStream stream, CancellationToken token)
        {
            var head = new StringBuilder();
            var buffer = new byte[1024];
            while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer, token);
                if (read == 0)
                    break;

                head.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            return head.ToString();
        }

        public void Dispose()
        {
            this.stop.Cancel();
            this.listener.Stop();
            this.stop.Dispose();
        }
    }
}