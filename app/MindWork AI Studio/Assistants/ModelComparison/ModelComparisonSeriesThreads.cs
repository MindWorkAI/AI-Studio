using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Creates the chats of a comparison series and readies them for a request.
/// </summary>
/// <remarks>
/// Both chats of a task get the same setup: the profile, the tools, and the data sources of the series, and the same
/// system prompt, which asks the model not to name itself. A model which says what it is would end the blindness of the
/// comparison. The series names the tools itself, so the chat runs them without a visible selection of its own.
/// </remarks>
public static class ModelComparisonSeriesThreads
{
    /// <summary>
    /// The system prompt of every chat of a comparison series, the same for both models.
    /// </summary>
    public const string SYSTEM_PROMPT = $"""
        {SystemPrompts.DEFAULT}

        The user compares you with another AI model without knowing which of the two answers. Do not state your name, the name of your model, or the company which made you, and do not hint at them. If the user asks, say that you cannot tell during the comparison.
        """;

    /// <summary>
    /// Creates a chat of a series, without any messages.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="name">The name of the chat, for the log and an export.</param>
    /// <returns>The chat; it gets its ID from the caller.</returns>
    public static ChatThread Create(ModelComparisonSeries series, string name) => new()
    {
        WorkspaceId = Guid.Empty,
        Name = name,
        IncludeDateTime = true,
        SystemPrompt = SYSTEM_PROMPT,
        SelectedProfile = series.ProfileId,
        SelectedChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE.Id,
        SelectedToolIds = [.. series.ToolIds],
        DataSourceOptions = DataSourceOptionsOf(series),
        Blocks = [],
    };

    /// <summary>
    /// Readies a chat of a series for the next request: what a chat does not store, and what the series decides anew.
    /// </summary>
    /// <param name="thread">The chat.</param>
    /// <param name="series">The series.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="toolIds">The tools of the series which the provider may use in this chat, see ToolRegistry.FilterToolIdsForProvider.</param>
    public static void PrepareForRequest(ChatThread thread, ModelComparisonSeries series, AIStudio.Settings.Provider provider, IEnumerable<string> toolIds)
    {
        thread.SystemPrompt = SYSTEM_PROMPT;
        thread.SystemPromptOrigin = null;
        thread.SelectedProvider = provider.Id;
        thread.SelectedProfile = series.ProfileId;
        thread.SelectedChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE.Id;
        thread.IncludeDateTime = true;
        thread.SelectedToolIds = [.. series.ToolIds];
        thread.DataSourceOptions = DataSourceOptionsOf(series);
        thread.RuntimeAssistantName = string.Empty;
        thread.RuntimeSelectedToolIds = [.. toolIds];
        thread.RuntimeToolsAreAssistantManaged = true;
    }

    /// <summary>
    /// Gets how a chat of a series searches its data sources: those of the series, in the way the series chose for
    /// both chats, without an automatic selection or validation which could treat the models differently.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The options.</returns>
    private static DataSourceOptions DataSourceOptionsOf(ModelComparisonSeries series) => new()
    {
        DisableDataSources = series.DataSourceIds.Count == 0,
        AutomaticDataSourceSelection = false,
        AutomaticValidation = false,
        RetrievalMode = series.RetrievalMode ?? DataSourceRetrievalMode.SEMANTIC_SEARCH,
        PreselectedDataSourceIds = [.. series.DataSourceIds],
    };
}