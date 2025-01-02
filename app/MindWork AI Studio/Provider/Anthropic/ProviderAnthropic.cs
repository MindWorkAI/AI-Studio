using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Anthropic;

public sealed class ProviderAnthropic(ILogger logger) : BaseProvider("https://api.anthropic.com/v1/", logger)
{
    private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    #region Implementation of IProvider

    public override string Id => LLMProviders.ANTHROPIC.ToName();

    public override string InstanceName { get; set; } = "Anthropic";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the Anthropic HTTP chat request:
        var chatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            Messages = [..chatThread.Blocks.Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text)).Select(n => new Message
            {
                Role = n.Role switch
                {
                    ChatRole.USER => "user",
                    ChatRole.AI => "assistant",
                    ChatRole.AGENT => "assistant",

                    _ => "user",
                },

                Content = n.Content switch
                {
                    ContentText text => text.Text,
                    _ => string.Empty,
                }
            }).ToList()],
            
            System = chatThread.PrepareSystemPrompt(settingsManager, chatThread, this.logger),
            MaxTokens = 4_096,
            
            // Right now, we only support streaming completions:
            Stream = true,
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "messages");

            // Set the authorization header:
            request.Headers.Add("x-api-key", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the Anthropic version:
            request.Headers.Add("anthropic-version", "2023-06-01");

            // Set the content:
            request.Content = new StringContent(chatRequest, Encoding.UTF8, "application/json");
            return request;
        }

        // Send the request using exponential backoff:
        using var responseData = await this.SendRequest(RequestBuilder, token);
        if(responseData.IsFailedAfterAllRetries)
        {
            this.logger.LogError($"Anthropic chat completion failed: {responseData.ErrorMessage}");
            yield break;
        }
        
        // Open the response stream:
        var stream = await responseData.Response!.Content.ReadAsStreamAsync(token);
        
        // Add a stream reader to read the stream, line by line:
        var streamReader = new StreamReader(stream);
        
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
            
            // Check for the end of the stream:
            if(line.StartsWith("event: message_stop", StringComparison.InvariantCulture))
                yield break;
            
            // Skip lines that do not start with "data: ". Regard
            // to the specification, we only want to read the data lines:
            if(!line.StartsWith("data: ", StringComparison.InvariantCulture))
                continue;
            
            // Ignore any type except "content_block_delta":
            if(!line.Contains("\"content_block_delta\"", StringComparison.InvariantCulture))
                continue;

            ResponseStreamLine anthropicResponse;
            try
            {
                // We know that the line starts with "data: ". Hence, we can
                // skip the first 6 characters to get the JSON data after that.
                var jsonData = line[6..];
                
                // Deserialize the JSON data:
                anthropicResponse = JsonSerializer.Deserialize<ResponseStreamLine>(jsonData, JSON_SERIALIZER_OPTIONS);
            }
            catch
            {
                // Skip invalid JSON data:
                continue;
            }    
            
            // Skip empty responses:
            if(anthropicResponse == default || string.IsNullOrWhiteSpace(anthropicResponse.Delta.Text))
                continue;
            
            // Yield the response:
            yield return anthropicResponse.Delta.Text;
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
        return Task.FromResult(new[]
        {
            new Model("claude-3-5-sonnet-latest", "Claude 3.5 Sonnet (latest)"),
            new Model("claude-3-5-sonnet-20240620", "Claude 3.5 Sonnet (20. June 2024)"),
            new Model("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet (22. October 2024)"),
            
            new Model("claude-3-5-haiku-latest", "Claude 3.5 Haiku (latest)"),
            new Model("claude-3-5-heiku-20241022", "Claude 3.5 Haiku (22. October 2024)"),
            
            new Model("claude-3-opus-20240229", "Claude 3.0 Opus (29. February 2024)"),
            new Model("claude-3-opus-latest", "Claude 3.0 Opus (latest)"),
            
            new Model("claude-3-sonnet-20240229", "Claude 3.0 Sonnet (29. February 2024)"),
            new Model("claude-3-haiku-20240307", "Claude 3.0 Haiku (7. March 2024)"),
        }.AsEnumerable());
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }

    #endregion
}