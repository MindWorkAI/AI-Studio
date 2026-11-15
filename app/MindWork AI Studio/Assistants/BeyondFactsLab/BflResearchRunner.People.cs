using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BflResearchRunner
{
    /// <summary>
    /// How many agents of the research on people work at once, over all people and preparations, so that a long list
    /// stays within the rate limits of the provider.
    /// </summary>
    public const int MAX_PERSON_AGENTS_AT_ONCE = 3;

    /// <summary>
    /// Lets no more than MAX_PERSON_AGENTS_AT_ONCE agents of the research on people work at once.
    /// </summary>
    private readonly SemaphoreSlim personAgents = new(MAX_PERSON_AGENTS_AT_ONCE, MAX_PERSON_AGENTS_AT_ONCE);

    /// <summary>
    /// The research on people which runs right now, each with the stage its agent works on, or null while it waits for
    /// an agent. Guarded by the lock of the research on the topics.
    /// </summary>
    private readonly Dictionary<BflPersonLocation, BflPersonResearchStage?> runningPeople = [];

    /// <summary>
    /// Happens when the research on a person starts, moves to its next stage, stores a stage, or ends.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<BflPersonLocation, Task>? PersonResearchChanged;

    /// <summary>
    /// Tells whether the research on a person runs right now, or waits for an agent.
    /// </summary>
    /// <param name="location">The research on the person.</param>
    public bool IsRunning(BflPersonLocation location)
    {
        lock (this.runningLock)
            return this.runningPeople.ContainsKey(location);
    }

    /// <summary>
    /// Gets the stage whose agent works on a person right now.
    /// </summary>
    /// <param name="location">The research on the person.</param>
    /// <returns>The stage, or null while the research waits for an agent or does not run.</returns>
    public BflPersonResearchStage? RunningStageOf(BflPersonLocation location)
    {
        lock (this.runningLock)
            return this.runningPeople.GetValueOrDefault(location);
    }

    /// <summary>
    /// Lets the agents research several people from the list of participants, each person replacing their earlier
    /// research. No more than MAX_PERSON_AGENTS_AT_ONCE agents work at once; the others wait.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="names">The names of the people, as the list of participants gives them.</param>
    /// <param name="provider">The provider of the preparation.</param>
    public Task RunPeopleAsync(Guid projectId, IEnumerable<string> names, AIStudio.Settings.Provider provider) => Task.WhenAll(names.Select(name => this.RunPersonAsync(projectId, name, provider)));

    /// <summary>
    /// Lets the agents research one person from the list of participants, stage by stage, and stores what each stage
    /// found, replacing the earlier research once the first stage is done.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every agent receives nothing but the public details about the event, the entry of the person in the list of
    /// participants, and what the earlier stages found, see BflPromptComposer.ComposePersonResearch. Only a person on
    /// the stored list is researched, so that no name which the person typed in elsewhere, or which a model derived
    /// from the preparation, gets into a query.
    /// </para>
    /// <para>
    /// Each stage waits for a free agent. The forms and the list are read anew before each stage, since the person may
    /// change them meanwhile. What goes wrong is reported through the message bus; the method itself does not fail.
    /// </para>
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="name">The name of the person, as the list of participants gives it.</param>
    /// <param name="provider">The provider of the preparation.</param>
    public async Task RunPersonAsync(Guid projectId, string name, AIStudio.Settings.Provider provider)
    {
        var location = BflPersonLocation.Of(projectId, name);
        if (!await conversations.CheckProviderAsync(new BflStepThreadLocation(projectId, BflStep.STRATEGY), BflThreadFactory.CreateHidden("research-person"), provider))
            return;

        var availability = await this.CheckAvailabilityAsync(provider);
        if (!availability.CanResearch)
        {
            await messageBus.SendError(new(AppIcons.WEB_RESEARCH, availability.Message));
            return;
        }

        lock (this.runningLock)
        {
            if (!this.runningPeople.TryAdd(location, null))
                return;
        }

        var sessionKey = SessionKey(location);
        var sessionId = Guid.Empty;
        var status = AssistantSessionStatus.FAILED;
        try
        {
            var cancellation = new CancellationTokenSource();
            sessionId = (await sessionService.TryBeginAsync(sessionKey, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.Name, cancellation, null, new(StringComparer.Ordinal))).SessionId;
            await this.NotifyAsync(this.PersonResearchChanged, location);

            var research = new BflPersonResearch();
            foreach (var stage in BflPersonResearchStage.All)
            {
                await this.personAgents.WaitAsync(cancellation.Token);
                try
                {
                    if (!await this.ResearchStageAsync(location, name, stage, research, provider, cancellation.Token))
                        return;
                }
                finally
                {
                    this.personAgents.Release();
                }
            }

            status = AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            status = AssistantSessionStatus.CANCELED;
        }
        catch (Exception exception) when (IsReportedFailure(exception))
        {
            await this.ReportFailureAsync(exception, location.Key);
        }
        finally
        {
            lock (this.runningLock)
                this.runningPeople.Remove(location);

            if (sessionId != Guid.Empty)
            {
                await sessionService.CompleteAsync(sessionKey, sessionId, status, string.Empty, null, new(StringComparer.Ordinal));
                await sessionService.ClearAsync(sessionKey);
            }

            await this.NotifyAsync(this.PersonResearchChanged, location);
        }
    }

    /// <summary>
    /// Stops the research on a person, or keeps it from starting while it waits for an agent. The stages done so far
    /// stay stored.
    /// </summary>
    /// <param name="location">The research on the person.</param>
    public Task CancelAsync(BflPersonLocation location) => sessionService.CancelAsync(SessionKey(location));

    /// <summary>
    /// Lets the agent of one stage research a person and stores what it found.
    /// </summary>
    /// <param name="location">The research on the person.</param>
    /// <param name="name">The name of the person.</param>
    /// <param name="stage">The stage.</param>
    /// <param name="research">The research of this run so far, which takes the new stage.</param>
    /// <param name="provider">The provider of the preparation.</param>
    /// <param name="token">Stops the agent.</param>
    /// <returns>Whether the stage is done, so that the next one may follow.</returns>
    /// <exception cref="OperationCanceledException">The person stopped the research.</exception>
    private async Task<bool> ResearchStageAsync(BflPersonLocation location, string name, BflPersonResearchStage stage, BflPersonResearch research, AIStudio.Settings.Provider provider, CancellationToken token)
    {
        if (await this.LoadPublicDetailsAsync(location.ProjectId) is not { } details)
            return false;

        var participants = (await store.LoadResearchAsync(location.ProjectId, BflResearchTopic.PARTICIPANTS, token))?.Participants ?? [];
        if (participants.FirstOrDefault(candidate => candidate.HasName(name)) is not { } participant)
        {
            await messageBus.SendError(new(AppIcons.WEB_RESEARCH, TA("This person is no longer on the list of participants, so the lab does not research them. Please research the participants again.")));
            return false;
        }

        lock (this.runningLock)
            this.runningPeople[location] = stage;

        await this.NotifyAsync(this.PersonResearchChanged, location);

        var language = (await settingsManager.GetActiveLanguagePlugin()).LangName;
        var parts = await BflPromptLibrary.LoadPersonResearchPartsAsync(stage, conversations.PromptOverridesDirectory, token);
        var hidden = BflThreadFactory.CreateHidden($"research-person-{stage.FileStem}");
        BflThreadFactory.PrepareForRequest(hidden, provider, BflPromptComposer.ComposePersonResearch(parts, details, participant, research.Stages, language), TOOL_IDS);

        // Neither the material nor the guidelines of the organization: the research learns public content only.
        var answer = await BflHiddenRequest.AskForAnswerAsync(hidden, provider, REQUEST, [], string.Empty, token);
        token.ThrowIfCancellationRequested();

        var found = BflResearch.Of(answer.Text, answer.ToolInvocations, answer.Sources, details, DateTimeOffset.UtcNow, BflPersonResearch.MAX_STAGE_TEXT_CHARACTERS);
        if (found.Text.Length is 0)
        {
            logger.LogWarning("The agent wrote no text for the stage '{Stage}' of the research '{Location}' of the BeyondFacts Lab.", stage, location.Key);
            await messageBus.SendError(new(AppIcons.ERROR, TA("The research ended without a result. Please try again, or try another provider.")));
            return false;
        }

        research.Name = participant.Name;
        research.Set(stage, found);
        await store.SavePersonResearchAsync(location.ProjectId, research, token);
        await this.NotifyAsync(this.PersonResearchChanged, location);
        return true;
    }

    /// <summary>
    /// Gets the key under which the research on a person registers in AssistantSessionService.
    /// </summary>
    /// <param name="location">The research on the person.</param>
    private static AssistantSessionKey SessionKey(BflPersonLocation location) => new(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.Key);
}