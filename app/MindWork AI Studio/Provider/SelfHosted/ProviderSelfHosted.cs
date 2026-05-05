using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider.SelfHosted;

public sealed class ProviderSelfHosted(Host host, string hostname) : BaseProvider(LLMProviders.SELF_HOSTED, $"{hostname}{host.BaseURL()}", LOGGER)
{
    private static readonly ILogger<ProviderSelfHosted> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderSelfHosted>();
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderSelfHosted).Namespace, nameof(ProviderSelfHosted));

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.SELF_HOSTED.ToName();
    
    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Self-hosted";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => host is Host.OLLAMA or Host.LM_STUDIO or Host.VLLM;
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "self-hosted provider",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters) =>
                           {
                               // Build the list of messages. The image format depends on the host:
                               // - Ollama uses the direct image URL format: { "type": "image_url", "image_url": "data:..." }
                               // - LM Studio, vLLM, and llama.cpp use the nested image URL format: { "type": "image_url", "image_url": { "url": "data:..." } }
                               var messages = host switch
                               {
                                   Host.OLLAMA => await chatThread.Blocks.BuildMessagesUsingDirectImageUrlAsync(this.Provider, chatModel),
                                   _ => await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel),
                               };

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
                           isTryingSecret: true,
                           requestPath: host.ChatURL(),
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
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER, isTrying: true);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, host, token);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Provider.Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER, isTrying: true);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, host, token: token, texts: texts);
    }
    
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.LLAMA_CPP:
                    // Right now, llama.cpp only supports one model.
                    // There is no API to list the model(s).
                    return ModelLoadResult.FromModels([ new Provider.Model("as configured by llama.cpp", null) ]);
            
                case Host.LM_STUDIO:
                case Host.OLLAMA:
                case Host.VLLM:
                    return await this.LoadModels( SecretStoreType.LLM_PROVIDER, ["embed"], [], token, apiKeyProvisional);
            }

            return ModelLoadResult.FromModels([]);
        }
        catch(Exception e)
        {
            LOGGER.LogError($"Failed to load text models from self-hosted provider: {e.Message}");
            return ModelLoadResult.Failure(ModelLoadFailureReason.UNKNOWN, e.Message);
        }
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.LM_STUDIO:
                case Host.OLLAMA:
                case Host.VLLM:
                    return await this.LoadModels( SecretStoreType.EMBEDDING_PROVIDER, [], ["embed"], token, apiKeyProvisional);
            }

            return ModelLoadResult.FromModels([]);
        }
        catch(Exception e)
        {
            LOGGER.LogError($"Failed to load text models from self-hosted provider: {e.Message}");
            return ModelLoadResult.Failure(ModelLoadFailureReason.UNKNOWN, e.Message);
        }
    }
    
    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.WHISPER_CPP:
                    return ModelLoadResult.FromModels(
                    [
                        new Provider.Model("loaded-model", TB("Model as configured by whisper.cpp")),
                    ]);
                
                case Host.OLLAMA:
                case Host.VLLM:
                    return await this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, [], [], token, apiKeyProvisional);
                
                default:
                    return ModelLoadResult.FromModels([]);
            }
        }
        catch (Exception e)
        {
            LOGGER.LogError($"Failed to load transcription models from self-hosted provider: {e.Message}");
            return ModelLoadResult.Failure(ModelLoadFailureReason.UNKNOWN, e.Message);
        }
    }
    
    #endregion

    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string[] ignorePhrases, string[] filterPhrases, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = await this.GetModelLoadingSecretKey(storeType, apiKeyProvisional, true);
                    
        using var lmStudioRequest = new HttpRequestMessage(HttpMethod.Get, "models");
        if(secretKey is not null)
            lmStudioRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
                    
        using var lmStudioResponse = await this.HttpClient.SendAsync(lmStudioRequest, token);
        if(!lmStudioResponse.IsSuccessStatusCode)
            return FailedModelLoadResult(GetDefaultModelLoadFailureReason(lmStudioResponse), $"Status={(int)lmStudioResponse.StatusCode} {lmStudioResponse.ReasonPhrase}");

        var lmStudioModelResponse = await lmStudioResponse.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return SuccessfulModelLoadResult(lmStudioModelResponse.Data.
            Where(model => !ignorePhrases.Any(ignorePhrase => model.Id.Contains(ignorePhrase, StringComparison.InvariantCulture)) &&
                           filterPhrases.All( filter => model.Id.Contains(filter, StringComparison.InvariantCulture)))
            .Select(n => new Provider.Model(n.Id, null)));
    }
}