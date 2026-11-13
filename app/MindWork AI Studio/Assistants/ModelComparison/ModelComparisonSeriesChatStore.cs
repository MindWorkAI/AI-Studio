using AIStudio.Chat;
using AIStudio.Chat.Embedded;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Lets the embedded chats of a comparison series live in the folder of the series.
/// </summary>
/// <param name="store">The store of the assistant.</param>
/// <param name="seriesId">The ID of the series.</param>
public sealed class ModelComparisonSeriesChatStore(ModelComparisonStore store, Guid seriesId) : IEmbeddedChatStore
{
    /// <inheritdoc />
    public Task<ChatThread?> LoadThreadAsync(Guid chatId, CancellationToken token = default) => store.LoadSeriesChatAsync(seriesId, chatId, token);

    /// <inheritdoc />
    public Task SaveThreadAsync(ChatThread thread, CancellationToken token = default) => store.SaveSeriesChatAsync(seriesId, thread, token);
}