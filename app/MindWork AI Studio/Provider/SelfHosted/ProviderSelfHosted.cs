using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.SelfHosted;

public sealed class ProviderSelfHosted(Settings.Provider provider) : BaseProvider($"{provider.Hostname}{provider.Host.BaseURL()}"), IProvider
{
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    #region Implementation of IProvider

    public string Id => "Self-hosted";
    
    public string InstanceName { get; set; } = "Self-hosted";
    
    public async IAsyncEnumerable<string> StreamChatCompletion(IJSRuntime jsRuntime, SettingsManager settings, Provider.Model chatModel, ChatThread chatThread, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Prepare the system prompt:
        var systemPrompt = new Message
        {
            Role = "system",
            Content = chatThread.SystemPrompt,
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

        // Build the HTTP post request:
        var request = new HttpRequestMessage(HttpMethod.Post, provider.Host.ChatURL());
        
        // Set the content:
        request.Content = new StringContent(providerChatRequest, Encoding.UTF8, "application/json");
        
        // Send the request with the ResponseHeadersRead option.
        // This allows us to read the stream as soon as the headers are received.
        // This is important because we want to stream the responses.
        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        
        // Open the response stream:
        var providerStream = await response.Content.ReadAsStreamAsync(token);
        
        // Add a stream reader to read the stream, line by line:
        var streamReader = new StreamReader(providerStream);
        
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
            if(providerResponse == default || providerResponse.Choices.Count == 0)
                continue;
            
            // Yield the response:
            yield return providerResponse.Choices[0].Delta.Content;
        }
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public async IAsyncEnumerable<ImageURL> StreamImageCompletion(IJSRuntime jsRuntime, SettingsManager settings, Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously


    public async Task<IEnumerable<Provider.Model>> GetTextModels(IJSRuntime jsRuntime, SettingsManager settings, string? apiKeyProvisional = null, CancellationToken token = default)
    {
        try
        {
            switch (provider.Host)
            {
                case Host.LLAMACPP:
                    // Right now, llama.cpp only supports one model.
                    // There is no API to list the model(s).
                    return [ new Provider.Model("as configured by llama.cpp") ];
            
                case Host.LM_STUDIO:
                case Host.OLLAMA:
                    var lmStudioRequest = new HttpRequestMessage(HttpMethod.Get, "models");
                    var lmStudioResponse = await this.httpClient.SendAsync(lmStudioRequest, token);
                    if(!lmStudioResponse.IsSuccessStatusCode)
                        return [];

                    var lmStudioModelResponse = await lmStudioResponse.Content.ReadFromJsonAsync<ModelsResponse>(token);
                    return lmStudioModelResponse.Data.Select(n => new Provider.Model(n.Id));
            }

            return [];
        }
        catch(Exception e)
        {
            Console.WriteLine($"Failed to load text models from self-hosted provider: {e.Message}");
            return [];
        }
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public Task<IEnumerable<Provider.Model>> GetImageModels(IJSRuntime jsRuntime, SettingsManager settings, string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    #endregion
}