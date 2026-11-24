using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks which API keys AI Studio accepts, and what it tells the user about the others.
/// </summary>
/// <remarks>
/// <para>
/// An HTTP header carries nothing but ASCII, so .NET refuses every other character in a key, and
/// only while sending. What a key picks up in the clipboard has to be found before, and named so
/// that the user knows what to do.
/// </para>
/// <para>
/// The invisible characters are built from their code points, so that nobody has to trust an
/// editor to keep characters it does not show. Nothing here asserts on the wording of a message:
/// I18N is process-wide state without a reset.
/// </para>
/// </remarks>
[TestFixture]
public sealed class APIKeyValidationTests
{
    private const string KEY = "sk-proj-AbC123";

    private static readonly string NO_BREAK_SPACE = ((char)0x00A0).ToString();
    private static readonly string LINE_SEPARATOR = ((char)0x2028).ToString();
    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();
    private static readonly string BYTE_ORDER_MARK = ((char)0xFEFF).ToString();
    private static readonly string COMBINING_DIAERESIS = ((char)0x0308).ToString();
    private static readonly string DECOMPOSED_A_UMLAUT = $"a{COMBINING_DIAERESIS}";
    private static readonly string DECOMPOSED_CAPITAL_U_UMLAUT = $"U{COMBINING_DIAERESIS}";

    [TestCase("sk-proj-AbC_123.xyz~")]
    [TestCase("AIzaSyD-9tSrke72PouQMnMX-a7eZSW0jkFMBWY")]
    [TestCase("dGhpcyBpcyBhIGtleQ+/==")]
    [TestCase("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~")]
    public void KeysOfPrintableAsciiPass(string key) => Assert.That(APIKeyValidation.Check(key), Is.EqualTo(APIKeyIssue.NONE));

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t\n")]
    public void AMissingKeyIsUpToTheCaller(string? key)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check(key).IsProblem, Is.False);
            Assert.That(APIKeyValidation.Validate(key), Is.Null);
            Assert.That(APIKeyValidation.TryPrepareForHeader(key, out var headerValue), Is.True);
            Assert.That(headerValue, Is.Empty);
        }
    }

    [Test]
    public void WhitespaceAroundTheKeyGoes()
    {
        string[] keys = [$" {KEY}\n", $"\t{KEY}\r\n", $"{NO_BREAK_SPACE}{KEY}{NO_BREAK_SPACE}", $"{KEY}{LINE_SEPARATOR}"];
        using (Assert.EnterMultipleScope())
        {
            foreach (var key in keys)
            {
                Assert.That(APIKeyValidation.Normalize(key), Is.EqualTo(KEY));
                Assert.That(APIKeyValidation.Check(key), Is.EqualTo(APIKeyIssue.NONE));
                Assert.That(APIKeyValidation.TryPrepareForHeader(key, out var headerValue), Is.True);
                Assert.That(headerValue, Is.EqualTo(KEY));
            }
        }
    }

    [TestCase(" ")]
    [TestCase("\t")]
    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\u00A0")]
    [TestCase("\u3000")]
    public void WhitespaceInsideTheKeyIsReported(string whitespace) => Assert.That(APIKeyValidation.Check($"sk-proj{whitespace}AbC123").Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE));

    [Test]
    public void AValueWhichAllowsSpacesKeepsThem()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check("my secret  phrase", allowsSpaces: true), Is.EqualTo(APIKeyIssue.NONE));
            Assert.That(APIKeyValidation.Check(" my secret phrase\n", allowsSpaces: true), Is.EqualTo(APIKeyIssue.NONE), "The whitespace around the value goes, as for a key.");
            Assert.That(APIKeyValidation.Check("my secret phrase", allowsSpaces: false).Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE));
            Assert.That(APIKeyValidation.Check("my secret phrase").Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE), "A key allows no spaces.");
        }
    }

    [TestCase("\t")]
    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\u00A0")]
    [TestCase("\u3000")]
    public void AValueWhichAllowsSpacesStillReportsOtherWhitespace(string whitespace) => Assert.That(APIKeyValidation.Check($"my{whitespace}secret", allowsSpaces: true).Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE));

    [TestCase(0x200B)]
    [TestCase(0x200D)]
    [TestCase(0x2060)]
    [TestCase(0x00AD)]
    [TestCase(0x202E)]
    [TestCase(0x0000)]
    [TestCase(0x0007)]
    [TestCase(0x007F)]
    [TestCase(0x034F)]
    [TestCase(0xFE0F)]
    [TestCase(0x3164)]
    [TestCase(0x2800)]
    [TestCase(0xE000)]
    public void AnInvisibleCharacterIsReported(int codePoint)
    {
        var invisible = ((char)codePoint).ToString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check($"sk-proj{invisible}AbC123"), Is.EqualTo(new APIKeyIssue(APIKeyIssueKind.INVISIBLE_CHARACTER, string.Empty)));
            Assert.That(APIKeyValidation.Check($"{KEY}{invisible}").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER), "Trimming must not remove a character which is no whitespace.");
        }
    }

    [Test]
    public void ACopiedByteOrderMarkIsReported()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check($"{BYTE_ORDER_MARK}{KEY}").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
            Assert.That(APIKeyValidation.Check($"{ZERO_WIDTH_SPACE}{KEY}").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
        }
    }

    [Test]
    public void ALoneSurrogateIsReportedAsInvisible()
    {
        var highSurrogate = ((char)0xD83D).ToString();
        var lowSurrogate = ((char)0xDD11).ToString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check($"sk-{highSurrogate}AbC").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
            Assert.That(APIKeyValidation.Check($"sk-AbC{highSurrogate}").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
            Assert.That(APIKeyValidation.Check($"sk-{lowSurrogate}AbC").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
        }
    }

    [TestCase("ä")]
    [TestCase("ö")]
    [TestCase("ü")]
    [TestCase("Ä")]
    [TestCase("Ö")]
    [TestCase("Ü")]
    [TestCase("ß")]
    [TestCase("ẞ")]
    [TestCase("§")]
    [TestCase("°")]
    [TestCase("´")]
    [TestCase("µ")]
    [TestCase("²")]
    [TestCase("³")]
    [TestCase("€")]
    [TestCase("„")]
    [TestCase("“")]
    [TestCase("‚")]
    [TestCase("‘")]
    [TestCase("–")]
    [TestCase("…")]
    [TestCase("é")]
    [TestCase("ø")]
    [TestCase("ş")]
    [TestCase("ł")]
    [TestCase("ж")]
    [TestCase("日")]
    [TestCase("한")]
    [TestCase("🔑")]
    public void AVisibleCharacterBeyondAsciiIsNamed(string character) => Assert.That(APIKeyValidation.Check($"sk-proj{character}AbC123"), Is.EqualTo(new APIKeyIssue(APIKeyIssueKind.NON_ASCII_CHARACTER, character)));

    [Test]
    public void ADecomposedUmlautIsOneVisibleCharacter()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check($"sk-{DECOMPOSED_A_UMLAUT}bc"), Is.EqualTo(new APIKeyIssue(APIKeyIssueKind.NON_ASCII_CHARACTER, "ä")));
            Assert.That(APIKeyValidation.Check($"sk-{DECOMPOSED_CAPITAL_U_UMLAUT}bc"), Is.EqualTo(new APIKeyIssue(APIKeyIssueKind.NON_ASCII_CHARACTER, "Ü")));
        }
    }

    [Test]
    public void TheFirstIssueWins()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.Check($"sk proj{ZERO_WIDTH_SPACE}“AbC").Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE));
            Assert.That(APIKeyValidation.Check($"sk{ZERO_WIDTH_SPACE}proj “AbC").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
            Assert.That(APIKeyValidation.Check($"sk“proj {ZERO_WIDTH_SPACE}AbC"), Is.EqualTo(new APIKeyIssue(APIKeyIssueKind.NON_ASCII_CHARACTER, "“")));
        }
    }

    [Test]
    public void AKeyWhichCannotBeSentIsNotPrepared()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(APIKeyValidation.TryPrepareForHeader($"sk-proj{ZERO_WIDTH_SPACE}AbC123", out var headerValue), Is.False);
            Assert.That(headerValue, Is.Empty);
        }
    }

    [Test]
    public void EveryIssueHasAMessage()
    {
        var issues = Enum.GetValues<APIKeyIssueKind>().Where(kind => kind is not APIKeyIssueKind.NONE).Select(kind => new APIKeyIssue(kind, "„"));
        using (Assert.EnterMultipleScope())
        {
            foreach (var issue in issues)
                Assert.That(APIKeyValidation.GetMessage(issue), Is.Not.Empty, $"The issue {issue.Kind} has no message.");

            Assert.That(APIKeyValidation.GetMessage(APIKeyIssue.NONE), Is.Empty);
            Assert.That(APIKeyValidation.GetUnusableKeyMessage("Tavily"), Does.Contain("Tavily"));
        }
    }

    [Test]
    public void AMessageNamesTheCharacterButNeverTheKey()
    {
        const string SECRET = "sk-secret-AbC123";
        string[] keys = [$"{SECRET} {SECRET}", $"{SECRET}{ZERO_WIDTH_SPACE}", $"{SECRET}“"];
        using (Assert.EnterMultipleScope())
        {
            foreach (var key in keys)
            {
                var message = APIKeyValidation.Validate(key);
                Assert.That(message, Is.Not.Null.And.Not.Empty);
                Assert.That(message, Does.Not.Contain("secret"));
            }

            Assert.That(APIKeyValidation.Validate($"{SECRET}“"), Does.Contain("“"));
        }
    }
}