using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Settings;

using Microsoft.JSInterop;

using MudBlazor;

namespace AIStudio.Provider;

public class NoProvider : IProvider
{
    #region Implementation of IProvider

    public string Id => "none";

    public string InstanceName { get; set; } = "None";

    public Task<IList<Model>> GetTextModels(IJSRuntime jsRuntime, SettingsManager settings, CancellationToken token = default) => Task.FromResult<IList<Model>>(new List<Model>());

    public Task<IList<Model>> GetImageModels(IJSRuntime jsRuntime, SettingsManager settings, CancellationToken token = default) => Task.FromResult<IList<Model>>(new List<Model>());

    public async IAsyncEnumerable<string> StreamChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, ChatThread chatChatThread, [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.FromResult(0);
        yield break;
    }

    public async IAsyncEnumerable<ImageURL> StreamImageCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.FromResult(0);
        yield break;
    }

    #endregion
}