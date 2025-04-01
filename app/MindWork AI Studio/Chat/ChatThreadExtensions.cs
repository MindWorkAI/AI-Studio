using AIStudio.Provider.SelfHosted;
using AIStudio.Settings.DataModel;

namespace AIStudio.Chat;

public static class ChatThreadExtensions
{
    /// <summary>
    /// Checks if the specified provider is allowed for the chat thread.
    /// </summary>
    /// <remarks>
    /// We don't check if the provider is allowed to use the data sources of the chat thread.
    /// That kind of check is done in the RAG process itself.<br/><br/>
    /// 
    /// One thing which is not so obvious: after RAG was used on this thread, the entire chat
    /// thread is kind of a data source by itself. Why? Because the augmentation data collected
    /// from the data sources is stored in the chat thread. This means we must check if the
    /// selected provider is allowed to use this thread's data.
    /// </remarks>
    /// <param name="chatThread">The chat thread to check.</param>
    /// <param name="provider">The provider to check.</param>
    /// <returns>True, when the provider is allowed for the chat thread. False, otherwise.</returns>
    public static bool IsLLMProviderAllowed<T>(this ChatThread? chatThread, T provider)
    {
        // No chat thread available means we have a new chat. That's fine:
        if (chatThread is null)
            return true;
        
        // The chat thread is available, but the data security is not specified.
        // Means, we never used RAG or RAG was enabled, but no data sources were selected.
        // That's fine as well:
        if (chatThread.DataSecurity is DataSourceSecurity.NOT_SPECIFIED)
            return true;

        //
        // Is the provider self-hosted?
        //
        var isSelfHostedProvider = provider switch
        {
            ProviderSelfHosted => true,
            AIStudio.Settings.Provider p => p.IsSelfHosted,
            
            _ => false,
        };
        
        //
        // Check the chat data security against the selected provider:
        //
        return isSelfHostedProvider switch
        {
            // The provider is self-hosted -- we can use any data source:
            true => true,
            
            // The provider is not self-hosted -- it depends on the data security of the chat thread:
            false => chatThread.DataSecurity is not DataSourceSecurity.SELF_HOSTED,
        };
    }
}