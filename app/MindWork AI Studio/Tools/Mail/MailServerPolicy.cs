using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Which mail servers AI Studio may connect to, whose mailboxes the AI may read, and in which encoding
/// a mailbox sends its username and password.
/// </summary>
/// <remarks>
/// Read anew for every decision rather than kept: all parts come from configuration plugins, which
/// may change while AI Studio runs.
/// </remarks>
/// <param name="AllowsOnlyOrganizationMailServers">Whether the organization allows only its own mail servers, see DataMailboxes.AllowOnlyOrganizationMailServers.</param>
/// <param name="OrganizationServers">The mail servers which the configuration plugins offer.</param>
public sealed record MailServerPolicy(bool AllowsOnlyOrganizationMailServers, IReadOnlyList<OrganizationMailServer> OrganizationServers)
{
    /// <summary>
    /// Every server: what holds as long as no organization rules otherwise.
    /// </summary>
    public static readonly MailServerPolicy ANY_SERVER = new(false, []);

    /// <summary>
    /// Reads the policy which holds right now.
    /// </summary>
    /// <param name="settingsManager">The settings, which hold the switch of the organization.</param>
    /// <returns>The policy.</returns>
    public static MailServerPolicy Read(SettingsManager settingsManager) => new(
        settingsManager.ConfigurationData.MailboxSettings.AllowOnlyOrganizationMailServers,
        PluginFactory.GetMailboxProviders().Select(provider => new OrganizationMailServer(provider.Host, provider.PasswordEncoding)).ToList());

    /// <summary>
    /// Whether AI Studio may connect to a server, and the AI read the mailboxes on it.
    /// </summary>
    /// <remarks>
    /// Port and encryption make no difference: a mailbox on the right host but another port is
    /// still on the server of the organization.
    /// </remarks>
    /// <param name="host">The host of the server.</param>
    /// <returns>True when the server is allowed.</returns>
    public bool IsAllowed(string host) => !this.AllowsOnlyOrganizationMailServers || this.OrganizationServers.Any(server => MailServerHosts.AreSame(server.Host, host));

    /// <summary>
    /// The encoding the organization names for a server.
    /// </summary>
    /// <remarks>
    /// Found by the host, like IsAllowed: the user may have typed the host rather than chosen the
    /// mail server of the organization. It holds whether or not the organization allows only its own
    /// mail servers.
    /// </remarks>
    /// <param name="host">The host of the server.</param>
    /// <returns>The name of the encoding, or null when the organization names none for this server.</returns>
    public string? GetOrganizationPasswordEncoding(string host) => this.OrganizationServers
        .FirstOrDefault(server => server.PasswordEncoding is not null && MailServerHosts.AreSame(server.Host, host))?
        .PasswordEncoding;

    /// <summary>
    /// The encoding in which a mailbox sends its username and password.
    /// </summary>
    /// <remarks>
    /// The encoding the organization names for the server comes first: it knows what its server
    /// expects, and it may name the encoding only after the mailbox was added. Every sign-in, the
    /// connection test and the synchronization alike, takes the encoding from here.
    /// </remarks>
    /// <param name="mailbox">The mailbox.</param>
    /// <returns>The name of the encoding.</returns>
    public string GetPasswordEncoding(DataSourceMailbox mailbox) => this.GetPasswordEncoding(mailbox.Host, mailbox.PasswordEncoding);

    /// <summary>
    /// The encoding in which a mailbox on a server sends its username and password, cf. GetPasswordEncoding of a mailbox.
    /// </summary>
    /// <param name="host">The host of the server.</param>
    /// <param name="mailboxEncoding">The name of the encoding of the mailbox.</param>
    /// <returns>The name of the encoding.</returns>
    public string GetPasswordEncoding(string host, string mailboxEncoding) => this.GetOrganizationPasswordEncoding(host) ?? mailboxEncoding;
}