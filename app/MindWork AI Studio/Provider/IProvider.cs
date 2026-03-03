using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Provider;

/// <summary>
/// A common interface for all providers.
/// </summary>
public interface IProvider
{
    /// <summary>
    /// The provider type.
    /// </summary>
    public LLMProviders Provider { get; }
    
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
    /// The additional API parameters.
    /// </summary>
    public string AdditionalJsonApiParameters { get; }
    
    /// <summary>
    /// Starts a chat completion stream.
    /// </summary>
    /// <param name="chatModel">The model to use for chat completion.</param>
    /// <param name="chatThread">The chat thread to continue.</param>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat completion stream.</returns>
    public IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, CancellationToken token = default);
    
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
    /// Transcribe an audio file.
    /// </summary>
    /// <param name="transcriptionModel">The model to use for transcription.</param>
    /// <param name="audioFilePath">The audio file path.</param>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>>The transcription result.</returns>
    public Task<string> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default);
        
    /// <summary>
    /// Embed a text file.
    /// </summary>
    /// <param name="embeddingModel">The model to use for embedding.</param>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="token">The cancellation token.</param>
    /// /// <param name="texts">A single string or a list of strings to embed.</param>
    /// <returns>>The embedded text as a single vector or as a list of vectors.</returns>
    public Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts);
    
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
    /// Load all possible transcription models that can be used with this provider.
    /// </summary>
    /// <param name="apiKeyProvisional">The provisional API key to use. Useful when the user is adding a new provider. When null, the stored API key is used.</param>
    /// <param name="token">>The cancellation token.</param>
    /// <returns>>The list of transcription models.</returns>
    public Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default);
}