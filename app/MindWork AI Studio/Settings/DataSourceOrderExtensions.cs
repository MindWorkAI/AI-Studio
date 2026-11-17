namespace AIStudio.Settings;

public static class DataSourceOrderExtensions
{
    /// <param name="dataSources">The data sources to sort.</param>
    extension(IEnumerable<IDataSource> dataSources)
    {
        /// <summary>
        /// Sorts data sources in a fixed order: by their number, then by their ID.
        /// </summary>
        /// <remarks>
        /// The same data sources always come in the same order, however the settings list them, the
        /// checks return them, or an agent selects them. Semantic Search offers them this way, because
        /// the providers cache a request from its beginning, and the tools are part of that beginning,
        /// see IToolImplementation.ResolveFunctionAsync. DataSourceRequirements names the first of
        /// several equally strict data sources this way, so that both ways of searching name the same.
        /// </remarks>
        /// <returns>The data sources in their fixed order.</returns>
        public IReadOnlyList<IDataSource> InFixedOrder() => dataSources
            .OrderBy(dataSource => dataSource.Num)
            .ThenBy(dataSource => dataSource.Id, StringComparer.Ordinal)
            .ToList();
    }
}