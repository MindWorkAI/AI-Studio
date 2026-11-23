using AIStudio.Settings.DataModel;

using MailKit.Security;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks the bytes a password becomes when signing in, for the special characters of German.
/// </summary>
/// <remarks>
/// Microsoft Exchange compares the bytes with the password of the Windows account in Windows-1252,
/// so a password sent in UTF-8 fails there as soon as it holds one of these characters.
/// </remarks>
[TestFixture]
public sealed class MailboxPasswordEncodingTests
{
    private const string USERNAME = "u";

    private static IEnumerable<TestCaseData> GermanCharacters()
    {
        yield return Case("ä", "C3 A4", "E4");
        yield return Case("ö", "C3 B6", "F6");
        yield return Case("ü", "C3 BC", "FC");
        yield return Case("Ä", "C3 84", "C4");
        yield return Case("Ö", "C3 96", "D6");
        yield return Case("Ü", "C3 9C", "DC");
        yield return Case("ß", "C3 9F", "DF");
        yield return Case("§", "C2 A7", "A7");
        yield return Case("°", "C2 B0", "B0");
        yield return Case("´", "C2 B4", "B4");
        yield return Case("µ", "C2 B5", "B5");
        yield return Case("²", "C2 B2", "B2");
        yield return Case("³", "C2 B3", "B3");
        yield return Case("€", "E2 82 AC", "80");
        yield return Case("„", "E2 80 9E", "84");
        yield return Case("“", "E2 80 9C", "93");
        yield return Case("‚", "E2 80 9A", "82");
        yield return Case("‘", "E2 80 98", "91");
        yield return Case("–", "E2 80 93", "96");
        yield return Case("…", "E2 80 A6", "85");
        yield break;

        static TestCaseData Case(string character, string utf8, string windows1252) => new TestCaseData(character, utf8, windows1252).SetArgDisplayNames(character);
    }

    [TestCaseSource(nameof(GermanCharacters))]
    public void PlainSendsTheBytesOfTheChosenEncoding(string character, string utf8, string windows1252)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GetPlainPasswordBytes(MailboxPasswordEncoding.UTF8, character), Is.EqualTo(utf8));
            Assert.That(GetPlainPasswordBytes(MailboxPasswordEncoding.WINDOWS_1252, character), Is.EqualTo(windows1252));
        }
    }

    [Test]
    public void AsciiIsTheSameInEveryEncoding()
    {
        const string PASSWORD = "Sommer-2026!";
        Assert.That(GetPlainPasswordBytes(MailboxPasswordEncoding.WINDOWS_1252, PASSWORD), Is.EqualTo(GetPlainPasswordBytes(MailboxPasswordEncoding.UTF8, PASSWORD)));
    }

    [TestCase("ẞ")]
    [TestCase("Straße 🙂")]
    public void Windows1252RefusesWhatItLacks(string text)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncoding.WINDOWS_1252.TryPrepare(text, out _), Is.False);
            Assert.That(MailboxPasswordEncoding.UTF8.TryPrepare(text, out var prepared), Is.True);
            Assert.That(prepared, Is.EqualTo(text));
        }
    }

    [Test]
    public void ALoneSurrogateIsRefusedByEveryEncoding()
    {
        const string TEXT = "abc\uD800";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncoding.UTF8.TryPrepare(TEXT, out _), Is.False);
            Assert.That(MailboxPasswordEncoding.WINDOWS_1252.TryPrepare(TEXT, out _), Is.False);
        }
    }

    [Test]
    public void Windows1252ComposesADecomposedUmlaut()
    {
        // An a followed by a combining diaeresis, as some password managers deliver an ä:
        const string DECOMPOSED = "Käse";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncoding.WINDOWS_1252.TryPrepare(DECOMPOSED, out var prepared), Is.True);
            Assert.That(prepared, Is.EqualTo("Käse"));
            Assert.That(GetPlainPasswordBytes(MailboxPasswordEncoding.WINDOWS_1252, DECOMPOSED), Is.EqualTo("4B E4 73 65"));
        }
    }

    [Test]
    public void Utf8TakesTheTextAsItIs()
    {
        const string DECOMPOSED = "Käse";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncoding.UTF8.TryPrepare(DECOMPOSED, out var prepared), Is.True);
            Assert.That(prepared, Is.EqualTo(DECOMPOSED));
        }
    }

    [Test]
    public void AnUnknownEncodingEncodesNothing()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncoding.UNKNOWN.TextEncoding, Is.Null);
            Assert.That(MailboxPasswordEncoding.UNKNOWN.TryPrepare("abc", out _), Is.False);
        }
    }

    /// <summary>
    /// The bytes of the password in the message which the SASL mechanism PLAIN sends, as hex.
    /// </summary>
    private static string GetPlainPasswordBytes(MailboxPasswordEncoding passwordEncoding, string password)
    {
        Assert.That(passwordEncoding.TryPrepare(password, out var preparedPassword), Is.True);

        // PLAIN sends the authorization identity, the username, and the password, each after a zero byte:
        var mechanism = new SaslMechanismPlain(passwordEncoding.TextEncoding!, USERNAME, preparedPassword);
        var message = Convert.FromBase64String(mechanism.Challenge(null, CancellationToken.None));
        var passwordBytes = message.AsSpan(1 + USERNAME.Length + 1);

        return string.Join(' ', Convert.ToHexString(passwordBytes).Chunk(2).Select(pair => new string(pair)));
    }
}