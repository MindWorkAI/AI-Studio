using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Tells the person that files in the test folder replace prompts of the lab, so that nobody mistakes the answers of
/// experimental prompts for those of the lab, and names the files there which replace no prompt, so that nobody
/// takes the answers of the lab for those of their experimental prompts.
/// </summary>
/// <remarks>
/// It checks the folder when it appears, e.g., for every step the person opens, and whenever a setting changes. It
/// shows nothing while the setting does not allow prompt overrides, or while every file in the folder matches the
/// prompt it replaces and every Markdown file there has the name of a prompt.
/// </remarks>
public partial class BflPromptOverrideNotice : MSGComponentBase
{
    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private ILogger<BflPromptOverrideNotice> Logger { get; init; } = null!;

    /// <summary>
    /// Gets or sets the CSS classes of the notice, e.g., for its margins.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// Whether files in the test folder replace prompts of the lab.
    /// </summary>
    private bool usesPromptOverrides;

    /// <summary>
    /// The Markdown files in the test folder which replace no prompt of the lab, relative to the folder.
    /// </summary>
    private IReadOnlyList<string> unknownFiles = [];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [Event.CONFIGURATION_CHANGED]);
        await base.OnInitializedAsync();
        await this.CheckAsync();
    }

    /// <inheritdoc />
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
        {
            // The person may switch the prompt overrides on or off in the options of the lab while the page is open:
            await this.CheckAsync();
            this.StateHasChanged();
        }

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    /// <summary>
    /// Checks whether files in the test folder replace prompts of the lab, and which files there replace none.
    /// </summary>
    private async Task CheckAsync()
    {
        try
        {
            this.usesPromptOverrides = await this.ConversationService.UsesPromptOverridesAsync();
            this.unknownFiles = this.ConversationService.FindUnknownPromptOverrides();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sending reports a problem with the prompts; the notice only stays hidden:
            this.Logger.LogDebug(exception, "Could not check the test folder of the BeyondFacts Lab.");
            this.usesPromptOverrides = false;
            this.unknownFiles = [];
        }
    }
}