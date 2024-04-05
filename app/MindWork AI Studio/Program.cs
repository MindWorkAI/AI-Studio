using AIStudio;
using AIStudio.Components;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddMudServices();
builder.Services.AddMudMarkdownServices();

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();