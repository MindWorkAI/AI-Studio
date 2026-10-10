using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Tools.Services;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// An e-mail mailbox on an IMAP server, which AI Studio embeds and indexes itself.
/// </summary>
/// <remarks>
/// Mailboxes are kept in Data.Mailboxes rather than in DataSources, which is why this is no
/// IDataSource: classic RAG, Semantic Search and the agents never see a mailbox. Only the mail
/// tools read from one.
/// </remarks>
public readonly record struct DataSourceMailbox : IIndexedDataSource, ISecretId
{
    public DataSourceMailbox()
    {
    }

    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();

    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;

    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.MAILBOX;

    /// <inheritdoc />
    public bool IsEnterpriseConfiguration { get; init; }

    /// <inheritdoc />
    public Guid EnterpriseConfigurationPluginId { get; init; } = Guid.Empty;

    /// <inheritdoc />
    public string EmbeddingId { get; init; } = Guid.Empty.ToString();

    /// <inheritdoc />
    public int MaxChunkTokenLength { get; init; }

    /// <inheritdoc />
    public int ChunkOverlapTokenLength { get; init; } = DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;

    /// <inheritdoc />
    /// <remarks>
    /// There is no default, the user has to choose one of the levels which IsAllowedMailboxConfidence
    /// accepts. Until then, the level is NONE, and no provider may read the mailbox.
    /// </remarks>
    public ConfidenceLevel ConfidenceLevel { get; init; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The host name of the IMAP server, e.g., imap.example.org.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// The port of the IMAP server. The default is the one for IMAP with TLS from the first byte on.
    /// </summary>
    public int Port { get; init; } = MailboxTransportSecurityExtensions.SSL_ON_CONNECT_PORT;

    /// <summary>
    /// How the connection to the IMAP server is encrypted.
    /// </summary>
    public MailboxTransportSecurity TransportSecurity { get; init; } = MailboxTransportSecurity.SSL_ON_CONNECT;

    /// <summary>
    /// How AI Studio signs in to the IMAP server.
    /// </summary>
    public MailboxAuthMethod AuthMethod { get; init; } = MailboxAuthMethod.PASSWORD;

    /// <summary>
    /// The username to sign in with, often the e-mail address.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The folder to which the synchronization and all mail tools are restricted, together with its subfolders.
    /// </summary>
    /// <remarks>
    /// The full path as the server names it, including the server's own hierarchy delimiter. Empty
    /// means the whole mailbox, apart from the trash and the junk folder. The sent mails and the
    /// drafts may come along from outside of it, see IncludeSentAndDrafts.
    /// </remarks>
    public string RootFolder { get; init; } = string.Empty;

    /// <summary>
    /// Whether the folders for sent mails and for drafts are synchronized as well, when they lie outside the root folder.
    /// </summary>
    /// <remarks>
    /// Without a root folder, both belong to the whole mailbox anyway. Only the folders the server
    /// marks as such count, without their subfolders. A mailbox stored before this setting existed
    /// loads with it switched on, so that its next sync fetches them.
    /// </remarks>
    public bool IncludeSentAndDrafts { get; init; } = true;

    /// <summary>
    /// How far back the index reaches. Flagged mails and drafts are indexed regardless of their age.
    /// </summary>
    public MailboxMaxAge MaxAge { get; init; } = MailboxMaxAge.LAST_12_MONTHS;

    /// <summary>
    /// Whether the text of attached documents is indexed as well.
    /// </summary>
    public bool IndexAttachments { get; init; } = true;

    /// <summary>
    /// The size in megabytes up to which the text of an attachment is indexed. Of a larger one, only the name is.
    /// </summary>
    public int MaxAttachmentSizeMegabytes { get; init; } = 10;

    /// <summary>
    /// Where a chat may still send data, once it has read from this mailbox.
    /// </summary>
    /// <remarks>
    /// An organization may demand a stricter one, see DataMailboxes.MinimumOutboundDataRestriction.
    /// </remarks>
    public OutboundDataRestriction OutboundDataRestriction { get; init; } = OutboundDataRestriction.ONLY_CONFIGURED_SERVICES;

    /// <summary>
    /// The maximum number of mails one search returns. Searched page by page, it is the size of a page.
    /// </summary>
    public ushort MaxMatches { get; init; } = 10;

    #region Implementation of ISecretId

    /// <remarks>
    /// The OS keyring stores the password under this ID together with the name of the mailbox, so
    /// that the user recognizes the entry there. Renaming a mailbox therefore stores the password
    /// anew, and deletes the old entry.
    /// </remarks>
    [JsonIgnore]
    string ISecretId.SecretId => this.IsEnterpriseConfiguration ? $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{this.Id}" : this.Id;

    [JsonIgnore]
    string ISecretId.SecretName => this.Name;

    #endregion
}