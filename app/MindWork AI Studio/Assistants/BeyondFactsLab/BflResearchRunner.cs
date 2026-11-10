using System.Collections.Frozen;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Researches the context of an event on the web, with one agent for each topic, see BflResearchTopic.
/// </summary>
/// <remarks>
/// <para>
/// Each agent works in a hidden chat of its own, which the person never sees and the lab never stores. It receives
/// nothing but the public details about the event, see BflPromptComposer.ComposeResearch: no other detail, no material,
/// no work state, and no conversation of the preparation. That is the point of the separation: the chats of the steps
/// know the whole preparation, which may be unpublished, and therefore never search the web themselves.
/// </para>
/// <para>
/// A single answer may call tools only so often, see ToolSelectionRules.MAX_TOOL_CALLS, so each topic gets an agent and
/// a budget of its own, and the agents run side by side. Like the conversations, a singleton, so that the work goes on
/// while the person looks at another page.
/// </para>
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="conversations">The conversations of the lab, whose checks of a provider the research shares.</param>
/// <param name="toolRegistry">The registry of the tools.</param>
/// <param name="settingsManager">The settings.</param>
/// <param name="sessionService">The registry of running assistant sessions.</param>
/// <param name="messageBus">The message bus, for errors.</param>
/// <param name="logger">The logger.</param>
public sealed class BflResearchRunner(BeyondFactsLabStore store, BflConversationService conversations, ToolRegistry toolRegistry, SettingsManager settingsManager, AssistantSessionService sessionService, MessageBus messageBus, ILogger<BflResearchRunner> logger)
{
    /// <summary>
    /// The request which lets an agent start its research. German, like the prompts.
    /// </summary>
    public const string REQUEST = "Recherchiere jetzt, wie es die Abschnitte „Dein Auftrag“ und „Vorgehen“ beschreiben, und antworte in der Form des Abschnitts „Form“.";

    /// <summary>
    /// The tools of a research agent: it searches the web and reads the pages it found.
    /// </summary>
    public static readonly FrozenSet<string> TOOL_IDS = FrozenSet.Create(StringComparer.Ordinal, ToolSelectionRules.WEB_SEARCH_TOOL_ID, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID);

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflResearchRunner).Namespace, nameof(BflResearchRunner));

    /// <summary>
    /// The research the agents do right now.
    /// </summary>
    private readonly HashSet<BflResearchLocation> running = [];

    /// <summary>
    /// Guards the research the agents do right now.
    /// </summary>
    private readonly Lock runningLock = new();

    /// <summary>
    /// Happens when an agent starts or ends its research, and when a new research is stored.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<BflResearchLocation, Task>? ResearchChanged;

    /// <summary>
    /// Tells whether an agent does a research right now.
    /// </summary>
    /// <param name="location">The research.</param>
    public bool IsRunning(BflResearchLocation location)
    {
        lock (this.runningLock)
            return this.running.Contains(location);
    }

    /// <summary>
    /// Checks whether the research can search the web with a provider.
    /// </summary>
    /// <param name="provider">The provider of the preparation.</param>
    /// <returns>What keeps each tool of the research from being offered.</returns>
    public Task<BflResearchAvailability> CheckAvailabilityAsync(AIStudio.Settings.Provider provider) => BflResearchAvailability.CheckAsync(toolRegistry, provider);

    /// <summary>
    /// Lets the agents of every topic research side by side, each replacing its earlier research.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="provider">The provider of the preparation.</param>
    public Task RunAllAsync(Guid projectId, AIStudio.Settings.Provider provider) => Task.WhenAll(BflResearchTopic.All.Select(topic => this.RunAsync(new BflResearchLocation(projectId, topic), provider)));

    /// <summary>
    /// Starts the research on every topic which has none yet, or whose public details changed since, e.g., once the
    /// person confirmed the event details.
    /// </summary>
    /// <remarks>
    /// It starts nothing and tells nothing while the event is not public, while the research cannot search the web, or
    /// while the person entered no public detail yet: then the area "Research" says why. It returns once the agents
    /// started, not when they are done; what goes wrong in their work is reported through the message bus.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="provider">The provider of the preparation.</param>
    /// <param name="forms">The forms of the preparation, by their steps.</param>
    /// <returns>The topics whose research started.</returns>
    public async Task<IReadOnlyList<BflResearchTopic>> StartWhenDueAsync(Guid projectId, AIStudio.Settings.Provider provider, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        var details = BflPromptComposer.PublicDetailsOf(forms);
        if (BflEventVisibility.IsNotPublic(forms) || details.Count is 0 || !(await this.CheckAvailabilityAsync(provider)).CanResearch)
            return [];

        var topics = TopicsDue(await store.LoadAllResearchAsync(projectId), details).Where(topic => !this.IsRunning(new BflResearchLocation(projectId, topic))).ToList();
        if (topics.Count > 0)
            _ = Task.WhenAll(topics.Select(topic => this.RunAsync(new BflResearchLocation(projectId, topic), provider)));

        return topics;
    }

    /// <summary>
    /// Finds the topics which need a research: those without one, and those whose public details changed since.
    /// </summary>
    /// <param name="research">The research of the preparation, by its topics.</param>
    /// <param name="details">The public details about the event as they are now.</param>
    /// <returns>The topics, in their order.</returns>
    public static IReadOnlyList<BflResearchTopic> TopicsDue(IReadOnlyDictionary<BflResearchTopic, BflResearch> research, IReadOnlyList<BflResearchDetail> details) =>
        [.. BflResearchTopic.All.Where(topic => !research.TryGetValue(topic, out var found) || found.IsOutdated(details))];

    /// <summary>
    /// Lets the agent of a topic research and stores what it found, replacing its earlier research.
    /// </summary>
    /// <remarks>
    /// What goes wrong is reported through the message bus; the method itself does not fail.
    /// </remarks>
    /// <param name="location">The research.</param>
    /// <param name="provider">The provider of the preparation.</param>
    public async Task RunAsync(BflResearchLocation location, AIStudio.Settings.Provider provider)
    {
        var hidden = BflThreadFactory.CreateHidden($"research-{location.Topic.FileStem}");
        if (!await conversations.CheckProviderAsync(new BflStepThreadLocation(location.ProjectId, BflStep.STRATEGY), hidden, provider))
            return;

        var availability = await this.CheckAvailabilityAsync(provider);
        if (!availability.CanResearch)
        {
            await messageBus.SendError(new(Icons.Material.Filled.TravelExplore, availability.Message));
            return;
        }

        lock (this.runningLock)
        {
            if (!this.running.Add(location))
                return;
        }

        var sessionKey = SessionKey(location);
        var sessionId = Guid.Empty;
        var status = AssistantSessionStatus.FAILED;
        try
        {
            // The forms on disk decide, since the person may have changed them since the research was asked for:
            var forms = await store.LoadFormsAsync(location.ProjectId);
            if (BflEventVisibility.IsNotPublic(forms))
            {
                await messageBus.SendError(new(Icons.Material.Filled.PublicOff, BflEventVisibility.NotPublicMessage));
                return;
            }

            var details = BflPromptComposer.PublicDetailsOf(forms);
            if (details.Count is 0)
            {
                await messageBus.SendError(new(Icons.Material.Filled.TravelExplore, string.Format(TA("Please enter public details about the event first, in the step '{0}', such as its topic or its link. The research receives these details only."), BflStep.FRAMEWORK.Title)));
                return;
            }

            var language = (await settingsManager.GetActiveLanguagePlugin()).LangName;
            var parts = await BflPromptLibrary.LoadResearchPartsAsync(location.Topic, conversations.PromptOverridesDirectory);
            BflThreadFactory.PrepareForRequest(hidden, provider, BflPromptComposer.ComposeResearch(parts, details, language), TOOL_IDS);

            var cancellation = new CancellationTokenSource();
            sessionId = (await sessionService.TryBeginAsync(sessionKey, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.Name, cancellation, null, new(StringComparer.Ordinal))).SessionId;
            await this.NotifyAsync(location);

            var answer = await BflHiddenRequest.AskForAnswerAsync(hidden, provider, REQUEST, [], cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                status = AssistantSessionStatus.CANCELED;
                return;
            }

            var research = BflResearch.Of(answer.Text, answer.ToolInvocations, answer.Sources, details, DateTimeOffset.UtcNow);
            if (research.Text.Length is 0)
            {
                logger.LogWarning("The agent wrote no text for the research '{Location}' of the BeyondFacts Lab.", location.Key);
                await messageBus.SendError(new(Icons.Material.Filled.ErrorOutline, TA("The research ended without a result. Please try again, or try another provider.")));
                return;
            }

            await store.SaveResearchAsync(location.ProjectId, location.Topic, research);
            status = AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            status = AssistantSessionStatus.CANCELED;
        }
        catch (BflPromptOverrideException exception)
        {
            logger.LogWarning(exception, "A prompt override of the BeyondFacts Lab has a placeholder which the lab does not fill.");
            await messageBus.SendError(new(Icons.Material.Filled.ErrorOutline, string.Format(TA("The file '{0}' in the test folder of the BeyondFacts Lab contains the placeholder '{1}', which the lab does not fill in this file. Please correct the file and try again."), exception.FileName, exception.Placeholder)));
        }
        catch (ProviderRequestException exception)
        {
            logger.LogError(exception, "The provider request for a research of the BeyondFacts Lab failed. Status={StatusCode}, Reason='{ReasonPhrase}'.", exception.StatusCode, exception.ReasonPhrase);
            await messageBus.SendError(new(Icons.Material.Filled.CloudOff, exception.UserMessage));
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation while the agent researched; nothing is left to store.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not do the research '{Location}'.", location.Key);
            await messageBus.SendError(new(Icons.Material.Filled.SaveAs, TA("AI Studio could not finish this research. Please try again.")));
        }
        finally
        {
            lock (this.runningLock)
                this.running.Remove(location);

            if (sessionId != Guid.Empty)
            {
                await sessionService.CompleteAsync(sessionKey, sessionId, status, string.Empty, null, new(StringComparer.Ordinal));
                await sessionService.ClearAsync(sessionKey);
            }

            await this.NotifyAsync(location);
        }
    }

    /// <summary>
    /// Stops the agent of a topic.
    /// </summary>
    /// <param name="location">The research.</param>
    public Task CancelAsync(BflResearchLocation location) => sessionService.CancelAsync(SessionKey(location));

    /// <summary>
    /// Tells every page which listens that a research changed.
    /// </summary>
    /// <param name="location">The research.</param>
    private async Task NotifyAsync(BflResearchLocation location)
    {
        if (this.ResearchChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<BflResearchLocation, Task>>())
        {
            try
            {
                await handler(location);
            }
            catch (Exception exception)
            {
                // A page which went away meanwhile must not stop the work:
                logger.LogDebug(exception, "A page of the BeyondFacts Lab could not show a change of a research.");
            }
        }
    }

    /// <summary>
    /// Gets the key under which a research registers in AssistantSessionService, beside the conversations.
    /// </summary>
    /// <param name="location">The research.</param>
    private static AssistantSessionKey SessionKey(BflResearchLocation location) => new(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.Key);
}