using System.Diagnostics.CodeAnalysis;

using AIStudio.Tools.Mail;
using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// A mail server of the organization, which a configuration plugin offers for new mailboxes.
/// </summary>
/// <remarks>
/// Live plugin content rather than a stored configuration object: it is a choice in the mailbox
/// dialog, not something the user owns. Large organizations run more than one mail system, so a
/// plugin may define several, and the ones of all plugins are offered together. Username and
/// password stay with each user.
/// </remarks>
public sealed record DataMailboxProvider : ILivePluginContent
{
    /// <summary>
    /// The stable ID of the mail server.
    /// </summary>
    public string Id { get; private init; } = string.Empty;

    /// <summary>
    /// The ID of the enterprise configuration plugin that provides this mail server.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; private init; } = Guid.Empty;

    /// <summary>
    /// The name shown to the user, e.g., the name of the mail system.
    /// </summary>
    public string Name { get; private init; } = string.Empty;

    /// <summary>
    /// The host of the IMAP server, as the organization wrote it.
    /// </summary>
    public string Host { get; private init; } = string.Empty;

    /// <summary>
    /// The port of the IMAP server.
    /// </summary>
    public int Port { get; private init; } = MailboxTransportSecurityExtensions.SSL_ON_CONNECT_PORT;

    /// <summary>
    /// How the connection to the IMAP server is encrypted.
    /// </summary>
    public MailboxTransportSecurity TransportSecurity { get; private init; } = MailboxTransportSecurity.SSL_ON_CONNECT;

    /// <summary>
    /// What the user enters as the username, in the words of the organization. Empty when the organization gave no hint.
    /// </summary>
    public string UsernameHint { get; private init; } = string.Empty;

    /// <summary>
    /// The page of the organization which explains how to set up the mailbox. Empty when there is none.
    /// </summary>
    public string HelpUrl { get; private init; } = string.Empty;

    /// <summary>
    /// Reads a mail server from an entry of the table MAILBOX_PROVIDERS.
    /// </summary>
    /// <remarks>
    /// An entry with an invalid field is dropped as a whole, also when only an optional field is
    /// invalid. Offering a server with a part of its settings left out would let the user connect
    /// differently than the organization meant.
    /// </remarks>
    /// <param name="index">The position of the entry, for the log.</param>
    /// <param name="table">The entry.</param>
    /// <param name="configPluginId">The ID of the configuration plugin which defines the entry.</param>
    /// <param name="logger">The logger to report an invalid entry to.</param>
    /// <param name="provider">The mail server, when the entry is valid.</param>
    /// <returns>True when the entry is valid.</returns>
    public static bool TryParseConfiguration(int index, LuaTable table, Guid configPluginId, ILogger logger, [NotNullWhen(true)] out DataMailboxProvider? provider)
    {
        provider = null;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid ID. The ID must be a valid GUID.", index);
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name) || string.IsNullOrWhiteSpace(name))
        {
            logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid Name field.", index);
            return false;
        }

        if (!table.TryGetValue("Host", out var hostValue) || !hostValue.TryRead<string>(out var host) || !MailServerHosts.TryGetIdnHost(host, out _))
        {
            logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid Host field. The host must be a host name or an IP address, without a scheme or a port.", index);
            return false;
        }

        var transportSecurity = MailboxTransportSecurity.SSL_ON_CONNECT;
        if (table.TryGetValue("TransportSecurity", out var transportSecurityValue) && (!transportSecurityValue.TryRead<string>(out var transportSecurityText) || !EnumNames.TryParse(transportSecurityText, out transportSecurity) || transportSecurity is MailboxTransportSecurity.UNKNOWN))
        {
            logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid TransportSecurity field. Allowed values are SSL_ON_CONNECT and STARTTLS.", index);
            return false;
        }

        var port = transportSecurity.GetUsualPort() ?? MailboxTransportSecurityExtensions.SSL_ON_CONNECT_PORT;
        if (table.TryGetValue("Port", out var portValue) && (!portValue.TryRead(out port) || port is < 1 or > 65535))
        {
            logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid Port field. The port must be a number from 1 to 65535.", index);
            return false;
        }

        var usernameHint = string.Empty;
        if (table.TryGetValue("UsernameHint", out var usernameHintValue))
        {
            if (!usernameHintValue.TryRead<string>(out var usernameHintText))
            {
                logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid UsernameHint field. The hint must be a text.", index);
                return false;
            }

            usernameHint = usernameHintText.Trim();
        }

        var helpUrl = string.Empty;
        if (table.TryGetValue("HelpUrl", out var helpUrlValue))
        {
            if (!helpUrlValue.TryRead<string>(out var helpUrlText) || !Uri.TryCreate(helpUrlText.Trim(), UriKind.Absolute, out var helpUri) || (helpUri.Scheme != Uri.UriSchemeHttps && helpUri.Scheme != Uri.UriSchemeHttp))
            {
                logger.LogWarning("The configured mailbox provider {ProviderIndex} does not contain a valid HelpUrl field. The URL must be an absolute http or https address.", index);
                return false;
            }

            helpUrl = helpUri.OriginalString;
        }

        provider = new DataMailboxProvider
        {
            Id = id.ToString(),
            EnterpriseConfigurationPluginId = configPluginId,
            Name = name.Trim(),
            Host = host.Trim(),
            Port = port,
            TransportSecurity = transportSecurity,
            UsernameHint = usernameHint,
            HelpUrl = helpUrl,
        };

        return true;
    }
}