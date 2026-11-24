using System.Text;

using AIStudio.Tools.Mail;

using MailKit.Security;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks which encodings a mailbox may use, and the bytes a password becomes in them when signing in.
/// </summary>
/// <remarks>
/// Depending on its configuration, a mail server compares the bytes with the password in another
/// encoding than UTF-8, e.g., Microsoft Exchange in the code page of its Windows system. A password sent
/// in the wrong encoding fails there as soon as it holds a character beyond ASCII. The expected bytes
/// come from the code page tables, not from .NET.
/// </remarks>
[TestFixture]
public sealed class MailboxPasswordEncodingsTests
{
    private const string USERNAME = "u";

    private static IEnumerable<TestCaseData> CharactersOfSeveralScripts()
    {
        // German, as on a German keyboard and in German typography:
        yield return Case("windows-1252", "ä", "E4", "C3 A4");
        yield return Case("windows-1252", "Ö", "D6", "C3 96");
        yield return Case("windows-1252", "ß", "DF", "C3 9F");
        yield return Case("windows-1252", "§", "A7", "C2 A7");
        yield return Case("windows-1252", "°", "B0", "C2 B0");
        yield return Case("windows-1252", "µ", "B5", "C2 B5");
        yield return Case("windows-1252", "€", "80", "E2 82 AC");
        yield return Case("windows-1252", "„", "84", "E2 80 9E");
        yield return Case("windows-1252", "“", "93", "E2 80 9C");
        yield return Case("windows-1252", "–", "96", "E2 80 93");

        // French, Spanish, and the Nordic languages:
        yield return Case("windows-1252", "é", "E9", "C3 A9");
        yield return Case("windows-1252", "ç", "E7", "C3 A7");
        yield return Case("windows-1252", "œ", "9C", "C5 93");
        yield return Case("windows-1252", "ñ", "F1", "C3 B1");
        yield return Case("windows-1252", "ø", "F8", "C3 B8");
        yield return Case("windows-1252", "å", "E5", "C3 A5");

        // Central Europe:
        yield return Case("windows-1250", "ł", "B3", "C5 82");
        yield return Case("windows-1250", "ś", "9C", "C5 9B");
        yield return Case("windows-1250", "ř", "F8", "C5 99");
        yield return Case("windows-1250", "ő", "F5", "C5 91");

        // Cyrillic, Greek, and Turkish:
        yield return Case("windows-1251", "ж", "E6", "D0 B6");
        yield return Case("windows-1251", "Ж", "C6", "D0 96");
        yield return Case("koi8-r", "ж", "D6", "D0 B6");
        yield return Case("windows-1253", "α", "E1", "CE B1");
        yield return Case("windows-1254", "ş", "FE", "C5 9F");
        yield return Case("windows-1254", "ğ", "F0", "C4 9F");
        yield return Case("windows-1254", "ı", "FD", "C4 B1");
        yield return Case("windows-1254", "İ", "DD", "C4 B0");

        // ISO-8859-15 has the euro sign where Latin-1 has the currency sign:
        yield return Case("iso-8859-15", "€", "A4", "E2 82 AC");

        // East Asia:
        yield return Case("shift_jis", "日", "93 FA", "E6 97 A5");
        yield return Case("shift_jis", "あ", "82 A0", "E3 81 82");
        yield return Case("gb2312", "中", "D6 D0", "E4 B8 AD");
        yield return Case("big5", "中", "A4 A4", "E4 B8 AD");
        yield return Case("ks_c_5601-1987", "한", "C7 D1", "ED 95 9C");
        yield break;

        static TestCaseData Case(string encodingName, string character, string inEncoding, string inUtf8) => new TestCaseData(encodingName, character, inEncoding, inUtf8).SetArgDisplayNames($"{character} in {encodingName}");
    }

    [TestCaseSource(nameof(CharactersOfSeveralScripts))]
    public void PlainSendsTheBytesOfTheChosenEncoding(string encodingName, string character, string inEncoding, string inUtf8)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GetPlainPasswordBytes(encodingName, character), Is.EqualTo(inEncoding));
            Assert.That(GetPlainPasswordBytes(MailboxPasswordEncodings.DEFAULT, character), Is.EqualTo(inUtf8));
        }
    }

    [Test]
    public void AsciiIsTheSameInEveryEncoding()
    {
        const string PASSWORD = "Sommer-2026!";
        var utf8 = GetPlainPasswordBytes(MailboxPasswordEncodings.DEFAULT, PASSWORD);
        Assert.That(MailboxPasswordEncodings.ALL.Select(name => GetPlainPasswordBytes(name, PASSWORD)), Is.All.EqualTo(utf8));
    }

    [Test]
    public void TheListStartsWithUtf8AndHoldsTheCodePagesOfWindows()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncodings.ALL[0], Is.EqualTo(MailboxPasswordEncodings.DEFAULT));
            Assert.That(MailboxPasswordEncodings.ALL, Is.Unique);
            Assert.That(MailboxPasswordEncodings.ALL, Is.SupersetOf(new[] { "windows-1250", "windows-1251", "windows-1252", "windows-1254", "shift_jis", "gb2312", "big5", "iso-8859-15" }));
            Assert.That(MailboxPasswordEncodings.ALL, Has.None.StartsWith("utf-16").And.None.StartsWith("utf-32").And.None.EqualTo("utf-7"));
        }
    }

    [TestCase("utf-8", ExpectedResult = "utf-8")]
    [TestCase(" Windows-1254 ", ExpectedResult = "windows-1254")]
    [TestCase("SHIFT_JIS", ExpectedResult = "shift_jis")]
    [TestCase("latin1", ExpectedResult = "iso-8859-1")]
    public string? AKnownEncodingIsFoundByItsName(string name)
    {
        return MailboxPasswordEncodings.TryGet(name, out var encoding) ? encoding.WebName : null;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("EBCDIC")]
    [TestCase("utf-16")]
    [TestCase("utf-32")]
    [TestCase("utf-7")]
    [TestCase("IBM037")]
    [TestCase("utf-8, windows-1252")]
    public void AnEncodingWhichChangesAsciiOrIsUnknownIsRefused(string? name)
    {
        Assert.That(MailboxPasswordEncodings.TryGet(name, out _), Is.False);
    }

    [TestCase("windows-1252", "ẞ")]
    [TestCase("windows-1252", "Straße 🙂")]
    [TestCase("windows-1252", "ş")]
    [TestCase("windows-1254", "€uro ẞ")]
    [TestCase("shift_jis", "한")]
    public void AnEncodingRefusesWhatItLacks(string encodingName, string text)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncodings.TryPrepare(GetKnownEncoding(encodingName), text, out _), Is.False);
            Assert.That(MailboxPasswordEncodings.TryPrepare(GetKnownEncoding(MailboxPasswordEncodings.DEFAULT), text, out var prepared), Is.True);
            Assert.That(prepared, Is.EqualTo(text));
        }
    }

    [Test]
    public void ALoneSurrogateIsRefusedByEveryEncoding()
    {
        const string TEXT = "abc\uD800";
        Assert.That(MailboxPasswordEncodings.ALL.Where(name => MailboxPasswordEncodings.TryPrepare(GetKnownEncoding(name), TEXT, out _)), Is.Empty);
    }

    [Test]
    public void ACodePageComposesADecomposedUmlaut()
    {
        // An a followed by a combining diaeresis, as some password managers deliver an ä:
        const string DECOMPOSED = "Ka\u0308se";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncodings.TryPrepare(GetKnownEncoding("windows-1252"), DECOMPOSED, out var prepared), Is.True);
            Assert.That(prepared, Is.EqualTo("K\u00E4se"));
            Assert.That(GetPlainPasswordBytes("windows-1252", DECOMPOSED), Is.EqualTo("4B E4 73 65"));
        }
    }

    [Test]
    public void Utf8TakesTheTextAsItIs()
    {
        // AI Studio always sent the text in UTF-8 as it was, so a server which knows a password in this form keeps accepting it:
        const string DECOMPOSED = "Ka\u0308se";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailboxPasswordEncodings.TryPrepare(GetKnownEncoding(MailboxPasswordEncodings.DEFAULT), DECOMPOSED, out var prepared), Is.True);
            Assert.That(prepared, Is.EqualTo(DECOMPOSED));
        }
    }

    /// <summary>
    /// The encoding of a name which a mailbox may use; fails the test for any other name.
    /// </summary>
    internal static Encoding GetKnownEncoding(string name)
    {
        Assert.That(MailboxPasswordEncodings.TryGet(name, out var encoding), Is.True, $"The encoding '{name}' should be known.");
        return encoding!;
    }

    /// <summary>
    /// The bytes of the password in the message which the SASL mechanism PLAIN sends, as hex.
    /// </summary>
    private static string GetPlainPasswordBytes(string encodingName, string password)
    {
        var encoding = GetKnownEncoding(encodingName);
        Assert.That(MailboxPasswordEncodings.TryPrepare(encoding, password, out var preparedPassword), Is.True);

        // PLAIN sends the authorization identity, the username, and the password, each after a zero byte:
        var mechanism = new SaslMechanismPlain(encoding, USERNAME, preparedPassword);
        var message = Convert.FromBase64String(mechanism.Challenge(null, CancellationToken.None));
        var passwordBytes = message.AsSpan(1 + USERNAME.Length + 1);

        return string.Join(' ', Convert.ToHexString(passwordBytes).Chunk(2).Select(pair => new string(pair)));
    }
}