using System.Security.Cryptography;
using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Version = System.Version;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Tools.Services;

/// <summary>
/// Calling Rust functions.
/// </summary>
public sealed partial class RustService : BackgroundService
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(RustService).Namespace, nameof(RustService));
    
    private readonly HttpClient http;

    private readonly JsonSerializerOptions jsonRustSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new TolerantEnumConverter() },
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

    #region Overrides of BackgroundService

    /// <summary>
    /// The main execution loop of the Rust service as a background thread.
    /// </summary>
    /// <param name="stopToken">The cancellation token to stop the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stopToken)
    {
        this.logger?.LogInformation("The Rust service was initialized.");
        
        // Start consuming Tauri events:
        await this.StartStreamTauriEvents(stopToken);
    }
    
    public override void Dispose()
    {
        this.http.Dispose();
        base.Dispose();
    }

    #endregion
}