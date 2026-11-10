using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A chat of the BeyondFacts Lab, such as the one of a step below its form "Your details".
/// </summary>
/// <remarks>
/// The chat itself lives in BflConversationService, so an answer goes on while the person looks elsewhere; this
/// component shows it and renders whenever the service says it changed. It offers no "send to": the lab neither
/// receives content from other assistants nor hands its answers on.
/// </remarks>
public partial class BflChat : MSGComponentBase
{
    /// <summary>
    /// Gets or sets where the chat to show belongs, e.g., to a step of a preparation.
    /// </summary>
    [Parameter, EditorRequired]
    public BflThreadLocation Location { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether the chat shows a heading of its own; it needs none where its place already names it.
    /// </summary>
    [Parameter]
    public bool ShowHeading { get; set; } = true;

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
    private BflMaterialService MaterialService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<BflChat> Logger { get; init; } = null!;

    [Inject]
    private ConversationTokenCounter TokenCounter { get; init; } = null!;

    /// <summary>
    /// Ends a count when the component goes away.
    /// </summary>
    private readonly CancellationTokenSource countCancellation = new();

    /// <summary>
    /// The token of countCancellation, taken while the source still lives.
    /// </summary>
    /// <remarks>
    /// A count goes on after an await, when the component may have gone away meanwhile, e.g., because the person moved
    /// on to another step. The source is disposed by then and refuses to hand out its token; the token taken before
    /// still tells that the count was cancelled.
    /// </remarks>
    private CancellationToken countToken;

    /// <summary>
    /// What the next request carries, as far as it was counted.
    /// </summary>
    private ConversationTokens tokens = ConversationTokens.UNAVAILABLE;

    /// <summary>
    /// The provider the tokens were counted for, to count again when the parent chooses another one.
    /// </summary>
    private string countedProviderId = string.Empty;

    /// <summary>
    /// The chat shown, or null while it loads.
    /// </summary>
    private BflConversation? conversation;

    /// <summary>
    /// Where the chat shown belongs, to notice when the parent shows another one.
    /// </summary>
    private BflThreadLocation? shownLocation;

    /// <summary>
    /// The message the person writes.
    /// </summary>
    private string userInput = string.Empty;

    /// <summary>
    /// The attributes of the message field, such as the spell checking the person chose.
    /// </summary>
    private readonly Dictionary<string, object?> inputAttributes = new();

    private bool IsRunning => this.conversation?.IsRunning ?? false;

    private BflTokenBudgetState Budget => BflTokenBudget.Of(this.tokens);

    private bool CanSend => this.conversation is { IsAvailable: true, IsRunning: false } && this.ProviderIssue is null && this.Budget is not BflTokenBudgetState.FULL;

    private bool CanSendMessage => this.CanSend && !string.IsNullOrWhiteSpace(this.userInput);

    /// <summary>
    /// Gets what the chat says before its conversation begins.
    /// </summary>
    private string Introduction => this.Location switch
    {
        BflOnboardingThreadLocation => TA("Tell the lab what you are preparing for. It tells you whether it fits your task and how to work with it. You can also let the lab begin."),
        _ => TA("The lab opens the conversation of this step with what your details already say, and asks for what is missing. You can also write first.")
    };

    /// <summary>
    /// Gets the name a message gets when the person exports it.
    /// </summary>
    private string ExportFileName => this.Location switch
    {
        BflStepThreadLocation stepLocation => stepLocation.Step.Title,
        _ => T("Is the lab right for my task?")
    };

    /// <summary>
    /// Gets the question before the conversation starts over.
    /// </summary>
    private string ResetQuestion => this.Location switch
    {
        BflStepThreadLocation => TA("Delete the conversation of this step and start it over? Your details stay as they are."),
        _ => TA("Delete this conversation and start it over?")
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.countToken = this.countCancellation.Token;
        this.ConversationService.ConversationChanged += this.OnConversationChangedAsync;
        this.MaterialService.MaterialsChanged += this.OnMaterialsChangedAsync;
        this.SettingsManager.InjectSpellchecking(this.inputAttributes);
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.shownLocation != this.Location)
        {
            this.shownLocation = this.Location;
            this.conversation = null;
            this.userInput = string.Empty;
            this.conversation = await this.ConversationService.GetAsync(this.Location);
            await this.RecountAsync();
        }
        else if (this.countedProviderId != this.Provider.Id)
            await this.RecountAsync();
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.ConversationService.ConversationChanged -= this.OnConversationChangedAsync;
        this.MaterialService.MaterialsChanged -= this.OnMaterialsChangedAsync;
        this.countCancellation.Cancel();
        this.countCancellation.Dispose();
        base.DisposeResources();
    }

    /// <summary>
    /// Renders the chat again when the service changed it, and counts again once an answer is complete.
    /// </summary>
    /// <param name="location">Where the chat which changed belongs.</param>
    private Task OnConversationChangedAsync(BflThreadLocation location)
    {
        if (location != this.Location)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            if (!this.IsRunning)
                await this.RecountAsync();

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Counts again when the material of the preparation changed, since every request of a step carries it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation whose material changed.</param>
    private Task OnMaterialsChangedAsync(Guid projectId)
    {
        if (this.Location is not BflStepThreadLocation stepLocation || stepLocation.ProjectId != projectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            if (!this.IsRunning)
                await this.RecountAsync();

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Counts what the next request of the chat carries: the system prompt, e.g., with the guide, and the conversation.
    /// </summary>
    /// <remarks>
    /// Only while no answer runs: the request appends to the conversation which the count reads. The message being
    /// written is left out; the reserve of the budget keeps room for it.
    /// </remarks>
    private async Task RecountAsync()
    {
        this.countedProviderId = this.Provider.Id;
        if (this.conversation is not { Thread: { } thread, IsRunning: false } || this.ProviderIssue is not null)
        {
            this.tokens = ConversationTokens.UNAVAILABLE;
            return;
        }

        try
        {
            var systemPrompt = await this.ConversationService.ComposeSystemPromptAsync(this.Location, this.Provider);
            var parts = ConversationParts.Of(thread, systemPrompt, string.Empty, null, false, null);
            this.tokens = await this.TokenCounter.CountAsync(this.Provider, parts, thread.ReportedHistoryFor(this.Provider.Model), this.countToken);
        }
        catch (OperationCanceledException)
        {
            // The component went away.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // Sending reports the same problem; the count only stays unknown:
            this.Logger.LogDebug(exception, "Could not count the tokens of a chat in the BeyondFacts Lab.");
            this.tokens = ConversationTokens.UNAVAILABLE;
        }
    }

    /// <summary>
    /// Gets how full the context of the model is, or null when nothing could be counted.
    /// </summary>
    private string? TokenBudgetText
    {
        get
        {
            if (!this.tokens.IsKnown)
                return null;

            var culture = Tools.PluginSystem.I18N.I.Culture;
            var counted = TokenAmount.Format(this.tokens.Tokens, culture);
            if (!this.tokens.Window.IsKnown)
                return string.Format(T("approx. {0} tokens; the context window of this model is unknown"), counted);

            var window = TokenAmount.Format(this.tokens.Window.DefaultTokens, culture);
            var budget = string.Format(this.tokens.IsEstimate && !this.tokens.HistoryIsReported ? T("approx. {0} of {1} tokens") : T("{0} of {1} tokens"), counted, window);
            return this.Budget switch
            {
                BflTokenBudgetState.FULL => $"{budget}. {TA("This conversation fills the context window of the model. Start it over, or choose a provider whose model reads more at once.")}",
                BflTokenBudgetState.WARNING => $"{budget}. {TA("This conversation fills most of the context window of the model. Soon, you will have to start it over.")}",
                _ => budget
            };
        }
    }

    /// <summary>
    /// Gets the classes of the message field; like on the chat page, they colour its helper text with the tokens.
    /// </summary>
    private string InputClass => this.Budget switch
    {
        BflTokenBudgetState.FULL => "mt-2 token-budget-exceeded",
        BflTokenBudgetState.WARNING => "mt-2 token-budget-nearly-full",
        _ => "mt-2"
    };

    /// <summary>
    /// Lets the model open the conversation.
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

        var text = this.userInput.Trim();
        this.userInput = string.Empty;
        await this.ConversationService.SendAsync(this.Location, this.Provider, text);
    }

    /// <summary>
    /// Sends the message when the person releases the shortcut they chose for the chat.
    /// </summary>
    /// <param name="keyEvent">The key released.</param>
    private async Task InputKeyEventAsync(KeyboardEventArgs keyEvent)
    {
        if (this.SettingsManager.ConfigurationData.Chat.ShortcutSendBehavior.IsSendShortcut(keyEvent))
            await this.SendAsync();
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
            { dialog => dialog.Message, this.ResetQuestion }
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
            this.Logger.LogError(exception, "Could not delete the chat '{Location}' of the BeyondFacts Lab.", this.Location.Key);
            await this.MessageBus.SendError(new(Icons.Material.Filled.DeleteForever, TA("AI Studio could not delete this conversation. Please try again.")));
        }
    }
}