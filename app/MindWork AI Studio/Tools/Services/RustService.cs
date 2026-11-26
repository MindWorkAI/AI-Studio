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

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(RustService).Namespace, nameof(RustService));

    /// <summary>
    /// How long the runtime may take to answer whether it is alive. Its handler does no work, so
    /// anything longer means that the runtime cannot answer at the moment.
    /// </summary>
    private static readonly TimeSpan ALIVE_CHECK_TIMEOUT = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the runtime may take to log one batch of log events.
    /// </summary>
    private static readonly TimeSpan LOG_TIMEOUT = TimeSpan.FromSeconds(30);

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

    /// <summary>
    /// A dedicated client for checking whether the runtime is alive.
    /// </summary>
    /// <remarks>
    /// All other calls share one HTTP/2 connection. A flood of requests can congest it, and the
    /// runtime may close it, while the runtime itself keeps running. This client opens a connection
    /// of its own, so its check fails only when the runtime cannot accept a connection and answer.
    /// </remarks>
    private readonly HttpClient aliveHttp;

    /// <summary>
    /// A dedicated client for the log events, so a burst of them never waits in front of other calls.
    /// </summary>
    private readonly HttpClient logHttp;

    private readonly RustLogForwarder logForwarder;

    private readonly SemaphoreSlim fileDialogLock = new(1, 1);
    private readonly SemaphoreSlim userLanguageLock = new(1, 1);
    private readonly SemaphoreSlim userNameLock = new(1, 1);

    /// <summary>
    /// The JSON options of nearly every call to the runtime, which names its fields in snake case.
    /// </summary>
    internal static readonly JsonSerializerOptions JSON_OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new RustEnumConverter(),
        },
        TypeInfoResolver = RustJsonContext.Default,
    }.AsReadOnly();

    /// <summary>
    /// The JSON options of the events of a file extraction, which name their fields themselves.
    /// </summary>
    /// <remarks>
    /// The same as the defaults of System.Text.Json, with which these events were read before.
    /// </remarks>
    internal static readonly JsonSerializerOptions CONTENT_STREAM_JSON_OPTIONS = new JsonSerializerOptions
    {
        TypeInfoResolver = RustContentStreamJsonContext.Default,
    }.AsReadOnly();

    /// <summary>
    /// The JSON options of the prompt injection filter, which names its fields in camel case.
    /// </summary>
    /// <remarks>
    /// The same as the defaults of the JSON calls of HttpClient, with which the filter was called before.
    /// </remarks>
    internal static readonly JsonSerializerOptions PROMPT_INJECTION_JSON_OPTIONS = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = RustPromptInjectionJsonContext.Default,
    }.AsReadOnly();

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
        this.aliveHttp = CreateHttpClient(apiPort, certificateFingerprint, ALIVE_CHECK_TIMEOUT);
        this.logHttp = CreateHttpClient(apiPort, certificateFingerprint, LOG_TIMEOUT);
        this.logForwarder = new(this.logHttp, RustLogForwarder.PAUSE_AFTER_FAILURE);
    }

    /// <summary>
    /// Talks to a runtime which a test plays itself.
    /// </summary>
    /// <remarks>
    /// Only for tests. The runtime filters prompt injections out of every file and text, so without
    /// it no test could check which file the app asks for, or what it does with the answer.
    /// </remarks>
    /// <param name="runtime">Answers every request in place of the runtime.</param>
    internal RustService(HttpMessageHandler runtime)
    {
        this.apiPort = string.Empty;
        this.certificateFingerprint = string.Empty;
        this.http = new HttpClient(runtime, disposeHandler: false) { BaseAddress = new Uri("https://127.0.0.1"), Timeout = TimeSpan.FromSeconds(100) };
        this.extractionHttp = new HttpClient(runtime, disposeHandler: false) { BaseAddress = new Uri("https://127.0.0.1"), Timeout = Timeout.InfiniteTimeSpan };
        this.aliveHttp = new HttpClient(runtime, disposeHandler: false) { BaseAddress = new Uri("https://127.0.0.1"), Timeout = ALIVE_CHECK_TIMEOUT };
        this.logHttp = new HttpClient(runtime, disposeHandler: false) { BaseAddress = new Uri("https://127.0.0.1"), Timeout = LOG_TIMEOUT };
        this.logForwarder = new(this.logHttp, RustLogForwarder.PAUSE_AFTER_FAILURE);
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
        // First, so the waiting log events still reach the runtime:
        this.logForwarder.Dispose();
        this.logHttp.Dispose();

        this.http.Dispose();
        this.extractionHttp.Dispose();
        this.aliveHttp.Dispose();
        this.userLanguageLock.Dispose();
        this.userNameLock.Dispose();
        base.Dispose();
    }

    #endregion
}
