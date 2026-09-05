using AIStudio.Settings;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    internal const int MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH = 200_000;

    private static TokenizerResponse CreateUnavailableTokenizerResponse(string message) => new(
        false,
        0,
        message,
        string.Empty);

    public async Task<TokenizerResponse> ValidateTokenizer(string filePath)
    {
        var result = await this.http.PostAsJsonAsync("/tokenizer/validate", new {
            file_path = filePath,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to validate the tokenizer '{result.StatusCode}'");
            return CreateUnavailableTokenizerResponse("An error occured while sending the path to the Rust framework for validation: "+result.StatusCode);
        }

        var response = await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);

        return response;
    }
    
    public async Task<TokenizerResponse> StoreTokenizer(string modelId, string filePath)
    {
        this.logger!.LogInformation($"Storing tokenizer for model '{modelId}' from file '{filePath}'");
        var result = await this.http.PostAsJsonAsync("/tokenizer/store", new {
            model_id = modelId,
            file_path = filePath,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the tokenizer '{result.StatusCode}'");
            return CreateUnavailableTokenizerResponse("An error occured while sending the path to the Rust framework for storing: "+result.StatusCode);
        }

        return await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
    }

    public async Task<TokenizerResponse> DeleteTokenizer(string modelId)
    {
        this.logger!.LogInformation($"Deleting tokenizer for model '{modelId}'");
        var result = await this.http.PostAsJsonAsync("/tokenizer/delete", new {
            model_id = modelId,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to delete the tokenizer '{result.StatusCode}'");
            return CreateUnavailableTokenizerResponse("An error occured while sending the tokenizer delete request to the Rust framework: "+result.StatusCode);
        }

        return await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
    }
    
    public Task<TokenizerResponse?> GetTokenCount(AIStudio.Settings.Provider provider, string text, CancellationToken cancellationToken = default) =>
        this.GetTokenCount(provider.InstanceName, provider.TokenizerPath, text, cancellationToken);

    public Task<TokenizerResponse?> GetTokenCount(EmbeddingProvider provider, string text, CancellationToken cancellationToken = default) =>
        this.GetTokenCount(provider.Name, provider.TokenizerPath, text, cancellationToken);

    private async Task<TokenizerResponse?> GetTokenCount(string providerName, string tokenizerPath, string text, CancellationToken cancellationToken)
    {
        var result = await this.http.PostAsJsonAsync("/tokenizer/count", new {
            text = text,
            tokenizer_path = tokenizerPath,
        }, this.jsonRustSerializerOptions, cancellationToken);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError("Failed to get the token count for provider '{ProviderName}': {StatusCode}", providerName, result.StatusCode);
            return CreateUnavailableTokenizerResponse("Error while getting token count from Rust service: "+result.StatusCode);
        }

        return await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions, cancellationToken);
    }
}
