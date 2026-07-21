using AIStudio.Agents;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Settings;
using AIStudio.Tools.Databases;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging.Console;

using MudBlazor.Services;

using MudExtensions.Services;

#if !DEBUG
using System.Reflection;
using Microsoft.Extensions.FileProviders;
#endif

namespace AIStudio;

internal sealed class Program
{
    public static RustService RUST_SERVICE = null!;
    public static Encryption ENCRYPTION = null!;
    public static string API_TOKEN = null!;
    public static IServiceProvider SERVICE_PROVIDER = null!;
    public static ILoggerFactory LOGGER_FACTORY = null!;
    public static DatabaseClientProvider DATABASE_CLIENT_PROVIDER = null!;
    
    public static async Task Main()
    {
        #if DEBUG
        // Read the environment variables from the .env file:
        var envFilePath = Path.Combine("..", "..", "startup.env");
        await EnvFile.Apply(envFilePath);
        #endif
        
        // Read the secret key for the IPC from the AI_STUDIO_SECRET_KEY environment variable:
        var secretPasswordEncoded = Environment.GetEnvironmentVariable("AI_STUDIO_SECRET_PASSWORD");
        if(string.IsNullOrWhiteSpace(secretPasswordEncoded))
        {
            Console.WriteLine("Error: The AI_STUDIO_SECRET_PASSWORD environment variable is not set.");
            return;
        }

        var secretPassword = Convert.FromBase64String(secretPasswordEncoded);
        var secretKeySaltEncoded = Environment.GetEnvironmentVariable("AI_STUDIO_SECRET_KEY_SALT");
        if(string.IsNullOrWhiteSpace(secretKeySaltEncoded))
        {
            Console.WriteLine("Error: The AI_STUDIO_SECRET_KEY_SALT environment variable is not set.");
            return;
        }

        var secretKeySalt = Convert.FromBase64String(secretKeySaltEncoded);
        
        var certificateFingerprint = Environment.GetEnvironmentVariable("AI_STUDIO_CERTIFICATE_FINGERPRINT");
        if(string.IsNullOrWhiteSpace(certificateFingerprint))
        {
            Console.WriteLine("Error: The AI_STUDIO_CERTIFICATE_FINGERPRINT environment variable is not set.");
            return;
        }
        
        var rustApiPort = Environment.GetEnvironmentVariable("AI_STUDIO_API_PORT");
        if(string.IsNullOrWhiteSpace(rustApiPort))
        {
            Console.WriteLine("Error: The AI_STUDIO_API_PORT environment variable is not set.");
            return;
        }
        
        var apiToken = Environment.GetEnvironmentVariable("AI_STUDIO_API_TOKEN");
        if(string.IsNullOrWhiteSpace(apiToken))
        {
            Console.WriteLine("Error: The AI_STUDIO_API_TOKEN environment variable is not set.");
            return;
        }
        
        API_TOKEN = apiToken;
        
        using var rust = new RustService(rustApiPort, certificateFingerprint);
        var appPort = await rust.GetAppPort();
        if(appPort == 0)
        {
            Console.WriteLine("Error: Failed to get the app port from Rust.");
            return;
        }
        
        var runtimeInfo = await rust.GetRuntimeInfo();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(kestrelServerOptions =>
        {
            kestrelServerOptions.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            });
        });
        
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles", LogLevel.Warning);
        builder.Logging.AddFilter("MudBlazor", LogLevel.Information);
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = TerminalLogger.FORMATTER_NAME;
        }).AddConsoleFormatter<TerminalLogger, ConsoleFormatterOptions>();

        if(runtimeInfo.LinuxPackageType == "flatpak")
        {
            try
            {
                var tauriDataDirectory = await rust.GetDataDirectory();
                if(string.IsNullOrWhiteSpace(tauriDataDirectory))
                    throw new InvalidOperationException("Rust returned an empty Tauri data directory.");

                var dataProtectionKeysDirectory = Path.Combine(tauriDataDirectory, "data-protection-keys");
                Directory.CreateDirectory(dataProtectionKeysDirectory);
                var writeTestPath = Path.Combine(dataProtectionKeysDirectory, $".write-test-{Guid.NewGuid():N}");
                using (new FileStream(writeTestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                }

                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory))
                    .SetApplicationName("org.mindworkai.AIStudio");
            }
            catch(Exception exception)
            {
                Console.WriteLine($"Error: Failed to configure Flatpak data-protection keys in the Tauri data directory: {exception.Message}");
                return;
            }
        }

        builder.Services.AddMudExtensions();
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 6_000; //milliseconds aka 6 seconds
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
        });

        builder.Services.AddMemoryCache(); // Needed for the Markdown library
        builder.Services.AddMudMarkdownServices();
        builder.Services.AddSingleton(new MudTheme());
        builder.Services.AddSingleton(MessageBus.INSTANCE);
        builder.Services.AddSingleton(rust);
        builder.Services.AddSingleton(typeof(RuntimeInfoResponse), runtimeInfo);
        builder.Services.AddMudMarkdownClipboardService<MarkdownClipboardService>();
        builder.Services.AddSingleton<SettingsManager>();
        builder.Services.AddSingleton<ThreadSafeRandom>();
        builder.Services.AddSingleton<AIJobService>();
        builder.Services.AddSingleton<AssistantSessionService>();
        builder.Services.AddSingleton<VoiceRecordingAvailabilityService>();
        builder.Services.AddSingleton<GlobalShortcutService>();
        builder.Services.AddSingleton<MediaTranscriptionService>();
        builder.Services.AddSingleton<AssistantPluginInstallService>();
        builder.Services.AddSingleton<UpdatePolicy>();
        builder.Services.AddSingleton<AssistantPluginGenerationService>();
        builder.Services.AddSingleton<DataSourceService>();
        builder.Services.AddScoped<PandocAvailabilityService>();
        builder.Services.AddTransient<HTMLParser>();
        builder.Services.AddTransient<AgentDataSourceSelection>();
        builder.Services.AddTransient<AgentRetrievalContextValidation>();
        builder.Services.AddTransient<AgentTextContentCleaner>();
        builder.Services.AddTransient<AssistantAuditAgent>();
        builder.Services.AddTransient<AssistantPluginAuditService>();
        builder.Services.AddHostedService<UpdateService>();
        builder.Services.AddHostedService<TemporaryChatService>();
        builder.Services.AddHostedService<TranscriptStagingCleanupService>();
        builder.Services.AddHostedService<EnterpriseEnvironmentService>();
        builder.Services.AddSingleton<DatabaseClientProvider>();
        builder.Services.AddHostedService<GlobalShortcutService>(serviceProvider => serviceProvider.GetRequiredService<GlobalShortcutService>());
        builder.Services.AddHostedService<RustAvailabilityMonitorService>();
        builder.Services.AddScoped<NativeShareService>();
        builder.Services.AddScoped<PluginShareService>();
        
        // ReSharper disable AccessToDisposedClosure
        builder.Services.AddHostedService<RustService>(_ => rust);
        // ReSharper restore AccessToDisposedClosure
        
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromDays(30);
                options.DisconnectedCircuitMaxRetained = 2;
            })
            .AddHubOptions(options =>
            {
                options.MaximumReceiveMessageSize = null;
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);
                options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            });

        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{appPort}")
        });

        builder.WebHost.UseUrls($"http://localhost:{appPort}");

        #if DEBUG
        builder.WebHost.UseWebRoot("wwwroot");
        builder.WebHost.UseStaticWebAssets();
        #endif

        // Execute the builder to get the app:
        var app = builder.Build();

        // Get the logging factory for e.g., static classes:
        LOGGER_FACTORY = app.Services.GetRequiredService<ILoggerFactory>();
        MessageBus.INSTANCE.Initialize(LOGGER_FACTORY.CreateLogger<MessageBus>());
        
        // Get a program logger:
        var programLogger = app.Services.GetRequiredService<ILogger<Program>>();
        programLogger.LogInformation("Starting the AI Studio server.");
        
        // Store the service provider (DI). We need it later for some classes,
        // which are not part of the request pipeline:
        SERVICE_PROVIDER = app.Services;
        
        // Initialize the encryption service:
        programLogger.LogInformation("Initializing the encryption service.");
        var encryptionLogger = app.Services.GetRequiredService<ILogger<Encryption>>();
        var encryption = new Encryption(encryptionLogger, secretPassword, secretKeySalt);
        var encryptionInitializer = encryption.Initialize();

        // Set the logger for the Rust service:
        programLogger.LogInformation("Initializing the Rust service.");
        var rustLogger = app.Services.GetRequiredService<ILogger<RustService>>();
        rust.SetLogger(rustLogger);
        rust.SetEncryptor(encryption);
        TerminalLogger.SetRustService(rust);

        RUST_SERVICE = rust;
        ENCRYPTION = encryption;
        DATABASE_CLIENT_PROVIDER = app.Services.GetRequiredService<DatabaseClientProvider>();

        programLogger.LogInformation("Initialize internal file system.");
        app.Use(Redirect.HandlerContentAsync);
        app.Use(FileHandler.HandlerAsync);

#if DEBUG
        app.UseStaticFiles();
        app.UseDeveloperExceptionPage();
#else
        var fileProvider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "wwwroot");
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = string.Empty,
        });
#endif

        app.UseAntiforgery();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        var serverTask = app.RunAsync();
        programLogger.LogInformation("Server was started successfully.");

        await encryptionInitializer;
        await rust.AppIsReady();
        programLogger.LogInformation("The AI Studio server is ready.");
        
        TaskScheduler.UnobservedTaskException += (sender, taskArgs) =>
        {
            programLogger.LogError(taskArgs.Exception, $"Unobserved task exception by sender '{sender ?? "n/a"}'.");
            taskArgs.SetObserved();
        };
        
        await serverTask;
        
        RUST_SERVICE.Dispose();
        DATABASE_CLIENT_PROVIDER.Dispose();
        PluginFactory.Dispose();
        programLogger.LogInformation("The AI Studio server was stopped.");
    }
}
