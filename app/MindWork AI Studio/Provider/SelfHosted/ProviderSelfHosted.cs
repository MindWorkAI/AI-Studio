using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.SelfHosted;

public sealed class ProviderSelfHosted(ILogger logger, Host host, string hostname) : BaseProvider($"{hostname}{host.BaseURL()}", logger)
{
    #region Implementation of IProvider

    public override string Id => LLMProviders.SELF_HOSTED.ToName();
    
    public override string InstanceName { get; set; } = "Self-hosted";
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, isTrying: true);
        
        // Prepare the system prompt:
        var systemPrompt = new Message
        {
            Role = "system",
            Content = chatThread.PrepareSystemPrompt(settingsManager, chatThread, this.logger),
        };
        
        // Prepare the OpenAI HTTP chat request:
        var providerChatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..chatThread.Blocks.Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text)).Select(n => new Message
            {
                Role = n.Role switch
                {
                    ChatRole.USER => "user",
                    ChatRole.AI => "assistant",
                    ChatRole.AGENT => "assistant",
                    ChatRole.SYSTEM => "system",

                    _ => "user",
                },

                Content = n.Content switch
                {
                    ContentText text => text.Text,
                    _ => string.Empty,
                }
            }).ToList()],
            
            // Right now, we only support streaming completions:
            Stream = true
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
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("self-hosted provider", RequestBuilder, token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    public override async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (host)
            {
                case Host.LLAMACPP:
                    // Right now, llama.cpp only supports one model.
                    // There is no API to list the model(s).
                    return [ new Provider.Model("as configured by llama.cpp", null) ];
            
                case Host.LM_STUDIO:
                case Host.OLLAMA:
                case Host.VLLM:
                    return await this.LoadModels(["embed"], [], token, apiKeyProvisional);
            }

            return [];
        }
        catch(Exception e)
        {
            this.logger.LogError($"Failed to load text models from self-hosted provider: {e.Message}");
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
                    return await this.LoadModels([], ["embed"], token, apiKeyProvisional);
            }

            return [];
        }
        catch(Exception e)
        {
            this.logger.LogError($"Failed to load text models from self-hosted provider: {e.Message}");
            return [];
        }
    }
    
    public override IReadOnlyCollection<Capability> GetModelCapabilities(Provider.Model model) => CapabilitiesOpenSource.GetCapabilities(model);
    
    #endregion

    private async Task<IEnumerable<Provider.Model>> LoadModels(string[] ignorePhrases, string[] filterPhrases, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this, isTrying: true) switch
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