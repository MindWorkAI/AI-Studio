using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Settings which apply to all mailboxes, as opposed to the settings of each one.
/// </summary>
public sealed class DataMailboxes(Expression<Func<Data, DataMailboxes>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataMailboxes() : this(null)
    {
    }

    /// <summary>
    /// The least strict outbound data restriction a mailbox may have.
    /// </summary>
    /// <remarks>
    /// Only an organization sets it; the default leaves every level to the user. A mailbox set to a
    /// less strict level gets this one when its mails reach a chat, see
    /// MailToolResults.GetRequirements, and its dialog no longer offers the less strict levels.
    /// </remarks>
    public OutboundDataRestriction MinimumOutboundDataRestriction { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumOutboundDataRestriction, OutboundDataRestriction.UNRESTRICTED);

    /// <summary>
    /// Whether mailboxes may only be on the mail servers which configuration plugins offer.
    /// </summary>
    /// <remarks>
    /// Only an organization sets it. AI Studio then connects to no other server, and the mail tools
    /// leave a mailbox on another server out, see MailServerPolicy. Its index stays, so the mailbox
    /// comes back as it was once the organization allows its server again.
    /// </remarks>
    public bool AllowOnlyOrganizationMailServers { get; set; } = ManagedConfiguration.Register(configSelection, n => n.AllowOnlyOrganizationMailServers, false);
}