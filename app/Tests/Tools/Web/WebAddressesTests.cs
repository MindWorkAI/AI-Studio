using AIStudio.Tools.Web;

namespace AIStudio.Tests.Tools.Web;

/// <summary>
/// Checks how web addresses are found in a chat and when two of them count as the same.
/// </summary>
/// <remarks>
/// A chat restricted by a mailbox may read a web page only when its address stands in the chat.
/// Two mistakes are possible, and they cost differently. An address found too short or too long
/// keeps the user from a page they gave the AI, which is annoying. Two addresses counted as the
/// same although they ask the server for different things would let the model put mail content
/// into a query, which is what the restriction is there to prevent.
/// </remarks>
[TestFixture]
public sealed class WebAddressesTests
{
    [TestCase("See https://example.org/report.", "https://example.org/report")]
    [TestCase("Is it https://example.org/report?", "https://example.org/report")]
    [TestCase("[The report](https://example.org/report)", "https://example.org/report")]
    [TestCase("<https://example.org/report>", "https://example.org/report")]
    [TestCase("\"https://example.org/report\"", "https://example.org/report")]
    [TestCase("'https://example.org/report'", "https://example.org/report")]
    [TestCase("(see https://example.org/report)", "https://example.org/report")]
    [TestCase("https://en.wikipedia.org/wiki/Mercury_(planet)", "https://en.wikipedia.org/wiki/Mercury_(planet)")]
    [TestCase("[Mercury](https://en.wikipedia.org/wiki/Mercury_(planet))", "https://en.wikipedia.org/wiki/Mercury_(planet)")]
    [TestCase("HTTPS://EXAMPLE.ORG/Report, and more", "HTTPS://EXAMPLE.ORG/Report")]
    public void AnAddressIsFoundAsItStands(string text, string expected) =>
        Assert.That(WebAddresses.Find(text), Is.EqualTo(new[] { expected }));

    [Test]
    public void AnAddressWithHtmlEntitiesIsAlsoFoundDecoded() =>
        Assert.That(WebAddresses.Find("https://example.org/search?q=budget&amp;year=2026"), Is.EqualTo(new[] { "https://example.org/search?q=budget&amp;year=2026", "https://example.org/search?q=budget&year=2026" }));

    [Test]
    public void TextWithoutAddressesHasNone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WebAddresses.Find("No address here, not even example.org."), Is.Empty, "Without a scheme, nobody can tell an address from a word.");
            Assert.That(WebAddresses.Find("ftp://example.org/report"), Is.Empty);
            Assert.That(WebAddresses.Find(null), Is.Empty);
        });
    }

    [TestCase("https://example.org/report", "https://EXAMPLE.org/report")]
    [TestCase("https://example.org/report", "HTTPS://example.org/report")]
    [TestCase("https://example.org/report", "https://example.org/report#summary")]
    [TestCase("https://example.org/report", "https://example.org:443/report")]
    [TestCase("https://example.org/report", "https://example.org./report")]
    [TestCase("https://bücher.example/", "https://xn--bcher-kva.example/")]
    [TestCase("https://example.org/a/../report", "https://example.org/report")]
    public void AddressesAskingForTheSameCountAsTheSame(string inChat, string fromModel)
    {
        Assert.Multiple(() =>
        {
            Assert.That(WebAddresses.TryCreateRequestKey(inChat, out var expected), Is.True);
            Assert.That(WebAddresses.TryCreateRequestKey(fromModel, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected), "The server gets the same request, so nothing more of the chat leaves AI Studio.");
        });
    }

    [TestCase("https://example.org/report?year=2026", "https://example.org/report?year=2026&note=budget")]
    [TestCase("https://example.org/report?year=2026", "https://example.org/report?year=2025")]
    [TestCase("https://example.org/report", "https://example.org/report?budget")]
    [TestCase("https://example.org/report", "https://example.org/Report")]
    [TestCase("https://example.org/report", "https://example.org/report/budget")]
    [TestCase("https://example.org/report", "https://budget@example.org/report")]
    [TestCase("https://example.org/report", "http://example.org/report")]
    [TestCase("https://example.org/report", "https://example.org:8443/report")]
    [TestCase("https://example.org/report", "https://budget.example.org/report")]
    public void AddressesAskingForSomethingElseDoNotCount(string inChat, string fromModel)
    {
        Assert.Multiple(() =>
        {
            Assert.That(WebAddresses.TryCreateRequestKey(inChat, out var expected), Is.True);
            Assert.That(WebAddresses.TryCreateRequestKey(fromModel, out var actual), Is.True);
            Assert.That(actual, Is.Not.EqualTo(expected), "The difference reaches the server, and it may be a piece of a mail.");
        });
    }

    [TestCase("mailto:someone@example.org")]
    [TestCase("file:///etc/passwd")]
    [TestCase("not an address")]
    [TestCase(null)]
    public void OnlyWebAddressesHaveARequestKey(string? address) =>
        Assert.That(WebAddresses.TryCreateRequestKey(address, out _), Is.False);
}