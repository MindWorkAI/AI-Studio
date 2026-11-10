using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Assistants.BeyondFactsLab;

public partial class BeyondFactsLabAssistant
{
    /// <summary>
    /// The provider for the chats of the preparation shown, or none.
    /// </summary>
    private AIStudio.Settings.Provider providerSettings = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Chooses the provider of a preparation as it opens: the one the person chose for it, as long as its confidence
    /// is still high enough, otherwise the preselected one.
    /// </summary>
    /// <param name="project">The preparation, or null when none is shown.</param>
    private void ResolveProvider(BflProject? project)
    {
        this.providerSettings = project is null
            ? AIStudio.Settings.Provider.NONE
            : this.SettingsManager.GetPreselectedProvider(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, project.ProviderId);
    }

    /// <summary>
    /// Takes over the provider the person chose and remembers it for the preparation.
    /// </summary>
    /// <param name="provider">The provider.</param>
    private async Task SelectProviderAsync(AIStudio.Settings.Provider provider)
    {
        this.providerSettings = provider;
        if (provider == AIStudio.Settings.Provider.NONE || this.ValidateProvider(provider) is not null)
            return;

        if (this.selectedProject?.Project is not { } project || project.ProviderId == provider.Id)
            return;

        try
        {
            this.ReplaceProject(await this.Store.SetProviderAsync(project.ProjectId, provider.Id));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // The choice holds while the page is open; only the next opening falls back to the preselected provider:
            this.Logger.LogWarning(exception, "Could not remember the provider of a preparation in the BeyondFacts Lab.");
        }
    }

    /// <summary>
    /// Checks whether a provider can run the chats of the lab.
    /// </summary>
    /// <remarks>
    /// The confidence is checked before: the selection offers only providers whose confidence is high enough.
    /// </remarks>
    /// <param name="provider">The provider.</param>
    /// <returns>Why the provider cannot run the chats, or null when it can.</returns>
    private string? ValidateProvider(AIStudio.Settings.Provider provider)
    {
        if (provider.UsedLLMProvider is LLMProviders.NONE)
            return TA("Please select a provider.");

        var contextWindow = provider.GetModelProfile().Context;
        if (!BflModelRequirements.HasEnoughContext(contextWindow))
            return string.Format(Culture, TA("The model of this provider reads at most {0} tokens at once. The lab needs at least {1}, because its guide alone takes about 20,000. Please select another provider."), TokenAmount.Format(contextWindow.DefaultTokens, Culture), TokenAmount.Format(BflModelRequirements.MINIMUM_CONTEXT_TOKENS, Culture));

        return null;
    }
}