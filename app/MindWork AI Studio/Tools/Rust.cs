namespace AIStudio.Tools;

/// <summary>
/// Calling Rust functions.
/// </summary>
public sealed class Rust(string apiPort) : IDisposable
{
    private readonly HttpClient http = new()
    {
        BaseAddress = new Uri($"http://127.0.0.1:{apiPort}"),
    };
    
    private ILogger<Rust>? logger;
    private Encryption? encryptor;

    public void SetLogger(ILogger<Rust> logService)
    {
        this.logger = logService;
    }
    
    public void SetEncryptor(Encryption encryptionService)
    {
        this.encryptor = encryptionService;
    }
    
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
        var url = new Uri($"http://127.0.0.1:{apiPort}/system/dotnet/port");
        while (tris++ < MAX_TRIES)
        {
            //
            // Note II: We use a new HttpClient instance for each try to avoid
            // .NET is caching the result. When we use the same HttpClient
            // instance, we would always get the same result (403 forbidden),
            // without even trying to connect to the Rust server.
            //
            using var initialHttp = new HttpClient();
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

        Console.WriteLine("Failed to receive the app port from Rust runtime.");
        return 0;
    }

    public async Task AppIsReady()
    {
        const string URL = "/system/dotnet/ready";
        this.logger!.LogInformation("Notifying Rust runtime that the app is ready.");
        var response = await this.http.PostAsync(URL, new StringContent(string.Empty));
        if (!response.IsSuccessStatusCode)
        {
             this.logger!.LogError($"Failed to notify Rust runtime that the app is ready: '{response.StatusCode}'");
        }
    }
    
    /// <summary>
    /// Tries to copy the given text to the clipboard.
    /// </summary>
    /// <param name="snackbar">The snackbar to show the result.</param>
    /// <param name="text">The text to copy to the clipboard.</param>
    public async Task CopyText2Clipboard(ISnackbar snackbar, string text)
    {
        var message = "Successfully copied the text to your clipboard";
        var iconColor = Color.Error;
        var severity = Severity.Error;
        try
        {
            var response = await this.http.PostAsync("/clipboard/set", new StringContent(await text.Encrypt(this.encryptor!)));
            if (!response.IsSuccessStatusCode)
            {
                this.logger!.LogError($"Failed to copy the text to the clipboard due to an network error: '{response.StatusCode}'");
                message = "Failed to copy the text to your clipboard.";
                return;
            }

            var state = await response.Content.ReadFromJsonAsync<SetClipboardResponse>();
            if (!state.Success)
            {
                this.logger!.LogError("Failed to copy the text to the clipboard.");
                message = "Failed to copy the text to your clipboard.";
                return;
            }
            
            iconColor = Color.Success;
            severity = Severity.Success;
            this.logger!.LogDebug("Successfully copied the text to the clipboard.");
        }
        finally
        {
            snackbar.Add(message, severity, config =>
            {
                config.Icon = Icons.Material.Filled.ContentCopy;
                config.IconSize = Size.Large;
                config.IconColor = iconColor;
            });
        }
    }
    
    public async Task<UpdateResponse> CheckForUpdate()
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(16));
            return await this.http.GetFromJsonAsync<UpdateResponse>("/updates/check", cts.Token);
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
    
    public async Task InstallUpdate(IJSRuntime jsRuntime)
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

    #region IDisposable

    public void Dispose()
    {
        this.http.Dispose();
    }

    #endregion
}