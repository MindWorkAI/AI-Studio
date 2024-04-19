using AIStudio.Components;
using AIStudio.Settings;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMudServices();
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