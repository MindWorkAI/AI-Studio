using AIStudio.Settings;
using Microsoft.JSInterop;

namespace AIStudio.Provider;

public interface IProvider
{
    public string Id { get; }

    public string InstanceName { get; set; }
    
    public IAsyncEnumerable<string> GetChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, Thread chatThread);
    
    public Task<IList<Model>> GetModels(IJSRuntime jsRuntime, SettingsManager settings);
}