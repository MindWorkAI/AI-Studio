using AIStudio.Provider;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    private readonly SemaphoreSlim tokenizerLock = new(1, 1);
    private string currentTokenizerPath = string.Empty;
    private bool hasInitializedTokenizer;

    public async Task<TokenizerResponse> ValidateTokenizer(string filePath)
    {
        var result = await this.http.PostAsJsonAsync("/tokenizer/validate", new {
            file_path = filePath,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to validate the tokenizer '{result.StatusCode}'");
            return new TokenizerResponse
            {
                Success = false,
                Message = "An error occured while sending the path to the Rust framework for validation: "+result.StatusCode,
                TokenCount = 0
            };
        }

        return await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
    }
    
    public async Task<TokenizerResponse> StoreTokenizer(string modelId, string previousmodelId, string filePath)
    {
        Console.WriteLine($"Storing tokenizer for model '{modelId}' with previous model '{previousmodelId}' from file '{filePath}'");
        var result = await this.http.PostAsJsonAsync("/tokenizer/store", new {
            model_id = modelId,
            previous_model_id = previousmodelId,
            file_path = filePath,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the tokenizer '{result.StatusCode}'");
            return new TokenizerResponse{
                Success = false,
                Message = "An error occured while sending the path to the Rust framework for storing: "+result.StatusCode,
                TokenCount = 0
            };
        }

        return await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
    }
    
    public async Task<TokenizerResponse?> GetTokenCount(string text)
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var payload = new { text };
            var response = await this.http.PostAsJsonAsync("/tokenizer/count", payload, this.jsonRustSerializerOptions, cts.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions, cancellationToken: cts.Token);
        }
        catch (Exception e)
        {
            if(this.logger is not null)
                this.logger.LogError(e, "Error while getting token count from Rust service.");
            else
                Console.WriteLine($"Error while getting token count from Rust service: '{e}'.");
            
            return null;
        }
    }

    public async Task<TokenizerResponse?> SetTokenizer(string providerName, string path)
    {
        Console.WriteLine($"Setting a new tokenizer for '{providerName}'");
        var result = await this.http.PostAsJsonAsync("/tokenizer/set", new {
            file_path = path,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to set the tokenizer '{result.StatusCode}'");
            return new TokenizerResponse{
                Success = false,
                Message = "An error occured while sending the path to the Rust framework for setting a tokenizer: "+result.StatusCode,
                TokenCount = 0
            };
        }

        return await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
    }

    public Task<TokenizerResponse?> EnsureTokenizer(Settings.Provider provider)
    {
        return this.EnsureTokenizer(provider.InstanceName, provider.TokenizerPath);
    }

    public Task<TokenizerResponse?> EnsureTokenizer(IProvider provider)
    {
        return this.EnsureTokenizer(provider.InstanceName, provider.TokenizerPath);
    }

    private async Task<TokenizerResponse?> EnsureTokenizer(string providerName, string path)
    {
        await this.tokenizerLock.WaitAsync();
        try
        {
            if (this.hasInitializedTokenizer && this.currentTokenizerPath == path)
                return new TokenizerResponse(true, 0, "Success");

            var response = await this.SetTokenizer(providerName, path);
            if (response is { Success: true })
            {
                this.currentTokenizerPath = path;
                this.hasInitializedTokenizer = true;
            }

            return response;
        }
        finally
        {
            this.tokenizerLock.Release();
        }
    }
}
