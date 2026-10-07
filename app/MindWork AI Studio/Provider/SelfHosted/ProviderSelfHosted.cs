using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Models.Live;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider.SelfHosted;

public sealed class ProviderSelfHosted(Host host, string hostname) : BaseProvider(LLMProviders.SELF_HOSTED, new Uri($"{hostname}{host.BaseURL()}"), ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED, LOGGER)
{
    private static readonly ILogger<ProviderSelfHosted> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderSelfHosted>();
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderSelfHosted).Namespace, nameof(ProviderSelfHosted));

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.SELF_HOSTED.ToSecretId();
    
    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Self-hosted";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => host is Host.OLLAMA or Host.LM_STUDIO or Host.VLLM or Host.LLAMA_CPP;
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        var effectiveChatModel = await this.ResolveChatModelForRequest(chatModel, token);
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "self-hosted provider",
                           effectiveChatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters, tools) =>
                           {
                               // Build the list of messages. The image format depends on the host:
                               // - Ollama uses the direct image URL format: { "type": "image_url", "image_url": "data:..." }
                               // - LM Studio, vLLM, and llama.cpp use the nested image URL format: { "type": "image_url", "image_url": { "url": "data:..." } }
                               var messages = host switch
                               {
                                   Host.OLLAMA => await chatThread.Blocks.BuildMessagesUsingDirectImageUrlAsync(this.CreateSettingsProvider(effectiveChatModel)),
                                   _ => await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.CreateSettingsProvider(effectiveChatModel)),
                               };

                               return new ChatCompletionAPIRequest
                               {
                                   Model = effectiveChatModel.Id,

                                   // Build the messages:
                                   // - First of all the system prompt
                                   // - Then none-empty user and AI messages
                                   Messages = [systemPrompt, ..messages],

                                   // Right now, we only support streaming completions:
                                   Stream = true,
                                   Tools = tools,
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
    public override async Task<TranscriptionResult> TranscribeAudioAsync(Provider.Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER, isTrying: true);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, host, token);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Provider.Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER, isTrying: true);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, host, token: token, texts: texts);
    }
    
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.LLAMA_CPP:
                    return await this.LoadLlamaCppTextModels(apiKeyProvisional, token);

                case Host.LM_STUDIO:
                case Host.OLLAMA:
                case Host.VLLM:
                    var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
                    return result with
                    {
                        Models = [..result.Models.Where(model => model.IsChatModel(this.Provider))]
                    };
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
                    var result = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, apiKeyProvisional, token);
                    return result with
                    {
                        Models = [..result.Models.Where(model => model.IsEmbeddingModel(this.Provider))]
                    };
            }

            return ModelLoadResult.FromModels([]);
        }
        catch(Exception e)
        {
            LOGGER.LogError($"Failed to load embedding models from self-hosted provider: {e.Message}");
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
                
                //
                // These two answer the models endpoint with everything they serve, and nothing in
                // that answer says which of them listens. Asking what each model is made for is the
                // only thing standing between this list and every chat and embedding model of the
                // installation, which is what it used to hold. An engine running no speech model at
                // all therefore offers nothing here, and says so, rather than offering models which
                // would fail the moment audio reaches them.
                //
                case Host.OLLAMA:
                case Host.VLLM:
                    var result = await this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, apiKeyProvisional, token);
                    return result with
                    {
                        Models = [..result.Models.Where(model => model.IsTranscriptionModel(this.Provider))]
                    };

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

    /// <summary>
    /// Everything the engine lists, in the order it listed it.
    /// </summary>
    /// <remarks>
    /// What kind of model each of these is stays unanswered here. It used to be answered right in
    /// this method, by looking for the word "embed" in the name: the text models were the ones
    /// without it, the embedding models the ones with it. That reading lost bge-m3 and all-minilm,
    /// which say what they are through another word, and handed them to the chat list instead. The
    /// callers ask the shared model kind detection now, the way every other provider does.
    /// </remarks>
    /// <param name="storeType">Which key to send along.</param>
    /// <param name="apiKeyProvisional">A key from a dialog which has not stored it yet.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models the engine named, unsorted and unfiltered.</returns>
    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
    {
        var secretKey = await this.GetModelLoadingSecretKey(storeType, apiKeyProvisional, isTryingSecret: true);

        try
        {
            using var lmStudioRequest = new HttpRequestMessage(HttpMethod.Get, "models");

            // An empty token is worse than none at all: a proxy which enforces authentication
            // rejects an empty bearer with 401, where it would have let a request without any
            // authorization header through. The dialogs hand us their key field as it stands, so
            // an empty string arrives here whenever the user stored no key:
            if(!string.IsNullOrWhiteSpace(secretKey))
                lmStudioRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

            using var lmStudioResponse = await this.HttpClient.SendAsync(lmStudioRequest, token);
            if(!lmStudioResponse.IsSuccessStatusCode)
            {
                var responseBody = await lmStudioResponse.Content.ReadAsStringAsync(token);
                LOGGER.LogError("Model loading request failed with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", lmStudioResponse.StatusCode, lmStudioResponse.ReasonPhrase, responseBody);
                return FailedModelLoadResult(this.GetModelLoadFailureReason(lmStudioResponse, responseBody), $"Status={(int)lmStudioResponse.StatusCode} {lmStudioResponse.ReasonPhrase}; Body='{responseBody}'");
            }

            //
            // Read with the shared options, the way every other model list of this app is read.
            // This one route did without them, which quietly cost it every field an engine spells
            // in snake case: owned_by has been arriving as nothing all along, and the next field
            // somebody adds here would have gone the same way without anything failing.
            //
            var lmStudioModelResponse = await lmStudioResponse.Content.ReadFromJsonAsync<ModelsResponse>(JSON_SERIALIZER_OPTIONS, token);
            var models = lmStudioModelResponse.Data ?? [];

            //
            // What the engine said about its own models, taken from the whole list rather than
            // from what is offered below: a model filtered out here as an embedding model is still
            // a model somebody may have configured this instance with, and this list is the only
            // place its window is ever stated.
            //
            ListedModels.Shared.Report(this.ConfiguredProviderId, ListingsOf(models));

            return SuccessfulModelLoadResult(models
                .Where(model => !string.IsNullOrWhiteSpace(model.Id))
                .Select(n => new Provider.Model(n.Id, null)));
        }
        catch (Exception e) when (this.IsTimeoutException(e, token))
        {
            await this.SendTimeoutError("loading the available models");
            LOGGER.LogError(e, "Timed out while loading models from self-hosted provider '{ProviderInstanceName}'.", this.InstanceName);
            return FailedModelLoadResult(ModelLoadFailureReason.PROVIDER_UNAVAILABLE, e.Message);
        }
    }
    private async Task<Provider.Model> ResolveChatModelForRequest(Provider.Model chatModel, CancellationToken token)
    {
        if (host is not Host.LLAMA_CPP || !chatModel.IsSystemModel)
            return chatModel;

        var modelLoadResult = await this.LoadLlamaCppTextModels(null, token);
        if (!modelLoadResult.Success)
            return chatModel;

        var availableModels = modelLoadResult.Models
            .Where(model => !model.IsSystemModel && !string.IsNullOrWhiteSpace(model.Id))
            .ToList();

        if (modelLoadResult.Models.All(model => !model.IsSystemModel) && availableModels.Count is 0)
        {
            LOGGER.LogError("The llama.cpp provider '{ProviderInstanceName}' does not offer a usable text model. Please check your provider settings.", this.InstanceName);
            throw new ProviderRequestException(
                ProviderRequestFailureReason.NONE,
                string.Format(
                    TB("The llama.cpp provider '{0}' does not offer a usable text model. Please check your provider settings."),
                    this.InstanceName));
        }

        if (availableModels.Count is 1)
            return availableModels[0];

        if (availableModels.Count > 1)
        {
            LOGGER.LogError(
                "The llama.cpp provider '{ProviderInstanceName}' offers {ModelCount} models, but the configured model is the legacy system placeholder. The provider settings must be updated to select a specific model.",
                this.InstanceName,
                availableModels.Count);
            throw new ProviderRequestException(
                ProviderRequestFailureReason.NONE,
                string.Format(
                    TB("The llama.cpp provider '{0}' offers multiple models. Please open the provider settings and select the model to use."),
                    this.InstanceName));
        }

        return chatModel;
    }

    private async Task<ModelLoadResult> LoadLlamaCppTextModels(string? apiKeyProvisional, CancellationToken token)
    {
        var secretKey = await this.GetModelLoadingSecretKey(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, true);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "models");
            if (!string.IsNullOrWhiteSpace(secretKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

            using var response = await this.HttpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
                    return LlamaCppLegacyModelResult();

                LOGGER.LogError("llama.cpp model loading request failed with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", response.StatusCode, response.ReasonPhrase, responseBody);
                return FailedModelLoadResult(this.GetModelLoadFailureReason(response, responseBody), $"Status={(int)response.StatusCode} {response.ReasonPhrase}; Body='{responseBody}'");
            }

            try
            {
                var modelResponse = JsonSerializer.Deserialize<ModelsResponse>(responseBody, JSON_SERIALIZER_OPTIONS);
                var responseModels = modelResponse.Data?
                    .Where(model => !string.IsNullOrWhiteSpace(model.Id))
                    .ToList() ?? [];

                if (responseModels.Count is 0)
                    return LlamaCppLegacyModelResult();

                var models = responseModels
                    .Where(this.IsMatchingLlamaCppTextModel)
                    .Select(model => new Provider.Model(model.Id, null))
                    .ToList();

                return SuccessfulModelLoadResult(models);
            }
            catch (JsonException e)
            {
                LOGGER.LogWarning(e, "The llama.cpp model loading response could not be parsed. Falling back to the legacy system-configured model.");
                return LlamaCppLegacyModelResult();
            }
        }
        catch (Exception e) when (this.IsTimeoutException(e, token))
        {
            await this.SendTimeoutError("loading the available models");
            LOGGER.LogError(e, "Timed out while loading models from llama.cpp provider '{ProviderInstanceName}'.", this.InstanceName);
            return FailedModelLoadResult(ModelLoadFailureReason.PROVIDER_UNAVAILABLE, e.Message);
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Failed to load models from llama.cpp provider '{ProviderInstanceName}'.", this.InstanceName);
            return FailedModelLoadResult(ModelLoadFailureReason.UNKNOWN, e.Message);
        }
    }

    /// <summary>
    /// What an engine stated about the models it serves.
    /// </summary>
    /// <param name="models">The models exactly as the engine listed them.</param>
    /// <returns>One listing per model, which says nothing for the models the engine was silent about.</returns>
    private static IEnumerable<ModelListing> ListingsOf(IEnumerable<Model> models) => models.Select(model => ModelListing.For(model.Id, model.ContextWindowTokens));

    /// <summary>
    /// Whether this is a model somebody can chat with, as far as llama.cpp and the rules say.
    /// </summary>
    /// <remarks>
    /// Two sources, and both have to agree. What a model is made for comes from the shared rules,
    /// the same answer the other engines get. What the running build of it puts out comes from
    /// llama.cpp itself, which states the modalities on this route: an engine serving a model that
    /// answers in something other than text knows that before any rule about the name could.
    /// </remarks>
    /// <param name="model">The model as llama.cpp listed it.</param>
    /// <returns>True when both agree that it answers a chat in text.</returns>
    private bool IsMatchingLlamaCppTextModel(Model model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
            return false;

        if (!new Provider.Model(model.Id, null).IsChatModel(this.Provider))
            return false;

        var outputModalities = model.Architecture?.OutputModalities;
        if (outputModalities is { Length: > 0 } &&
            !outputModalities.Any(modality => string.Equals(modality, "text", StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static ModelLoadResult LlamaCppLegacyModelResult()
    {
        return ModelLoadResult.FromModels([ AIStudio.Provider.Model.SYSTEM_MODEL ]);
    }
}
