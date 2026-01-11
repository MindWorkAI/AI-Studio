using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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

    public override string Id => LLMProviders.SELF_HOSTED.ToName();
    
    public override string InstanceName { get; set; } = "Self-hosted";
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER, isTrying: true);
        
        // Prepare the system prompt:
        var systemPrompt = new TextMessage
        {
            Role = "system",
            Content = chatThread.PrepareSystemPrompt(settingsManager, chatThread),
        };
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters();

        // Build the list of messages. The image format depends on the host:
        // - Ollama uses the direct image URL format: { "type": "image_url", "image_url": "data:..." }
        // - LM Studio, vLLM, and llama.cpp use the nested image URL format: { "type": "image_url", "image_url": { "url": "data:..." } }
        var messages = host switch
        {
            Host.OLLAMA => await chatThread.Blocks.BuildMessagesUsingDirectImageUrlAsync(this.Provider, chatModel),
            _ => await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel),
        };
        
        // Prepare the OpenAI HTTP chat request:
        var providerChatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..messages],
            
            // Right now, we only support streaming completions:
            Stream = true,
            AdditionalApiParameters = apiParameters
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, host.ChatURL());

            // Set the authorization header:
            if (requestedSecret.Success)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(providerChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>("self-hosted provider", RequestBuilder, token))
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
    
    public override async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.LLAMA_CPP:
                    // Right now, llama.cpp only supports one model.
                    // There is no API to list the model(s).
                    return [ new Provider.Model("as configured by llama.cpp", null) ];
            
                case Host.LM_STUDIO:
                case Host.OLLAMA:
                case Host.VLLM:
                    return await this.LoadModels( SecretStoreType.LLM_PROVIDER, ["embed"], [], token, apiKeyProvisional);
            }

            return [];
        }
        catch(Exception e)
        {
            LOGGER.LogError($"Failed to load text models from self-hosted provider: {e.Message}");
            return [];
        }
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }

    public override async Task<IEnumerable<Provider.Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
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

            return [];
        }
        catch(Exception e)
        {
            LOGGER.LogError($"Failed to load text models from self-hosted provider: {e.Message}");
            return [];
        }
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.WHISPER_CPP:
                    return Task.FromResult<IEnumerable<Provider.Model>>(
                        new List<Provider.Model>
                        {
                            new("loaded-model", TB("Model as configured by whisper.cpp")),
                        });
                
                case Host.OLLAMA:
                case Host.VLLM:
                    return this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, [], [], token, apiKeyProvisional);
                
                default:
                    return Task.FromResult(Enumerable.Empty<Provider.Model>());
            }
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Failed to load transcription models from self-hosted provider.");
            return Task.FromResult(Enumerable.Empty<Provider.Model>());
        }
    }
    
    #endregion

    private async Task<IEnumerable<Provider.Model>> LoadModels(SecretStoreType storeType, string[] ignorePhrases, string[] filterPhrases, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this, storeType, isTrying: true) switch
            {
                { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
                _ => null,
            }
        };
                    
        using var lmStudioRequest = new HttpRequestMessage(HttpMethod.Get, "models");
        if(secretKey is not null)
            lmStudioRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyProvisional);
                    
        using var lmStudioResponse = await this.httpClient.SendAsync(lmStudioRequest, token);
        if(!lmStudioResponse.IsSuccessStatusCode)
            return [];

        var lmStudioModelResponse = await lmStudioResponse.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return lmStudioModelResponse.Data.
            Where(model => !ignorePhrases.Any(ignorePhrase => model.Id.Contains(ignorePhrase, StringComparison.InvariantCulture)) &&
                           filterPhrases.All( filter => model.Id.Contains(filter, StringComparison.InvariantCulture)))
            .Select(n => new Provider.Model(n.Id, null));
    }
}