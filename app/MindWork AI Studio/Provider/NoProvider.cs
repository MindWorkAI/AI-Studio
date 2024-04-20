using AIStudio.Settings;

using Microsoft.JSInterop;

namespace AIStudio.Provider;

public class NoProvider : IProvider
{
    #region Implementation of IProvider

    public string Id => "none";

    public string InstanceName { get; set; } = "None";
    
    public IAsyncEnumerable<string> GetChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, Thread chatThread) => throw new NotImplementedException();

    public Task<IList<Model>> GetModels(IJSRuntime jsRuntime, SettingsManager settings) => throw new NotImplementedException();

    #endregion
}