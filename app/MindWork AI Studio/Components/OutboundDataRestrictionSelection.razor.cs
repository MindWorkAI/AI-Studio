using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Lets the user choose where a chat may still send data once it has read from a mailbox or a data
/// source, and explains the choice.
/// </summary>
/// <remarks>
/// Only the levels allowed are offered: an organization may rule out the less strict ones, and so
/// does a data source which only self-hosted providers may use, see
/// DataSourceOutboundDataExtensions.GetMinimumOutboundDataRestriction. The dialog keeps the value it
/// passes within those, since it stores it.
/// </remarks>
public partial class OutboundDataRestrictionSelection : MSGComponentBase
{
    /// <summary>
    /// The level chosen.
    /// </summary>
    [Parameter]
    public OutboundDataRestriction Value { get; set; }

    [Parameter]
    public EventCallback<OutboundDataRestriction> ValueChanged { get; set; }

    /// <summary>
    /// The label of the setting. The blocked message of a tool quotes it, so the user finds the
    /// setting by the words they read there, see OutboundDataRequirementExtensions.GetToolBlockedMessage.
    /// </summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Why the setting matters for this kind of source.
    /// </summary>
    [Parameter]
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// The least strict level the organization allows for this kind of source.
    /// </summary>
    [Parameter]
    public OutboundDataRestriction OrganizationMinimum { get; set; } = OutboundDataRestriction.UNRESTRICTED;

    /// <summary>
    /// The security policy of the data source, or NOT_SPECIFIED for a source which has none.
    /// </summary>
    [Parameter]
    public DataSourceSecurity SecurityPolicy { get; set; } = DataSourceSecurity.NOT_SPECIFIED;

    private OutboundDataRestriction Minimum => DataSourceOutboundDataExtensions.GetMinimumOutboundDataRestriction(this.SecurityPolicy, this.OrganizationMinimum);

    /// <summary>
    /// Why less strict levels are not offered, or null when every level is.
    /// </summary>
    private string? LimitationText
    {
        get
        {
            if (this.SecurityPolicy is DataSourceSecurity.SELF_HOSTED)
                return T("Only self-hosted providers may use this data source. A chat which found something in it may therefore only use the services configured in AI Studio, such as the allowed private hosts of the tool 'Read Web Page'.");

            if (this.OrganizationMinimum is not OutboundDataRestriction.UNRESTRICTED)
                return T("Your organization does not allow less strict settings than the ones offered here.");

            return null;
        }
    }
}