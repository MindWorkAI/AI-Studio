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
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

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
            Stream = true,
            MaxTokens = -1,
        }, JSON_SERIALIZER_OPTIONS);

        StreamReader? streamReader = null;
        try
        {
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
            
            // Send the request using exponential backoff:
            var responseData = await this.SendRequest(RequestBuilder, token);
            if(responseData.IsFailedAfterAllRetries)
            {
                this.logger.LogError($"Self-hosted provider's chat completion failed: {responseData.ErrorMessage}");
                yield break;
            }
            
            // Open the response stream:
            var providerStream = await responseData.Response!.Content.ReadAsStreamAsync(token);

            // Add a stream reader to read the stream, line by line:
            streamReader = new StreamReader(providerStream);
        }
        catch(Exception e)
        {
            this.logger.LogError($"Failed to stream chat completion from self-hosted provider '{this.InstanceName}': {e.Message}");
        }

        if (streamReader is null)
            yield break;
        
        // Read the stream, line by line:
        while (true)
        {
            try
            {
                if(streamReader.EndOfStream)
                    break;
            }
            catch (Exception e)
            {
                this.logger.LogWarning($"Failed to read the end-of-stream state from self-hosted provider '{this.InstanceName}': {e.Message}");
                break;
            }
                
            // Check if the token is canceled:
            if (token.IsCancellationRequested)
                yield break;

            // Read the next line:
            string? line;
            try
            {
                line = await streamReader.ReadLineAsync(token);
            }
            catch (Exception e)
            {
                this.logger.LogError($"Failed to read the stream from self-hosted provider '{this.InstanceName}': {e.Message}");
                break;
            }

            // Skip empty lines:
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            // Skip lines that do not start with "data: ". Regard
            // to the specification, we only want to read the data lines:
            if (!line.StartsWith("data: ", StringComparison.InvariantCulture))
                continue;

            // Check if the line is the end of the stream:
            if (line.StartsWith("data: [DONE]", StringComparison.InvariantCulture))
                yield break;

            ResponseStreamLine providerResponse;
            try
            {
                // We know that the line starts with "data: ". Hence, we can
                // skip the first 6 characters to get the JSON data after that.
                var jsonData = line[6..];

                // Deserialize the JSON data:
                providerResponse = JsonSerializer.Deserialize<ResponseStreamLine>(jsonData, JSON_SERIALIZER_OPTIONS);
            }
            catch
            {
                // Skip invalid JSON data:
                continue;
            }

            // Skip empty responses:
            if (providerResponse == default || providerResponse.Choices.Count == 0)
                continue;

            // Yield the response:
            yield return providerResponse.Choices[0].Delta.Content;
        }
        
        streamReader.Dispose();
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
                    
        var lmStudioRequest = new HttpRequestMessage(HttpMethod.Get, "models");
        if(secretKey is not null)
            lmStudioRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyProvisional);
                    
        var lmStudioResponse = await this.httpClient.SendAsync(lmStudioRequest, token);
        if(!lmStudioResponse.IsSuccessStatusCode)
            return [];

        var lmStudioModelResponse = await lmStudioResponse.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return lmStudioModelResponse.Data.
            Where(model => !ignorePhrases.Any(ignorePhrase => model.Id.Contains(ignorePhrase, StringComparison.InvariantCulture)) &&
                           filterPhrases.All( filter => model.Id.Contains(filter, StringComparison.InvariantCulture)))
            .Select(n => new Provider.Model(n.Id, null));
    }
}