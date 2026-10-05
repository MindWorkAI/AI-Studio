using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Which mail servers AI Studio may connect to, and whose mailboxes the AI may read.
/// </summary>
/// <remarks>
/// Read anew for every decision rather than kept: both parts come from configuration plugins, which
/// may change while AI Studio runs.
/// </remarks>
/// <param name="AllowsOnlyOrganizationMailServers">Whether the organization allows only its own mail servers, see DataMailboxes.AllowOnlyOrganizationMailServers.</param>
/// <param name="OrganizationHosts">The hosts of the mail servers which the configuration plugins offer.</param>
public sealed record MailServerPolicy(bool AllowsOnlyOrganizationMailServers, IReadOnlyList<string> OrganizationHosts)
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
        PluginFactory.GetMailboxProviders().Select(provider => provider.Host).ToList());

    /// <summary>
    /// Whether AI Studio may connect to a server, and the AI read the mailboxes on it.
    /// </summary>
    /// <remarks>
    /// Port and encryption make no difference: a mailbox on the right host but another port is
    /// still on the server of the organization.
    /// </remarks>
    /// <param name="host">The host of the server.</param>
    /// <returns>True when the server is allowed.</returns>
    public bool IsAllowed(string host) => !this.AllowsOnlyOrganizationMailServers || this.OrganizationHosts.Any(organizationHost => MailServerHosts.AreSame(organizationHost, host));
}