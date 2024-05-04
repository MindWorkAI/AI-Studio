using AIStudio.Chat;
using AIStudio.Settings;
using Microsoft.JSInterop;

using MudBlazor;

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
    
    /// <summary>
    /// Starts a chat completion stream.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="settings">The settings manager to access the API key.</param>
    /// <param name="chatModel">The model to use for chat completion.</param>
    /// <param name="chatThread">The chat thread to continue.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat completion stream.</returns>
    public IAsyncEnumerable<string> StreamChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, ChatThread chatThread, CancellationToken token = default);
    
    /// <summary>
    /// Starts an image completion stream.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="settings">The settings manager to access the API key.</param>
    /// <param name="imageModel">The model to use for image completion.</param>
    /// <param name="promptPositive">The positive prompt.</param>
    /// <param name="promptNegative">The negative prompt.</param>
    /// <param name="referenceImageURL">The reference image URL.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The image completion stream.</returns>
    public IAsyncEnumerable<ImageURL> StreamImageCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, CancellationToken token = default);
    
    /// <summary>
    /// Load all possible text models that can be used with this provider.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="settings">The settings manager to access the API key.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The list of text models.</returns>
    public Task<IList<Model>> GetTextModels(IJSRuntime jsRuntime, SettingsManager settings, CancellationToken token = default);
    
    /// <summary>
    /// Load all possible image models that can be used with this provider.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="settings">The settings manager to access the API key.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The list of image models.</returns>
    public Task<IList<Model>> GetImageModels(IJSRuntime jsRuntime, SettingsManager settings, CancellationToken token = default);
}