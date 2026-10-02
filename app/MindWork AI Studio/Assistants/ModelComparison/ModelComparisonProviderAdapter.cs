using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Connects a provider of the app to the model comparison.
/// </summary>
public static class ModelComparisonProviderAdapter
{
    /// <summary>
    /// Creates the participant for a provider.
    /// </summary>
    /// <param name="providerSettings">The provider, with the model to ask.</param>
    /// <param name="settingsManager">The settings the provider needs for its request.</param>
    /// <param name="component">The component whose confidence requirements the provider has to meet.</param>
    /// <param name="title">The title of the assistant, as the name of the throwaway chat.</param>
    /// <returns>The participant.</returns>
    /// <remarks>
    /// The provider is built once here, not once per run: it holds nothing but its own
    /// <c>HttpClient</c>, so every run of a batch reuses the same connection instead of paying for a
    /// fresh one every time.
    /// </remarks>
    public static ModelComparisonParticipant CreateParticipant(AIStudio.Settings.Provider providerSettings, SettingsManager settingsManager, Tools.Components component, string title)
    {
        var provider = providerSettings.CreateProvider();
        return new()
        {
            Label = providerSettings.ToString(),
            Ask = (prompt, token) => StreamAnswer(provider, providerSettings, settingsManager, component, title, prompt, token),
        };
    }

    /// <summary>
    /// Asks the model straight through the provider, without <c>ContentText.CreateFromProviderAsync</c>.
    /// </summary>
    /// <remarks>
    /// Two reasons. The stream of chunks is what the first-token time is measured on, and the
    /// streaming event of a <c>ContentText</c> only fires every few seconds in the energy saving
    /// mode. And that method turns a provider which is not allowed into an empty answer without a
    /// word, which would let the model lose every vote. Here it is an error, so the run reports it
    /// as a failure of that model.
    ///
    /// The request is what the model comparison promises: no profile, no system prompt of its own,
    /// no tools and no data sources, only the prompt.
    /// </remarks>
    private static async IAsyncEnumerable<string> StreamAnswer(
        IProvider provider,
        AIStudio.Settings.Provider providerSettings,
        SettingsManager settingsManager,
        Tools.Components component,
        string title,
        string prompt,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (!settingsManager.IsProviderConfident(providerSettings, component))
            throw new InvalidOperationException($"The provider '{providerSettings.InstanceName}' does not meet the confidence requirements of the model comparison.");

        var chatThread = new ChatThread
        {
            IncludeDateTime = false,
            SelectedProvider = providerSettings.Id,
            SelectedProfile = Profile.NO_PROFILE.Id,
            SelectedToolIds = [],
            SystemPrompt = string.Empty,
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = title,
            Blocks = [],
            RuntimeComponent = component,
            RuntimeSelectedToolIds = [],
            RuntimeToolsAreAssistantManaged = true,
        };

        chatThread.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = prompt,
            },
        });

        // The provider writes tool traces into the last block of the AI, so the thread has one:
        chatThread.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = new ContentText(),
        });

        await foreach (var chunk in provider.StreamChatCompletion(providerSettings.Model, chatThread, settingsManager, token).ConfigureAwait(false))
            yield return chunk.Content;
    }
}