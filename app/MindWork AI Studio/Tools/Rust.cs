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
                Console.WriteLine($"   Try {tris}/{MAX_TRIES}");
                await Task.Delay(wait4Try);
                continue;
            }
            
            var appPortContent = await response.Content.ReadAsStringAsync();
            var appPort = int.Parse(appPortContent);
            Console.WriteLine($"   Received app port from Rust runtime: '{appPort}'");
            return appPort;
        }

        Console.WriteLine("Failed to receive the app port from Rust runtime.");
        return 0;
    }

    public async Task AppIsReady()
    {
        const string URL = "/system/dotnet/ready";
        Console.WriteLine($"Notifying Rust runtime that the app is ready.");
        var response = await this.http.PostAsync(URL, new StringContent(string.Empty));
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to notify Rust runtime that the app is ready: '{response.StatusCode}'");
        }
    }
    
    /// <summary>
    /// Tries to copy the given text to the clipboard.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="snackbar">The snackbar to show the result.</param>
    /// <param name="text">The text to copy to the clipboard.</param>
    public async Task CopyText2Clipboard(IJSRuntime jsRuntime, ISnackbar snackbar, string text)
    {
        var response = await jsRuntime.InvokeAsync<SetClipboardResponse>("window.__TAURI__.invoke", "set_clipboard", new SetClipboardText(text));
        var msg = response.Success switch
        {
            true => "Successfully copied text to clipboard!",
            false => $"Failed to copy text to clipboard: {response.Issue}",
        };
                
        var severity = response.Success switch
        {
            true => Severity.Success,
            false => Severity.Error,
        };
                
        snackbar.Add(msg, severity, config =>
        {
            config.Icon = Icons.Material.Filled.ContentCopy;
            config.IconSize = Size.Large;
            config.IconColor = severity switch
            {
                Severity.Success => Color.Success,
                Severity.Error => Color.Error,
                        
                _ => Color.Default,
            };
        });
    }
    
    public async Task<UpdateResponse> CheckForUpdate(IJSRuntime jsRuntime)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(16));
        return await jsRuntime.InvokeAsync<UpdateResponse>("window.__TAURI__.invoke", cts.Token, "check_for_update");
    }
    
    public async Task InstallUpdate(IJSRuntime jsRuntime)
    {
        var cts = new CancellationTokenSource();
        await jsRuntime.InvokeVoidAsync("window.__TAURI__.invoke", cts.Token, "install_update");
    }

    #region IDisposable

    public void Dispose()
    {
        this.http.Dispose();
    }

    #endregion
}