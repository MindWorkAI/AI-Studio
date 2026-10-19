using AIStudio.Tools.Services;
using AIStudio.Tools.UserAPIKeys;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The panel of the home page which asks the user for the API key of the providers that one configuration
/// plugin marked with "PromptForUserAPIKey". The home page shows one such panel per plugin, as long as one
/// of its providers has no API key.
/// </summary>
public partial class UserAPIKeyPrompt : MSGComponentBase
{
    /// <summary>
    /// The providers of the configuration plugin which wait for the API key of the user.
    /// </summary>
    [Parameter, EditorRequired]
    public PendingUserAPIKeyGroup Group { get; set; } = null!;

    /// <summary>
    /// Whether the panel is expanded.
    /// </summary>
    [Parameter]
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Called when the user expands or collapses the panel.
    /// </summary>
    [Parameter]
    public Func<bool, Task> ExpandedChanged { get; set; } = _ => Task.CompletedTask;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<UserAPIKeyPrompt> Logger { get; init; } = null!;

    private string apiKey = string.Empty;
    private string issue = string.Empty;
    private bool isBusy;

    private string Title => string.IsNullOrWhiteSpace(this.Group.Prompt?.Title) ? T("Your API key is missing") : this.Group.Prompt.Title;

    private async Task StoreAPIKeyAsync()
    {
        var enteredAPIKey = this.apiKey.Trim();
        if (this.isBusy || string.IsNullOrWhiteSpace(enteredAPIKey))
            return;

        this.isBusy = true;
        this.issue = string.Empty;
        try
        {
            var result = await PendingUserAPIKeys.StoreAsync(this.Group, enteredAPIKey, this.RustService, this.Logger);
            if (result.Check is UserAPIKeyCheckOutcome.REJECTED)
            {
                this.issue = T("The provider did not accept this API key. Please check the key and try again.");
                return;
            }

            //
            // Once the key is stored, the home page builds its panels anew, and this panel loses what it shows.
            // Hence, a failure to store the key goes into a message of its own instead of into this panel:
            //
            if (result.FailedProviderNames.Count > 0)
                await this.MessageBus.SendError(new DataErrorMessage(Icons.Material.Filled.Key, string.Format(T("AI Studio could not store the API key for these providers: {0}"), string.Join(", ", result.FailedProviderNames))));

            if (result.FailedProviderNames.Count == this.Group.Targets.Count)
                return;

            this.apiKey = string.Empty;
            if (result.Check is UserAPIKeyCheckOutcome.ACCEPTED)
                await this.MessageBus.SendSuccess(new DataSuccessMessage(Icons.Material.Filled.Key, T("AI Studio checked your API key and stored it. You can now use the AI providers of your organization.")));
            else
                await this.MessageBus.SendWarning(new DataWarningMessage(Icons.Material.Filled.Key, T("AI Studio stored your API key but could not check it. If the AI providers of your organization do not work, please correct the key in the settings.")));

            // Tell everyone that the providers are usable now. The home page removes this panel once no provider waits any longer:
            await this.SendMessage<bool>(Event.CONFIGURATION_CHANGED);
        }
        finally
        {
            this.isBusy = false;
        }
    }
}