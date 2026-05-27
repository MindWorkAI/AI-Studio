using AIStudio.Provider;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    private readonly SemaphoreSlim tokenizerLock = new(1, 1);
    private string currentTokenizerPath = string.Empty;
    private bool hasInitializedTokenizer;

    private static TokenizerResponse CreateUnavailableTokenizerResponse(string message) => new(
        false,
        0,
        message,
        TokenizerStatus.UNAVAILABLE,
        string.Empty);

    public async Task<TokenizerResponse> GetTokenizerInfo(CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.http.GetFromJsonAsync<TokenizerResponse>("/system/tokenizer/info", this.jsonRustSerializerOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.logger?.LogWarning("Fetching tokenizer info from Rust service was cancelled by caller.");
            return CreateUnavailableTokenizerResponse("Operation cancelled by caller.");
        }
        catch (Exception e)
        {
            this.logger?.LogError(e, "Error while fetching tokenizer info from Rust service.");
            return CreateUnavailableTokenizerResponse(e.Message);
        }
    }

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
    
    public async Task<TokenizerResponse?> GetTokenCount(string text)
    {
        var result = await this.http.PostAsJsonAsync("/tokenizer/count", new {
            text = text,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to get the token count '{result.StatusCode}'");
            this.hasInitializedTokenizer = false;
            return CreateUnavailableTokenizerResponse("Error while getting token count from Rust service: "+result.StatusCode);
        }

        var response = await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
        if (response is not { Status: TokenizerStatus.AVAILABLE })
            this.hasInitializedTokenizer = false;

        return response;
    }

    public async Task<TokenizerResponse?> SetTokenizer(string providerName, string path)
    {
        this.logger!.LogInformation($"Setting a new tokenizer for '{providerName}'");
        var result = await this.http.PostAsJsonAsync("/tokenizer/set", new {
            file_path = path,
        }, this.jsonRustSerializerOptions);

        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to set the tokenizer '{result.StatusCode}'");
            this.hasInitializedTokenizer = false;
            return CreateUnavailableTokenizerResponse("An error occured while sending the path to the Rust framework for setting a tokenizer: "+result.StatusCode);
        }

        var response = await result.Content.ReadFromJsonAsync<TokenizerResponse>(this.jsonRustSerializerOptions);
        if (response is not { Success: true, Status: TokenizerStatus.AVAILABLE })
            this.hasInitializedTokenizer = false;

        return response;
    }

    public async Task<TokenizerResponse?> EnsureTokenizer(string providerName, string path)
    {
        await this.tokenizerLock.WaitAsync();
        try
        {
            if (this.hasInitializedTokenizer && this.currentTokenizerPath == path)
                return new TokenizerResponse(true, 0, string.Empty, TokenizerStatus.AVAILABLE);

            var response = await this.SetTokenizer(providerName, path);
            if (response is { Success: true, Status: TokenizerStatus.AVAILABLE })
            {
                this.currentTokenizerPath = path;
                this.hasInitializedTokenizer = true;
            }
            else
            {
                this.currentTokenizerPath = string.Empty;
                this.hasInitializedTokenizer = false;
            }

            return response;
        }
        finally
        {
            this.tokenizerLock.Release();
        }
    }
}
