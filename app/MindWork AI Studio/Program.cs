using AIStudio;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools;

using MudBlazor;
using MudBlazor.Services;

#if !DEBUG
using System.Reflection;
using Microsoft.Extensions.FileProviders;
#endif

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
builder.Services.AddSingleton<Rust>();
builder.Services.AddMudMarkdownClipboardService<MarkdownClipboardService>();
builder.Services.AddSingleton<SettingsManager>();
builder.Services.AddSingleton<Random>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(x =>
    {
        x.MaximumReceiveMessageSize = null;
    });

var port = args.Length > 0 ? args[0] : "5000";
builder.WebHost.UseUrls($"http://localhost:{port}");

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

Console.WriteLine("RUST/TAURI SERVER STARTED");
await serverTask;