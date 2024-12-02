using System.Security.Cryptography;
using System.Text.Json;

using AIStudio.Tools.Rust;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Tools;

/// <summary>
/// Calling Rust functions.
/// </summary>
public sealed class RustService : IDisposable
{
    private readonly HttpClient http;

    private readonly JsonSerializerOptions jsonRustSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    private ILogger<RustService>? logger;
    private Encryption? encryptor;
    
    private readonly string apiPort;
    private readonly string certificateFingerprint;
    
    public RustService(string apiPort, string certificateFingerprint)
    {
        this.apiPort = apiPort;
        this.certificateFingerprint = certificateFingerprint;
        var certificateValidationHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                if(certificate is null)
                    return false;
                
                var currentCertificateFingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                return currentCertificateFingerprint == certificateFingerprint;
            },
        };
        
        this.http = new HttpClient(certificateValidationHandler)
        {
            BaseAddress = new Uri($"https://127.0.0.1:{apiPort}"),
            DefaultRequestVersion = Version.Parse("2.0"),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        
        this.http.DefaultRequestHeaders.AddApiToken();
    }

    public void SetLogger(ILogger<RustService> logService)
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
            var encryptedText = await text.Encrypt(this.encryptor!);
            var response = await this.http.PostAsync("/clipboard/set", new StringContent(encryptedText.EncryptedData));
            if (!response.IsSuccessStatusCode)
            {
                this.logger!.LogError($"Failed to copy the text to the clipboard due to an network error: '{response.StatusCode}'");
                message = "Failed to copy the text to your clipboard.";
                return;
            }

            var state = await response.Content.ReadFromJsonAsync<SetClipboardResponse>(this.jsonRustSerializerOptions);
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

    /// <summary>
    /// Try to get the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to get the API key for.</param>
    /// <param name="isTrying">Indicates if we are trying to get the API key. In that case, we don't log errors.</param>
    /// <returns>The requested secret.</returns>
    public async Task<RequestedSecret> GetAPIKey(ISecretId secretId, bool isTrying = false)
    {
        var secretRequest = new SelectSecretRequest($"provider::{secretId.SecretId}::{secretId.SecretName}::api_key", Environment.UserName, isTrying);
        var result = await this.http.PostAsJsonAsync("/secrets/get", secretRequest, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            if(!isTrying)
                this.logger!.LogError($"Failed to get the API key for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new RequestedSecret(false, new EncryptedText(string.Empty), "Failed to get the API key due to an API issue.");
        }
        
        var secret = await result.Content.ReadFromJsonAsync<RequestedSecret>(this.jsonRustSerializerOptions);
        if (!secret.Success && !isTrying)
            this.logger!.LogError($"Failed to get the API key for secret ID '{secretId.SecretId}': '{secret.Issue}'");
        
        return secret;
    }
    
    /// <summary>
    /// Try to store the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to store the API key for.</param>
    /// <param name="key">The API key to store.</param>
    /// <returns>The store secret response.</returns>
    public async Task<StoreSecretResponse> SetAPIKey(ISecretId secretId, string key)
    {
        var encryptedKey = await this.encryptor!.Encrypt(key);
        var request = new StoreSecretRequest($"provider::{secretId.SecretId}::{secretId.SecretName}::api_key", Environment.UserName, encryptedKey);
        var result = await this.http.PostAsJsonAsync("/secrets/store", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the API key for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new StoreSecretResponse(false, "Failed to get the API key due to an API issue.");
        }
        
        var state = await result.Content.ReadFromJsonAsync<StoreSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to store the API key for secret ID '{secretId.SecretId}': '{state.Issue}'");
        
        return state;
    }
    
    /// <summary>
    /// Tries to delete the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to delete the API key for.</param>
    /// <returns>The delete secret response.</returns>
    public async Task<DeleteSecretResponse> DeleteAPIKey(ISecretId secretId)
    {
        var request = new SelectSecretRequest($"provider::{secretId.SecretId}::{secretId.SecretName}::api_key", Environment.UserName, false);
        var result = await this.http.PostAsJsonAsync("/secrets/delete", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to delete the API key for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new DeleteSecretResponse{Success = false, WasEntryFound = false, Issue = "Failed to delete the API key due to an API issue."};
        }
        
        var state = await result.Content.ReadFromJsonAsync<DeleteSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to delete the API key for secret ID '{secretId.SecretId}': '{state.Issue}'");
        
        return state;
    }

    #region IDisposable

    public void Dispose()
    {
        this.http.Dispose();
    }

    #endregion
}