using System.Globalization;
using System.Net.Sockets;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;

using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// A connection to the IMAP server of a mailbox: connects, signs in, and works with the folders.
/// </summary>
/// <remarks>
/// Every sign-in is exactly one attempt with the password. An IMAP client would otherwise try one
/// mechanism after the other and the LOGIN command last, and a directory such as Active Directory
/// counts each rejected attempt on its way to locking the account. For the same reason, nothing is
/// sent at all when the password is empty or a setting is unknown to this version.<br/><br/>
/// Before signing in, the caller has to make sure that no failed sign-in is on record for this
/// mailbox. Whatever fails arrives as MailboxConnectionException with its failure code, and nothing
/// here writes to the log: the answers of a server may name the user, so the caller logs the
/// mailbox ID and the failure code instead.
/// </remarks>
public sealed class ImapMailboxConnector : IAsyncDisposable
{
    /// <summary>
    /// The longest folder name accepted when creating a folder.
    /// </summary>
    public const int MAX_FOLDER_NAME_LENGTH = 200;

    /// <summary>
    /// The largest text part which is fetched, in bytes. Nobody writes a mail that long; a part this
    /// large is a generated report or a newsletter gone wrong, and its mail is indexed by its header
    /// block alone.
    /// </summary>
    public const long MAX_TEXT_PART_BYTES = 4 * 1024 * 1024;

    /// <summary>
    /// How many UIDs go into one FETCH. A long list of scattered UIDs makes a long command line, and
    /// some servers refuse lines beyond a few kilobytes.
    /// </summary>
    private const int MAX_UIDS_PER_FETCH = 500;

    private const string PLAIN_MECHANISM = "PLAIN";

    /// <summary>
    /// Characters IMAP reserves as wildcards in LIST, which therefore make no folder name.
    /// </summary>
    private static readonly char[] LIST_WILDCARDS = ['*', '%'];

    /// <summary>
    /// How long a single command may take until the connection counts as lost.
    /// </summary>
    private static readonly TimeSpan COMMAND_TIMEOUT = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long saying goodbye to the server may take. The connection is closed either way.
    /// </summary>
    private static readonly TimeSpan DISCONNECT_TIMEOUT = TimeSpan.FromSeconds(5);

    private readonly ImapClient client = new()
    {
        Timeout = (int)COMMAND_TIMEOUT.TotalMilliseconds,

        //
        // Revocation lists are not checked, as with the HTTP connections of AI Studio. Otherwise a
        // server with an internal CA would fail as soon as the list of that CA is out of reach,
        // which is common inside a Flatpak and outside the company network.
        //
        CheckCertificateRevocation = false,
    };

    /// <summary>
    /// The folder the reading methods work in, cf. OpenFolderAsync.
    /// </summary>
    private IMailFolder? openFolder;

    /// <summary>
    /// Whether the server tells which mails changed since a point it handed out before (CONDSTORE).
    /// Without it, the flags of every mail have to be fetched to find the changed ones.
    /// </summary>
    public bool SupportsChangeTracking => this.client.Capabilities.HasFlag(ImapCapabilities.CondStore);

    /// <summary>
    /// Connects to the IMAP server of the mailbox and signs in.
    /// </summary>
    /// <param name="mailbox">The mailbox to connect to.</param>
    /// <param name="password">The password, as stored in the OS keyring or just typed in.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="MailboxConnectionException">The connection or the sign-in failed.</exception>
    public async Task ConnectAsync(DataSourceMailbox mailbox, string password, CancellationToken token)
    {
        if (!TryGetSocketOptions(mailbox.TransportSecurity, out var socketOptions) || mailbox.AuthMethod is not MailboxAuthMethod.PASSWORD || !TryGetIdnHost(mailbox.Host, out var idnHost) || mailbox.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(mailbox.Username) || string.IsNullOrEmpty(password))
            throw new MailboxConnectionException(MailboxConnectionFailure.INVALID_SETTINGS, "The mailbox settings are incomplete, or this version of AI Studio does not know them.");

        this.client.ServerCertificateValidationCallback = ExternalHttpClientTimeout.CreateServerCertificateValidationCallback(idnHost, ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
        try
        {
            await this.client.ConnectAsync(idnHost, mailbox.Port, socketOptions, token);
        }
        catch (NotSupportedException e)
        {
            // Thrown when the server does not offer STARTTLS. The connection never goes on without it:
            throw new MailboxConnectionException(MailboxConnectionFailure.TLS_FAILED, "The IMAP server does not offer STARTTLS.", e);
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Connecting to the IMAP server failed: {failure}.", e);
        }

        try
        {
            await this.SignInAsync(mailbox.Username, password, token);
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Signing in to the IMAP server failed: {failure}.", e);
        }
    }

    /// <summary>
    /// Lists all folders of the mailbox, ordered by their full names.
    /// </summary>
    /// <exception cref="MailboxConnectionException">The server could not list the folders.</exception>
    public async Task<IReadOnlyList<MailServerFolder>> GetFoldersAsync(CancellationToken token)
    {
        try
        {
            var folders = new Dictionary<string, MailServerFolder>(StringComparer.Ordinal);
            foreach (var folderNamespace in this.client.PersonalNamespaces)
            {
                foreach (var folder in await this.client.GetFoldersAsync(folderNamespace, StatusItems.None, false, token))
                {
                    if (!folder.Attributes.HasFlag(FolderAttributes.NonExistent))
                        folders.TryAdd(folder.FullName, ToServerFolder(folder));
                }
            }

            // Servers whose personal namespace starts below the inbox, e.g. "INBOX.", do not list it:
            if (this.client.Inbox is { } inbox)
                folders.TryAdd(inbox.FullName, ToServerFolder(inbox));

            return folders.Values.OrderBy(folder => folder.FullName, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Listing the folders failed: {failure}.", e);
        }
    }

    /// <summary>
    /// Creates a folder for mails.
    /// </summary>
    /// <param name="parentFullName">The full name of the folder to create it in, or empty for the top level.</param>
    /// <param name="name">The name of the new folder, cf. IsValidFolderName.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The folder the server created.</returns>
    /// <exception cref="ArgumentException">The name is no valid folder name.</exception>
    /// <exception cref="MailboxConnectionException">The server did not create the folder, e.g., because it exists already.</exception>
    public async Task<MailServerFolder> CreateFolderAsync(string parentFullName, string name, CancellationToken token)
    {
        try
        {
            var parent = string.IsNullOrEmpty(parentFullName)
                ? this.client.GetFolder(this.client.PersonalNamespaces[0])
                : await this.client.GetFolderAsync(parentFullName, token);

            var folderName = name.Trim();
            if (!IsValidFolderName(folderName, parent.DirectorySeparator))
                throw new ArgumentException("The folder name is empty, too long, or contains a character the server reserves.", nameof(name));

            var createdFolder = await parent.CreateAsync(folderName, true, token);
            if (createdFolder is null)
                throw new MailboxConnectionException(MailboxConnectionFailure.SERVER_ERROR, "The IMAP server did not report the folder it created.");

            return ToServerFolder(createdFolder);
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Creating the folder failed: {failure}.", e);
        }
    }

    /// <summary>
    /// Opens a folder for the reading methods below, and reads how it stands.
    /// </summary>
    /// <remarks>
    /// The folder is opened read-only (EXAMINE), so nothing fetched from it can mark a mail as read,
    /// whatever the server makes of the request. The unread count comes from STATUS, which is asked
    /// before the folder is opened: a server need not answer STATUS for the folder which is open.
    /// </remarks>
    /// <param name="folderPath">The full path of the folder.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How the folder stands.</returns>
    /// <exception cref="MailboxConnectionException">The server could not open the folder.</exception>
    public async Task<MailFolderState> OpenFolderAsync(string folderPath, CancellationToken token)
    {
        try
        {
            var folder = await this.client.GetFolderAsync(folderPath, token);
            await folder.StatusAsync(StatusItems.Unread, token);
            var unseenCount = folder.Unread;

            await folder.OpenAsync(FolderAccess.ReadOnly, token);
            this.openFolder = folder;

            var highestModSeq = this.SupportsChangeTracking && folder.HighestModSeq > 0 ? (long)folder.HighestModSeq : (long?)null;
            return new(folder.UidValidity, folder.UidNext?.Id, highestModSeq, folder.Count, unseenCount);
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Opening a folder failed: {failure}.", e);
        }
    }

    /// <summary>
    /// Finds the mails of the open folder which belong into the index.
    /// </summary>
    /// <remarks>
    /// Those which arrived since the given day, and every flagged one, however old. The day counts
    /// as a whole, in the time zone of the server, since that is how IMAP compares dates.
    /// </remarks>
    /// <param name="receivedSince">The first day of the period, or null for all mails.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Their UIDs, in ascending order.</returns>
    /// <exception cref="MailboxConnectionException">The server could not search the folder.</exception>
    public async Task<IReadOnlyList<long>> SearchIndexedMailsAsync(DateTimeOffset? receivedSince, CancellationToken token)
    {
        var folder = this.GetOpenFolder();
        var query = receivedSince is { } since
            ? SearchQuery.DeliveredAfter(since.UtcDateTime.Date).Or(SearchQuery.Flagged)
            : SearchQuery.All;

        try
        {
            var uids = await folder.SearchAsync(query, token);
            return uids.Select(uid => (long)uid.Id).Order().ToList();
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Searching a folder failed: {failure}.", e);
        }
    }

    /// <summary>
    /// Reads the flags of mails in the open folder.
    /// </summary>
    /// <param name="uids">The UIDs to ask about.</param>
    /// <param name="changedSinceModSeq">With CONDSTORE, only mails changed after this HIGHESTMODSEQ are reported. Null asks about all of them.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The flags by UID. A mail which is gone, or did not change, is missing.</returns>
    /// <exception cref="MailboxConnectionException">The server could not read the flags.</exception>
    public async Task<IReadOnlyDictionary<long, MailFlags>> FetchFlagsAsync(IReadOnlyCollection<long> uids, long? changedSinceModSeq, CancellationToken token)
    {
        var request = new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Flags);
        if (changedSinceModSeq is { } modSeq && this.SupportsChangeTracking)
            request.ChangedSince = (ulong)modSeq;

        var flagsByUid = new Dictionary<long, MailFlags>();
        foreach (var summary in await this.FetchAsync(uids, request, token))
            flagsByUid[summary.UniqueId.Id] = MailSummaryReader.ReadFlags(summary.Flags);

        return flagsByUid;
    }

    /// <summary>
    /// Reads what the sync needs to know about mails in the open folder before it fetches any text.
    /// </summary>
    /// <remarks>
    /// That is the complete header block, the structure, the size, the arrival time and the flags,
    /// and the ids by which the server tells a mail apart in every folder, wherever it knows them:
    /// the EMAILID with OBJECTID, the X-GM-MSGID with Gmail. Neither is asked for elsewhere, since a
    /// server answers an item it does not know with an error.
    /// </remarks>
    /// <param name="uids">The UIDs of the mails.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>One summary per mail which still exists.</returns>
    /// <exception cref="MailboxConnectionException">The server could not read the mails.</exception>
    public async Task<IReadOnlyList<IMessageSummary>> FetchSummariesAsync(IReadOnlyCollection<long> uids, CancellationToken token)
    {
        var items = MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate | MessageSummaryItems.Size | MessageSummaryItems.BodyStructure | MessageSummaryItems.Headers;
        if (this.client.Capabilities.HasFlag(ImapCapabilities.ObjectID))
            items |= MessageSummaryItems.EmailId;

        if (this.client.Capabilities.HasFlag(ImapCapabilities.GMailExt1))
            items |= MessageSummaryItems.GMailMessageId;

        return await this.FetchAsync(uids, new FetchRequest(items), token);
    }

    /// <summary>
    /// Fetches the text parts of a mail in the open folder, and nothing else of it.
    /// </summary>
    /// <remarks>
    /// Attachments stay on the server. A part larger than MAX_TEXT_PART_BYTES is left out as well.
    /// </remarks>
    /// <param name="summary">The summary of the mail, with its structure.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The HTML and the plain text part, each null when the mail has none.</returns>
    /// <exception cref="MailboxConnectionException">The server could not deliver the parts.</exception>
    public async Task<MailTextParts> FetchTextPartsAsync(IMessageSummary summary, CancellationToken token)
    {
        try
        {
            var htmlBody = await this.FetchTextPartAsync(summary.UniqueId, summary.HtmlBody, token);
            var textBody = await this.FetchTextPartAsync(summary.UniqueId, summary.TextBody, token);
            return new(htmlBody, textBody);
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Fetching the text of a mail failed: {failure}.", e);
        }
    }

    /// <summary>
    /// Whether a name can become a folder below a parent with this hierarchy delimiter.
    /// </summary>
    /// <param name="name">The name, without leading or trailing whitespace.</param>
    /// <param name="directorySeparator">The hierarchy delimiter of the server, or the null character when it has none.</param>
    public static bool IsValidFolderName(string name, char directorySeparator)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MAX_FOLDER_NAME_LENGTH || name != name.Trim())
            return false;

        if (name.Any(char.IsControl) || name.IndexOfAny(LIST_WILDCARDS) >= 0)
            return false;

        return directorySeparator is '\0' || !name.Contains(directorySeparator);
    }

    /// <summary>
    /// Which failure an exception of the IMAP client stands for.
    /// </summary>
    /// <returns>The failure, or null when the exception is none of the connection, e.g., a cancellation by the user.</returns>
    internal static MailboxConnectionFailure? Classify(Exception exception, CancellationToken token) => exception switch
    {
        OperationCanceledException when token.IsCancellationRequested => null,

        AuthenticationException or SaslException => MailboxConnectionFailure.AUTHENTICATION_FAILED,
        SslHandshakeException => MailboxConnectionFailure.TLS_FAILED,

        // A timeout may surface as any of these, a cancellation the user never asked for included:
        SocketException or IOException or TimeoutException or OperationCanceledException => MailboxConnectionFailure.NETWORK_UNAVAILABLE,

        ProtocolException or CommandException or FolderNotFoundException => MailboxConnectionFailure.SERVER_ERROR,

        _ => null,
    };

    /// <summary>
    /// What a folder is for, from the attributes the server lists it with.
    /// </summary>
    /// <remarks>
    /// A folder may carry more than one of them. The trash and the junk folder come first, since the
    /// synchronization leaves them out, and then the folders AI Studio writes to.
    /// </remarks>
    internal static MailFolderSpecialUse ToSpecialUse(FolderAttributes attributes)
    {
        if (attributes.HasFlag(FolderAttributes.Trash))
            return MailFolderSpecialUse.TRASH;

        if (attributes.HasFlag(FolderAttributes.Junk))
            return MailFolderSpecialUse.JUNK;

        if (attributes.HasFlag(FolderAttributes.Sent))
            return MailFolderSpecialUse.SENT;

        if (attributes.HasFlag(FolderAttributes.Drafts))
            return MailFolderSpecialUse.DRAFTS;

        if (attributes.HasFlag(FolderAttributes.All))
            return MailFolderSpecialUse.ALL;

        if (attributes.HasFlag(FolderAttributes.Archive))
            return MailFolderSpecialUse.ARCHIVE;

        if (attributes.HasFlag(FolderAttributes.Flagged))
            return MailFolderSpecialUse.FLAGGED;

        if (attributes.HasFlag(FolderAttributes.Important))
            return MailFolderSpecialUse.IMPORTANT;

        return MailFolderSpecialUse.NONE;
    }

    private IMailFolder GetOpenFolder() => this.openFolder is { IsOpen: true } folder
        ? folder
        : throw new InvalidOperationException("No folder is open. Call OpenFolderAsync first.");

    /// <summary>
    /// Fetches from the open folder, a limited number of UIDs per command.
    /// </summary>
    private async Task<IReadOnlyList<IMessageSummary>> FetchAsync(IReadOnlyCollection<long> uids, IFetchRequest request, CancellationToken token)
    {
        var folder = this.GetOpenFolder();
        var summaries = new List<IMessageSummary>(uids.Count);

        try
        {
            foreach (var batch in uids.Order().Chunk(MAX_UIDS_PER_FETCH))
            {
                var uidSet = new UniqueIdSet(batch.Select(uid => new UniqueId((uint)uid)), SortOrder.Ascending);
                summaries.AddRange(await folder.FetchAsync(uidSet, request, token));
            }
        }
        catch (Exception e) when (Classify(e, token) is { } failure)
        {
            throw new MailboxConnectionException(failure, $"Fetching mails failed: {failure}.", e);
        }

        return summaries;
    }

    private async Task<string?> FetchTextPartAsync(UniqueId uid, BodyPartText? part, CancellationToken token)
    {
        if (part is null || part.Octets > MAX_TEXT_PART_BYTES)
            return null;

        var entity = await this.GetOpenFolder().GetBodyPartAsync(uid, part, token);
        return entity is TextPart textPart ? textPart.Text : null;
    }

    private async Task SignInAsync(string username, string password, CancellationToken token)
    {
        if (this.client.AuthenticationMechanisms.Contains(PLAIN_MECHANISM))
        {
            await this.client.AuthenticateAsync(new SaslMechanismPlain(username, password), token);
            return;
        }

        if (this.client.Capabilities.HasFlag(ImapCapabilities.LoginDisabled))
            throw new MailboxConnectionException(MailboxConnectionFailure.SERVER_ERROR, "The IMAP server offers no way to sign in with a password.");

        //
        // With no mechanism left to try, the client signs in with the LOGIN command alone:
        //
        this.client.AuthenticationMechanisms.Clear();
        await this.client.AuthenticateAsync(username, password, token);
    }

    private static bool TryGetSocketOptions(MailboxTransportSecurity transportSecurity, out SecureSocketOptions socketOptions)
    {
        //
        // Never StartTlsWhenAvailable or Auto: both carry on without encryption when the server
        // does not offer it, and the password would travel in plain text.
        //
        socketOptions = transportSecurity switch
        {
            MailboxTransportSecurity.SSL_ON_CONNECT => SecureSocketOptions.SslOnConnect,
            MailboxTransportSecurity.STARTTLS => SecureSocketOptions.StartTls,

            _ => SecureSocketOptions.None,
        };

        return socketOptions is not SecureSocketOptions.None;
    }

    /// <summary>
    /// The host in the form certificates and the allowed hosts for root certificates use.
    /// </summary>
    private static bool TryGetIdnHost(string host, out string idnHost)
    {
        idnHost = host.Trim();
        switch (Uri.CheckHostName(idnHost))
        {
            case UriHostNameType.IPv4:
            case UriHostNameType.IPv6:
                return true;

            case UriHostNameType.Dns:
                try
                {
                    idnHost = new IdnMapping().GetAscii(idnHost);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }

            default:
                return false;
        }
    }

    private static MailServerFolder ToServerFolder(IMailFolder folder) => new(
        folder.FullName,
        folder.Name,
        folder.ParentFolder?.FullName ?? string.Empty,
        folder.DirectorySeparator,
        ToSpecialUse(folder.Attributes),
        folder.Attributes.HasFlag(FolderAttributes.Inbox) || folder.FullName.Equals("INBOX", StringComparison.OrdinalIgnoreCase),
        !folder.Attributes.HasFlag(FolderAttributes.NoSelect));

    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (this.client.IsConnected)
        {
            using var timeout = new CancellationTokenSource(DISCONNECT_TIMEOUT);
            try
            {
                await this.client.DisconnectAsync(true, timeout.Token);
            }
            catch (Exception e) when (Classify(e, CancellationToken.None) is not null)
            {
                // The connection closes either way, and nothing waits for the goodbye.
            }
        }

        this.client.Dispose();
    }

    #endregion
}