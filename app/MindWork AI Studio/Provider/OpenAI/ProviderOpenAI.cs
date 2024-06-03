using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The OpenAI provider.
/// </summary>
public sealed class ProviderOpenAI() : BaseProvider("https://api.openai.com/v1/"), IProvider
{
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public string Id => "OpenAI";

    /// <inheritdoc />
    public string InstanceName { get; set; } = "OpenAI";

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, ChatThread chatThread, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await settings.GetAPIKey(jsRuntime, this);
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

        // Build the HTTP post request:
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        
        // Set the authorization header:
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestedSecret.Secret);
        
        // Set the content:
        request.Content = new StringContent(openAIChatRequest, Encoding.UTF8, "application/json");
        
        // Send the request with the ResponseHeadersRead option.
        // This allows us to read the stream as soon as the headers are received.
        // This is important because we want to stream the responses.
        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        
        // Open the response stream:
        var openAIStream = await response.Content.ReadAsStreamAsync(token);
        
        // Add a stream reader to read the stream, line by line:
        var streamReader = new StreamReader(openAIStream);
        
        // Read the stream, line by line:
        while(!streamReader.EndOfStream)
        {
            // Check if the token is cancelled:
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
    public async IAsyncEnumerable<ImageURL> StreamImageCompletion(IJSRuntime jsRuntime, SettingsManager settings, Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <inheritdoc />
    public Task<IEnumerable<Model>> GetTextModels(IJSRuntime jsRuntime, SettingsManager settings, string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(jsRuntime, settings, "gpt-", token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Model>> GetImageModels(IJSRuntime jsRuntime, SettingsManager settings, string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(jsRuntime, settings, "dall-e-", token, apiKeyProvisional);
    }

    #endregion

    private async Task<IEnumerable<Model>> LoadModels(IJSRuntime jsRuntime, SettingsManager settings, string prefix, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await settings.GetAPIKey(jsRuntime, this) switch
            {
                { Success: true } result => result.Secret,
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
        return modelResponse.Data.Where(n => n.Id.StartsWith(prefix, StringComparison.InvariantCulture));
    }
}