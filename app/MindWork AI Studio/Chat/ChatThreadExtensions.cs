namespace AIStudio.Chat;

public static class ChatThreadExtensions
{
    /// <summary>
    /// Checks if the specified provider is allowed for the chat thread.
    /// </summary>
    /// <remarks>
    /// A provider the organization prohibited is never allowed, whatever the chat thread.<br/><br/>
    ///
    /// We don't check if the provider is allowed to use the data sources of the chat thread.
    /// That kind of check is done when the available data sources are resolved.<br/><br/>
    ///
    /// One thing which is not so obvious: after RAG was used on this thread, the entire chat
    /// thread is kind of a data source by itself. Why? Because the augmentation data collected
    /// from the data sources is stored in the chat thread. This means we must check if the
    /// selected provider is allowed to use this thread's data security and confidence level.
    /// The check itself is ChatRestrictionsExtensions.IsLLMProviderAllowed, which content that left
    /// the thread is checked with as well.
    /// </remarks>
    /// <param name="chatThread">The chat thread to check; null for a new chat, which demands nothing yet.</param>
    /// <param name="provider">The provider to check.</param>
    /// <returns>True, when the provider is allowed for the chat thread. False, otherwise.</returns>
    public static bool IsLLMProviderAllowed<T>(this ChatThread? chatThread, T provider) => (chatThread?.CaptureRestrictions() ?? ChatRestrictions.NONE).IsLLMProviderAllowed(provider);
}
