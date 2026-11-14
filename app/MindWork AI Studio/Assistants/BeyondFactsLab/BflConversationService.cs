using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Opens the chats of the BeyondFacts Lab, such as those of the steps of a preparation, and puts together what each of
/// their requests carries.
/// </summary>
/// <remarks>
/// The chats themselves run in EmbeddedChatService, like the chats of other assistants: it keeps them while the app
/// runs, so that an answer goes on while the person looks at another page, and registers each request in
/// AssistantSessionService. A chat lives where the lab keeps it, see BflChatStore. What the lab decides for every
/// request comes from here, see PrepareAsync: the system prompt with the guide, the details, and the material, the
/// images of the material, the tools, and where the chat may send data. The service does nothing until the page of the
/// lab asks for a chat.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="chatService">The service which runs the embedded chats.</param>
/// <param name="settingsManager">The settings.</param>
/// <param name="messageBus">The message bus, for errors.</param>
/// <param name="toolRegistry">The registry of the tools, which tells the chats what they can do on the web.</param>
/// <param name="logger">The logger.</param>
public sealed class BflConversationService(BeyondFactsLabStore store, EmbeddedChatService chatService, SettingsManager settingsManager, MessageBus messageBus, ToolRegistry toolRegistry, ILogger<BflConversationService> logger)
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

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflConversationService).Namespace, nameof(BflConversationService));

    /// <summary>
    /// Gets the folder whose files replace the prompts of the lab, or null while the person does not allow it. The
    /// setting counts anew for every request, so that switching it on or off shows in the next answer.
    /// </summary>
    internal string? PromptOverridesDirectory => BflPromptOverrides.ActiveDirectory(settingsManager.ConfigurationData.BeyondFactsLab, store.PromptOverridesDirectory);

    /// <summary>
    /// Gets a chat, loading it on the first call.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat; it is not available when this version cannot open the stored one.</returns>
    public Task<EmbeddedConversation> GetAsync(BflThreadLocation location, CancellationToken token = default) => chatService.GetAsync(this.StoreOf(location), Tools.Components.BEYOND_FACTS_LAB_ASSISTANT, location.ChatId, () => BflThreadFactory.Create(location), token);

    /// <summary>
    /// Gets where a chat of the lab lives.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <returns>The store, which serves this chat only.</returns>
    public IEmbeddedChatStore StoreOf(BflThreadLocation location) => new BflChatStore(store, location);

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
    /// What goes wrong is reported through the message bus; the method itself does not fail.
    /// </remarks>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="text">The message.</param>
    /// <param name="isHidden">Whether the person does not see the message, e.g., the request which opens a conversation.</param>
    public async Task SendAsync(BflThreadLocation location, AIStudio.Settings.Provider provider, string text, bool isHidden = false)
    {
        await this.GetAsync(location);
        await chatService.SendAsync(location.ChatId, new EmbeddedChatRequest
        {
            Provider = provider,
            Message = new ContentText { Text = text },
            IsHidden = isHidden,
            PrepareAsync = thread => this.PrepareAsync(location, provider, thread)
        });
    }

    /// <summary>
    /// Stops the answer the model writes in a chat.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    public Task CancelAsync(BflThreadLocation location) => chatService.CancelAsync(location.ChatId);

    /// <summary>
    /// Deletes a conversation, so that it starts over.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <returns>False while the model answers in the chat.</returns>
    public async Task<bool> ResetAsync(BflThreadLocation location)
    {
        await this.GetAsync(location);
        return await chatService.ResetAsync(location.ChatId, () => BflThreadFactory.Create(location));
    }

    /// <summary>
    /// Stops every answer of a preparation and forgets its chats, e.g., because the person deleted it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <returns>The IDs of the chats the preparation could have had.</returns>
    public async Task<IReadOnlyList<Guid>> ForgetProjectAsync(Guid projectId)
    {
        List<Guid> chatIds = [.. BflStep.All.Select(step => new BflStepThreadLocation(projectId, step).ChatId)];
        foreach (var chatId in chatIds)
            await chatService.ForgetAsync(chatId);

        return chatIds;
    }

    /// <summary>
    /// Readies a chat of the lab for a request, see EmbeddedChatRequest.PrepareAsync: checks the provider and puts
    /// together what the request carries.
    /// </summary>
    /// <remarks>
    /// What goes wrong is reported through the message bus; the request then does not go out.
    /// </remarks>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="thread">The chat.</param>
    /// <returns>What the request carries, or EmbeddedChatPreparation.REFUSED.</returns>
    public async Task<EmbeddedChatPreparation> PrepareAsync(BflThreadLocation location, AIStudio.Settings.Provider provider, ChatThread thread)
    {
        if (!await this.CheckProviderAsync(location, thread, provider))
            return EmbeddedChatPreparation.REFUSED;

        BflRequestMaterial requestMaterial;
        try
        {
            requestMaterial = await this.ComposeRequestAsync(location, provider);
        }
        catch (BflPromptOverrideException exception)
        {
            logger.LogWarning(exception, "A prompt override of the BeyondFacts Lab has a placeholder which the lab does not fill.");
            await messageBus.SendError(new(AppIcons.ERROR, string.Format(TA("The file '{0}' in the test folder of the BeyondFacts Lab contains the placeholder '{1}', which the lab does not fill in this file. Please correct the file and try again."), exception.FileName, exception.Placeholder)));
            return EmbeddedChatPreparation.REFUSED;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            logger.LogError(exception, "The BeyondFacts Lab could not put together the system prompt of the chat '{Location}'.", location.Key);
            await messageBus.SendError(new(AppIcons.ERROR, TA("The BeyondFacts Lab could not put together its instructions for the model. Please report this as a bug.")));
            return EmbeddedChatPreparation.REFUSED;
        }

        return Prepare(location, provider, thread, requestMaterial);
    }

    /// <summary>
    /// Readies a chat of the lab for a request with what the lab put together for it.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="provider">The provider which answers.</param>
    /// <param name="thread">The chat, which gets its prompt, its tools, and its restriction.</param>
    /// <param name="requestMaterial">The system prompt and the images of the material.</param>
    /// <returns>The preparation, whose request carries the images, see WithImages.</returns>
    internal static EmbeddedChatPreparation Prepare(BflThreadLocation location, AIStudio.Settings.Provider provider, ChatThread thread, BflRequestMaterial requestMaterial)
    {
        // A chat stored before the lab offered tools gets its restriction now, before it may read a page:
        BflChatTools.Restrict(thread, location);
        BflThreadFactory.PrepareForRequest(thread, provider, requestMaterial.SystemPrompt, BflChatTools.For(location));

        var images = requestMaterial.Images;
        return EmbeddedChatPreparation.READY with { ThreadForRequest = request => WithImages(request, images) };
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
}