using AIStudio.Agents;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

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
        builder.Services.AddMudMarkdownClipboardService<MarkdownClipboardService>();
        builder.Services.AddSingleton<SettingsManager>();
        builder.Services.AddSingleton<ThreadSafeRandom>();
        builder.Services.AddSingleton<DataSourceService>();
        builder.Services.AddTransient<HTMLParser>();
        builder.Services.AddTransient<AgentDataSourceSelection>();
        builder.Services.AddTransient<AgentRetrievalContextValidation>();
        builder.Services.AddTransient<AgentTextContentCleaner>();
        builder.Services.AddHostedService<UpdateService>();
        builder.Services.AddHostedService<TemporaryChatService>();
        builder.Services.AddHostedService<EnterpriseEnvironmentService>();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(options =>
            {
                options.MaximumReceiveMessageSize = null;
                options.ClientTimeoutInterval = TimeSpan.FromDays(14);
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);
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

        RUST_SERVICE = rust;
        ENCRYPTION = encryption;

        programLogger.LogInformation("Initialize internal file system.");
        app.Use(Redirect.HandlerContentAsync);

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
        PluginFactory.Dispose();
        programLogger.LogInformation("The AI Studio server was stopped.");
    }
}