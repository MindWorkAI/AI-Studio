using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Models;
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
                    return await this.LoadLlamaCppTextModels(["embed"], [], apiKeyProvisional, token);
            
                case Host.LM_STUDIO:
                case Host.OLLAMA:
                case Host.VLLM:
                    return await this.LoadModels( SecretStoreType.LLM_PROVIDER, ["embed"], [], apiKeyProvisional, token);
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
                    return await this.LoadModels( SecretStoreType.EMBEDDING_PROVIDER, [], ["embed"], apiKeyProvisional, token);
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
                    return await this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, [], [], apiKeyProvisional, token);
                
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

    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string[] ignorePhrases, string[] filterPhrases, string? apiKeyProvisional, CancellationToken token)
    {
        var secretKey = await this.GetModelLoadingSecretKey(storeType, apiKeyProvisional, true);

        try
        {
            using var lmStudioRequest = new HttpRequestMessage(HttpMethod.Get, "models");
            if(secretKey is not null)
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

            return SuccessfulModelLoadResult(models.
                Where(model => !string.IsNullOrWhiteSpace(model.Id) &&
                               !ignorePhrases.Any(ignorePhrase => model.Id.Contains(ignorePhrase, StringComparison.InvariantCulture)) &&
                               filterPhrases.All( filter => model.Id.Contains(filter, StringComparison.InvariantCulture)))
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

        var modelLoadResult = await this.LoadLlamaCppTextModels(["embed"], [], null, token);
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

    private async Task<ModelLoadResult> LoadLlamaCppTextModels(string[] ignorePhrases, string[] filterPhrases, string? apiKeyProvisional, CancellationToken token)
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
                    .Where(model => IsMatchingLlamaCppTextModel(model, ignorePhrases, filterPhrases))
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
    /// <remarks>
    /// A window of zero or less is dropped rather than repaired. An engine answering that way is
    /// telling us something we cannot interpret, and falling back to what the rules say about the
    /// weights is the one answer nobody has to invent.
    /// </remarks>
    /// <param name="models">The models exactly as the engine listed them.</param>
    /// <returns>One listing per model the engine said something usable about.</returns>
    private static IEnumerable<ModelListing> ListingsOf(IEnumerable<Model> models)
    {
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Id) || model.ContextWindowTokens is not > 0)
                continue;

            yield return new(model.Id, ContextWindow.Of(model.ContextWindowTokens.Value));
        }
    }

    private static bool IsMatchingLlamaCppTextModel(Model model, string[] ignorePhrases, string[] filterPhrases)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
            return false;

        if (ignorePhrases.Any(ignorePhrase => model.Id.Contains(ignorePhrase, StringComparison.InvariantCultureIgnoreCase)))
            return false;

        if (!filterPhrases.All(filter => model.Id.Contains(filter, StringComparison.InvariantCultureIgnoreCase)))
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
