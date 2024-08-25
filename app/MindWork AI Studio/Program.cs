using AIStudio;
using AIStudio.Agents;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Services;

using MudBlazor.Services;

#if !DEBUG
using System.Reflection;
using Microsoft.Extensions.FileProviders;
#endif

if(args.Length == 0)
{
    Console.WriteLine("Please provide the port of the runtime API.");
    return;
}

var rustApiPort = args[0];
using var rust = new Rust(rustApiPort);
var appPort = await rust.GetAppPort();
if(appPort == 0)
{
    Console.WriteLine("Failed to get the app port from Rust.");
    return;
}

// Read the secret key for the IPC from the AI_STUDIO_SECRET_KEY environment variable:
var secretKey = Environment.GetEnvironmentVariable("AI_STUDIO_SECRET_KEY");
if(string.IsNullOrWhiteSpace(secretKey))
{
    Console.WriteLine("The AI_STUDIO_SECRET_KEY environment variable is not set.");
    return;
}

var builder = WebApplication.CreateBuilder();
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

builder.Services.AddMudMarkdownServices();
builder.Services.AddSingleton(MessageBus.INSTANCE);
builder.Services.AddSingleton(rust);
builder.Services.AddMudMarkdownClipboardService<MarkdownClipboardService>();
builder.Services.AddSingleton<SettingsManager>();
builder.Services.AddSingleton<ThreadSafeRandom>();
builder.Services.AddTransient<HTMLParser>();
builder.Services.AddTransient<AgentTextContentCleaner>();
builder.Services.AddHostedService<UpdateService>();
builder.Services.AddHostedService<TemporaryChatService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = null;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(1_200);
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

var app = builder.Build();
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

await rust.AppIsReady();
await serverTask;