using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Google;

public class ProviderGoogle(ILogger logger) : BaseProvider("https://generativelanguage.googleapis.com/v1beta/", logger)
{
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GOOGLE.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Google Gemini";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, [EnumeratorCancellation] CancellationToken token = default)
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
        
        // Prepare the Google HTTP chat request:
        var geminiChatRequest = JsonSerializer.Serialize(new ChatRequest
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
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(geminiChatRequest, Encoding.UTF8, "application/json");
            return request;
        }

        // Send the request using exponential backoff:
        using var responseData = await this.SendRequest(RequestBuilder, token);
        if(responseData.IsFailedAfterAllRetries)
        {
            this.logger.LogError($"Google chat completion failed: {responseData.ErrorMessage}");
            yield break;
        }
        
        // Open the response stream:
        var geminiStream = await responseData.Response!.Content.ReadAsStreamAsync(token);
        
        // Add a stream reader to read the stream, line by line:
        var streamReader = new StreamReader(geminiStream);
        
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

            ResponseStreamLine geminiResponse;
            try
            {
                // We know that the line starts with "data: ". Hence, we can
                // skip the first 6 characters to get the JSON data after that.
                var jsonData = line[6..];
                
                // Deserialize the JSON data:
                geminiResponse = JsonSerializer.Deserialize<ResponseStreamLine>(jsonData, JSON_SERIALIZER_OPTIONS);
            }
            catch
            {
                // Skip invalid JSON data:
                continue;
            }    
            
            // Skip empty responses:
            if(geminiResponse == default || geminiResponse.Choices.Count == 0)
                continue;
            
            // Yield the response:
            yield return geminiResponse.Choices[0].Delta.Content;
        }
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <inheritdoc />
    public override async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModels(token, apiKeyProvisional);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Models.Where(model =>
                model.Name.StartsWith("models/gemini-", StringComparison.InvariantCultureIgnoreCase))
            .Select(n => new Provider.Model(n.Name.Replace("models/", string.Empty), n.DisplayName));
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }

    public override async Task<IEnumerable<Provider.Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModels(token, apiKeyProvisional);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Models.Where(model =>
                model.Name.StartsWith("models/text-embedding-", StringComparison.InvariantCultureIgnoreCase))
            .Select(n => new Provider.Model(n.Name.Replace("models/", string.Empty), n.DisplayName));
    }

    #endregion

    private async Task<ModelsResponse> LoadModels(CancellationToken token, string? apiKeyProvisional = null)
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
            return default;

        var request = new HttpRequestMessage(HttpMethod.Get, $"models?key={secretKey}");
        var response = await this.httpClient.SendAsync(request, token);
        
        if(!response.IsSuccessStatusCode)
            return default;

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse;
    }
}