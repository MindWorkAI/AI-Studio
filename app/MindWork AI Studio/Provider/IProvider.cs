using AIStudio.Chat;
using AIStudio.Settings;

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
    public string InstanceName { get; }
    
    /// <summary>
    /// Starts a chat completion stream.
    /// </summary>
    /// <param name="chatModel">The model to use for chat completion.</param>
    /// <param name="chatThread">The chat thread to continue.</param>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat completion stream.</returns>
    public IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, CancellationToken token = default);
    
    /// <summary>
    /// Starts an image completion stream.
    /// </summary>
    /// <param name="imageModel">The model to use for image completion.</param>
    /// <param name="promptPositive">The positive prompt.</param>
    /// <param name="promptNegative">The negative prompt.</param>
    /// <param name="referenceImageURL">The reference image URL.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The image completion stream.</returns>
    public IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, CancellationToken token = default);
    
    /// <summary>
    /// Load all possible text models that can be used with this provider.
    /// </summary>
    /// <param name="apiKeyProvisional">The provisional API key to use. Useful when the user is adding a new provider. When null, the stored API key is used.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The list of text models.</returns>
    public Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default);

    /// <summary>
    /// Load all possible image models that can be used with this provider.
    /// </summary>
    /// <param name="apiKeyProvisional">The provisional API key to use. Useful when the user is adding a new provider. When null, the stored API key is used.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The list of image models.</returns>
    public Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default);

    /// <summary>
    /// Load all possible embedding models that can be used with this provider.
    /// </summary>
    /// <param name="apiKeyProvisional">The provisional API key to use. Useful when the user is adding a new provider. When null, the stored API key is used.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The list of embedding models.</returns>
    public Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    /// <summary>
    /// Get the capabilities of a model.
    /// </summary>
    /// <param name="model">The model to get the capabilities for.</param>
    /// <returns>The capabilities of the model.</returns>
    public IReadOnlyCollection<Capability> GetModelCapabilities(Model model);
}