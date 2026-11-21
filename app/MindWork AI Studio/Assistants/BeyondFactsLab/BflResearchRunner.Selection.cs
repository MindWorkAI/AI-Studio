namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BflResearchRunner
{
    /// <summary>
    /// The request which lets the model suggest whom to research. German, like the prompts.
    /// </summary>
    public const string SELECTION_REQUEST = "Wähle jetzt die Personen aus, wie es der Abschnitt „Vorgehen“ beschreibt, und antworte in der Form des Abschnitts „Form“.";

    /// <summary>
    /// The preparations for which the model suggests whom to research right now. Guarded by the lock of the research
    /// on the topics.
    /// </summary>
    private readonly HashSet<Guid> suggesting = [];

    /// <summary>
    /// Happens when the lab starts or ends suggesting whom of the list of participants of a preparation to research.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<Guid, Task>? SelectionChanged;

    /// <summary>
    /// Tells whether the lab suggests whom of the list of participants of a preparation to research right now.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    public bool IsSuggesting(Guid projectId)
    {
        lock (this.runningLock)
            return this.suggesting.Contains(projectId);
    }

    /// <summary>
    /// Lets the model suggest whom of a long list of participants to research in depth, and stores the suggestion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the research, this request knows the details of the preparation, see
    /// BflPromptComposer.ComposeParticipantSelection. It therefore has no tools, and the lab accepts its answer only
    /// when every name stands on the list, see BflParticipantSelectionCheck; the model gets one chance to correct it.
    /// The person checks and changes the suggestion before any person is researched.
    /// </para>
    /// <para>
    /// A list of no more than BflParticipantSelection.MAX_SUGGESTED_PEOPLE people needs no suggestion. What goes wrong
    /// is reported through the message bus; the method itself does not fail.
    /// </para>
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="provider">The provider of the preparation.</param>
    /// <param name="token">Stops the request.</param>
    public async Task SuggestSelectionAsync(Guid projectId, AIStudio.Settings.Provider provider, CancellationToken token = default)
    {
        var hidden = BflThreadFactory.CreateHidden("participant-selection");
        if (!await conversations.CheckProviderAsync(new BflStepThreadLocation(projectId, BflStep.STRATEGY), hidden, provider))
            return;

        lock (this.runningLock)
        {
            if (!this.suggesting.Add(projectId))
                return;
        }

        try
        {
            await this.NotifyAsync(this.SelectionChanged, projectId);
            if (await store.LoadResearchAsync(projectId, BflResearchTopic.PARTICIPANTS, token) is not { } list || list.Participants.Count <= BflParticipantSelection.MAX_SUGGESTED_PEOPLE)
                return;

            var forms = await store.LoadFormsAsync(projectId);
            var template = await BflPromptLibrary.ReadAsync(BflPromptLibrary.PARTICIPANT_SELECTION_PATH, conversations.PromptOverridesDirectory, token);

            // No tools: the request knows the preparation, and nothing of it may reach the web.
            BflThreadFactory.PrepareForRequest(hidden, provider, BflPromptComposer.ComposeParticipantSelection(template, forms, list.Participants));
            var check = BflParticipantSelectionCheck.Read(await BflHiddenRequest.AskAsync(hidden, provider, SELECTION_REQUEST, [], string.Empty, token), list.Participants);
            if (check.Problems.Count > 0)
                check = BflParticipantSelectionCheck.Read(await BflHiddenRequest.AskAsync(hidden, provider, BflPromptComposer.ComposeParticipantSelectionRepairRequest(check.Problems), [], string.Empty, token), list.Participants);

            if (check.Problems.Count > 0)
            {
                logger.LogWarning("The model suggested whom to research for the preparation '{ProjectId}' of the BeyondFacts Lab in a way the lab does not accept: {Problems}", projectId, string.Join(" | ", check.Problems));
                await messageBus.SendError(new(AppIcons.ERROR, TA("The lab could not suggest whom to research in depth. Please choose the people yourself.")));
                return;
            }

            await store.SaveParticipantSelectionAsync(projectId, new BflParticipantSelection
            {
                Names = [.. check.Names],
                Source = BflParticipantSelectionSource.SUGGESTION,
                ListCreatedAtUtc = list.CreatedAtUtc,
            }, token);
        }
        catch (OperationCanceledException)
        {
            // The research which asked for the suggestion was stopped.
        }
        catch (Exception exception) when (IsReportedFailure(exception))
        {
            await this.ReportFailureAsync(exception, $"{projectId:D}/research/participant-selection");
        }
        finally
        {
            lock (this.runningLock)
                this.suggesting.Remove(projectId);

            await this.NotifyAsync(this.SelectionChanged, projectId);
        }
    }
}