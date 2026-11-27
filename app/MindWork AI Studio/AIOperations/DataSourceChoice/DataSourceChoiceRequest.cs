using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.AIOperations.DataSourceChoice;

/// <summary>
/// A prompt for which a model chooses the data sources to search.
/// </summary>
/// <param name="SystemPrompt">The system prompt of the chat.</param>
/// <param name="LastUserPrompt">The last prompt of the user.</param>
/// <param name="DataSources">The data sources to choose from, which the chat may use.</param>
public sealed record DataSourceChoiceRequest(string SystemPrompt, IContent LastUserPrompt, IReadOnlyList<IDataSource> DataSources);