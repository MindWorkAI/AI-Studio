using AIStudio.Tools.OpenXml;

namespace AIStudio.Tests.Tools.OpenXml;

/// <summary>
/// Checks that a text from a model can always go into an Office document.
/// </summary>
[TestFixture]
public sealed class OpenXmlTextTests
{
    [Test]
    public void ATextXmlCanCarryStaysAsItIs()
    {
        const string TEXT = "Mars, 2026 – erreichbar? Ja, mit Geduld.";

        Assert.That(OpenXmlText.XmlSafe(TEXT), Is.SameAs(TEXT));
    }

    [Test]
    public void AControlCharacterIsLeftOut()
    {
        Assert.That(OpenXmlText.XmlSafe("Mars\u0001 und\u0008 Venus"), Is.EqualTo("Mars und Venus"), "Control characters a model produces now and then would keep the file from being saved.");
    }

    [Test]
    public void AnEmojiStaysWhereasHalfOfItGoes()
    {
        //
        // The halves are put together in code: an attribute stores its strings as UTF-8, which
        // cannot hold half of an emoji, so a test case would not test what it says.
        //
        const char HIGH = '\ud83d';
        const char LOW = '\ude80';

        Assert.Multiple(() =>
        {
            Assert.That(OpenXmlText.XmlSafe($"Rakete {HIGH}{LOW}"), Is.EqualTo($"Rakete {HIGH}{LOW}"), "An emoji takes two chars, and the pair stays.");
            Assert.That(OpenXmlText.XmlSafe($"Rakete {HIGH}!"), Is.EqualTo("Rakete !"), "Half of a pair alone cannot go into XML.");
            Assert.That(OpenXmlText.XmlSafe($"{LOW}Rakete"), Is.EqualTo("Rakete"), "Neither can the other half.");
        });
    }
}