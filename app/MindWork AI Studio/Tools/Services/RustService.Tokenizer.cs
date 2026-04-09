using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<TokenCountInfo?> GetTokenCount(string text)
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var payload = new { text };
            var response = await this.http.PostAsJsonAsync("/system/tokenizer/count", payload, this.jsonRustSerializerOptions, cts.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TokenCountInfo>(this.jsonRustSerializerOptions, cancellationToken: cts.Token);
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
}