using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Mistral;

public sealed class ProviderMistral(ILogger logger) : BaseProvider("https://api.mistral.ai/v1/", logger), IProvider
{
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    #region Implementation of IProvider

    public string Id => "Mistral";
    
    public string InstanceName { get; set; } = "Mistral";

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the system prompt:
        var systemPrompt = new RegularMessage
        {
            Role = "system",
            Content = chatThread.SystemPrompt,
        };
        
        // Prepare the Mistral HTTP chat request:
        var mistralChatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..chatThread.Blocks.Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text)).Select(n => new RegularMessage
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

            RandomSeed = chatThread.Seed,
            
            // Right now, we only support streaming completions:
            Stream = true,
            SafePrompt = false,
        }, JSON_SERIALIZER_OPTIONS);

        // Build the HTTP post request:
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        
        // Set the authorization header:
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));
        
        // Set the content:
        request.Content = new StringContent(mistralChatRequest, Encoding.UTF8, "application/json");
        
        // Send the request with the ResponseHeadersRead option.
        // This allows us to read the stream as soon as the headers are received.
        // This is important because we want to stream the responses.
        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        
        // Open the response stream:
        var mistralStream = await response.Content.ReadAsStreamAsync(token);
        
        // Add a stream reader to read the stream, line by line:
        var streamReader = new StreamReader(mistralStream);
        
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

            ResponseStreamLine mistralResponse;
            try
            {
                // We know that the line starts with "data: ". Hence, we can
                // skip the first 6 characters to get the JSON data after that.
                var jsonData = line[6..];
                
                // Deserialize the JSON data:
                mistralResponse = JsonSerializer.Deserialize<ResponseStreamLine>(jsonData, JSON_SERIALIZER_OPTIONS);
            }
            catch
            {
                // Skip invalid JSON data:
                continue;
            }    
            
            // Skip empty responses:
            if(mistralResponse == default || mistralResponse.Choices.Count == 0)
                continue;
            
            // Yield the response:
            yield return mistralResponse.Choices[0].Delta.Content;
        }
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public async IAsyncEnumerable<ImageURL> StreamImageCompletion(Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <inheritdoc />
    public async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
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
        return modelResponse.Data.Where(n => 
            !n.Id.StartsWith("code", StringComparison.InvariantCulture) &&
            !n.Id.Contains("embed", StringComparison.InvariantCulture))
            .Select(n => new Provider.Model(n.Id));
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public Task<IEnumerable<Provider.Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    #endregion
}