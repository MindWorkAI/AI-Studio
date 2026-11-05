namespace AIStudio.Tools.RAG;

/// <summary>
/// Which passages of the classic RAG process fit into its budget, see PassageBudget.SelectAsync.
/// </summary>
/// <param name="Contexts">The passages which fit, grouped by data source in the order the data sources came in, and within a data source in the order of their rank. A passage which had to be shortened is here in its shorter form.</param>
/// <param name="LeftOutCount">How many passages did not fit.</param>
public sealed record PassageSelection(IReadOnlyList<PreparedRetrievalContext> Contexts, int LeftOutCount);