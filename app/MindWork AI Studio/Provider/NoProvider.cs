using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Provider;

public class NoProvider : IProvider
{
    #region Implementation of IProvider

    public string Id => "none";

    public string InstanceName { get; set; } = "None";

    public Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult<IEnumerable<Model>>([]);

    public Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult<IEnumerable<Model>>([]);
    
    public Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult<IEnumerable<Model>>([]);

    public async IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatChatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.FromResult(0);
        yield break;
    }

    public async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.FromResult(0);
        yield break;
    }

    public IReadOnlyCollection<Capability> GetModelCapabilities(Model model) => [ Capability.NONE ];

    #endregion
}