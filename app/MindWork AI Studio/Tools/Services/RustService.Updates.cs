using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<UpdateResponse> CheckForUpdate()
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var response = await this.http.GetFromJsonAsync<UpdateResponse>("/updates/check", this.jsonRustSerializerOptions, cts.Token);
            this.logger!.LogInformation($"Checked for an update: update available='{response.UpdateIsAvailable}'; error='{response.Error}'; next version='{response.NewVersion}'; changelog len='{response.Changelog.Length}'");
            return response;
        }
        catch (Exception e)
        {
            this.logger!.LogError(e, "Failed to check for an update.");
            return new UpdateResponse
            {
                Error = true,
                UpdateIsAvailable = false,
            };
        }
    }
    
    public async Task InstallUpdate()
    {
        try
        {
            var cts = new CancellationTokenSource();
            await this.http.GetAsync("/updates/install", cts.Token);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}