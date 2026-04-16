using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Mistral;

public sealed class ProviderMistral() : BaseProvider(LLMProviders.MISTRAL, "https://api.mistral.ai/v1/", LOGGER)
{
    private static readonly ILogger<ProviderMistral> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderMistral>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.MISTRAL.ToName();
    
    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Mistral";
    
    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "Mistral",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters) =>
                           {
                               if (TryPopBoolParameter(apiParameters, "safe_prompt", out var parsedSafePrompt))
                                   apiParameters["safe_prompt"] = parsedSafePrompt;

                               if (TryPopIntParameter(apiParameters, "random_seed", out var parsedRandomSeed))
                                   apiParameters["random_seed"] = parsedRandomSeed;

                               // Build the list of messages:
                               var messages = await chatThread.Blocks.BuildMessagesUsingDirectImageUrlAsync(this.Provider, chatModel);

                               return new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,

                                   // Build the messages:
                                   // - First of all the system prompt
                                   // - Then none-empty user and AI messages
                                   Messages = [systemPrompt, ..messages],

                                   // Right now, we only support streaming completions:
                                   Stream = true,
                                   AdditionalApiParameters = apiParameters
                               };
                           },
                           token: token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override async Task<string> TranscribeAudioAsync(Provider.Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }

    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Provider.Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModelList(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
        if(!modelResponse.Success)
            return modelResponse;
        
        return modelResponse with
        {
            Models =
            [
                ..modelResponse.Models.Where(n =>
                    !n.Id.StartsWith("code", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("embed", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase))
            ]
        };
    }
    
    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModelList(SecretStoreType.EMBEDDING_PROVIDER, apiKeyProvisional, token);
        if(!modelResponse.Success)
            return modelResponse;
        
        return modelResponse with
        {
            Models = [..modelResponse.Models.Where(n => n.Id.Contains("embed", StringComparison.InvariantCulture))]
        };
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        // Source: https://docs.mistral.ai/capabilities/audio_transcription
        return Task.FromResult(ModelLoadResult.FromModels(
        [
            new Provider.Model("voxtral-mini-latest", "Voxtral Mini Latest"),
        ]));
    }
    
    #endregion
    
    private Task<ModelLoadResult> LoadModelList(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data.Select(n => new Provider.Model(n.Id, null)),
            token,
            apiKeyProvisional);
    }
}