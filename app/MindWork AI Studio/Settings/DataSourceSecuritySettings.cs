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
}