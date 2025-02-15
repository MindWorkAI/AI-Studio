using System.Security.Cryptography;

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
            this.logger!.LogError($"Failed to get the data directory from Rust: '{response.StatusCode}'");
            return string.Empty;
        }
        
        return await response.Content.ReadAsStringAsync();
    }
}