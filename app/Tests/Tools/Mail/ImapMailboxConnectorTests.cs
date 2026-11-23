using System.Net.Sockets;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

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

    private static readonly MailboxPasswordEncoding[] BOTH_ENCODINGS = [MailboxPasswordEncoding.UTF8, MailboxPasswordEncoding.WINDOWS_1252];

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
        yield return Case("unknown password encoding", MAILBOX with { PasswordEncoding = MailboxPasswordEncoding.UNKNOWN }, PASSWORD);
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
        var exception = await Assert.ThrowsAsync<MailboxConnectionException>(() => connector.ConnectAsync(mailbox, password, MailServerPolicy.ANY_SERVER, mayTryOtherEncoding: true, CancellationToken.None));
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
        var policy = new MailServerPolicy(true, ["imap.intra.example.org"]);
        var exception = await Assert.ThrowsAsync<MailboxConnectionException>(() => connector.ConnectAsync(MAILBOX, PASSWORD, policy, mayTryOtherEncoding: true, CancellationToken.None));
        Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.SERVER_NOT_ALLOWED));
    }

    private static IEnumerable<TestCaseData> SignInEncodings()
    {
        const MailboxPasswordEncoding UTF8 = MailboxPasswordEncoding.UTF8;
        const MailboxPasswordEncoding WINDOWS_1252 = MailboxPasswordEncoding.WINDOWS_1252;

        // ASCII sends the same bytes in every encoding, so a second attempt would fail the same way:
        yield return Case("ASCII", UTF8, "someone", "Sommer-2026!", true, [UTF8]);
        yield return Case("ASCII, Windows-1252 stored", WINDOWS_1252, "someone", "Sommer-2026!", true, [WINDOWS_1252]);

        yield return Case("umlaut in the password", UTF8, "someone", "Grüße-2026", true, [UTF8, WINDOWS_1252]);
        yield return Case("umlaut, Windows-1252 stored", WINDOWS_1252, "someone", "Grüße-2026", true, [WINDOWS_1252, UTF8]);
        yield return Case("umlaut in the username", UTF8, @"FIRMA\müller", "Sommer-2026!", true, [UTF8, WINDOWS_1252]);
        yield return Case("section sign", UTF8, "someone", "Abs§2", true, [UTF8, WINDOWS_1252]);
        yield return Case("euro sign", UTF8, "someone", "100€", true, [UTF8, WINDOWS_1252]);

        // Windows-1252 has no capital sharp s:
        yield return Case("capital sharp s", UTF8, "someone", "STRAẞE", true, [UTF8]);
        yield return Case("capital sharp s, Windows-1252 stored", WINDOWS_1252, "someone", "STRAẞE", true, [UTF8]);

        // Without PLAIN, MailKit sends the LOGIN command in UTF-8, whatever it is handed:
        yield return Case("umlaut without PLAIN", UTF8, "someone", "Grüße-2026", false, [UTF8]);
        yield return Case("umlaut without PLAIN, Windows-1252 stored", WINDOWS_1252, "someone", "Grüße-2026", false, [UTF8]);

        // No encoding has a lone surrogate:
        yield return Case("lone surrogate", UTF8, "someone", "abc\uD800", true, []);
        yield break;

        static TestCaseData Case(string name, MailboxPasswordEncoding storedEncoding, string username, string password, bool offersPlain, MailboxPasswordEncoding[] expected) => new TestCaseData(storedEncoding, username, password, offersPlain, expected).SetArgDisplayNames(name);
    }

    [TestCaseSource(nameof(SignInEncodings))]
    public void ASignInUsesOnlyTheEncodingsWhichMakeADifference(MailboxPasswordEncoding storedEncoding, string username, string password, bool offersPlain, MailboxPasswordEncoding[] expected)
    {
        Assert.That(ImapMailboxConnector.GetSignInEncodings(storedEncoding, username, password, offersPlain), Is.EqualTo(expected));
    }

    [Test]
    public async Task ASynchronizationSignsInOnceInTheStoredEncoding()
    {
        // The server would accept the other encoding, but only a connection test may find that out:
        var server = new SignInServer(accepted: MailboxPasswordEncoding.WINDOWS_1252);
        var exception = await SignInExpectingFailureAsync(server, BOTH_ENCODINGS, mayTryOtherEncoding: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.AUTHENTICATION_FAILED));
            Assert.That(server.Attempts, Is.EqualTo(new[] { MailboxPasswordEncoding.UTF8 }));
            Assert.That(server.Reconnects, Is.Zero);
        }
    }

    [Test]
    public async Task AConnectionTestTriesTheOtherEncodingOnce()
    {
        var server = new SignInServer(accepted: MailboxPasswordEncoding.WINDOWS_1252);
        var accepted = await ImapMailboxConnector.SignInWithEncodingsAsync(BOTH_ENCODINGS, mayTryOtherEncoding: true, server.SignInAsync, server.ReconnectAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accepted, Is.EqualTo(MailboxPasswordEncoding.WINDOWS_1252));
            Assert.That(server.Attempts, Is.EqualTo(BOTH_ENCODINGS));
            Assert.That(server.Reconnects, Is.EqualTo(1), "Some servers hang up after a refused sign-in.");
        }
    }

    [Test]
    public async Task AConnectionTestStopsOnceEveryEncodingWasRefused()
    {
        var server = new SignInServer(accepted: null);
        var exception = await SignInExpectingFailureAsync(server, BOTH_ENCODINGS, mayTryOtherEncoding: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.AUTHENTICATION_FAILED));
            Assert.That(server.Attempts, Is.EqualTo(BOTH_ENCODINGS));
            Assert.That(server.Reconnects, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AConnectionTestWithASingleEncodingSignsInOnce()
    {
        // GetSignInEncodings offers one encoding for ASCII, where every encoding sends the same bytes:
        var server = new SignInServer(accepted: null);
        var exception = await SignInExpectingFailureAsync(server, [MailboxPasswordEncoding.UTF8], mayTryOtherEncoding: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.AUTHENTICATION_FAILED));
            Assert.That(server.Attempts, Has.Count.EqualTo(1));
            Assert.That(server.Reconnects, Is.Zero);
        }
    }

    [Test]
    public async Task OnlyARefusedSignInLeadsToTheOtherEncoding()
    {
        var server = new SignInServer(accepted: MailboxPasswordEncoding.WINDOWS_1252, failure: new IOException("The connection was reset."));
        var exception = await SignInExpectingFailureAsync(server, BOTH_ENCODINGS, mayTryOtherEncoding: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.NETWORK_UNAVAILABLE));
            Assert.That(server.Attempts, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task AFailedReconnectEndsTheSignIn()
    {
        var server = new SignInServer(accepted: MailboxPasswordEncoding.WINDOWS_1252, reconnectFailure: new MailboxConnectionException(MailboxConnectionFailure.NETWORK_UNAVAILABLE, "The server is gone."));
        var exception = await SignInExpectingFailureAsync(server, BOTH_ENCODINGS, mayTryOtherEncoding: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.NETWORK_UNAVAILABLE));
            Assert.That(server.Attempts, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task WithoutAnEncodingNothingIsSent()
    {
        // No encoding has every character, e.g., of a password with a lone surrogate:
        var server = new SignInServer(accepted: MailboxPasswordEncoding.UTF8);
        var exception = await SignInExpectingFailureAsync(server, [], mayTryOtherEncoding: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception?.Failure, Is.EqualTo(MailboxConnectionFailure.INVALID_SETTINGS));
            Assert.That(server.Attempts, Is.Empty);
        }
    }

    private static async Task<MailboxConnectionException?> SignInExpectingFailureAsync(SignInServer server, MailboxPasswordEncoding[] encodings, bool mayTryOtherEncoding) =>
        await Assert.ThrowsAsync<MailboxConnectionException>(() => ImapMailboxConnector.SignInWithEncodingsAsync(encodings, mayTryOtherEncoding, server.SignInAsync, server.ReconnectAsync, CancellationToken.None));

    /// <summary>
    /// Stands in for an IMAP server: accepts a sign-in in one encoding only, and counts every attempt.
    /// </summary>
    /// <param name="accepted">The encoding the server accepts, or null for a wrong password.</param>
    /// <param name="failure">What every sign-in throws instead of an answer, e.g., a lost connection.</param>
    /// <param name="reconnectFailure">What connecting again throws.</param>
    private sealed class SignInServer(MailboxPasswordEncoding? accepted, Exception? failure = null, Exception? reconnectFailure = null)
    {
        public List<MailboxPasswordEncoding> Attempts { get; } = [];

        public int Reconnects { get; private set; }

        public async Task SignInAsync(MailboxPasswordEncoding encoding)
        {
            await Task.Yield();
            this.Attempts.Add(encoding);
            if (failure is not null)
                throw failure;

            if (encoding != accepted)
                throw new AuthenticationException("The server rejected the credentials.");
        }

        public async Task ReconnectAsync()
        {
            await Task.Yield();
            this.Reconnects++;
            if (reconnectFailure is not null)
                throw reconnectFailure;
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
}