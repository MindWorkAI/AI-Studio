using AIStudio.Components;
using AIStudio.Dialogs;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The chat of one step of the BeyondFacts Lab, below its form "Your details".
/// </summary>
/// <remarks>
/// The chat itself lives in BflConversationService, so an answer goes on while the person looks elsewhere; this
/// component shows it and renders whenever the service says it changed. It offers no "send to": the lab neither
/// receives content from other assistants nor hands its answers on.
/// </remarks>
public partial class BflStepChat : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the step of the preparation whose chat to show.
    /// </summary>
    [Parameter, EditorRequired]
    public BflThreadLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the provider which answers.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets why the provider cannot answer, or null when it can.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<BflStepChat> Logger { get; init; } = null!;

    /// <summary>
    /// The chat shown, or null while it loads.
    /// </summary>
    private BflConversation? conversation;

    /// <summary>
    /// The step whose chat is shown, to notice when the parent shows another one.
    /// </summary>
    private BflThreadLocation? shownLocation;

    /// <summary>
    /// The message the person writes.
    /// </summary>
    private string userInput = string.Empty;

    private bool IsRunning => this.conversation?.IsRunning ?? false;

    private bool CanSend => this.conversation is { IsAvailable: true, IsRunning: false } && this.ProviderIssue is null;

    private bool CanSendMessage => this.CanSend && !string.IsNullOrWhiteSpace(this.userInput);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.ConversationService.ConversationChanged += this.OnConversationChangedAsync;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.shownLocation == this.Location)
            return;

        this.shownLocation = this.Location;
        this.conversation = null;
        this.userInput = string.Empty;
        this.conversation = await this.ConversationService.GetAsync(this.Location);
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.ConversationService.ConversationChanged -= this.OnConversationChangedAsync;
        base.DisposeResources();
    }

    /// <summary>
    /// Renders the chat again when the service changed it.
    /// </summary>
    /// <param name="location">The step whose chat changed.</param>
    private Task OnConversationChangedAsync(BflThreadLocation location) => location == this.Location ? this.InvokeAsync(this.StateHasChanged) : Task.CompletedTask;

    /// <summary>
    /// Lets the model open the conversation of the step.
    /// </summary>
    private async Task StartAsync()
    {
        if (this.CanSend)
            await this.ConversationService.StartAsync(this.Location, this.Provider);
    }

    /// <summary>
    /// Sends the message of the person.
    /// </summary>
    private async Task SendAsync()
    {
        if (!this.CanSendMessage)
            return;

        var text = this.userInput;
        this.userInput = string.Empty;
        await this.ConversationService.SendAsync(this.Location, this.Provider, text);
    }

    /// <summary>
    /// Stops the answer the model writes.
    /// </summary>
    private Task CancelAsync() => this.ConversationService.CancelAsync(this.Location);

    /// <summary>
    /// Deletes the conversation after asking, so that it starts over.
    /// </summary>
    private async Task ResetAsync()
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, TA("Delete the conversation of this step and start it over? Your details stay as they are.") }
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Start over"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        try
        {
            await this.ConversationService.ResetAsync(this.Location);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete the chat of a step in the BeyondFacts Lab.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.DeleteForever, TA("AI Studio could not delete this conversation. Please try again.")));
        }
    }
}