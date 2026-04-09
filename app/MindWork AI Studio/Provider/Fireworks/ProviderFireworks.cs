using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Fireworks;

public class ProviderFireworks() : BaseProvider(LLMProviders.FIREWORKS, "https://api.fireworks.ai/inference/v1/", LOGGER)
{
    private static readonly ILogger<ProviderFireworks> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderFireworks>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.FIREWORKS.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Fireworks.ai";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ResponseStreamLine, ChatCompletionAnnotationStreamLine>(
                           "Fireworks",
                           chatModel,
                           chatThread,
                           settingsManager,
                           () => chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel),
                           (systemPrompt, messages, apiParameters, stream, tools) =>
                               Task.FromResult(new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,
                                   Messages = [systemPrompt, ..messages],
                                   Stream = stream,
                                   Tools = tools,
                                   ParallelToolCalls = tools is null ? null : true,
                                   AdditionalApiParameters = apiParameters
                               }),
                           token: token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override async Task<string> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }
    
    /// <inhertidoc />
    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        // Source: https://docs.fireworks.ai/api-reference/audio-transcriptions#param-model
        return Task.FromResult<IEnumerable<Model>>(
            new List<Model>
            {
                new("whisper-v3", "Whisper v3"),
                // new("whisper-v3-turbo", "Whisper v3 Turbo"), // does not work
            });
    }
    
    #endregion
}
