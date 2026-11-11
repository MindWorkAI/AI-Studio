using AIStudio.Components;
using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The conversation in which the BeyondFacts Lab tells whether it fits a task and how to work with it.
/// </summary>
/// <remarks>
/// It belongs to no preparation, so it has a provider of its own. The person may describe an unpublished task here,
/// so the lab offers the same providers as for a preparation; but its prompt leaves out the guide, so any model may
/// answer.
/// </remarks>
public partial class BflOnboarding : MSGComponentBase
{
    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    /// <summary>
    /// The provider which answers.
    /// </summary>
    private AIStudio.Settings.Provider providerSettings = Settings.Provider.NONE;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // The provider which answered last, as long as its confidence is still high enough, otherwise the preselected one:
        var conversation = await this.ConversationService.GetAsync(BflOnboardingThreadLocation.INSTANCE);
        this.providerSettings = this.SettingsManager.GetPreselectedProvider(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, conversation.Thread?.SelectedProvider);
    }

    /// <summary>
    /// Checks whether a provider can run the conversation.
    /// </summary>
    /// <remarks>
    /// The confidence is checked before: the selection offers only providers whose confidence is high enough.
    /// </remarks>
    /// <param name="provider">The provider.</param>
    /// <returns>Why the provider cannot run the conversation, or null when it can.</returns>
    private string? ValidateProvider(AIStudio.Settings.Provider provider) => provider.UsedLLMProvider is LLMProviders.NONE ? TA("Please select a provider.") : null;
}