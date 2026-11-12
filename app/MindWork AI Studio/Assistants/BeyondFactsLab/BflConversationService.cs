using System.Collections.Concurrent;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Runs the chats of the BeyondFacts Lab, such as those of the steps of a preparation.
/// </summary>
/// <remarks>
/// A singleton, so that an answer goes on while the person looks at another page: the page asks for the chat again
/// when it comes back and listens to ConversationChanged. Each request registers in AssistantSessionService, which
/// shows the work in the navigation. The service does nothing until the page of the lab asks for a chat.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="settingsManager">The settings.</param>
/// <param name="sessionService">The registry of running assistant sessions.</param>
/// <param name="messageBus">The message bus, for errors.</param>
/// <param name="toolRegistry">The registry of the tools, which tells the chats what they can do on the web.</param>
/// <param name="logger">The logger.</param>
public sealed class BflConversationService(BeyondFactsLabStore store, SettingsManager settingsManager, AssistantSessionService sessionService, MessageBus messageBus, ToolRegistry toolRegistry, ILogger<BflConversationService> logger)
{
    /// <summary>
    /// The request which lets the model open the conversation of a step. The person does not see it.
    /// </summary>
    /// <remarks>
    /// German, like the prompts: what the first message does is up to the prompt of the step.
    /// </remarks>
    public const string OPENING_REQUEST = "Beginne das Gespräch dieses Schritts mit deiner ersten Nachricht, wie es der Abschnitt „Dieser Schritt“ beschreibt.";

    /// <summary>
    /// The request which lets the model open the conversation about whether the lab fits a task. The person does not
    /// see it.
    /// </summary>
    public const string ONBOARDING_OPENING_REQUEST = "Beginne das Gespräch mit deiner ersten Nachricht, wie es der Abschnitt „Gesprächsführung“ beschreibt.";

    /// <summary>
    /// How often a streamed answer renders at most.
    /// </summary>
    private static readonly TimeSpan STREAMING_RENDER_MIN_TIME = TimeSpan.FromMilliseconds(100);

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflConversationService).Namespace, nameof(BflConversationService));

    /// <summary>
    /// The chats loaded so far, by their places.
    /// </summary>
    private readonly ConcurrentDictionary<BflThreadLocation, BflConversation> conversations = new();

    /// <summary>
    /// Serializes loading a chat, so that two pages never load the same chat twice.
    /// </summary>
    private readonly SemaphoreSlim loadLock = new(1, 1);

    /// <summary>
    /// Guards whether a chat answers right now, so that two requests never run in the same chat.
    /// </summary>
    private readonly Lock runningLock = new();

    /// <summary>
    /// Gets the folder whose files replace the prompts of the lab, or null while the person does not allow it. The
    /// setting counts anew for every request, so that switching it on or off shows in the next answer.
    /// </summary>
    internal string? PromptOverridesDirectory =>BflPromptOverrides.ActiveDirectory(settingsManager.ConfigurationData.BeyondFactsLab, store.PromptOverridesDirectory);

    /// <summary>
    /// Happens whenever a chat changed: a message, a chunk of an answer, or the end of a request.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<BflThreadLocation, Task>? ConversationChanged;

    /// <summary>
    /// Gets a chat, loading it on the first call.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat; it is not available when this version cannot open the stored one.</returns>
    public async Task<BflConversation> GetAsync(BflThreadLocation location, CancellationToken token = default)
    {
        if (this.conversations.TryGetValue(location, out var conversation))
            return conversation;

        await this.loadLock.WaitAsync(token);
        try
        {
            if (this.conversations.TryGetValue(location, out conversation))
                return conversation;

            ChatThread? thread;
            try
            {
                thread = await store.LoadThreadAsync(location, token) ?? BflThreadFactory.Create(location);
            }
            catch (InvalidDataException exception)
            {
                // The file stays as it is, see BeyondFactsLabStore.SaveThreadAsync:
                logger.LogWarning(exception, "Could not open the chat '{Location}' of the BeyondFacts Lab.", location.Key);
                thread = null;
            }

            conversation = new BflConversation { Location = location, Thread = thread };
            this.conversations[location] = conversation;
            return conversation;
        }
        finally
        {
            this.loadLock.Release();
        }
    }

    /// <summary>
    /// Lets the model open a conversation, as its prompt describes.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers.</param>
    public Task StartAsync(BflThreadLocation location, AIStudio.Settings.Provider provider) => this.SendAsync(location, provider, OpeningRequestFor(location), isHidden: true);

    /// <summary>
    /// Gets the request which lets the model open a conversation.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <returns>The request, which names the section of the prompt that describes the first message.</returns>
    public static string OpeningRequestFor(BflThreadLocation location) => location is BflOnboardingThreadLocation ? ONBOARDING_OPENING_REQUEST : OPENING_REQUEST;

    /// <summary>
    /// Sends a message of the person and lets the model answer it.
    /// </summary>
    /// <remarks>
    /// The message is stored before the model answers, so that a crash loses at most the answer. What goes wrong is
    /// reported through the message bus; the method itself does not fail.
    /// </remarks>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="text">The message.</param>
    /// <param name="isHidden">Whether the person does not see the message, e.g., the request which opens a conversation.</param>
    public async Task SendAsync(BflThreadLocation location, AIStudio.Settings.Provider provider, string text, bool isHidden = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var conversation = await this.GetAsync(location);
        if (conversation.Thread is not { } thread || !await this.CheckProviderAsync(location, thread, provider))
            return;

        BflRequestMaterial requestMaterial;
        try
        {
            requestMaterial = await this.ComposeRequestAsync(location, provider);
        }
        catch (BflPromptOverrideException exception)
        {
            logger.LogWarning(exception, "A prompt override of the BeyondFacts Lab has a placeholder which the lab does not fill.");
            await messageBus.SendError(new(AppIcons.ERROR, string.Format(TA("The file '{0}' in the test folder of the BeyondFacts Lab contains the placeholder '{1}', which the lab does not fill in this file. Please correct the file and try again."),exception.FileName, exception.Placeholder)));
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not put together the system prompt of the chat '{Location}'.", location.Key);
            await messageBus.SendError(new(AppIcons.ERROR, TA("The BeyondFacts Lab could not put together its instructions for the model. Please report this as a bug.")));
            return;
        }

        lock (this.runningLock)
        {
            if (conversation.IsRunning)
                return;

            conversation.IsRunning = true;
        }

        ContentBlock? answerBlock = null;
        var sessionKey = SessionKey(location);
        var sessionId = Guid.Empty;
        var status = AssistantSessionStatus.FAILED;
        try
        {
            // A chat stored before the lab offered tools gets its restriction now, before it may read a page:
            BflChatTools.Restrict(thread, location);
            BflThreadFactory.PrepareForRequest(thread, provider, requestMaterial.SystemPrompt, BflChatTools.For(location));
            var request = new ContentText { Text = text };
            thread.Blocks.Add(new ContentBlock
            {
                Time = DateTimeOffset.Now,
                ContentType = ContentType.TEXT,
                Role = ChatRole.USER,
                Content = request,
                HideFromUser = isHidden
            });

            await store.SaveThreadAsync(location, thread);

            var answer = new ContentText { InitialRemoteWait = true };
            answerBlock = new ContentBlock
            {
                Time = DateTimeOffset.Now,
                ContentType = ContentType.TEXT,
                Role = ChatRole.AI,
                Content = answer
            };

            var lastRendering = DateTimeOffset.MinValue;
            answer.StreamingEvent = async () =>
            {
                var now = DateTimeOffset.Now;
                if (now - lastRendering < STREAMING_RENDER_MIN_TIME)
                    return;

                lastRendering = now;
                await this.NotifyAsync(location);
            };

            answer.StreamingDone = () => this.NotifyAsync(location);
            thread.Blocks.Add(answerBlock);

            var cancellation = new CancellationTokenSource();
            sessionId = (await sessionService.TryBeginAsync(sessionKey, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.Name, cancellation, thread, new(StringComparer.Ordinal))).SessionId;
            await this.NotifyAsync(location);

            await answer.CreateFromProviderAsync(provider.CreateProvider(), provider.Model, request, WithImages(thread, requestMaterial.Images), cancellation.Token);
            status = cancellation.IsCancellationRequested ? AssistantSessionStatus.CANCELED : AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            status = AssistantSessionStatus.CANCELED;
        }
        catch (ProviderRequestException exception)
        {
            logger.LogError(exception, "The provider request of the BeyondFacts Lab failed. Status={StatusCode}, Reason='{ReasonPhrase}'.", exception.StatusCode, exception.ReasonPhrase);
            await messageBus.SendError(new(AppIcons.PROVIDER_ERROR, exception.UserMessage));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not store the chat '{Location}'.", location.Key);
            await messageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not save this conversation. Please try again.")));
        }
        finally
        {
            await this.FinishAsync(conversation, thread, answerBlock, sessionKey, sessionId, status);
        }
    }

    /// <summary>
    /// Stops the answer the model writes in a chat.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    public Task CancelAsync(BflThreadLocation location) => sessionService.CancelAsync(SessionKey(location));

    /// <summary>
    /// Deletes a conversation, so that it starts over.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <returns>False while the model answers in the chat.</returns>
    public async Task<bool> ResetAsync(BflThreadLocation location)
    {
        var conversation = await this.GetAsync(location);
        lock (this.runningLock)
        {
            if (conversation.IsRunning)
                return false;
        }

        await store.DeleteThreadAsync(location);
        conversation.Thread = BflThreadFactory.Create(location);

        // The new conversation keeps the ID of the chat, so what the old one received would otherwise wait for the next start:
        await ChatProvenance.ForgetChatsAsync([location.ChatId]);
        await this.NotifyAsync(location);
        return true;
    }

    /// <summary>
    /// Stops every answer of a preparation and forgets its chats, e.g., because the person deleted it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <returns>The IDs of the chats the preparation could have had.</returns>
    public async Task<IReadOnlyList<Guid>> ForgetProjectAsync(Guid projectId)
    {
        var locations = BflStep.All.Select(step => new BflStepThreadLocation(projectId, step)).ToList();
        foreach (var location in locations)
        {
            await sessionService.CancelAsync(SessionKey(location));
            this.conversations.TryRemove(location, out _);
        }

        return [.. locations.Select(location => location.ChatId)];
    }

    /// <summary>
    /// Puts together the system prompt of a chat, anew for every request, so that changed details, material, and a
    /// changed form of address count right away. The chat also uses it to count what the next request carries.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers; its model decides how much of the material fits.</param>
    /// <returns>The system prompt.</returns>
    /// <exception cref="FileNotFoundException">The app comes without one of the prompts.</exception>
    /// <exception cref="FormatException">A prompt has a placeholder without a value.</exception>
    public async Task<string> ComposeSystemPromptAsync(BflThreadLocation location, AIStudio.Settings.Provider provider) => (await this.ComposeRequestAsync(location, provider)).SystemPrompt;

    /// <summary>
    /// Checks whether files in the test folder replace prompts of the lab right now, so that the lab can tell the
    /// person.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True while the setting allows prompt overrides and a file replaces a prompt with another text.</returns>
    public async Task<bool> UsesPromptOverridesAsync(CancellationToken token = default) => this.PromptOverridesDirectory is { } directory && (await BflPromptOverrides.FindAsync(directory, token)).Count > 0;

    /// <summary>
    /// Puts together what a request of a chat carries beside the conversation: the system prompt, and the images of
    /// the material the model may see.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers; its model decides how much of the material fits.</param>
    /// <returns>The system prompt and the images.</returns>
    /// <exception cref="FileNotFoundException">The app comes without one of the prompts.</exception>
    /// <exception cref="FormatException">A prompt has a placeholder without a value.</exception>
    internal async Task<BflRequestMaterial> ComposeRequestAsync(BflThreadLocation location, AIStudio.Settings.Provider provider)
    {
        var addressForm = Tools.PluginSystem.I18N.I.AddressForm;
        var language = (await settingsManager.GetActiveLanguagePlugin()).LangName;
        var tools = await BflResearchAvailability.CheckAsync(toolRegistry, provider);
        switch (location)
        {
            case BflStepThreadLocation stepLocation:
                var forms = await store.LoadFormsAsync(stepLocation.ProjectId);
                var workStates = await store.LoadWorkStatesAsync(stepLocation.ProjectId);
                var parts = await BflPromptLibrary.LoadPartsAsync(stepLocation.Step, addressForm, this.PromptOverridesDirectory);
                var research = await this.LoadResearchAsync(stepLocation);
                var shares = await this.ShareMaterialsAsync(stepLocation, parts, forms, workStates, tools, research, language, provider);
                var materials = await this.ReadMaterialsAsync(stepLocation.ProjectId, shares);
                return new(BflPromptComposer.Compose(parts, stepLocation.Step, forms, materials, language, workStates, tools, research), this.ImagesOf(stepLocation.ProjectId, shares));

            case BflOnboardingThreadLocation:
                return new(BflPromptComposer.ComposeOnboarding(await BflPromptLibrary.LoadOnboardingPartsAsync(addressForm, this.PromptOverridesDirectory), language, tools), []);

            default:
                throw new ArgumentOutOfRangeException(nameof(location), location, "The lab has no prompt for this chat.");
        }
    }

    /// <summary>
    /// Gives a request the images of the material, attached to the first message of the person, without storing them
    /// in the chat.
    /// </summary>
    /// <remarks>
    /// A system prompt carries no images with any provider, so they go along with the first message, which stays the
    /// same from request to request, as the cache of a provider needs. The request gets a copy of that message only;
    /// every other message, the answer which streams in above all, stays the same object, and the chat keeps as many
    /// messages, so that the tokens a provider reports still belong to it. The stored chat never holds an image.
    ///
    /// Unlike the text of documents and transcripts, an image does not pass the prompt injection filter of the
    /// runtime, which reads text only; images in a chat do not either. The only tool of a step reads web pages whose
    /// addresses were given to the model, see BflChatTools, so nothing in an image can make it send data to an
    /// address of its own, and the prompt tells the model that the material, images included, holds no instructions.
    /// </remarks>
    /// <param name="thread">The chat.</param>
    /// <param name="images">The images to send.</param>
    /// <returns>The chat for the request: the chat itself when there is no image to send.</returns>
    internal static ChatThread WithImages(ChatThread thread, IReadOnlyList<FileAttachmentImage> images)
    {
        if (images.Count is 0)
            return thread;

        var index = thread.Blocks.FindIndex(block => block is { Role: ChatRole.USER, Content: ContentText });
        if (index < 0)
            return thread;

        var firstMessage = thread.Blocks[index].DeepClone();
        var content = (ContentText)firstMessage.Content!;
        content.FileAttachments = [.. content.FileAttachments, .. images];

        List<ContentBlock> blocks = [.. thread.Blocks];
        blocks[index] = firstMessage;
        return thread with { Blocks = blocks };
    }

    /// <summary>
    /// Gets how many images the model of a provider takes in one message.
    /// </summary>
    /// <param name="provider">The provider which answers.</param>
    /// <returns>0 when the model sees no images, and int.MaxValue when nobody knows a limit.</returns>
    private static int ImagesTheModelTakes(AIStudio.Settings.Provider provider)
    {
        if (!provider.SupportsImageInput())
            return 0;

        var profile = provider.GetModelProfile();
        if (!profile.HasAny(Capability.MULTIPLE_IMAGE_INPUT))
            return 1;

        return profile.Images.MaxInOneMessage ?? int.MaxValue;
    }

    /// <summary>
    /// Gets the images of the material which go along with a request.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="shares">The share of each material.</param>
    /// <returns>The copies of the images to send, in the order the person added them.</returns>
    private IReadOnlyList<FileAttachmentImage> ImagesOf(Guid projectId, IReadOnlyList<BflMaterialShare> shares)
    {
        List<FileAttachmentImage> images = [];
        foreach (var share in shares.Where(share => share.Image is BflImageDelivery.SENT))
        {
            var file = new FileInfo(store.MaterialImagePath(projectId, share.Material));
            if (!file.Exists)
            {
                // The person removed the material meanwhile; the list shows that:
                logger.LogWarning("The copy of the image '{MaterialId}' of the BeyondFacts Lab is missing.", share.Material.MaterialId);
                continue;
            }

            images.Add(new(share.Material.Name, file.FullName, file.Length));
        }

        return images;
    }

    /// <summary>
    /// Decides how much of the material of a preparation the prompt of a step carries, e.g., to show the person what
    /// the model does not read.
    /// </summary>
    /// <param name="location">The step.</param>
    /// <param name="provider">The provider which answers; its model decides how much of the material fits.</param>
    /// <returns>The share of each material this version can open, in the order the person added them.</returns>
    /// <exception cref="FileNotFoundException">The app comes without one of the prompts.</exception>
    /// <exception cref="FormatException">A prompt has a placeholder without a value.</exception>
    public async Task<IReadOnlyList<BflMaterialShare>> ShareMaterialsAsync(BflStepThreadLocation location, AIStudio.Settings.Provider provider)
    {
        var language = (await settingsManager.GetActiveLanguagePlugin()).LangName;
        var forms = await store.LoadFormsAsync(location.ProjectId);
        var workStates = await store.LoadWorkStatesAsync(location.ProjectId);
        var parts = await BflPromptLibrary.LoadPartsAsync(location.Step, Tools.PluginSystem.I18N.I.AddressForm, this.PromptOverridesDirectory);
        var tools = await BflResearchAvailability.CheckAsync(toolRegistry, provider);
        var research = await this.LoadResearchAsync(location);
        return await this.ShareMaterialsAsync(location, parts, forms, workStates, tools, research, language, provider);
    }

    /// <summary>
    /// Loads the research a step carries in its prompt: the step "Strategy" all of it, every other step none.
    /// </summary>
    /// <param name="location">The step.</param>
    /// <returns>The research this version can open, by its topics.</returns>
    private async Task<IReadOnlyDictionary<BflResearchTopic, BflResearch>> LoadResearchAsync(BflStepThreadLocation location) => location.Step is BflStep.STRATEGY
        ? await store.LoadAllResearchAsync(location.ProjectId)
        : new Dictionary<BflResearchTopic, BflResearch>();

    /// <summary>
    /// Decides how much of the material of a preparation a request of a step carries: which images go along, and how
    /// much of each text fits beside them.
    /// </summary>
    /// <param name="location">The step.</param>
    /// <param name="parts">The texts of the prompt of the step.</param>
    /// <param name="forms">The forms of the preparation.</param>
    /// <param name="workStates">The work states of the preparation, whose confirmed ones take room in the prompt as well.</param>
    /// <param name="tools">What keeps the tools of the lab from being offered, which the prompt describes as well.</param>
    /// <param name="research">The research the prompt of the step carries, by its topics.</param>
    /// <param name="language">The name of the language the model answers in.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <returns>The share of each material this version can open, in the order the person added them.</returns>
    private async Task<IReadOnlyList<BflMaterialShare>> ShareMaterialsAsync(BflStepThreadLocation location, BflPromptParts parts, IReadOnlyDictionary<BflStep, BflStepFormData> forms, IReadOnlyDictionary<BflStep, BflWorkState> workStates, BflResearchAvailability tools, IReadOnlyDictionary<BflResearchTopic, BflResearch> research, string language, AIStudio.Settings.Provider provider)
    {
        var materials = (await store.ListMaterialsAsync(location.ProjectId)).Select(entry => entry.Material).OfType<BflMaterial>().ToList();
        if (materials.Count is 0)
            return [];

        var imageShares = BflMaterialBudget.ShareImages([.. materials.Where(material => material.Kind is BflMaterialKind.IMAGE)], ImagesTheModelTakes(provider));
        var sentImages = imageShares.Count(share => share.Image is BflImageDelivery.SENT);

        var promptWithoutMaterials = BflPromptComposer.Compose(parts, location.Step, forms, [], language, workStates, tools, research);
        var characters = BflMaterialBudget.CharactersFor(provider.GetModelProfile().Context, promptWithoutMaterials.Length, sentImages);
        var textShares = BflMaterialBudget.Share([.. materials.Where(material => material.Kind is not BflMaterialKind.IMAGE)], characters);

        var sharesById = textShares.Concat(imageShares).ToDictionary(share => share.Material.MaterialId);
        return [.. materials.Select(material => sharesById[material.MaterialId])];
    }

    /// <summary>
    /// Reads as much of each material as its share allows, and no more.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="shares">The share of each material.</param>
    /// <returns>What the prompt carries of each material which could be read.</returns>
    private async Task<IReadOnlyList<BflMaterialExcerpt>> ReadMaterialsAsync(Guid projectId, IReadOnlyList<BflMaterialShare> shares)
    {
        List<BflMaterialExcerpt> excerpts = [];
        foreach (var share in shares)
        {
            // An image has no text; the prompt names it and says whether it goes along:
            if (share.Material.Kind is BflMaterialKind.IMAGE)
            {
                excerpts.Add(new(share, string.Empty));
                continue;
            }

            try
            {
                var text = await store.ReadMaterialTextAsync(projectId, share.Material.MaterialId, share.Characters);
                excerpts.Add(new(share, share.IsComplete ? text : BflMaterialBudget.EndAtLineEnd(text)));
            }
            catch (InvalidDataException exception)
            {
                // The person removed the material meanwhile, or its file went missing; the list shows that:
                logger.LogWarning(exception, "Could not read the material '{MaterialId}' of the BeyondFacts Lab.", share.Material.MaterialId);
            }
        }

        return excerpts;
    }

    /// <summary>
    /// Checks whether a provider may answer in a chat, and tells the person why not.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="thread">The chat.</param>
    /// <param name="provider">The provider.</param>
    /// <returns>True when the provider may answer.</returns>
    internal async Task<bool> CheckProviderAsync(BflThreadLocation location, ChatThread thread, AIStudio.Settings.Provider provider)
    {
        string? reason = null;
        if (provider.UsedLLMProvider is LLMProviders.NONE)
            reason = TA("Please select a provider.");
        else if (!settingsManager.IsProviderConfident(provider, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT) || !thread.IsLLMProviderAllowed(provider))
            reason = TA("The selected provider may not see your preparation. Please select another provider.");
        else if (!BflModelRequirements.HasEnoughContext(location, provider.GetModelProfile().Context))
            reason = TA("The model of the selected provider reads too little at once for the lab. Please select another provider.");

        if (reason is null)
            return true;

        await messageBus.SendError(new(AppIcons.PROVIDER_ERROR, reason));
        return false;
    }

    /// <summary>
    /// Ends a request: removes an empty answer, stores the chat, and closes the session.
    /// </summary>
    /// <param name="conversation">The chat.</param>
    /// <param name="thread">The thread of the chat.</param>
    /// <param name="answerBlock">The block of the answer, or null when the request did not get that far.</param>
    /// <param name="sessionKey">The key of the session.</param>
    /// <param name="sessionId">The ID of the session, or Guid.Empty when none began.</param>
    /// <param name="status">How the request ended.</param>
    private async Task FinishAsync(BflConversation conversation, ChatThread thread, ContentBlock? answerBlock, AssistantSessionKey sessionKey, Guid sessionId, AssistantSessionStatus status)
    {
        if (answerBlock is { Content: ContentText answer } && string.IsNullOrWhiteSpace(answer.Text))
            thread.Blocks.Remove(answerBlock);

        if (answerBlock is not null)
        {
            try
            {
                await store.SaveThreadAsync(conversation.Location, thread);
            }
            catch (FileNotFoundException)
            {
                // The person deleted the preparation while the model answered; nothing is left to store.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                logger.LogError(exception, "The BeyondFacts Lab could not store the chat '{Location}'.", conversation.Location.Key);
                await messageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not save this conversation. Please try again.")));
            }
        }

        lock (this.runningLock)
            conversation.IsRunning = false;

        if (sessionId != Guid.Empty)
        {
            await sessionService.CompleteAsync(sessionKey, sessionId, status, string.Empty, thread, new(StringComparer.Ordinal));

            // The lab keeps the chat itself; the registry only shows that the lab is at work:
            await sessionService.ClearAsync(sessionKey);
        }

        await this.NotifyAsync(conversation.Location);
    }

    /// <summary>
    /// Tells every page which listens that a chat changed.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    private async Task NotifyAsync(BflThreadLocation location)
    {
        if (this.ConversationChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<BflThreadLocation, Task>>())
        {
            try
            {
                await handler(location);
            }
            catch (Exception exception)
            {
                // A page which went away meanwhile must not stop the answer:
                logger.LogDebug(exception, "A page of the BeyondFacts Lab could not show a change of a chat.");
            }
        }
    }

    /// <summary>
    /// Gets the key under which a request of a chat registers in AssistantSessionService.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    private static AssistantSessionKey SessionKey(BflThreadLocation location) => new(Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.Key);
}