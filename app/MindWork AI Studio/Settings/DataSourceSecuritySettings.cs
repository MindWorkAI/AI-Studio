using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

public sealed class DataSourceSecuritySettings(Expression<Func<Data, DataSourceSecuritySettings>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataSourceSecuritySettings() : this(null)
    {
    }

    /// <summary>
    /// Provider instance IDs trusted by an organization for data-source security checks.
    /// </summary>
    public HashSet<string> TrustedProviderIds { get; set; } = ManagedConfiguration.Register(configSelection, n => n.TrustedProviderIds, []);

    /// <summary>
    /// The least strict outbound data restriction a data source may have.
    /// </summary>
    /// <remarks>
    /// Only an organization sets it; the default leaves every level to the user. A data source set to
    /// a less strict level gets this one when its passages reach a chat, see
    /// DataSourceOutboundDataExtensions.GetEffectiveOutboundDataRestriction, and its dialog no longer
    /// offers the less strict levels.
    /// </remarks>
    public OutboundDataRestriction MinimumOutboundDataRestriction { get; set; } = ManagedConfiguration.Register(configSelection, n => n.MinimumOutboundDataRestriction, OutboundDataRestriction.UNRESTRICTED);
}