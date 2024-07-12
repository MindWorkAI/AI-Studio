using AIStudio.Components.CommonDialogs;
using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

using DialogOptions = AIStudio.Components.CommonDialogs.DialogOptions;

namespace AIStudio.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IMessageBusReceiver
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
    
    private bool isUpdateAvailable;
    private bool performingUpdate;
    private bool userDismissedUpdate;
    private string updateToVersion = string.Empty;
    private UpdateResponse? currentUpdateResponse;
    
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
        this.MessageBus.ApplyFilters(this, [], [ Event.UPDATE_AVAILABLE, Event.USER_SEARCH_FOR_UPDATE ]);
        
        // Set the js runtime for the update service:
        UpdateService.SetBlazorDependencies(this.JsRuntime, this.Snackbar);
        
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
        if (dialogResult.Canceled)
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
            if (dialogResult.Canceled)
            {
                context.PreventNavigation();
                return;
            }

            // User accepted to leave the chat page, reset the chat state:
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.RESET_CHAT_STATE);
        }
    }
}