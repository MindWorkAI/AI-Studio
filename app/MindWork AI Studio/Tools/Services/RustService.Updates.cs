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
            
            if (response == default)
            {
                this.logger!.LogError("Failed to check for an update: the Rust endpoint returned an empty response.");
                return new UpdateResponse
                {
                    Error = true,
                    UpdateIsAvailable = false,
                    NewVersion = string.Empty,
                    Changelog = string.Empty
                };
            }

            if (response.Error)
                this.logger!.LogWarning("The Rust updater reported an error while checking for updates.");

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
                NewVersion = string.Empty,
                Changelog = string.Empty
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