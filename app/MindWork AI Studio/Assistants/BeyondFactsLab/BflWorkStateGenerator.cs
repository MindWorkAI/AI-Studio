using AIStudio.Provider;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Lets the model write the draft of the work state of a step, together with suggestions for the empty fields of its
/// form.
/// </summary>
/// <remarks>
/// The model writes in a hidden copy of the conversation of the step, with the same system prompt, so it knows all
/// the step knows; the person never sees the request, and the chat stays as it is. Like the conversations, a
/// singleton, so that the work goes on while the person looks at another page. Writing the work state needs no tool:
/// the model answers in Markdown sections, which BflMarkdownSections reads strictly. When the sections are wrong, the
/// model gets one chance to correct them. Nothing the model writes reaches a prompt of the lab before the person
/// confirms it, and no suggestion reaches the form before the person takes it over.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="conversations">The conversations of the lab, whose prompts and checks the work state shares.</param>
/// <param name="sessionService">The registry of running assistant sessions.</param>
/// <param name="messageBus">The message bus, for errors.</param>
/// <param name="logger">The logger.</param>
public sealed class BflWorkStateGenerator(BeyondFactsLabStore store, BflConversationService conversations, AssistantSessionService sessionService, MessageBus messageBus, ILogger<BflWorkStateGenerator> logger)
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflWorkStateGenerator).Namespace, nameof(BflWorkStateGenerator));

    /// <summary>
    /// The steps whose work state the model writes right now.
    /// </summary>
    private readonly HashSet<BflStepThreadLocation> running = [];

    /// <summary>
    /// Guards the steps whose work state the model writes right now.
    /// </summary>
    private readonly Lock runningLock = new();

    /// <summary>
    /// Happens when the model starts or ends writing a work state, and when a new draft is stored.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<BflStepThreadLocation, Task>? WorkStateChanged;

    /// <summary>
    /// Tells whether the model writes the work state of a step right now.
    /// </summary>
    /// <param name="location">The step.</param>
    public bool IsRunning(BflStepThreadLocation location)
    {
        lock (this.runningLock)
            return this.running.Contains(location);
    }

    /// <summary>
    /// Lets the model write the draft of the work state of a step and stores it, replacing an earlier draft and
    /// earlier suggestions.
    /// </summary>
    /// <remarks>
    /// What goes wrong is reported through the message bus; the method itself does not fail. A confirmed work state
    /// stays as it is until the person confirms the new draft.
    /// </remarks>
    /// <param name="location">The step.</param>
    /// <param name="provider">The provider which writes the work state.</param>
    public async Task GenerateAsync(BflStepThreadLocation location, AIStudio.Settings.Provider provider)
    {
        var conversation = await conversations.GetAsync(location);
        if (conversation.Thread is not { } thread || conversation.IsRunning || !await conversations.CheckProviderAsync(location, thread, provider))
            return;

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
            var form = await store.LoadFormAsync(location.ProjectId, location.Step);
            var requestMaterial = await conversations.ComposeRequestAsync(location, provider);
            var template = await BflPromptLibrary.ReadAsync(BflPromptLibrary.WORK_STATE_PATH, conversations.PromptOverridesDirectory);
            var request = BflPromptComposer.ComposeWorkStateRequest(template, location.Step, form);

            // A copy of the conversation, which the person never sees and the lab never stores:
            var hidden = thread with { Blocks = [.. thread.Blocks] };
            BflThreadFactory.PrepareForRequest(hidden, provider, requestMaterial.SystemPrompt);

            var cancellation = new CancellationTokenSource();
            sessionId = (await sessionService.TryBeginAsync(sessionKey, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.Name, cancellation, null, new(StringComparer.Ordinal))).SessionId;
            await this.NotifyAsync(location);

            var answer = BflMarkdownSections.Read(await BflHiddenRequest.AskAsync(hidden, provider, request, requestMaterial.Images, cancellation.Token), location.Step, form);
            if (answer.Problems.Count > 0 && !cancellation.IsCancellationRequested)
            {
                logger.LogInformation("The model wrote a work state of the BeyondFacts Lab with {Count} problems; it may correct them once.", answer.Problems.Count);
                answer = BflMarkdownSections.Read(await BflHiddenRequest.AskAsync(hidden, provider, BflPromptComposer.ComposeWorkStateRepairRequest(answer.Problems), requestMaterial.Images, cancellation.Token), location.Step, form);
            }

            if (cancellation.IsCancellationRequested)
            {
                status = AssistantSessionStatus.CANCELED;
                return;
            }

            // Even after the second try, the person gets what the model wrote, as long as it wrote anything; they check and edit the draft anyway:
            if (answer.Sections.Count is 0)
            {
                logger.LogWarning("The model wrote no readable work state of the BeyondFacts Lab: {Problems}", string.Join(" ", answer.Problems));
                await messageBus.SendError(new(AppIcons.ERROR, TA("The model did not write a work state the lab can read. Please try again, or try another provider.")));
                return;
            }

            var workState = await store.LoadWorkStateAsync(location.ProjectId, location.Step);
            workState.Draft = BflWorkStateSections.ForStep(location.Step).ToDictionary(section => section.Key, section => answer.Sections.GetValueOrDefault(section.Key, string.Empty), StringComparer.Ordinal);
            workState.FieldSuggestions = new(answer.FieldSuggestions, StringComparer.Ordinal);
            await store.SaveWorkStateAsync(location.ProjectId, location.Step, workState);
            status = AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            status = AssistantSessionStatus.CANCELED;
        }
        catch (BflPromptOverrideException exception)
        {
            logger.LogWarning(exception, "A prompt override of the BeyondFacts Lab has a placeholder which the lab does not fill.");
            await messageBus.SendError(new(AppIcons.ERROR, string.Format(TA("The file '{0}' in the test folder of the BeyondFacts Lab contains the placeholder '{1}', which the lab does not fill in this file. Please correct the file and try again."), exception.FileName, exception.Placeholder)));
        }
        catch (ProviderRequestException exception)
        {
            logger.LogError(exception, "The provider request for a work state of the BeyondFacts Lab failed. Status={StatusCode}, Reason='{ReasonPhrase}'.", exception.StatusCode, exception.ReasonPhrase);
            await messageBus.SendError(new(AppIcons.PROVIDER_ERROR, exception.UserMessage));
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation while the model wrote; nothing is left to store.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not write the work state of '{Location}'.", location.Key);
            await messageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not create the work state. Please try again.")));
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
    /// Stops the model writing the work state of a step.
    /// </summary>
    /// <param name="location">The step.</param>
    public Task CancelAsync(BflStepThreadLocation location) => sessionService.CancelAsync(SessionKey(location));

    /// <summary>
    /// Tells every page which listens that a work state changed.
    /// </summary>
    /// <param name="location">The step.</param>
    private async Task NotifyAsync(BflStepThreadLocation location)
    {
        if (this.WorkStateChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<BflStepThreadLocation, Task>>())
        {
            try
            {
                await handler(location);
            }
            catch (Exception exception)
            {
                // A page which went away meanwhile must not stop the work:
                logger.LogDebug(exception, "A page of the BeyondFacts Lab could not show a change of a work state.");
            }
        }
    }

    /// <summary>
    /// Gets the key under which writing the work state of a step registers in AssistantSessionService, beside the
    /// conversation of the step.
    /// </summary>
    /// <param name="location">The step.</param>
    private static AssistantSessionKey SessionKey(BflStepThreadLocation location) => new(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, $"{location.Key}/work-state");
}