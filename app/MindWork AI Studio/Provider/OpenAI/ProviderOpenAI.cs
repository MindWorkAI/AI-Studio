using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The OpenAI provider.
/// </summary>
public sealed class ProviderOpenAI(ILogger logger) : BaseProvider("https://api.openai.com/v1/", logger)
{
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.OPEN_AI.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "OpenAI";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the system prompt:
        var systemPrompt = new Message
        {
            Role = "system",
            Content = chatThread.SystemPrompt,
        };
        
        // Prepare the OpenAI HTTP chat request:
        var openAIChatRequest = JsonSerializer.Serialize(new ChatRequest
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

            Seed = chatThread.Seed,
            
            // Right now, we only support streaming completions:
            Stream = true,
            FrequencyPenalty = 0f,
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(openAIChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        // Send the request using exponential backoff:
        using var responseData = await this.SendRequest(RequestBuilder, token);
        if(responseData.IsFailedAfterAllRetries)
        {
            this.logger.LogError($"OpenAI chat completion failed: {responseData.ErrorMessage}");
            yield break;
        }

        // Open the response stream:
        var openAIStream = await responseData.Response!.Content.ReadAsStreamAsync(token);
        
        // Add a stream reader to read the stream, line by line:
        var streamReader = new StreamReader(openAIStream);
        
        // Read the stream, line by line:
        while(!streamReader.EndOfStream)
        {
            // Check if the token is canceled:
            if(token.IsCancellationRequested)
                yield break;
            
            // Read the next line:
            var line = await streamReader.ReadLineAsync(token);
            
            // Skip empty lines:
            if(string.IsNullOrWhiteSpace(line))
                continue;
            
            // Skip lines that do not start with "data: ". Regard
            // to the specification, we only want to read the data lines:
            if(!line.StartsWith("data: ", StringComparison.InvariantCulture))
                continue;

            // Check if the line is the end of the stream:
            if (line.StartsWith("data: [DONE]", StringComparison.InvariantCulture))
                yield break;

            ResponseStreamLine openAIResponse;
            try
            {
                // We know that the line starts with "data: ". Hence, we can
                // skip the first 6 characters to get the JSON data after that.
                var jsonData = line[6..];
                
                // Deserialize the JSON data:
                openAIResponse = JsonSerializer.Deserialize<ResponseStreamLine>(jsonData, JSON_SERIALIZER_OPTIONS);
            }
            catch
            {
                // Skip invalid JSON data:
                continue;
            }    
            
            // Skip empty responses:
            if(openAIResponse == default || openAIResponse.Choices.Count == 0)
                continue;
            
            // Yield the response:
            yield return openAIResponse.Choices[0].Delta.Content;
        }
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(["gpt-", "o1-"], token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(["dall-e-"], token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(["text-embedding-"], token, apiKeyProvisional);
    }

    #endregion

    private async Task<IEnumerable<Model>> LoadModels(string[] prefixes, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this) switch
            {
                { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
                _ => null,
            }
        };

        if (secretKey is null)
            return [];
        
        var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse.Data.Where(model => prefixes.Any(prefix => model.Id.StartsWith(prefix, StringComparison.InvariantCulture)));
    }
}