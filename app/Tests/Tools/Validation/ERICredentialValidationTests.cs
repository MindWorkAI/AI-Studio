using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks which usernames, passwords, and access tokens of ERI data sources AI Studio accepts, and
/// what it tells the user about the others.
/// </summary>
/// <remarks>
/// <para>
/// ERI v1 sends all three in HTTP headers, which carry nothing but ASCII. An access token is checked
/// like an API key; a password and a username may contain spaces, since people choose them.
/// </para>
/// <para>
/// The invisible characters are built from their code points, so that nobody has to trust an
/// editor to keep characters it does not show. Nothing here asserts on the wording of a message:
/// I18N is process-wide state without a reset. Whether a message names a character shows instead
/// in whether two values with different characters get different messages.
/// </para>
/// </remarks>
[TestFixture]
public sealed class ERICredentialValidationTests
{
    private const string DATA_SOURCE_NAME = "Project archive";

    // A part of each secret which no message could contain by chance, whatever its language:
    private const string SECRET_PART = "Xq7";

    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();
    private static readonly string COMBINING_DIAERESIS = ((char)0x0308).ToString();

    private static IEnumerable<string> CharactersBeyondAscii() =>
    [
        "ä", "ö", "ü", "Ä", "Ö", "Ü", "ß", "ẞ", "§", "°", "´", "µ", "²", "³", "€", "„", "“", "‚", "‘", "–", "…",
        "é", "ø", "ş", "ł", "ж", "日本", "한",
        $"a{COMBINING_DIAERESIS}", $"U{COMBINING_DIAERESIS}",
    ];

    [TestCase("eyJhbGciOiJIUzI1NiJ9.e30.Xq7-_~+/=")]
    [TestCase("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~")]
    public void ATokenOfPrintableAsciiPasses(string token) => Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.TOKEN, token), Is.EqualTo(APIKeyIssue.NONE));

    [Test]
    public void ATokenIsCheckedLikeAnAPIKey()
    {
        string[] tokens = [$"{SECRET_PART} token", $"{SECRET_PART}{ZERO_WIDTH_SPACE}", $"{SECRET_PART}„", $"{SECRET_PART}a{COMBINING_DIAERESIS}", $" {SECRET_PART}\n"];
        using (Assert.EnterMultipleScope())
        {
            foreach (var token in tokens)
                Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.TOKEN, token), Is.EqualTo(APIKeyValidation.Check(token)));

            Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.TOKEN, $"{SECRET_PART} token").Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE), "A token allows no spaces.");
        }
    }

    [Test]
    public void APasswordMayContainSpaces()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.USERNAME_PASSWORD, "Mein geheimes Passwort 2024!"), Is.EqualTo(APIKeyIssue.NONE));
            Assert.That(ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, "Mein geheimes Passwort 2024!"), Is.Null);
        }
    }

    [Test]
    public void TheWhitespaceAroundAPasswordGoes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.USERNAME_PASSWORD, " Passwort\n"), Is.EqualTo(APIKeyIssue.NONE));
            Assert.That(ERICredentialValidation.Normalize(" Passwort\n"), Is.EqualTo("Passwort"));
        }
    }

    [TestCaseSource(nameof(CharactersBeyondAscii))]
    public void ACharacterBeyondAsciiInAPasswordIsReported(string character) => Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.USERNAME_PASSWORD, $"Pass{character}wort 2024").Kind, Is.EqualTo(APIKeyIssueKind.NON_ASCII_CHARACTER));

    [TestCase(0x0009)]
    [TestCase(0x000A)]
    [TestCase(0x000D)]
    [TestCase(0x00A0)]
    [TestCase(0x3000)]
    public void OtherWhitespaceInAPasswordIsReported(int codePoint) => Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.USERNAME_PASSWORD, $"Pass{(char)codePoint}wort").Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE));

    [TestCase(0x200B)]
    [TestCase(0xFEFF)]
    [TestCase(0x00AD)]
    public void AnInvisibleCharacterInAPasswordIsReported(int codePoint) => Assert.That(ERICredentialValidation.CheckSecret(AuthMethod.USERNAME_PASSWORD, $"Pass{(char)codePoint}wort").Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));

    [Test]
    public void AMessageAboutAPasswordNamesNeitherTheCharacterNorThePassword()
    {
        var umlaut = ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, $"Grüße-{SECRET_PART}");
        var decomposedUmlaut = ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, $"Gru{COMBINING_DIAERESIS}ße-{SECRET_PART}");
        var euro = ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, $"Preis€-{SECRET_PART}");
        var tab = ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, $"Pass\t{SECRET_PART}");
        var invisible = ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, $"Pass{ZERO_WIDTH_SPACE}{SECRET_PART}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(umlaut, Is.Not.Null.And.Not.Empty);
            Assert.That(decomposedUmlaut, Is.EqualTo(umlaut), "The message must not depend on the character.");
            Assert.That(euro, Is.EqualTo(umlaut), "The message must not depend on the character.");
            Assert.That(tab, Is.Not.Null.And.Not.Empty);
            Assert.That(invisible, Is.EqualTo(tab), "Whitespace and invisible characters share one message.");
            foreach (var message in new[] { umlaut, tab })
                Assert.That(message, Does.Not.Contain(SECRET_PART));
        }
    }

    [Test]
    public void AMessageAboutATokenNamesTheCharacterButNeverTheToken()
    {
        var lowQuotationMark = ERICredentialValidation.ValidateSecret(AuthMethod.TOKEN, $"{SECRET_PART}„token");
        var highQuotationMark = ERICredentialValidation.ValidateSecret(AuthMethod.TOKEN, $"{SECRET_PART}“token");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lowQuotationMark, Does.Contain("„"));
            Assert.That(highQuotationMark, Is.Not.EqualTo(lowQuotationMark));
            foreach (var token in new[] { $"{SECRET_PART} token", $"{SECRET_PART}{ZERO_WIDTH_SPACE}token", $"{SECRET_PART}„token" })
            {
                var message = ERICredentialValidation.ValidateSecret(AuthMethod.TOKEN, token);
                Assert.That(message, Is.Not.Null.And.Not.Empty);
                Assert.That(message, Does.Not.Contain(SECRET_PART));
            }
        }
    }

    [Test]
    public void AUsernameMayContainSpaces()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERICredentialValidation.CheckUsername("Max Mustermann"), Is.EqualTo(APIKeyIssue.NONE));
            Assert.That(ERICredentialValidation.ValidateUsername("Max Mustermann"), Is.Null);
            Assert.That(ERICredentialValidation.CheckUsername("Max\tMustermann").Kind, Is.EqualTo(APIKeyIssueKind.WHITESPACE));
        }
    }

    [Test]
    public void AMessageAboutAUsernameNamesTheCharacter()
    {
        var composed = ERICredentialValidation.ValidateUsername("jürgen");
        var decomposed = ERICredentialValidation.ValidateUsername($"ju{COMBINING_DIAERESIS}rgen");
        var otherUmlaut = ERICredentialValidation.ValidateUsername("jörgen");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERICredentialValidation.CheckUsername($"ju{COMBINING_DIAERESIS}rgen"), Is.EqualTo(new APIKeyIssue(APIKeyIssueKind.NON_ASCII_CHARACTER, "ü")));
            Assert.That(composed, Is.Not.Null.And.Not.Empty);
            Assert.That(decomposed, Is.EqualTo(composed), "A decomposed umlaut is named as the one character the user sees.");
            Assert.That(otherUmlaut, Is.Not.EqualTo(composed));
        }
    }

    [Test]
    public void EveryIssueHasAMessage()
    {
        string[] values = [$"a b{ZERO_WIDTH_SPACE}", $"a{ZERO_WIDTH_SPACE}", "a„", "a\tb"];
        using (Assert.EnterMultipleScope())
        {
            foreach (var value in values)
            {
                Assert.That(ERICredentialValidation.ValidateSecret(AuthMethod.TOKEN, value), Is.Not.Null.And.Not.Empty, $"The token '{value}' has no message.");
                Assert.That(ERICredentialValidation.ValidateUsername(value), Is.Not.Null.And.Not.Empty, $"The username '{value}' has no message.");
                Assert.That(ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, value), Is.Not.Null.And.Not.Empty, $"The password '{value}' has no message.");
            }
        }
    }

    [Test]
    public void AStoredValueIsNamedByItsDataSourceOnly()
    {
        var token = ERICredentialValidation.GetUnusableSecretMessage(AuthMethod.TOKEN, DATA_SOURCE_NAME);
        var password = ERICredentialValidation.GetUnusableSecretMessage(AuthMethod.USERNAME_PASSWORD, DATA_SOURCE_NAME);
        var username = ERICredentialValidation.GetUnusableUsernameMessage(DATA_SOURCE_NAME);
        using (Assert.EnterMultipleScope())
        {
            foreach (var message in new[] { token, password, username })
                Assert.That(message, Does.Contain(DATA_SOURCE_NAME));

            Assert.That(new[] { token, password, username }, Is.Unique, "Each value needs its own message.");
        }
    }

    [TestCase(AuthMethod.NONE)]
    [TestCase(AuthMethod.KERBEROS)]
    public void WithoutASecretNothingIsChecked(AuthMethod authMethod)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERICredentialValidation.CheckSecret(authMethod, $"a{ZERO_WIDTH_SPACE}„"), Is.EqualTo(APIKeyIssue.NONE));
            Assert.That(ERICredentialValidation.ValidateSecret(authMethod, $"a{ZERO_WIDTH_SPACE}„"), Is.Null);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \n")]
    public void AMissingValueIsUpToTheCaller(string? value)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERICredentialValidation.ValidateSecret(AuthMethod.TOKEN, value), Is.Null);
            Assert.That(ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, value), Is.Null);
            Assert.That(ERICredentialValidation.ValidateUsername(value), Is.Null);
        }
    }
}