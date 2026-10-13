using AIStudio.Provider;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Asks the user for the API key of the providers which a configuration plugin marked with
/// "PromptForUserAPIKey", as long as these providers have none. Renders nothing otherwise.
/// </summary>
public partial class UserAPIKeyPrompt : MSGComponentBase
{
    private sealed class GroupState
    {
        public string APIKey { get; set; } = string.Empty;

        public string Issue { get; set; } = string.Empty;

        public bool IsBusy { get; set; }
    }

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<UserAPIKeyPrompt> Logger { get; init; } = null!;

    private readonly Dictionary<Guid, GroupState> states = new();
    private IReadOnlyList<PendingUserAPIKeyGroup> groups = [];
    private int refreshGeneration;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.STARTUP_COMPLETED ]);
        await base.OnInitializedAsync();
        await this.Refresh();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
            case Event.CONFIGURATION_CHANGED:
            case Event.STARTUP_COMPLETED:
                await this.Refresh();
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }

    #endregion

    private GroupState GetState(PendingUserAPIKeyGroup group)
    {
        if (!this.states.TryGetValue(group.ConfigPluginId, out var state))
        {
            state = new();
            this.states[group.ConfigPluginId] = state;
        }

        return state;
    }

    private async Task Refresh()
    {
        if (!this.SettingsManager.HasCompletedInitialSettingsLoad)
            return;

        // Several events may ask for a refresh at once; only the latest answer counts:
        var generation = ++this.refreshGeneration;
        var pendingGroups = await PendingUserAPIKeys.FindAsync(this.SettingsManager, this.RustService);
        if (generation != this.refreshGeneration)
            return;

        this.groups = pendingGroups;
        foreach (var obsoleteId in this.states.Keys.Where(id => pendingGroups.All(group => group.ConfigPluginId != id)).ToList())
            this.states.Remove(obsoleteId);
    }

    private async Task StoreAPIKey(PendingUserAPIKeyGroup group)
    {
        var state = this.GetState(group);
        var apiKey = state.APIKey.Trim();
        if (state.IsBusy || string.IsNullOrWhiteSpace(apiKey))
            return;

        state.IsBusy = true;
        state.Issue = string.Empty;
        try
        {
            //
            // Ask one of the providers whether it accepts the key. A rejected key is not stored. When
            // the provider cannot answer for another reason, we store the key anyway and say so:
            // otherwise, an unreachable server would keep the user from finishing the setup.
            //
            var check = await group.Targets[0].LoadModels(apiKey);
            if (check.FailureReason is ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY or ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR)
            {
                this.Logger.LogWarning("The provider '{ProviderName}' rejected the API key the user entered: {FailureReason}.", group.Targets[0].Name, check.FailureReason);
                state.Issue = T("The API key was not accepted. Please check the key and try again.");
                return;
            }

            var failedNames = new List<string>();
            foreach (var target in group.Targets)
            {
                var storeResponse = await this.RustService.SetAPIKey(target.SecretId, apiKey, target.StoreType);
                if (storeResponse.Success)
                    continue;

                this.Logger.LogError("Failed to store the API key for the provider '{ProviderName}': {Issue}", target.Name, storeResponse.Issue);
                failedNames.Add(target.Name);
            }

            if (failedNames.Count > 0)
                state.Issue = string.Format(T("The API key could not be stored for: {0}"), string.Join(", ", failedNames));
            else
                state.APIKey = string.Empty;

            if (failedNames.Count < group.Targets.Count)
            {
                if (check.Success)
                    await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Key, T("The API key was accepted and stored. You can use your AI providers now.")));
                else
                    await this.MessageBus.SendWarning(new(Icons.Material.Filled.Key, T("The API key was stored, but could not be checked, because the provider did not answer as expected. If the AI providers do not work, please correct the key in the settings.")));
            }

            // Tell everyone that the providers are usable now:
            await this.SendMessage<bool>(Event.CONFIGURATION_CHANGED);
            await this.Refresh();
        }
        finally
        {
            state.IsBusy = false;
        }
    }
}
