using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.GWDG;

public sealed class ProviderGWDG() : BaseProvider(LLMProviders.GWDG, "https://chat-ai.academiccloud.de/v1/", LOGGER)
{
    private static readonly ILogger<ProviderGWDG> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderGWDG>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GWDG.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "GWDG SAIA";
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "GWDG",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters) =>
                           {
                               // Build the list of messages:
                               var messages = await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel);

                               return new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,

                                   // Build the messages:
                                   // - First of all the system prompt
                                   // - Then none-empty user and AI messages
                                   Messages = [systemPrompt, ..messages],

                                   Stream = true,
                                   AdditionalApiParameters = apiParameters
                               };
                           },
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
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models = [..result.Models.Where(model => !model.Id.StartsWith("e5-mistral-7b-instruct", StringComparison.InvariantCultureIgnoreCase))]
        };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models = [..result.Models.Where(model => model.Id.StartsWith("e5-", StringComparison.InvariantCultureIgnoreCase))]
        };
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        // Source: https://docs.hpc.gwdg.de/services/saia/index.html#voice-to-text
        return Task.FromResult(ModelLoadResult.FromModels(
        [
            new Model("whisper-large-v2", "Whisper v2 Large"),
        ]));
    }
    
    #endregion

    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        var result = await this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data,
            token,
            apiKeyProvisional);

        if (!result.Success)
            LOGGER.LogWarning("Failed to load models for provider {ProviderId}. FailureReason: {FailureReason}. TechnicalDetails: {TechnicalDetails}", this.Id, result.FailureReason, result.TechnicalDetails);

        return result;
    }
}
