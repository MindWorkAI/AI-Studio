using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Layout;

public partial class MainLayout : LayoutComponentBase, IMessageBusReceiver, IDisposable
{
    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private Rust Rust { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    public string AdditionalHeight { get; private set; } = "0em";
    
    private string PaddingLeft => this.navBarOpen ? $"padding-left: {NAVBAR_EXPANDED_WIDTH_INT - NAVBAR_COLLAPSED_WIDTH_INT}em;" : "padding-left: 0em;";
    
    private const int NAVBAR_COLLAPSED_WIDTH_INT = 4;
    private const int NAVBAR_EXPANDED_WIDTH_INT = 10;
    private static readonly string NAVBAR_COLLAPSED_WIDTH = $"{NAVBAR_COLLAPSED_WIDTH_INT}em";
    private static readonly string NAVBAR_EXPANDED_WIDTH = $"{NAVBAR_EXPANDED_WIDTH_INT}em";
    
    private bool navBarOpen;
    private bool isUpdateAvailable;
    private bool performingUpdate;
    private bool userDismissedUpdate;
    private string updateToVersion = string.Empty;
    private UpdateResponse? currentUpdateResponse;
    
    private static readonly IReadOnlyCollection<NavBarItem> NAV_ITEMS = new List<NavBarItem>
    {
        new("Home", Icons.Material.Filled.Home, Color.Default, Routes.HOME, true),
        new("Chat", Icons.Material.Filled.Chat, Color.Default, Routes.CHAT, false),
        new("Assistants", Icons.Material.Filled.Apps, Color.Default, Routes.ASSISTANTS, false),
        new("Supporters", Icons.Material.Filled.Favorite, Color.Error, Routes.SUPPORTERS, false),
        new("About", Icons.Material.Filled.Info, Color.Default, Routes.ABOUT, false),
        new("Settings", Icons.Material.Filled.Settings, Color.Default, Routes.SETTINGS, false),
    };
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.NavigationManager.RegisterLocationChangingHandler(this.OnLocationChanging);
        
        //
        // We use the Tauri API (Rust) to get the data and config directories
        // for this app.
        //
        var dataDir = await this.JsRuntime.InvokeAsync<string>("window.__TAURI__.path.appLocalDataDir");
        var configDir = await this.JsRuntime.InvokeAsync<string>("window.__TAURI__.path.appConfigDir");
        
        // Store the directories in the settings manager:
        SettingsManager.ConfigDirectory = configDir;
        SettingsManager.DataDirectory = Path.Join(dataDir, "data");
        Directory.CreateDirectory(SettingsManager.DataDirectory);
        
        // Ensure that all settings are loaded:
        await this.SettingsManager.LoadSettings();
        
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.UPDATE_AVAILABLE, Event.USER_SEARCH_FOR_UPDATE, Event.CONFIGURATION_CHANGED ]);
        
        // Set the js runtime for the update service:
        UpdateService.SetBlazorDependencies(this.JsRuntime, this.Snackbar);
        TemporaryChatService.Initialize();
        
        // Should the navigation bar be open by default?
        if(this.SettingsManager.ConfigurationData.App.NavigationBehavior is NavBehavior.ALWAYS_EXPAND)
            this.navBarOpen = true;
        
        await base.OnInitializedAsync();
    }

    #endregion

    #region Implementation of IMessageBusReceiver

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.USER_SEARCH_FOR_UPDATE:
                this.userDismissedUpdate = false;
                break;
            
            case Event.UPDATE_AVAILABLE:
                if (data is UpdateResponse updateResponse)
                {
                    this.currentUpdateResponse = updateResponse;
                    this.isUpdateAvailable = updateResponse.UpdateIsAvailable;
                    this.updateToVersion = updateResponse.NewVersion;
                    
                    await this.InvokeAsync(this.StateHasChanged);
                    await this.SendMessage<bool>(Event.STATE_HAS_CHANGED);
                }
                
                break;
            
            case Event.CONFIGURATION_CHANGED:
                if(this.SettingsManager.ConfigurationData.App.NavigationBehavior is NavBehavior.ALWAYS_EXPAND)
                    this.navBarOpen = true;
                else
                    this.navBarOpen = false;
                
                this.StateHasChanged();
                break;
        }
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion
    
    private async Task DismissUpdate()
    {
        this.userDismissedUpdate = true;
        this.AdditionalHeight = "0em";
        
        await this.SendMessage<bool>(Event.STATE_HAS_CHANGED);
    }
    
    private bool IsUpdateAlertVisible
    {
        get
        {
            var state = this.isUpdateAvailable && !this.userDismissedUpdate;
            this.AdditionalHeight = state ? "3em" : "0em";
            
            return state;
        }
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
        await this.Rust.InstallUpdate(this.JsRuntime);
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

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}