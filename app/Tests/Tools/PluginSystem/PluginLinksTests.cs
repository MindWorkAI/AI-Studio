using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks which texts in the metadata of a plugin count as links.
/// </summary>
/// <remarks>
/// AI Studio offers such a link as a button, and a click opens it right away. A plugin may therefore
/// only link to a website or to a mail address. Any other scheme would let a plugin open something on
/// the computer of the user, and plain text, which a support contact may be, gets no button at all. The
/// links which the Assistant Builder fills in tell nothing about a plugin and count as not stated.
/// </remarks>
[TestFixture]
public sealed class PluginLinksTests
{
    [TestCase("https://example.org")]
    [TestCase("http://example.org/plugins")]
    [TestCase("mailto:support@example.org")]
    [TestCase("mailto:support@example.org?subject=Plugin")]
    [TestCase("  https://example.org  ")]
    public void AWebsiteOrAMailAddressIsALink(string text)
    {
        Assert.That(PluginLinks.TryParse(text, out _), Is.True);
    }

    [TestCase("")]
    [TestCase("MindWork AI")]
    [TestCase("example.org")]
    [TestCase("file:///etc/passwd")]
    [TestCase("javascript:alert(1)")]
    [TestCase("ftp://example.org")]
    [TestCase("mailto:")]
    [TestCase("mailto:?subject=Plugin")]
    public void AnythingElseIsNoLink(string text)
    {
        Assert.That(PluginLinks.TryParse(text, out _), Is.False);
    }

    [Test]
    public void OnlyAMailtoLinkWritesAMail()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PluginLinks.TryParse("mailto:support@example.org", out var mail) && PluginLinks.IsMail(mail), Is.True);
            Assert.That(PluginLinks.TryParse("https://example.org", out var website) && PluginLinks.IsMail(website), Is.False);
        }
    }

    [TestCase(AssistantPluginGenerationService.DEFAULT_SUPPORT_CONTACT, true)]
    [TestCase(AssistantPluginGenerationService.DEFAULT_SOURCE_URL, true)]
    [TestCase($"  {AssistantPluginGenerationService.DEFAULT_SOURCE_URL}  ", true)]
    [TestCase("mailto:support@example.org", false)]
    [TestCase("https://github.com/MindWorkAI/AI-Studio/wiki", false)]
    public void OnlyTheLinksOfTheAssistantBuilderArePlaceholders(string text, bool isPlaceholder)
    {
        Assert.That(PluginLinks.IsPlaceholder(text), Is.EqualTo(isPlaceholder));
    }

    [TestCase("mailto:support@example.org", "support@example.org")]
    [TestCase("mailto:support@example.org?subject=Plugin", "support@example.org")]
    [TestCase("mailto:support%2Bplugins@example.org", "support+plugins@example.org")]
    [TestCase("https://example.org/plugins?id=1", "https://example.org/plugins?id=1")]
    public void AMailLinkShowsItsAddressAndAWebsiteShowsItself(string text, string displayText)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PluginLinks.TryParse(text, out var link), Is.True);
            Assert.That(PluginLinks.GetDisplayText(link!), Is.EqualTo(displayText));
        }
    }
}