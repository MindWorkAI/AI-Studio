using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.RAG;

/// <summary>
/// What a chat has to require from now on, because passages of data sources reached the model.
/// </summary>
/// <remarks>
/// Both ways of searching the data sources ask here, Semantic Search and the classic RAG process,
/// so that both demand the same of the same data sources: two answers which drift apart would leave
/// one of the ways open.
/// </remarks>
/// <param name="Confidence">The provider confidence the chat requires from now on.</param>
/// <param name="Security">The data security the chat requires from now on; NOT_SPECIFIED leaves it as it was.</param>
/// <param name="OutboundData">Where the chat may still send data, and which data source demands it.</param>
public sealed record DataSourceRequirements(ConfidenceLevel Confidence, DataSourceSecurity Security, OutboundDataRequirement OutboundData)
{
    /// <summary>
    /// Demands nothing: what a search requires which brought no passage into the chat.
    /// </summary>
    public static readonly DataSourceRequirements NONE = new(ConfidenceLevel.NONE, DataSourceSecurity.NOT_SPECIFIED, OutboundDataRequirement.NONE);

    /// <summary>
    /// What the passages of these data sources demand.
    /// </summary>
    /// <remarks>
    /// Only the data sources with passages count: a data source which found nothing brought nothing
    /// into the chat. A data source which only self-hosted providers may use restricts the chat to
    /// those. Of the outbound data restrictions, the strictest wins, see
    /// DataSourceOutboundDataExtensions.GetEffectiveOutboundDataRestriction. Of several equally strict
    /// data sources, the first in their fixed order is named, see DataSourceOrderExtensions.InFixedOrder,
    /// so that the name does not depend on the order in which one way of searching got them.
    /// </remarks>
    /// <param name="dataSources">The data sources searched.</param>
    /// <param name="passageCounts">How many passages of each data source reached the model, in the same order.</param>
    /// <param name="organizationMinimum">The least strict outbound data restriction the organization allows, see DataSourceSecuritySettings.MinimumOutboundDataRestriction.</param>
    /// <returns>What the chat requires from now on; NONE when no data source brought a passage.</returns>
    /// <exception cref="ArgumentException">The counts do not match the data sources.</exception>
    public static DataSourceRequirements Of(IReadOnlyList<IDataSource> dataSources, IReadOnlyList<int> passageCounts, OutboundDataRestriction organizationMinimum)
    {
        if (passageCounts.Count != dataSources.Count)
            throw new ArgumentException($"There are {passageCounts.Count} passage counts for {dataSources.Count} data sources.", nameof(passageCounts));

        var contributingDataSources = dataSources.Where((_, index) => passageCounts[index] > 0).InFixedOrder();
        if (contributingDataSources.Count == 0)
            return NONE;

        var outboundData = OutboundDataRequirement.NONE;
        foreach (var dataSource in contributingDataSources)
            outboundData = outboundData.StricterOf(new(dataSource.GetEffectiveOutboundDataRestriction(organizationMinimum), OutboundDataOrigin.DATA_SOURCE, dataSource.Id));

        var requiresSelfHosted = contributingDataSources.OfType<IExternalDataSource>().Any(dataSource => dataSource.SecurityPolicy is DataSourceSecurity.SELF_HOSTED);
        return new(contributingDataSources.GetRequiredConfidenceLevel(), requiresSelfHosted ? DataSourceSecurity.SELF_HOSTED : DataSourceSecurity.ALLOW_ANY, outboundData);
    }
}