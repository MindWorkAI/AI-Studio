using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using DialogOptions = AIStudio.Dialogs.DialogOptions;
using EnterpriseEnvironment = AIStudio.Tools.EnterpriseEnvironment;

namespace AIStudio.Layout;

public partial class MainLayout : LayoutComponentBase, IMessageBusReceiver, ILang, IDisposable
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
    
    private ILanguagePlugin Lang { get; set; } = PluginFactory.BaseLanguage;
    
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
        this.Logger.LogInformation($"The OS says '{userLanguage}' is the user language.");
        
        // Ensure that all settings are loaded:
        await this.SettingsManager.LoadSettings();
        
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [],
        [
            Event.UPDATE_AVAILABLE, Event.CONFIGURATION_CHANGED, Event.COLOR_THEME_CHANGED, Event.SHOW_ERROR,
            Event.SHOW_ERROR, Event.SHOW_WARNING, Event.SHOW_SUCCESS, Event.STARTUP_PLUGIN_SYSTEM,
            Event.PLUGINS_RELOADED
        ]);
        
        // Set the snackbar for the update service:
        UpdateService.SetBlazorDependencies(this.Snackbar);
        TemporaryChatService.Initialize();
        
        // Should the navigation bar be open by default?
        if(this.SettingsManager.ConfigurationData.App.NavigationBehavior is NavBehavior.ALWAYS_EXPAND)
            this.navBarOpen = true;

        // Solve issue https://github.com/MudBlazor/MudBlazor/issues/11133:
        MudGlobal.TooltipDefaults.Duration = TimeSpan.Zero;
        
        // Send a message to start the plugin system:
        await this.MessageBus.SendMessage<bool>(this, Event.STARTUP_PLUGIN_SYSTEM);
        
        await this.themeProvider.WatchSystemDarkModeAsync(this.SystemeThemeChanged);
        await this.UpdateThemeConfiguration();
        this.LoadNavItems();

        await base.OnInitializedAsync();
    }

    private void LoadNavItems()
    {
        this.navItems = new List<NavBarItem>(this.GetNavItems());
    }

    #endregion

    #region Implementation of ILang

    /// <inheritdoc />
    public string T(string fallbackEN) => this.GetText(this.Lang, fallbackEN);

    /// <inheritdoc />
    public string T(string fallbackEN, string? typeNamespace, string? typeName) => this.GetText(this.Lang, fallbackEN, typeNamespace, typeName);

    #endregion

    #region Implementation of IMessageBusReceiver

    public string ComponentName => nameof(MainLayout);
    
    public async Task ProcessMessage<TMessage>(ComponentBase? sendingComponent, Event triggeredEvent, TMessage? data)
    {
        await this.InvokeAsync(async () =>
        {
            switch (triggeredEvent)
            {
                case Event.UPDATE_AVAILABLE:
                    if (data is UpdateResponse updateResponse)
                    {
                        this.currentUpdateResponse = updateResponse;
                        var message = string.Format(T("An update to version {0} is available."), updateResponse.NewVersion);
                        this.Snackbar.Add(message, Severity.Info, config =>
                        {
                            config.Icon = Icons.Material.Filled.Update;
                            config.IconSize = Size.Large;
                            config.HideTransitionDuration = 600;
                            config.VisibleStateDuration = 32_000;
                            config.OnClick = async _ =>
                            {
                                await this.ShowUpdateDialog();
                            };
                            config.Action = T("Show details");
                            config.ActionVariant = Variant.Filled;
                        });
                    }

                    break;

                case Event.CONFIGURATION_CHANGED:
                    if (this.SettingsManager.ConfigurationData.App.NavigationBehavior is NavBehavior.ALWAYS_EXPAND)
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

                case Event.SHOW_SUCCESS:
                    if (data is DataSuccessMessage success)
                        success.Show(this.Snackbar);

                    break;

                case Event.SHOW_ERROR:
                    if (data is DataErrorMessage error)
                        error.Show(this.Snackbar);

                    break;

                case Event.SHOW_WARNING:
                    if (data is DataWarningMessage warning)
                        warning.Show(this.Snackbar);

                    break;

                case Event.STARTUP_PLUGIN_SYSTEM:
                    _ = Task.Run(async () =>
                    {
                        // Set up the plugin system:
                        if (PluginFactory.Setup())
                        {
                            // Ensure that all internal plugins are present:
                            await PluginFactory.EnsureInternalPlugins();

                            //
                            // Check if there is an enterprise configuration plugin to download:
                            //
                            var enterpriseEnvironment = this.MessageBus.CheckDeferredMessages<EnterpriseEnvironment>(Event.STARTUP_ENTERPRISE_ENVIRONMENT).FirstOrDefault();
                            if (enterpriseEnvironment != default)
                                await PluginFactory.TryDownloadingConfigPluginAsync(enterpriseEnvironment.ConfigurationId, enterpriseEnvironment.ConfigurationServerUrl);

                            // Load (but not start) all plugins without waiting for them:
                            #if DEBUG
                            var pluginLoadingTimeout = new CancellationTokenSource();
                            #else
                            var pluginLoadingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            #endif
                            await PluginFactory.LoadAll(pluginLoadingTimeout.Token);

                            // Set up hot reloading for plugins:
                            PluginFactory.SetUpHotReloading();
                        }
                    });
                    break;

                case Event.PLUGINS_RELOADED:
                    this.Lang = await this.SettingsManager.GetActiveLanguagePlugin();
                    I18N.Init(this.Lang);
                    this.LoadNavItems();

                    await this.InvokeAsync(this.StateHasChanged);
                    break;
            }
        });
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion

    private IEnumerable<NavBarItem> GetNavItems()
    {
        var palette = this.ColorTheme.GetCurrentPalette(this.SettingsManager);
        
        yield return new(T("Home"), Icons.Material.Filled.Home, palette.DarkLighten, palette.GrayLight, Routes.HOME, true);
        yield return new(T("Chat"), Icons.Material.Filled.Chat, palette.DarkLighten, palette.GrayLight, Routes.CHAT, false);
        yield return new(T("Assistants"), Icons.Material.Filled.Apps, palette.DarkLighten, palette.GrayLight, Routes.ASSISTANTS, false);

        if (PreviewFeatures.PRE_WRITER_MODE_2024.IsEnabled(this.SettingsManager))
            yield return new(T("Writer"), Icons.Material.Filled.Create, palette.DarkLighten, palette.GrayLight, Routes.WRITER, false);

        yield return new(T("Plugins"), Icons.Material.TwoTone.Extension, palette.DarkLighten, palette.GrayLight, Routes.PLUGINS, false);
        yield return new(T("Supporters"), Icons.Material.Filled.Favorite, palette.Error.Value, "#801a00", Routes.SUPPORTERS, false);
        yield return new(T("About"), Icons.Material.Filled.Info, palette.DarkLighten, palette.GrayLight, Routes.ABOUT, false);
        yield return new(T("Settings"), Icons.Material.Filled.Settings, palette.DarkLighten, palette.GrayLight, Routes.SETTINGS, false);
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

        var dialogReference = await this.DialogService.ShowAsync<UpdateDialog>(T("Update"), dialogParameters, DialogOptions.FULLSCREEN_NO_HEADER);
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
                { "Message", T("Are you sure you want to leave the chat page? All unsaved changes will be lost.") },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Leave Chat Page"), dialogParameters, DialogOptions.FULLSCREEN);
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
            this.useDarkMode = await this.themeProvider.GetSystemDarkModeAsync();
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