using AIStudio.Settings;

using Microsoft.JSInterop;

namespace AIStudio.Provider;

public class NoProvider : IProvider
{
    #region Implementation of IProvider

    public string Id => "none";

    public string InstanceName { get; set; } = "None";

    public async IAsyncEnumerable<string> GetChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, Thread chatThread)
    {
        await Task.CompletedTask;
        yield return "";
    }

    public Task<IList<Model>> GetModels(IJSRuntime jsRuntime, SettingsManager settings) => Task.FromResult<IList<Model>>(new List<Model>());

    #endregion
}