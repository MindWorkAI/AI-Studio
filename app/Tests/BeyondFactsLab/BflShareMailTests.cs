using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the mailto link with which a person sends a shared session to the research team of the BeyondFacts Lab.
/// </summary>
[TestFixture]
public sealed class BflShareMailTests
{
    [Test]
    public void TheLinkNamesTheResearchTeamAndItsSubject() => Assert.That(BflShareMail.CreateUri(), Does.StartWith($"mailto:{BflShareMail.RECIPIENT}?subject="));

    [Test]
    public void TheSubjectAndTheTextAreEncoded()
    {
        var uri = BflShareMail.CreateUri("team@example.org", "Müller & Co? 1+1", "Grüße\nund Dank");

        Assert.That(uri, Is.EqualTo("mailto:team@example.org?subject=M%C3%BCller%20%26%20Co%3F%201%2B1&body=Gr%C3%BC%C3%9Fe%0D%0Aund%20Dank"));
    }

    [TestCase("Eins\nZwei")]
    [TestCase("Eins\r\nZwei")]
    [TestCase("Eins\rZwei")]
    public void EveryLineBreakIsCrLf(string body) => Assert.That(BflShareMail.CreateUri("team@example.org", "Betreff", body), Does.EndWith("&body=Eins%0D%0AZwei"));

    [Test]
    public void ALongTextLosesItsEndAtAWord()
    {
        var body = string.Join(' ', Enumerable.Repeat("Wörter", 600));
        var uri = BflShareMail.CreateUri("team@example.org", "Betreff", body);
        var text = Uri.UnescapeDataString(uri[(uri.IndexOf("&body=", StringComparison.Ordinal) + "&body=".Length)..]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(uri.Length, Is.LessThanOrEqualTo(BflShareMail.MAX_URI_LENGTH));
            Assert.That(body, Does.StartWith(text));
            Assert.That(text, Does.EndWith("Wörter"));
        }
    }

    [Test]
    public void TheTextOfTheLabFitsTheLimit() => Assert.That(BflShareMail.CreateUri(), Does.Contain("&body=").And.Length.LessThanOrEqualTo(BflShareMail.MAX_URI_LENGTH));

    [Test]
    public void TheTextOfTheLabHasCrLfOnly()
    {
        var uri = BflShareMail.CreateUri();
        Assert.That(uri.Replace("%0D%0A", string.Empty), Does.Not.Contain("%0A").And.Not.Contain("%0D"));
    }
}