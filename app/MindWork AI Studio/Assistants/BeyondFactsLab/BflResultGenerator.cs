using AIStudio.Provider;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Writes the drafts of the results of a preparation: the model writes four of them, and the reflection folder is put
/// together without a model.
/// </summary>
/// <remarks>
/// The model writes in a hidden copy of the conversation of the last step, with its system prompt, so it knows the
/// details, the material, and the confirmed work states of all steps; the person never sees the request, and the chat
/// stays as it is. Like the conversations, a singleton, so that the work goes on while the person looks at another
/// page. Nothing a draft says leaves AI Studio before the person confirms and exports it.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="conversations">The conversations of the lab, whose prompts and checks the results share.</param>
/// <param name="sessionService">The registry of running assistant sessions.</param>
/// <param name="messageBus">The message bus, for errors.</param>
/// <param name="logger">The logger.</param>
public sealed class BflResultGenerator(BeyondFactsLabStore store, BflConversationService conversations, AssistantSessionService sessionService, MessageBus messageBus, ILogger<BflResultGenerator> logger)
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflResultGenerator).Namespace, nameof(BflResultGenerator));

    /// <summary>
    /// The results the model writes right now.
    /// </summary>
    private readonly HashSet<BflResultLocation> running = [];

    /// <summary>
    /// Guards the results the model writes right now.
    /// </summary>
    private readonly Lock runningLock = new();

    /// <summary>
    /// Happens when the model starts or ends writing a result, and when a new draft is stored.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<BflResultLocation, Task>? ResultChanged;

    /// <summary>
    /// Tells whether the model writes a result right now.
    /// </summary>
    /// <param name="location">The result.</param>
    public bool IsRunning(BflResultLocation location)
    {
        lock (this.runningLock)
            return this.running.Contains(location);
    }

    /// <summary>
    /// Lets the model write the draft of a result and stores it, replacing an earlier draft.
    /// </summary>
    /// <remarks>
    /// What goes wrong is reported through the message bus; the method itself does not fail. A confirmed result stays
    /// as it is until the person confirms the new draft.
    /// </remarks>
    /// <param name="location">The result, one a model writes.</param>
    /// <param name="provider">The provider which writes the result.</param>
    public async Task GenerateAsync(BflResultLocation location, AIStudio.Settings.Provider provider)
    {
        if (!location.Kind.IsWrittenByModel)
            return;

        var stepLocation = new BflStepThreadLocation(location.ProjectId, BflStep.RESULTS);
        var conversation = await conversations.GetAsync(stepLocation);
        if (conversation.Thread is not { } thread || conversation.IsRunning || !await conversations.CheckProviderAsync(stepLocation, thread, provider))
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
            var workStates = await store.LoadWorkStatesAsync(location.ProjectId);
            var revisions = BflStaleness.ConfirmedRevisionsOf(workStates);
            var requestMaterial = await conversations.ComposeRequestAsync(stepLocation, provider);
            var template = await BflPromptLibrary.ReadAsync(BflPromptLibrary.ResultPath(location.Kind), conversations.PromptOverridesDirectory);
            var request = BflPromptComposer.ComposeResultRequest(template, workStates);

            // A copy of the conversation, which the person never sees and the lab never stores:
            var hidden = thread with { Blocks = [.. thread.Blocks] };
            BflThreadFactory.PrepareForRequest(hidden, provider, requestMaterial.SystemPrompt);

            var cancellation = new CancellationTokenSource();
            sessionId = (await sessionService.TryBeginAsync(sessionKey, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.Name, cancellation, null, new(StringComparer.Ordinal))).SessionId;
            await this.NotifyAsync(location);

            var text = BflResultText.Read(await BflHiddenRequest.AskAsync(hidden, provider, request, requestMaterial.Images, cancellation.Token));
            if (cancellation.IsCancellationRequested)
            {
                status = AssistantSessionStatus.CANCELED;
                return;
            }

            if (text.Length is 0)
            {
                logger.LogWarning("The model wrote no text for the result '{Location}' of the BeyondFacts Lab.", location.Key);
                await messageBus.SendError(new(Icons.Material.Filled.ErrorOutline, TA("The model did not write this material. Please try again, or try another provider.")));
                return;
            }

            await this.StoreDraftAsync(location, text, revisions);
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
            logger.LogError(exception, "The provider request for a result of the BeyondFacts Lab failed. Status={StatusCode}, Reason='{ReasonPhrase}'.", exception.StatusCode, exception.ReasonPhrase);
            await messageBus.SendError(new(Icons.Material.Filled.CloudOff, exception.UserMessage));
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation while the model wrote; nothing is left to store.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not write the result '{Location}'.", location.Key);
            await messageBus.SendError(new(Icons.Material.Filled.SaveAs, TA("AI Studio could not create this material. Please try again.")));
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
    /// Puts the reflection folder of a preparation together and stores it as a draft, replacing an earlier draft.
    /// </summary>
    /// <remarks>
    /// What goes wrong is reported through the message bus; the method itself does not fail.
    /// </remarks>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="projectName">The name of the preparation.</param>
    /// <param name="forms">The forms of the preparation, by their steps, to tell where a work state stands.</param>
    /// <param name="includePersonalDisclosures">Whether personal disclosures go into the folder.</param>
    /// <param name="includeConversations">Whether the conversations of the steps go into the folder as an appendix.</param>
    public async Task AssembleReflectionFolderAsync(Guid projectId, string projectName, IReadOnlyDictionary<BflStep, BflStepFormData> forms, bool includePersonalDisclosures, bool includeConversations)
    {
        var location = new BflResultLocation(projectId, BflResultKind.REFLECTION_FOLDER);
        try
        {
            var workStates = await store.LoadWorkStatesAsync(projectId);
            var materials = (await store.ListMaterialsAsync(projectId)).Select(entry => entry.Material).OfType<BflMaterial>().ToList();
            var messages = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>();
            if (includeConversations)
            {
                foreach (var step in BflStep.All)
                {
                    if ((await conversations.GetAsync(new BflStepThreadLocation(projectId, step))).Thread is { } thread)
                        messages[step] = BflReflectionFolder.MessagesOf(thread);
                }
            }

            var text = BflReflectionFolder.Build(new BflReflectionFolderInput(projectName, workStates, forms, materials, messages, includePersonalDisclosures, Tools.PluginSystem.I18N.I.Culture));
            await this.StoreDraftAsync(location, text, BflStaleness.ConfirmedRevisionsOf(workStates));
        }
        catch (FileNotFoundException)
        {
            // The person deleted the preparation meanwhile; nothing is left to store.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not put the reflection folder of '{ProjectId}' together.", projectId);
            await messageBus.SendError(new(Icons.Material.Filled.SaveAs, TA("AI Studio could not create this material. Please try again.")));
        }
        finally
        {
            await this.NotifyAsync(location);
        }
    }

    /// <summary>
    /// Stops the model writing a result.
    /// </summary>
    /// <param name="location">The result.</param>
    public Task CancelAsync(BflResultLocation location) => sessionService.CancelAsync(SessionKey(location));

    /// <summary>
    /// Stores the new draft of a result; a confirmed version stays as it is.
    /// </summary>
    /// <param name="location">The result.</param>
    /// <param name="text">The text of the draft.</param>
    /// <param name="workStateRevisions">The revisions of the confirmed work states the draft was built from.</param>
    private async Task StoreDraftAsync(BflResultLocation location, string text, IReadOnlyDictionary<BflStep, int> workStateRevisions)
    {
        var result = await store.LoadResultAsync(location.ProjectId, location.Kind);
        result.Draft = new BflResultVersion
        {
            Text = text,
            WorkStateRevisions = new(workStateRevisions),
            ChangedAtUtc = DateTimeOffset.UtcNow,
        };

        await store.SaveResultAsync(location.ProjectId, location.Kind, result);
    }

    /// <summary>
    /// Tells every page which listens that a result changed.
    /// </summary>
    /// <param name="location">The result.</param>
    private async Task NotifyAsync(BflResultLocation location)
    {
        if (this.ResultChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<BflResultLocation, Task>>())
        {
            try
            {
                await handler(location);
            }
            catch (Exception exception)
            {
                // A page which went away meanwhile must not stop the work:
                logger.LogDebug(exception, "A page of the BeyondFacts Lab could not show a change of a result.");
            }
        }
    }

    /// <summary>
    /// Gets the key under which writing a result registers in AssistantSessionService, beside the conversations.
    /// </summary>
    /// <param name="location">The result.</param>
    private static AssistantSessionKey SessionKey(BflResultLocation location) => new(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.Key);
}