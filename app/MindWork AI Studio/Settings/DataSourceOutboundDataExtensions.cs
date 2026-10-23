using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

public static class DataSourceOutboundDataExtensions
{
    /// <summary>
    /// The level of a new data source, and of one stored before data sources had a level.
    /// </summary>
    /// <remarks>
    /// The same level as for internal web pages: no web search, and no addresses the model chooses
    /// itself outside the services configured in AI Studio, while the links which stand in the chat
    /// stay readable. A data source holds the documents of the user or of their organization, so a
    /// chat which found something in it must not carry that out in a search query.
    /// </remarks>
    public const OutboundDataRestriction DEFAULT_OUTBOUND_DATA_RESTRICTION = OutboundDataRestriction.ONLY_LINKS_FROM_CHAT;

    /// <summary>
    /// The least strict level a data source with this security policy may have.
    /// </summary>
    /// <remarks>
    /// A data source which only self-hosted providers may see must not reach a third party through a
    /// tool either, so it allows the services configured in AI Studio only. The internal pages among
    /// them stay readable: the wiki, and the allowed private hosts of Read Web Page.
    /// </remarks>
    /// <param name="securityPolicy">The security policy of the data source; NOT_SPECIFIED for one which has none, such as a local data source.</param>
    /// <param name="organizationMinimum">The least strict level the organization allows, see DataSourceSecuritySettings.MinimumOutboundDataRestriction.</param>
    /// <returns>The least strict level allowed.</returns>
    public static OutboundDataRestriction GetMinimumOutboundDataRestriction(DataSourceSecurity securityPolicy, OutboundDataRestriction organizationMinimum) => securityPolicy is DataSourceSecurity.SELF_HOSTED
        ? organizationMinimum.StricterOf(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)
        : organizationMinimum;

    /// <summary>
    /// The least strict level this data source may have.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="organizationMinimum">The least strict level the organization allows, see DataSourceSecuritySettings.MinimumOutboundDataRestriction.</param>
    /// <returns>The least strict level allowed.</returns>
    public static OutboundDataRestriction GetMinimumOutboundDataRestriction(this IDataSource dataSource, OutboundDataRestriction organizationMinimum) =>
        GetMinimumOutboundDataRestriction(dataSource is IExternalDataSource externalDataSource ? externalDataSource.SecurityPolicy : DataSourceSecurity.NOT_SPECIFIED, organizationMinimum);

    /// <summary>
    /// Where a chat may still send data, once passages of this data source reached it.
    /// </summary>
    /// <remarks>
    /// The level of the data source, tightened to the least strict one it may have. This holds at
    /// runtime, not only in the dialog: a data source stored before, or one a configuration plugin
    /// provides, is never edited, so its stored level may be less strict.
    /// </remarks>
    /// <param name="dataSource">The data source.</param>
    /// <param name="organizationMinimum">The least strict level the organization allows, see DataSourceSecuritySettings.MinimumOutboundDataRestriction.</param>
    /// <returns>The level which applies.</returns>
    public static OutboundDataRestriction GetEffectiveOutboundDataRestriction(this IDataSource dataSource, OutboundDataRestriction organizationMinimum) =>
        dataSource.OutboundDataRestriction.StricterOf(dataSource.GetMinimumOutboundDataRestriction(organizationMinimum));
}