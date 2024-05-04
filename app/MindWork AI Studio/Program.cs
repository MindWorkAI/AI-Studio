using AIStudio.Components;
using AIStudio.Settings;

using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddSingleton<SettingsManager>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(x =>
    {
        x.MaximumReceiveMessageSize = null;
    });

builder.WebHost.UseKestrel();
builder.WebHost.UseWebRoot("wwwroot");
builder.WebHost.UseStaticWebAssets();
builder.WebHost.UseUrls("http://localhost:5000");

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();