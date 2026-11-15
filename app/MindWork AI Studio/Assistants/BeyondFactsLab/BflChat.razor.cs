using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A chat of the BeyondFacts Lab, such as the one of a step below its form "Your details".
/// </summary>
/// <remarks>
/// The embedded chat of assistants with what the lab decides for each of its places: the opening request, the
/// introduction, the question before starting over, and, for every request, the system prompt with the guide, the
/// details, and the material, and the guidelines of the organization in front of the first message, see
/// BflConversationService.PrepareAsync. The count of tokens shows how full the context
/// of the model is, since every request carries the guide. It offers no "send to": the lab neither receives content
/// from other assistants nor hands its answers on.
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

    /// <summary>
    /// Gets or sets a number which changes whenever a detail changes which takes much room in the next request, such as
    /// whom the step "Test situation" simulates, whose research the prompt carries.
    /// </summary>
    [Parameter]
    public int DetailsVersion { get; set; }

    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private BflMaterialService MaterialService { get; init; } = null!;

    [Inject]
    private ILogger<BflChat> Logger { get; init; } = null!;

    /// <summary>
    /// Readies a request of the chat shown, with the provider chosen; made anew whenever either changes.
    /// </summary>
    private Func<ChatThread, Task<EmbeddedChatPreparation>> prepareAsync = _ => Task.FromResult(EmbeddedChatPreparation.REFUSED);

    /// <summary>
    /// Gets what the next request of the chat shown carries beside the conversation, for the count of tokens; made anew
    /// whenever the chat or the provider changes.
    /// </summary>
    private Func<Task<string?>> systemPromptForCountAsync = () => Task.FromResult<string?>(null);

    /// <summary>
    /// Changes whenever the material of the preparation changes, since every request of a step carries it.
    /// </summary>
    private int materialsVersion;

    /// <summary>
    /// Gets a number which changes whenever what the next request carries beside the conversation changed: the
    /// material, the guidelines of the organization after a configuration plugin was reloaded, or a detail which takes
    /// much room, see DetailsVersion.
    /// </summary>
    private int ContextVersion => HashCode.Combine(this.materialsVersion, this.SettingsManager.ConfigurationData.BeyondFactsLab.OrganizationRules, this.DetailsVersion);

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

    /// <summary>
    /// Gets the button with which the person ends an exercise of the step "Test situation" and asks for its evaluation,
    /// see Prompts/steps/06-test.md; the other chats have none.
    /// </summary>
    private EmbeddedChatQuickMessage? QuickMessage => this.Location is BflStepThreadLocation { Step: BflStep.TEST_SITUATION }
        ? new(T("End and evaluate the exercise"), AppIcons.END_EXERCISE, T("I would like to end the exercise now and get its evaluation."), TA("This button ends the exercise, and the lab evaluates it. While the lab simulates people, it gives feedback only then; write 'Pause' to get brief feedback in between."))
        : null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.MaterialService.MaterialsChanged += this.OnMaterialsChangedAsync;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // Bound to the chat and the provider shown now, so that a request never takes those of the next render:
        var location = this.Location;
        var provider = this.Provider;
        this.prepareAsync = thread => this.ConversationService.PrepareAsync(location, provider, thread);
        this.systemPromptForCountAsync = () => this.SystemPromptForCountAsync(location, provider);
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.MaterialService.MaterialsChanged -= this.OnMaterialsChangedAsync;
        base.DisposeResources();
    }

    /// <summary>
    /// Creates the chat of the place shown, for a place which has no stored chat yet.
    /// </summary>
    private ChatThread CreateThread() => BflThreadFactory.Create(this.Location);

    /// <summary>
    /// Lets the chat count again when the material of the preparation changed.
    /// </summary>
    /// <param name="projectId">The ID of the preparation whose material changed.</param>
    private Task OnMaterialsChangedAsync(Guid projectId)
    {
        if (this.Location is not BflStepThreadLocation stepLocation || stepLocation.ProjectId != projectId)
            return Task.CompletedTask;

        return this.InvokeAsync(() =>
        {
            this.materialsVersion++;
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Puts together what the next request of a chat carries beside the conversation, for the count of tokens: the
    /// system prompt, and the guidelines of the organization in front of the first message.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <returns>The text to count, or null when it cannot be put together now.</returns>
    private async Task<string?> SystemPromptForCountAsync(BflThreadLocation location, AIStudio.Settings.Provider provider)
    {
        try
        {
            return await this.ConversationService.ComposeTextForCountAsync(location, provider);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // Sending reports the same problem; the count only stays unknown:
            this.Logger.LogDebug(exception, "Could not count the tokens of a chat in the BeyondFacts Lab.");
            return null;
        }
    }
}