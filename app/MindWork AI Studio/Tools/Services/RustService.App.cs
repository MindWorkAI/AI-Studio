using System.Security.Cryptography;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<int> GetAppPort()
    {
        Console.WriteLine("Trying to get app port from Rust runtime...");
        
        //
        // Note I: In the production environment, the Rust runtime is already running
        // and listening on the given port. In the development environment, the IDE
        // starts the Rust runtime in parallel with the .NET runtime. Since the
        // Rust runtime needs some time to start, we have to wait for it to be ready.
        //
        const int MAX_TRIES = 160;
        var tris = 0;
        var wait4Try = TimeSpan.FromMilliseconds(250);
        var url = new Uri($"https://127.0.0.1:{this.apiPort}/system/dotnet/port");
        while (tris++ < MAX_TRIES)
        {
            //
            // Note II: We use a new HttpClient instance for each try to avoid
            // .NET is caching the result. When we use the same HttpClient
            // instance, we would always get the same result (403 forbidden),
            // without even trying to connect to the Rust server.
            //

            using var initialHttp = new HttpClient(new HttpClientHandler
            {
                //
                // Note III: We have to create also a new HttpClientHandler instance
                // for each try to avoid .NET is caching the result. This is necessary
                // because it gets disposed when the HttpClient instance gets disposed.
                //
                ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                {
                    if(certificate is null)
                        return false;
            
                    var currentCertificateFingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                    return currentCertificateFingerprint == this.certificateFingerprint;
                }
            });
            
            initialHttp.DefaultRequestVersion = Version.Parse("2.0");
            initialHttp.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            initialHttp.DefaultRequestHeaders.AddApiToken();
            
            try
            {
                var response = await initialHttp.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Try {tris}/{MAX_TRIES} to get the app port from Rust runtime");
                    await Task.Delay(wait4Try);
                    continue;
                }

                var appPortContent = await response.Content.ReadAsStringAsync();
                var appPort = int.Parse(appPortContent);
                Console.WriteLine($"Received app port from Rust runtime: '{appPort}'");
                return appPort;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: Was not able to get the app port from Rust runtime: '{e.Message}'");
                Console.WriteLine(e.InnerException);
                throw;
            }
        }

        Console.WriteLine("Failed to receive the app port from Rust runtime.");
        return 0;
    }

    public async Task AppIsReady()
    {
        const string URL = "/system/dotnet/ready";
        this.logger!.LogInformation("Notifying Rust runtime that the app is ready.");
        try
        {
            var response = await this.http.GetAsync(URL);
            if (!response.IsSuccessStatusCode)
            {
                this.logger!.LogError($"Failed to notify Rust runtime that the app is ready: '{response.StatusCode}'");
            }
        }
        catch (Exception e)
        {
            this.logger!.LogError(e, "Failed to notify the Rust runtime that the app is ready.");
            throw;
        }
    }
    
    public async Task<string> GetConfigDirectory()
    {
        var response = await this.http.GetAsync("/system/directories/config");
        if (!response.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to get the config directory from Rust: '{response.StatusCode}'");
            return string.Empty;
        }
        
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<string> GetDataDirectory()
    {
        var response = await this.http.GetAsync("/system/directories/data");
        if (!response.IsSuccessStatusCode)
        {
            this.logger?.LogError($"Failed to get the data directory from Rust: '{response.StatusCode}'");
            return string.Empty;
        }
        
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<RuntimeInfoResponse> GetRuntimeInfo()
    {
        var response = await this.http.GetFromJsonAsync<RuntimeInfoResponse>("/system/runtime/info", this.jsonRustSerializerOptions);
        return response;
    }

    /// <summary>
    /// Requests the Rust runtime to exit the entire desktop application.
    /// </summary>
    public async Task<bool> ExitApplication()
    {
        try
        {
            var response = await this.http.PostAsync("/app/exit", null);
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to exit the app due to network error: {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<AppExitResponse>(this.jsonRustSerializerOptions);
            if (result is null || !result.Success)
            {
                this.logger?.LogError("Failed to exit the app: {Error}", result?.ErrorMessage ?? "Unknown error");
                return false;
            }

            this.logger?.LogInformation("Exit request sent to Rust runtime.");
            return true;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while requesting application exit.");
            return false;
        }
    }

    /// <summary>
    /// Requests the Rust runtime to restart the entire desktop application, e.g., to apply a prepared
    /// restore of a backup.
    /// </summary>
    /// <remarks>
    /// In the development environment, the runtime cannot restart the .NET server, which it did not
    /// start, and refuses. The developer restarts both instead.
    /// </remarks>
    /// <returns>True when the restart is on its way.</returns>
    public async Task<bool> RestartApplication()
    {
        try
        {
            var response = await this.http.PostAsync("/app/restart", null);
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to restart the app due to network error: {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<AppRestartResponse>(this.jsonRustSerializerOptions);
            if (result is null || !result.Success)
            {
                this.logger?.LogError("Failed to restart the app: {Error}", result?.ErrorMessage ?? "Unknown error");
                return false;
            }

            this.logger?.LogInformation("Restart request sent to Rust runtime.");
            return true;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while requesting application restart.");
            return false;
        }
    }

    /// <summary>
    /// Asks the runtime to show AI Studio in the given size, which it reaches by zooming the webview.
    /// </summary>
    /// <param name="request">The size, and the scale of the custom size.</param>
    /// <returns>What the size comes to on the screen AI Studio shows on, or null when the runtime could not apply it.</returns>
    public async Task<InterfaceSizeResponse?> SetInterfaceSize(SetInterfaceSizeRequest request)
    {
        try
        {
            var response = await this.http.PostAsJsonAsync("/app/interface-size", request, this.jsonRustSerializerOptions);
            var result = await this.ReadInterfaceSizeResponse(response, "set the interface size");
            if (result is not null)
                this.logger?.LogInformation("Set the interface sizes of {ScreenCount} screens. AI Studio shows with the size '{Size}' at {CurrentPercent} % and a zoom of {Zoom:0.###} on the screen '{ScreenKey}'.", request.Screens.Count, result.Size, result.CurrentPercent, result.Zoom, result.ScreenKey);

            return result;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while setting the interface size.");
            return null;
        }
    }

    /// <summary>
    /// Asks the runtime what the interface size comes to on the screen AI Studio shows on, e.g., which custom scales it allows.
    /// </summary>
    /// <returns>The report of the runtime, or null when it could not measure the screen.</returns>
    public async Task<InterfaceSizeResponse?> GetInterfaceSize()
    {
        try
        {
            var response = await this.http.GetAsync("/app/interface-size");
            return await this.ReadInterfaceSizeResponse(response, "read the interface size");
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Exception while reading the interface size.");
            return null;
        }
    }

    private async Task<InterfaceSizeResponse?> ReadInterfaceSizeResponse(HttpResponseMessage response, string action)
    {
        if (!response.IsSuccessStatusCode)
        {
            this.logger?.LogError("Failed to {Action} due to network error: {StatusCode}.", action, response.StatusCode);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<InterfaceSizeResponse>(this.jsonRustSerializerOptions);
        if (result is null || !result.Success)
        {
            this.logger?.LogError("Failed to {Action}: {Error}", action, result?.ErrorMessage ?? "Unknown error");
            return null;
        }

        return result;
    }
}