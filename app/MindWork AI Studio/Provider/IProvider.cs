using AIStudio.Settings;
using Microsoft.JSInterop;

namespace AIStudio.Provider;

/// <summary>
/// A common interface for all providers.
/// </summary>
public interface IProvider
{
    /// <summary>
    /// The provider's ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The provider's instance name. Useful for multiple instances of the same provider,
    /// e.g., to distinguish between different OpenAI API keys.
    /// </summary>
    public string InstanceName { get; set; }
    
    public IAsyncEnumerable<string> GetChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, Thread chatThread);
    
    public Task<IList<Model>> GetModels(IJSRuntime jsRuntime, SettingsManager settings);
}