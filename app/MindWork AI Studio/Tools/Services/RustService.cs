using System.Security.Cryptography;
using System.Text.Json;

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

    /// <summary>
    /// A dedicated client for file extraction.
    /// </summary>
    /// <remarks>
    /// Extraction needs its own client because <see cref="HttpClient.Timeout"/> is a client-wide
    /// setting which also covers reading the streamed response body. A per-request cancellation
    /// token can only shorten that limit, never extend it. Reading a large file from a slow
    /// network share legitimately exceeds the default limit, so this client has no timeout of its
    /// own and the extraction bounds each request itself.
    /// </remarks>
    private readonly HttpClient extractionHttp;

    private readonly SemaphoreSlim fileDialogLock = new(1, 1);
    private readonly SemaphoreSlim userLanguageLock = new(1, 1);
    private readonly SemaphoreSlim userNameLock = new(1, 1);

    private readonly JsonSerializerOptions jsonRustSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new RustEnumConverter(),
        },
    };
    
    private ILogger<RustService>? logger;
    private Encryption? encryptor;
    private string? cachedUserLanguage;
    private string? cachedUserName;
    
    private readonly string apiPort;
    private readonly string certificateFingerprint;
    
    public RustService(string apiPort, string certificateFingerprint)
    {
        this.apiPort = apiPort;
        this.certificateFingerprint = certificateFingerprint;

        // The default timeout of HttpClient, kept explicit so the difference to the
        // extraction client below is visible:
        this.http = CreateHttpClient(apiPort, certificateFingerprint, TimeSpan.FromSeconds(100));
        this.extractionHttp = CreateHttpClient(apiPort, certificateFingerprint, Timeout.InfiniteTimeSpan);
    }

    private static HttpClient CreateHttpClient(string apiPort, string certificateFingerprint, TimeSpan timeout)
    {
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

        var client = new HttpClient(certificateValidationHandler)
        {
            BaseAddress = new Uri($"https://127.0.0.1:{apiPort}"),
            DefaultRequestVersion = Version.Parse("2.0"),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Timeout = timeout,
        };

        client.DefaultRequestHeaders.AddApiToken();
        return client;
    }

    public void SetLogger(ILogger<RustService> logService)
    {
        this.logger = logService;
    }
    
    public void SetEncryptor(Encryption encryptionService)
    {
        this.encryptor = encryptionService;
    }

    private Task ReportRustServiceUnavailable(string reason) => MessageBus.INSTANCE.SendMessage(null, Event.RUST_SERVICE_UNAVAILABLE, reason);

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
        this.extractionHttp.Dispose();
        this.userLanguageLock.Dispose();
        this.userNameLock.Dispose();
        base.Dispose();
    }

    #endregion
}
