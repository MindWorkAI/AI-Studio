using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Layout;

public partial class MainLayout : LayoutComponentBase, IMessageBusReceiver, IDisposable
{
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;
    
    [Inject]
    private ILogger<MainLayout> Logger { get; init; } = null!;
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;
    
    private string PaddingLeft => this.navBarOpen ? $"padding-left: {NAVBAR_EXPANDED_WIDTH_INT - NAVBAR_COLLAPSED_WIDTH_INT}em;" : "padding-left: 0em;";
    
    private const int NAVBAR_COLLAPSED_WIDTH_INT = 4;
    private const int NAVBAR_EXPANDED_WIDTH_INT = 10;
    private static readonly string NAVBAR_COLLAPSED_WIDTH = $"{NAVBAR_COLLAPSED_WIDTH_INT}em";
    private static readonly string NAVBAR_EXPANDED_WIDTH = $"{NAVBAR_EXPANDED_WIDTH_INT}em";
    
    private bool navBarOpen;
    private bool performingUpdate;
    private UpdateResponse? currentUpdateResponse;
    private MudThemeProvider themeProvider = null!;
    private bool useDarkMode;

    private IReadOnlyCollection<NavBarItem> navItems = [];
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.NavigationManager.RegisterLocationChangingHandler(this.OnLocationChanging);
        
        //
        // We use the Tauri API (Rust) to get the data and config directories
        // for this app.
        //
        var dataDir = await this.RustService.GetDataDirectory();
        var configDir = await this.RustService.GetConfigDirectory();
        
        this.Logger.LogInformation($"The data directory is: '{dataDir}'");
        this.Logger.LogInformation($"The config directory is: '{configDir}'");
        
        // Store the directories in the settings manager:
        SettingsManager.ConfigDirectory = configDir;
        SettingsManager.DataDirectory = dataDir;
        Directory.CreateDirectory(SettingsManager.DataDirectory);
        
        //
        // Read the user language from Rust:
        //
        var userLanguage = await this.RustService.ReadUserLanguage();
        this.Logger.LogInformation($"The user language is: '{userLanguage}'");
        
        // Ensure that all settings are loaded:
        await this.SettingsManager.LoadSettings();
        
        //
        // We cannot process the plugins before the settings are loaded,
        // and we know our data directory.
        //
        if(PreviewFeatures.PRE_PLUGINS_2025.IsEnabled(this.SettingsManager))
        {
            // Ensure that all internal plugins are present:
            await PluginFactory.EnsureInternalPlugins();
            
            // Load (but not start) all plugins, without waiting for them:
            var pluginLoadingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _ = PluginFactory.LoadAll(pluginLoadingTimeout.Token);
            
            // Set up hot reloading for plugins:
            PluginFactory.SetUpHotReloading();
        }
        
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.UPDATE_AVAILABLE, Event.CONFIGURATION_CHANGED, Event.COLOR_THEME_CHANGED, Event.SHOW_ERROR ]);
        
        // Set the snackbar for the update service:
        UpdateService.SetBlazorDependencies(this.Snackbar);
        TemporaryChatService.Initialize();
        
        // Should the navigation bar be open by default?
        if(this.SettingsManager.ConfigurationData.App.NavigationBehavior is NavBehavior.ALWAYS_EXPAND)
            this.navBarOpen = true;

        // Solve issue https://github.com/MudBlazor/MudBlazor/issues/11133:
        MudGlobal.TooltipDefaults.Duration = TimeSpan.Zero;
        
        await this.themeProvider.WatchSystemPreference(this.SystemeThemeChanged);
        await this.UpdateThemeConfiguration();
        this.LoadNavItems();

        await base.OnInitializedAsync();
    }

    private void LoadNavItems()
    {
        this.navItems = new List<NavBarItem>(this.GetNavItems());
    }

    #endregion

    #region Implementation of IMessageBusReceiver

    public string ComponentName => nameof(MainLayout);
    
    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.UPDATE_AVAILABLE:
                if (data is UpdateResponse updateResponse)
                {
                    this.currentUpdateResponse = updateResponse;
                    this.Snackbar.Add($"An update to version {updateResponse.NewVersion} is available.", Severity.Info, config =>
                    {
                        config.Icon = Icons.Material.Filled.Update;
                        config.IconSize = Size.Large;
                        config.HideTransitionDuration = 600;
                        config.VisibleStateDuration = 32_000;
                        config.OnClick = async _ =>
                        {
                            await this.ShowUpdateDialog();
                        };
                        config.Action = "Show details";
                        config.ActionVariant = Variant.Filled;
                        config.ActionColor = Color.Dark;
                    });
                }
                
                break;
            
            case Event.CONFIGURATION_CHANGED:
                if(this.SettingsManager.ConfigurationData.App.NavigationBehavior is NavBehavior.ALWAYS_EXPAND)
                    this.navBarOpen = true;
                else
                    this.navBarOpen = false;
                
                await this.UpdateThemeConfiguration();
                this.LoadNavItems();
                this.StateHasChanged();
                break;
            
            case Event.COLOR_THEME_CHANGED:
                this.StateHasChanged();
                break;
            
            case Event.SHOW_ERROR:
                if (data is Error error)
                    error.Show(this.Snackbar);
                
                break;
        }
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion

    private IEnumerable<NavBarItem> GetNavItems()
    {
        var palette = this.ColorTheme.GetCurrentPalette(this.SettingsManager);
        
        yield return new("Home", Icons.Material.Filled.Home, palette.DarkLighten, palette.GrayLight, Routes.HOME, true);
        yield return new("Chat", Icons.Material.Filled.Chat, palette.DarkLighten, palette.GrayLight, Routes.CHAT, false);
        yield return new("Assistants", Icons.Material.Filled.Apps, palette.DarkLighten, palette.GrayLight, Routes.ASSISTANTS, false);

        if (PreviewFeatures.PRE_WRITER_MODE_2024.IsEnabled(this.SettingsManager))
            yield return new("Writer", Icons.Material.Filled.Create, palette.DarkLighten, palette.GrayLight, Routes.WRITER, false);

        if (PreviewFeatures.PRE_PLUGINS_2025.IsEnabled(this.SettingsManager))
            yield return new("Plugins", Icons.Material.TwoTone.Extension, palette.DarkLighten, palette.GrayLight, Routes.PLUGINS, false);
        
        yield return new("Supporters", Icons.Material.Filled.Favorite, palette.Error.Value, "#801a00", Routes.SUPPORTERS, false);
        yield return new("About", Icons.Material.Filled.Info, palette.DarkLighten, palette.GrayLight, Routes.ABOUT, false);
        yield return new("Settings", Icons.Material.Filled.Settings, palette.DarkLighten, palette.GrayLight, Routes.SETTINGS, false);
    }

    private async Task ShowUpdateDialog()
    {
        if(this.currentUpdateResponse is null)
            return;
        
        //
        // Replace the fir line with `# Changelog`:
        //
        var changelog = this.currentUpdateResponse.Value.Changelog;
        if (!string.IsNullOrWhiteSpace(changelog))
        {
            var lines = changelog.Split('\n');
            if (lines.Length > 0)
                lines[0] = "# Changelog";
            
            changelog = string.Join('\n', lines);
        }
        
        var updatedResponse = this.currentUpdateResponse.Value with { Changelog = changelog };
        var dialogParameters = new DialogParameters<UpdateDialog>
        {
            { x => x.UpdateResponse, updatedResponse }
        };

        var dialogReference = await this.DialogService.ShowAsync<UpdateDialog>("Update", dialogParameters, DialogOptions.FULLSCREEN_NO_HEADER);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.performingUpdate = true;
        this.StateHasChanged();
        await this.RustService.InstallUpdate();
    }
    
    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters
            {
                { "Message", "Are you sure you want to leave the chat page? All unsaved changes will be lost." },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Leave Chat Page", dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
            {
                context.PreventNavigation();
                return;
            }

            // User accepted to leave the chat page, reset the chat state:
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.RESET_CHAT_STATE);
        }
    }
    
    private async Task SystemeThemeChanged(bool isDark)
    {
        this.Logger.LogInformation($"The system theme changed to {(isDark ? "dark" : "light")}.");
        await this.UpdateThemeConfiguration();
    }

    private async Task UpdateThemeConfiguration()
    {
        if (this.SettingsManager.ConfigurationData.App.PreferredTheme is Themes.SYSTEM)
            this.useDarkMode = await this.themeProvider.GetSystemPreference();
        else
            this.useDarkMode = this.SettingsManager.ConfigurationData.App.PreferredTheme == Themes.DARK;

        this.SettingsManager.IsDarkMode = this.useDarkMode;
        await this.MessageBus.SendMessage<bool>(this, Event.COLOR_THEME_CHANGED);
        this.StateHasChanged();
    }

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}