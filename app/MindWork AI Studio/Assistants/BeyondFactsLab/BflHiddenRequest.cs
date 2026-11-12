using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Sends a request which the person never sees, in a hidden copy of a conversation of the lab, e.g., for a work state
/// or a result, or in a hidden chat of its own, e.g., for a research agent.
/// </summary>
/// <remarks>
/// The copy carries the system prompt and the messages of the conversation, so the model knows all the step knows.
/// The lab never stores it, and the conversation itself stays as it is.
/// </remarks>
public static class BflHiddenRequest
{
    /// <summary>
    /// Sends a request in the hidden copy of a conversation and waits for the whole answer.
    /// </summary>
    /// <param name="hidden">The hidden copy of the conversation, which keeps the request and the answer.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="text">The request.</param>
    /// <param name="images">The images of the material, which go along with the first message.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The answer, empty when the provider failed.</returns>
    public static async Task<string> AskAsync(ChatThread hidden, AIStudio.Settings.Provider provider, string text, IReadOnlyList<FileAttachmentImage> images, CancellationToken token) => (await AskForAnswerAsync(hidden, provider, text, images, token)).Text;

    /// <summary>
    /// Sends a request in a hidden chat and waits for the whole answer, together with what the tools did meanwhile.
    /// </summary>
    /// <param name="hidden">The hidden chat, which keeps the request and the answer.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="text">The request.</param>
    /// <param name="images">The images of the material, which go along with the first message.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The answer with the calls of the tools and their sources; its text is empty when the provider failed.</returns>
    public static async Task<ContentText> AskForAnswerAsync(ChatThread hidden, AIStudio.Settings.Provider provider, string text, IReadOnlyList<FileAttachmentImage> images, CancellationToken token)
    {
        var request = new ContentText { Text = text };
        hidden.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = request,
            HideFromUser = true
        });

        var answer = new ContentText { InitialRemoteWait = true };
        hidden.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = answer,
            HideFromUser = true
        });

        await answer.CreateFromProviderAsync(provider.CreateProvider(), provider.Model, request, BflConversationService.WithImages(hidden, images), token);
        return answer;
    }
}