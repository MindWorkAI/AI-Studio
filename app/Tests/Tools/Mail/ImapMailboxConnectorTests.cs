using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks the parts of the IMAP connection which decide something without a server.
/// </summary>
/// <remarks>
/// Which failure an exception stands for matters more than it seems: only a rejected sign-in may
/// pause the mailbox, and a sign-in counted as a network failure would be tried again every
/// synchronization, until the directory locks the account.
/// </remarks>
[TestFixture]
public sealed class ImapMailboxConnectorTests
{
    private const string PASSWORD = "correct horse battery staple";

    private static readonly DataSourceMailbox MAILBOX = new()
    {
        Host = "imap.example.org",
        Port = 993,
        TransportSecurity = MailboxTransportSecurity.SSL_ON_CONNECT,
        AuthMethod = MailboxAuthMethod.PASSWORD,
        Username = "someone@example.org",
    };

    private static IEnumerable<TestCaseData> ExceptionsWithTheirFailure()
    {
        yield return Case(new AuthenticationException("The server rejected the credentials."), MailboxConnectionFailure.AUTHENTICATION_FAILED);
        yield return Case(new SslHandshakeException("The certificate is not trusted."), MailboxConnectionFailure.TLS_FAILED);
        yield return Case(new SocketException((int)SocketError.HostNotFound), MailboxConnectionFailure.NETWORK_UNAVAILABLE);
        yield return Case(new IOException("The connection was reset.", new SocketException((int)SocketError.ConnectionReset)), MailboxConnectionFailure.NETWORK_UNAVAILABLE);
        yield return Case(new TimeoutException(), MailboxConnectionFailure.NETWORK_UNAVAILABLE);
        yield return Case(new OperationCanceledException(), MailboxConnectionFailure.NETWORK_UNAVAILABLE);
        yield return Case(new ImapProtocolException("The server sent garbage."), MailboxConnectionFailure.SERVER_ERROR);
        yield return Case(new FolderNotFoundException("Archive"), MailboxConnectionFailure.SERVER_ERROR);
        yield break;

        static TestCaseData Case(Exception exception, MailboxConnectionFailure failure) => new TestCaseData(exception, failure).SetArgDisplayNames(exception.GetType().Name, failure.ToString());
    }

    private static IEnumerable<TestCaseData> InvalidSettings()
    {
        yield return Case("unknown transport security", MAILBOX with { TransportSecurity = MailboxTransportSecurity.UNKNOWN }, PASSWORD);
        yield return Case("unknown auth method", MAILBOX with { AuthMethod = MailboxAuthMethod.UNKNOWN }, PASSWORD);
        yield return Case("unknown password encoding", MAILBOX with { PasswordEncoding = MailboxPasswordEncoding.FromName("EBCDIC") }, PASSWORD);
        yield return Case("password encoding which changes ASCII", MAILBOX with { PasswordEncoding = MailboxPasswordEncoding.FromName("utf-16") }, PASSWORD);
        yield return Case("no host", MAILBOX with { Host = " " }, PASSWORD);
        yield return Case("a URL as host", MAILBOX with { Host = "imaps://imap.example.org" }, PASSWORD);
        yield return Case("port 0", MAILBOX with { Port = 0 }, PASSWORD);
        yield return Case("port 65536", MAILBOX with { Port = 65536 }, PASSWORD);
        yield return Case("no username", MAILBOX with { Username = string.Empty }, PASSWORD);
        yield return Case("no password", MAILBOX, string.Empty);
        yield break;

        static TestCaseData Case(string name, DataSourceMailbox mailbox, string password) => new TestCaseData(mailbox, password).SetArgDisplayNames(name);
    }

    [TestCaseSource(nameof(ExceptionsWithTheirFailure))]
    public void AnExceptionOfTheClientStandsForItsFailure(Exception exception, MailboxConnectionFailure failure)
    {
        Assert.That(ImapMailboxConnector.Classify(exception, CancellationToken.None), Is.EqualTo(failure));
    }

    [Test]
    public void ACancellationByTheUserIsNoFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(ImapMailboxConnector.Classify(new OperationCanceledException(cancellation.Token), cancellation.Token), Is.Null);
    }

    [Test]
    public void AnExceptionOfAnotherKindIsLeftAlone()
    {
        Assert.That(ImapMailboxConnector.Classify(new InvalidOperationException(), CancellationToken.None), Is.Null);
    }

    [TestCaseSource(nameof(InvalidSettings))]
    public async Task InvalidSettingsNeverReachTheServer(DataSourceMailbox mailbox, string password)
    {
        //
        // The host of the mailbox does not exist. Were anything sent, the failure would be a network
        // failure instead of this one.
        //
        await using var connector = new ImapMailboxConnector();
        var exception = await Assert.ThrowsAsync<MailboxConnectionException>(() => connector.ConnectAsync(mailbox, password, MailServerPolicy.ANY_SERVER, CancellationToken.None));
        Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.INVALID_SETTINGS));
    }

    [Test]
    public async Task AServerTheOrganizationDoesNotAllowIsNeverContacted()
    {
        //
        // As above, the host of the mailbox does not exist. Were anything sent, the failure would be
        // a network failure instead of this one.
        //
        await using var connector = new ImapMailboxConnector();
        var policy = new MailServerPolicy(true, [new("imap.intra.example.org", null)]);
        var exception = await Assert.ThrowsAsync<MailboxConnectionException>(() => connector.ConnectAsync(MAILBOX, PASSWORD, policy, CancellationToken.None));
        Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.SERVER_NOT_ALLOWED));
    }

    private static IEnumerable<TestCaseData> SignInPreparations()
    {
        const string UTF8 = "utf-8";
        const string WINDOWS_1252 = "windows-1252";

        yield return Case("ASCII", UTF8, "someone", "Sommer-2026!", true, true);
        yield return Case("umlaut in the password", UTF8, "someone", "Grüße-2026", true, true);
        yield return Case("umlaut in the username", WINDOWS_1252, @"FIRMA\müller", "Sommer-2026!", true, true);
        yield return Case("Windows-1252 with umlaut and euro sign", WINDOWS_1252, "someone", "Grüße-100€", true, true);
        yield return Case("UTF-8 with Turkish and Japanese", UTF8, "kişi", "şifre-日本", true, true);
        yield return Case("Windows-1254 with Turkish", "windows-1254", "kişi", "şifre-2026", true, true);
        yield return Case("Shift-JIS with Japanese", "shift_jis", "利用者", "日本-2026", true, true);

        // Windows-1252 lacks the capital sharp s and the Turkish s with cedilla, and AI Studio never switches to UTF-8 on its own:
        yield return Case("capital sharp s in Windows-1252", WINDOWS_1252, "someone", "STRAẞE", true, false);
        yield return Case("Turkish s in Windows-1252", WINDOWS_1252, "kişi", "Sommer-2026!", true, false);

        // Without PLAIN, MailKit sends the LOGIN command in UTF-8, whatever it is handed:
        yield return Case("umlaut without PLAIN in UTF-8", UTF8, "someone", "Grüße-2026", false, true);
        yield return Case("umlaut without PLAIN in Windows-1252", WINDOWS_1252, "someone", "Grüße-2026", false, false);
        yield return Case("ASCII without PLAIN in Windows-1252", WINDOWS_1252, "someone", "Sommer-2026!", false, true);

        // No encoding has a lone surrogate:
        yield return Case("lone surrogate", UTF8, "someone", "abc\uD800", true, false);
        yield break;

        static TestCaseData Case(string name, string encoding, string username, string password, bool offersPlain, bool expected) => new TestCaseData(encoding, username, password, offersPlain).Returns(expected).SetArgDisplayNames(name);
    }

    [TestCaseSource(nameof(SignInPreparations))]
    public bool ASignInGoesAheadOnlyWhenTheEncodingCarriesTheUsernameAndThePassword(string encoding, string username, string password, bool offersPlain)
    {
        return ImapMailboxConnector.TryPrepareSignIn(MailboxPasswordEncodingTests.GetKnownEncoding(encoding), username, password, offersPlain, out _, out _);
    }

    [Test]
    public void APreparedSignInComposesADecomposedUmlautForWindows1252()
    {
        // An a followed by a combining diaeresis, as some password managers deliver an ä:
        const string DECOMPOSED = "Ka\u0308se";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ImapMailboxConnector.TryPrepareSignIn(MailboxPasswordEncodingTests.GetKnownEncoding("windows-1252"), "müller", DECOMPOSED, true, out var username, out var password), Is.True);
            Assert.That(username, Is.EqualTo("müller"));
            Assert.That(password, Is.EqualTo("Käse"));
        }
    }

    [TestCase("Projects", '/', ExpectedResult = true)]
    [TestCase("Projekte 2026", '.', ExpectedResult = true)]
    [TestCase("Ablage/Alt", '.', ExpectedResult = true)]
    [TestCase("Ablage/Alt", '/', ExpectedResult = false)]
    [TestCase("Ablage.Alt", '.', ExpectedResult = false)]
    [TestCase("Flat", '\0', ExpectedResult = true)]
    [TestCase("", '/', ExpectedResult = false)]
    [TestCase("   ", '/', ExpectedResult = false)]
    [TestCase(" Projects", '/', ExpectedResult = false)]
    [TestCase("All*", '/', ExpectedResult = false)]
    [TestCase("100%", '/', ExpectedResult = false)]
    [TestCase("Line\nBreak", '/', ExpectedResult = false)]
    public bool AFolderNameNeedsNoReservedCharacter(string name, char directorySeparator) => ImapMailboxConnector.IsValidFolderName(name, directorySeparator);

    [Test]
    public void AFolderNameHasAMaximumLength()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ImapMailboxConnector.IsValidFolderName(new string('a', ImapMailboxConnector.MAX_FOLDER_NAME_LENGTH), '/'), Is.True);
            Assert.That(ImapMailboxConnector.IsValidFolderName(new string('a', ImapMailboxConnector.MAX_FOLDER_NAME_LENGTH + 1), '/'), Is.False);
        }
    }

    [TestCase(FolderAttributes.None, ExpectedResult = MailFolderSpecialUse.NONE)]
    [TestCase(FolderAttributes.HasChildren | FolderAttributes.Subscribed, ExpectedResult = MailFolderSpecialUse.NONE)]
    [TestCase(FolderAttributes.Sent, ExpectedResult = MailFolderSpecialUse.SENT)]
    [TestCase(FolderAttributes.Drafts, ExpectedResult = MailFolderSpecialUse.DRAFTS)]
    [TestCase(FolderAttributes.Archive, ExpectedResult = MailFolderSpecialUse.ARCHIVE)]
    [TestCase(FolderAttributes.All, ExpectedResult = MailFolderSpecialUse.ALL)]
    [TestCase(FolderAttributes.Flagged, ExpectedResult = MailFolderSpecialUse.FLAGGED)]
    [TestCase(FolderAttributes.Important, ExpectedResult = MailFolderSpecialUse.IMPORTANT)]
    [TestCase(FolderAttributes.Trash | FolderAttributes.HasNoChildren, ExpectedResult = MailFolderSpecialUse.TRASH)]
    [TestCase(FolderAttributes.Junk | FolderAttributes.Important, ExpectedResult = MailFolderSpecialUse.JUNK)]
    public MailFolderSpecialUse AFolderIsForWhatTheServerAnnounces(FolderAttributes attributes) => ImapMailboxConnector.ToSpecialUse(attributes);

    [Test]
    public void AHeaderBlockWithAVeryLongFieldIsReadAsAWhole()
    {
        //
        // The IMAP client parses every header block like this, with the default options of MimeKit,
        // whose limit the connector raises before the first mail is fetched:
        //
        RuntimeHelpers.RunClassConstructor(typeof(ImapMailboxConnector).TypeHandle);

        var recipients = string.Join(",\r\n ", Enumerable.Range(1, 1000).Select(number => $"recipient{number.ToString(CultureInfo.InvariantCulture)}@example.org"));
        var headerBlock = Encoding.ASCII.GetBytes($"From: alice@example.org\r\nTo: {recipients}\r\nSubject: All hands\r\n\r\n");

        var headers = Parse(headerBlock, ParserOptions.Default);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => Parse(headerBlock, new ParserOptions()), Throws.TypeOf<FormatException>(), "MimeKit reads such a field by default, so the limit needs no raising.");
            Assert.That(headers[HeaderId.Subject], Is.EqualTo("All hands"));
            Assert.That(MailHeaders.ReadMailboxes(headers, HeaderId.To), Has.Count.EqualTo(1000));
        }

        return;

        static HeaderList Parse(byte[] headerBlock, ParserOptions options)
        {
            using var stream = new MemoryStream(headerBlock);
            return new MimeParser(options, stream).ParseHeaders();
        }
    }
}