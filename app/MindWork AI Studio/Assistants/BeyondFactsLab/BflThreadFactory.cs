using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Creates the chats of the BeyondFacts Lab and readies them for a request.
/// </summary>
/// <remarks>
/// A chat of the lab has no profile, no chat template, and no data sources: its context is the prompt of the lab,
/// the guide, and the details of the preparation. The lab manages its tools itself; so far, it offers none.
/// </remarks>
public static class BflThreadFactory
{
    /// <summary>
    /// Creates a chat of the lab, without any messages.
    /// </summary>
    /// <param name="location">Where the chat belongs, e.g., to a step of a preparation.</param>
    /// <returns>The chat.</returns>
    public static ChatThread Create(BflThreadLocation location) => new()
    {
        ChatId = location.ChatId,
        WorkspaceId = Guid.Empty,
        Name = location.ChatName,
        IncludeDateTime = true,
        SelectedProfile = Profile.NO_PROFILE.Id,
        SelectedChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE.Id,
        SelectedToolIds = [],
        Blocks = []
    };

    /// <summary>
    /// Readies a chat for the next request: its prompt and provider, and what a chat does not store.
    /// </summary>
    /// <remarks>
    /// The system prompt is put together anew for every request, so a changed detail or form of address counts from
    /// the next answer on.
    /// </remarks>
    /// <param name="thread">The chat.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="systemPrompt">The system prompt of the chat, see BflPromptComposer.</param>
    public static void PrepareForRequest(ChatThread thread, AIStudio.Settings.Provider provider, string systemPrompt)
    {
        thread.SystemPrompt = systemPrompt;
        thread.SelectedProvider = provider.Id;
        thread.SelectedProfile = Profile.NO_PROFILE.Id;
        thread.SelectedChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE.Id;
        thread.DataSourceOptions.DisableDataSources = true;
        thread.RuntimeComponent = Tools.Components.BEYOND_FACTS_LAB_ASSISTANT;
        thread.RuntimeAssistantName = string.Empty;
        thread.RuntimeSelectedToolIds = [];
        thread.RuntimeToolsAreAssistantManaged = true;
    }
}