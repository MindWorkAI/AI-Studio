using System.Text;
using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;
using AIStudio.Tools.RAG.RAGProcesses;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.Web;

namespace AIStudio.Chat;

/// <summary>
/// Text content in the chat.
/// </summary>
public sealed class ContentText : IContent
{
    private static readonly ILogger<ContentText> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ContentText>();
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ContentText).Namespace, nameof(ContentText));
    
    /// <summary>
    /// The minimum time between two streaming events, when the user
    /// enables the energy saving mode.
    /// </summary>
    private static readonly TimeSpan MIN_TIME = TimeSpan.FromSeconds(3);

    #region Implementation of IContent

    /// <inheritdoc />
    [JsonIgnore]
    public bool InitialRemoteWait { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public bool IsStreaming { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingDone { get; set; } = IContent.NO_STREAMING_HANDLER;

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingEvent { get; set; } = IContent.NO_STREAMING_HANDLER;

    /// <inheritdoc />
    public List<Source> Sources { get; set; } = [];
    
    /// <inheritdoc />
    public List<FileAttachment> FileAttachments { get; set; } = [];

    public List<ToolInvocationTrace> ToolInvocations { get; set; } = [];

    /// <summary>
    /// The passages the data sources handed to the AI for this answer, as the AI received them.
    /// </summary>
    /// <remarks>
    /// Stored with the answer, so the user can still read what it rests on after the chat was
    /// reloaded. Each passage belongs to one of the sources, see SourcePassageExtensions.GetPassagesOf.
    /// Empty for every answer written before passages were kept, and for every answer no data
    /// source contributed to.
    /// </remarks>
    public List<SourcePassage> SourcePassages { get; set; } = [];

    /// <summary>
    /// Adds sources to this answer, together with the passages the AI received from them.
    /// </summary>
    /// <remarks>
    /// Both at once, because the title of a source names where its passages are, see
    /// SourcePassageExtensions.NamePassagesInTitles. A passage added on its own would leave a title
    /// behind which names only the pages known before it.
    /// </remarks>
    /// <param name="sources">The sources to add.</param>
    /// <param name="passages">The passages to add.</param>
    public void AddSources(IEnumerable<ISource> sources, IEnumerable<SourcePassage> passages)
    {
        this.Sources.MergeSources(sources);
        this.SourcePassages.MergePassages(passages);
        this.Sources.NamePassagesInTitles(this.SourcePassages);
    }

    /// <summary>
    /// Whether the user was told that a text stays in the chat file, since the provenance store could not take it.
    /// </summary>
    /// <remarks>
    /// Once per run of the app: a store which cannot be written stays so for a while, and a message
    /// with every answer would tell the user nothing new.
    /// </remarks>
    private static int UNSTORED_CONTENT_REPORTED;

    /// <summary>
    /// Moves what the AI received for this answer into the provenance store, so the chat keeps only references to it.
    /// </summary>
    /// <remarks>
    /// Called once the answer is complete, also when it was stopped or failed: until then, a passage
    /// found twice is told apart by its text, see SourcePassageExtensions.MergePassages, and only the
    /// passages which made it into the answer get stored. Nothing here may stop the answer from being
    /// stored, so every failure ends in the passages keeping their texts.
    /// </remarks>
    /// <param name="chatId">The chat this answer belongs to.</param>
    public async Task StoreReceivedContentAsync(Guid chatId)
    {
        // Most answers, those of the agents among them, received nothing to store, and need no database:
        if (!this.SourcePassages.Any(passage => passage.HoldsTextsToStore()))
            return;

        try
        {
            var store = await Program.SERVICE_PROVIDER.GetRequiredService<DatabaseClientProvider>().GetProvenanceStoreAsync();
            if (await this.StoreReceivedContentAsync(store, chatId))
                return;
        }
        catch (Exception exception)
        {
            LOGGER.LogError(exception, "Failed to store what the AI received for an answer of the chat '{ChatId}'. It stays in the chat file.", chatId);
        }

        if (Interlocked.Exchange(ref UNSTORED_CONTENT_REPORTED, 1) is 0)
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.Storage, TB("AI Studio could not store what the AI received apart from your chat. It stays in the chat itself, so you can still read it.")));
    }

    /// <summary>
    /// Moves what the AI received for this answer into the given provenance store.
    /// </summary>
    /// <remarks>
    /// The list of passages is replaced as a whole rather than changed: the user interface may read
    /// it meanwhile. A passage which the store could not take keeps its texts.
    /// </remarks>
    /// <param name="store">The provenance store.</param>
    /// <param name="chatId">The chat this answer belongs to.</param>
    /// <returns>True when everything was stored, false when something stays in the chat itself.</returns>
    internal async Task<bool> StoreReceivedContentAsync(ProvenanceStoreClient store, Guid chatId)
    {
        var allStored = true;
        var passages = new List<SourcePassage>(this.SourcePassages.Count);
        foreach (var passage in this.SourcePassages)
        {
            if (!passage.HoldsTextsToStore())
            {
                passages.Add(passage);
                continue;
            }

            Guid? provenanceId = null;
            try
            {
                // Not cancellable: the answer is complete, also when the user stopped it.
                provenanceId = await store.StoreAsync(chatId, passage.GetProvenanceKind(), passage.Text, passage.SurroundingContent, CancellationToken.None);
            }
            catch (Exception exception)
            {
                LOGGER.LogWarning(exception, "Failed to store a passage of the source '{SourceUrl}'. It stays in the chat file.", passage.SourceUrl);
            }

            if (provenanceId is null)
            {
                allStored = false;
                passages.Add(passage);
                continue;
            }

            passages.Add(passage with { ProvenanceId = provenanceId, Text = string.Empty, SurroundingContent = [] });
        }

        this.SourcePassages = passages;
        return allStored;
    }

    /// <summary>
    /// What the provider said everything sent along with this answer cost, where it said anything.
    /// </summary>
    /// <remarks>
    /// Kept on the answer rather than beside the chat, so that it is stored, loaded, and exported
    /// with the message it belongs to -- and so that it goes away when the message does. An edited
    /// or regenerated answer removes its block, which removes these numbers with it. Null for every
    /// answer written before this was recorded, and at every provider which reports nothing.
    ///
    /// Having a report is not the same as the report still being true. Whether it still describes
    /// the conversation is decided by ChatThread.ReportedHistoryFor, which looks at the thread around
    /// the answer, not just at the answer.
    /// </remarks>
    public ReportedTokenUsage? ReportedUsage { get; set; }

    [JsonIgnore]
    public ToolRuntimeStatus ToolRuntimeStatus { get; set; } = new();

    /// <summary>
    /// What the tool conversation of the running request adds to it, as far as it has got.
    /// </summary>
    /// <remarks>
    /// A model which calls tools asks several times before it answers, and every one of those
    /// requests carries everything the tools returned so far -- up to three hundred thousand
    /// characters of it. None of that is in this block's text, and none of it is in the traces
    /// either: those say what happened, not what it costs. So it is kept here, where whoever
    /// counts the conversation walks past anyway.<br/><br/>
    /// Replaced as a whole, never appended to: it is written by the thread which runs the tools
    /// and read by the one which renders, and an exchange leaves the reader with a list which was
    /// true at some moment rather than with one being rewritten under it.<br/><br/>
    /// Gone when the answer is there, and never persisted. The accumulated tool conversation lives
    /// in the provider adapter, which is created for one request and dropped with it -- so the next
    /// request does not carry it, and a number which still counted it would promise a cost nobody
    /// is going to pay.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<string> PendingToolConversation { get; set; } = [];

    /// <summary>
    /// Clears what the previous run of the tools left behind.
    /// </summary>
    /// <remarks>
    /// Both parts at once, because both belong to one request: the traces the user reads and the
    /// payload the counting needs. They were cleared separately for exactly as long as there was
    /// only one of them.
    /// </remarks>
    public void BeginToolRun()
    {
        this.ToolInvocations.Clear();
        this.PendingToolConversation = [];
    }

    /// <summary>
    /// Says that no request is running anymore.
    /// </summary>
    /// <remarks>
    /// The traces stay -- they are what the user reads afterwards to see how the answer came
    /// about. What goes is the payload, which belonged to a request that is over.
    /// </remarks>
    public void EndToolRun() => this.PendingToolConversation = [];

    /// <summary>
    /// Keeps what the provider says the request behind this answer cost.
    /// </summary>
    /// <remarks>
    /// The one place where a stream's usage becomes part of the answer, for every path which writes
    /// one. It arrives on one line of the stream, usually the last one, and only where the provider
    /// reports it at all -- so a usage which states nothing leaves what was reported before alone.
    /// </remarks>
    /// <param name="usage">What the current chunk of the stream says, which is mostly nothing.</param>
    /// <param name="modelId">The model the request went to.</param>
    /// <param name="blockCount">How many blocks the conversation had when the request went out, this answer included.</param>
    public void RecordReportedUsage(TokenUsage usage, string modelId, int blockCount)
    {
        if (!usage.IsKnown)
            return;

        this.ReportedUsage = new()
        {
            PromptTokens = usage.PromptTokens,
            ModelId = modelId,
            BlockCount = blockCount,
        };
    }

    /// <summary>
    /// Adds what one chunk of the stream brings to this answer: its text, its sources together with
    /// their passages, and what the provider says the request cost.
    /// </summary>
    /// <remarks>
    /// The chat and the job service both write answers from a stream, and they used to do so step
    /// by step each. A step one of them missed, such as the passages, would have been missing from
    /// every answer written that way.
    /// </remarks>
    /// <param name="chunk">The chunk of the stream.</param>
    /// <param name="modelId">The model the request went to.</param>
    /// <param name="blockCount">How many blocks the conversation had when the request went out, this answer included.</param>
    public void ApplyStreamChunk(ContentStreamChunk chunk, string modelId, int blockCount)
    {
        // The first chunk ends the waiting animation:
        this.InitialRemoteWait = false;
        this.IsStreaming = true;

        this.Text += chunk;
        if (chunk.Sources.Count > 0 || chunk.Passages.Count > 0)
            this.AddSources(chunk.Sources, chunk.Passages);

        this.RecordReportedUsage(chunk.Usage, modelId, blockCount);
    }

    /// <inheritdoc />
    public async Task<ChatThread> CreateFromProviderAsync(IProvider provider, Model chatModel, IContent? lastUserPrompt, ChatThread? chatThread, CancellationToken token = default)
    {
        if(chatThread is null)
        {
            await this.CompleteWithoutStreaming();
            return new();
        }
        
        if(!chatThread.IsLLMProviderAllowed(provider))
        {
            LOGGER.LogError("The provider is not allowed for this chat thread due to data security or confidence-level requirements. Skipping the AI process.");
            await this.CompleteWithoutStreaming();
            return chatThread;
        }

        if(!await this.CheckSelectedModelAvailability(provider, chatModel, token))
        {
            await this.CompleteWithoutStreaming();
            return chatThread;
        }

        // Call the RAG process. Right now, we only have one RAG process:
        if (lastUserPrompt is not null)
        {
            try
            {
                var rag = new AISrcSelWithRetCtxVal();
                chatThread = await rag.ProcessAsync(provider, chatModel, lastUserPrompt, chatThread, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                //
                // The user canceled the request. That is not an error, and it must not reach the
                // user as one. We do not rethrow here: the streaming task below observes the same
                // token and ends the request itself, which keeps its finally block intact. That
                // block is what tells the UI that the streaming is over.
                //
                LOGGER.LogInformation("The RAG process was canceled before the answer was requested.");
            }
            catch (Exception e)
            {
                LOGGER.LogError(e, "Skipping the RAG process due to an error.");

                //
                // The answer is about to be created without the data the user expected it to use.
                // Without this message, that answer is indistinguishable from one that did use it:
                //
                await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.Source, TB("Your data sources could not be used. This answer was created without them.")));
            }
        }

        // Store the last time we got a response. We use this later
        // to determine whether we should notify the UI about the
        // new content or not. Depends on the energy saving mode
        // the user chose.
        var last = DateTimeOffset.Now;

        // Get the settings manager:
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;

        // What the request carries, for telling later whether a report still describes this chat:
        var blocksSent = chatThread.Blocks.Count;

        // Start another thread by using a task to uncouple
        // the UI thread from the AI processing:
        try
        {
            await Task.Run(async () =>
            {
                try
                {
                    // We show the waiting animation until we get the first response:
                    this.InitialRemoteWait = true;

                    // Iterate over the responses from the AI:
                    await foreach (var contentStreamChunk in provider.StreamChatCompletion(chatModel, chatThread, settings, token))
                    {
                        // When the user cancels the request, we stop the loop:
                        if (token.IsCancellationRequested)
                            break;

                        // Add the response, its sources, and what it cost:
                        this.ApplyStreamChunk(contentStreamChunk, chatModel.Id, blocksSent);

                        // Notify the UI that the content has changed,
                        // depending on the energy saving mode:
                        var now = DateTimeOffset.Now;
                        switch (settings.ConfigurationData.App.IsSavingEnergy)
                        {
                            // Energy saving mode is off. We notify the UI
                            // as fast as possible -- no matter the odds:
                            case false:
                                await this.StreamingEvent();
                                break;

                            // Energy saving mode is on. We notify the UI
                            // only when the time between two events is
                            // greater than the minimum time:
                            case true when now - last > MIN_TIME:
                                last = now;
                                await this.StreamingEvent();
                                break;
                        }
                    }
                }
                finally
                {
                    // Stop the waiting animation (in case the loop
                    // was stopped, or no content was received):
                    this.InitialRemoteWait = false;
                    this.IsStreaming = false;
                }
            }, token);
        }
        finally
        {
            this.Text = this.Text.RemoveThinkTags().Trim();
            this.EndToolRun();

            // Before whoever is told next stores the chat, so that it refers to what the AI received instead of holding it:
            await this.StoreReceivedContentAsync(chatThread.ChatId);

            // Inform the UI that the streaming is done:
            await this.StreamingDone();
        }

        return chatThread;
    }

    private async Task CompleteWithoutStreaming()
    {
        this.InitialRemoteWait = false;
        this.IsStreaming = false;
        await this.StreamingDone();
    }

    private static bool ModelsMatch(Model modelA, Model modelB)
    {
        var idA = modelA.Id.Trim();
        var idB = modelB.Id.Trim();
        return string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> CheckSelectedModelAvailability(IProvider provider, Model chatModel, CancellationToken token = default)
    {
        if(chatModel.IsSystemModel)
            return true;

        if (string.IsNullOrWhiteSpace(chatModel.Id))
        {
            LOGGER.LogWarning("Skipping AI request because model ID is null or white space.");
            return false;
        }

        if (!provider.HasModelLoadingCapability)
            return true;

        IReadOnlyList<Model> loadedModels;
        try
        {
            var modelLoadResult = await provider.GetTextModels(token: token);
            if (!modelLoadResult.Success)
            {
                var userMessage = modelLoadResult.FailureReason.ToUserMessage(provider.InstanceName);
                if (!string.IsNullOrWhiteSpace(userMessage))
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, userMessage));

                LOGGER.LogWarning("Skipping selected model availability check for '{ProviderInstanceName}' (provider={ProviderType}) because loading the model list failed with reason {FailureReason}.", provider.InstanceName, provider.Provider, modelLoadResult.FailureReason);
                return false;
            }

            loadedModels = modelLoadResult.Models;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            LOGGER.LogWarning(e, "Skipping selected model availability check for '{ProviderInstanceName}' (provider={ProviderType}) because the model list could not be loaded.", provider.InstanceName, provider.Provider);
            return true;
        }

        var availableModels = loadedModels.Where(model => !string.IsNullOrWhiteSpace(model.Id)).ToList();
        if (availableModels.Count == 0)
        {
            var emptyModelsMessage = string.Format(
                TB("We could load models from '{0}', but the provider did not return any usable text models."),
                provider.InstanceName);

            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, emptyModelsMessage));
            LOGGER.LogWarning("Skipping AI request because there are no models available from '{ProviderInstanceName}' (provider={ProviderType}).", provider.InstanceName, provider.Provider);
            return false;
        }

        if(availableModels.Any(model => ModelsMatch(model, chatModel)))
            return true;
        
        var message = string.Format(
            TB("The selected model '{0}' is no longer available from '{1}' (provider={2}). Please adapt your provider settings."),
            chatModel.Id,
            provider.InstanceName,
            provider.Provider);
        
        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, message));
        LOGGER.LogWarning("Skipping AI request because model '{ModelId}' is not available from '{ProviderInstanceName}' (provider={ProviderType}).", chatModel.Id, provider.InstanceName, provider.Provider);
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The reported usage stays behind on purpose. A clone continues somewhere else -- as the
    /// example conversation of a chat template, or as an assistant's conversation carried over into
    /// a chat -- with another system prompt around it, and what the provider stated was for the
    /// request this answer came out of.
    /// </remarks>
    public IContent DeepClone() => new ContentText
    {
        Text = this.Text,
        InitialRemoteWait = this.InitialRemoteWait,
        IsStreaming = this.IsStreaming,
        Sources = [..this.Sources],
        SourcePassages = [..this.SourcePassages],
        FileAttachments = [..this.FileAttachments],
        ToolInvocations = [..this.ToolInvocations.Select(x => new ToolInvocationTrace
        {
            Order = x.Order,
            ToolId = x.ToolId,
            ToolName = x.ToolName,
            ToolIcon = x.ToolIcon,
            ToolCallId = x.ToolCallId,
            Status = x.Status,
            WasExecuted = x.WasExecuted,
            StatusMessage = x.StatusMessage,
            Arguments = new Dictionary<string, string>(x.Arguments, StringComparer.Ordinal),
            Result = x.Result,
            JsonResult = x.JsonResult?.DeepClone(),
        })],
    };

    #endregion

    /// <summary>
    /// The web addresses in the documents attached to this message, as request keys, see
    /// WebAddresses.CreateRequestKey.
    /// </summary>
    /// <remarks>
    /// The texts of the documents are known only when a message is sent, from the provenance store
    /// or from the disk, so their addresses are collected right then, see PrepareTextContentForAI,
    /// and count as given to the model, see ChatThread.IsWebAddressGivenToTheModel. Not stored:
    /// every request collects them anew from what it sends.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlySet<string> RuntimeAttachmentWebAddresses { get; internal set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Whether this message holds attached documents which were never stored, see StoreAttachedDocumentsAsync.
    /// </summary>
    internal bool HasDocumentsToStore() => this.FileAttachments.Any(attachment => attachment is { ProvenanceId: null, Type: FileAttachmentType.DOCUMENT });

    /// <summary>
    /// Stores the attached documents of this message which were never sent, reading each one from the disk once.
    /// </summary>
    /// <remarks>
    /// A document which cannot be read or stored right now keeps no entry: the message reads it when
    /// it is built, as every message did before, and tells the user then what went wrong. The list of
    /// attachments is replaced as a whole rather than changed, since the user interface may read it
    /// meanwhile.
    /// </remarks>
    /// <param name="store">The provenance store.</param>
    /// <param name="chatId">The chat of this message.</param>
    /// <param name="rustService">The runtime, which reads the documents.</param>
    /// <param name="reportingScope">The request the documents are read for, which tells the user once about everything filtered out of them.</param>
    internal async Task StoreAttachedDocumentsAsync(ProvenanceStoreClient store, Guid chatId, RustService rustService, PromptInjectionReportingScope? reportingScope)
    {
        var attachments = new List<FileAttachment>(this.FileAttachments.Count);
        var changed = false;
        foreach (var attachment in this.FileAttachments)
        {
            var storedAttachment = await StoreAttachedDocumentAsync(attachment, store, chatId, rustService, reportingScope);
            changed |= !ReferenceEquals(storedAttachment, attachment);
            attachments.Add(storedAttachment);
        }

        if (changed)
            this.FileAttachments = attachments;
    }

    /// <summary>
    /// Stores one attached document which was never sent.
    /// </summary>
    /// <returns>The attachment with the entry of its text, or the attachment as it was when there is nothing to store, or nothing could be.</returns>
    private static async Task<FileAttachment> StoreAttachedDocumentAsync(FileAttachment attachment, ProvenanceStoreClient store, Guid chatId, RustService rustService, PromptInjectionReportingScope? reportingScope)
    {
        if (attachment.ProvenanceId is not null || attachment.Type is not FileAttachmentType.DOCUMENT)
            return attachment;

        var document = attachment.Normalize();
        if (document.Type is not FileAttachmentType.DOCUMENT || !document.Exists)
            return attachment;

        try
        {
            // Quietly: building the message tells the user about a missing Pandoc, when it reads the document after all.
            if (FileTypes.RequiresPandoc(document.FilePath))
            {
                var pandocState = await Pandoc.CheckAvailabilityAsync(rustService, showMessages: false, showSuccessMessage: false);
                if (pandocState is not { IsAvailable: true, CheckWasSuccessful: true })
                    return attachment;
            }

            var extraction = await rustService.ReadArbitraryFileData(document.FilePath, int.MaxValue, reportingScope: reportingScope);
            if (!extraction.HasUsableContent)
                return attachment;

            // Told here, since no later request reads the document again:
            await ReportIncompleteReadingAsync(document, extraction);

            // Not cancellable: what was read is what the request sends.
            var provenanceId = await store.StoreAsync(chatId, ProvenanceKind.ATTACHMENT, extraction.Content, null, CancellationToken.None);
            return provenanceId is null ? attachment : attachment with { ProvenanceId = provenanceId };
        }
        catch (Exception exception)
        {
            LOGGER.LogWarning(exception, "Failed to store the file attachment '{FilePath}'. It is read from the disk with every request.", document.FilePath);
            return attachment;
        }
    }

    /// <summary>
    /// Tells the user when a document could be read, but not as it should have been.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="extraction">What reading it gave.</param>
    internal static async Task ReportIncompleteReadingAsync(FileAttachment document, FileExtractionResult extraction)
    {
        //
        // The file is usable, but we lost parts of it. The user has to know which
        // parts are missing, because the answer will be based on the rest.
        //
        if (extraction.Outcome is FileExtractionOutcome.PARTIAL)
        {
            LOGGER.LogWarning("Parts of the file attachment '{FilePath}' could not be read: pages={FailedPages}.", document.FilePath, string.Join(", ", extraction.FailedPages));
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.Description, extraction.ToPartialUserMessage(document.FileName)));
        }

        // The file was read correctly, but its extension lies about what it contains:
        if (extraction.HasExtensionMismatch)
        {
            LOGGER.LogWarning("The file attachment '{FilePath}' is actually a '{DetectedFormat}'.", document.FilePath, extraction.DetectedFormat);
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.RuleFolder, extraction.ToExtensionMismatchUserMessage(document.FileName)));
        }
    }

    /// <summary>
    /// Reads the stored texts of the attached documents.
    /// </summary>
    /// <param name="attachments">The attachments.</param>
    /// <param name="getProvenanceStore">Gets the provenance store, which is asked only when a document has an entry.</param>
    /// <returns>The texts by the ids of their entries. An entry the store lacks, or cannot read, is missing.</returns>
    private static async Task<IReadOnlyDictionary<Guid, string>> ReadStoredDocumentTextsAsync(IEnumerable<FileAttachment> attachments, Func<Task<ProvenanceStoreClient>> getProvenanceStore)
    {
        var provenanceIds = attachments.Select(attachment => attachment.ProvenanceId).OfType<Guid>().Distinct().ToList();
        if (provenanceIds.Count is 0)
            return new Dictionary<Guid, string>();

        try
        {
            var store = await getProvenanceStore();
            var entries = await store.GetEntriesAsync(provenanceIds, CancellationToken.None);
            return entries.ToDictionary(entry => entry.Key, entry => entry.Value.Content);
        }
        catch (Exception exception)
        {
            LOGGER.LogWarning(exception, "Failed to read the stored texts of the file attachments. Their files are read instead.");
            return new Dictionary<Guid, string>();
        }
    }

    /// <summary>
    /// Builds the text the AI gets for this message, with the content of its attached documents.
    /// </summary>
    /// <param name="reportingScope">The request this message is part of, which tells the user once about everything filtered out of its attachments, or null when the request reads no documents.</param>
    /// <returns>The text for the AI.</returns>
    public Task<string> PrepareTextContentForAI(PromptInjectionReportingScope? reportingScope) => this.PrepareTextContentForAI(reportingScope, Program.RUST_SERVICE, () => Program.SERVICE_PROVIDER.GetRequiredService<DatabaseClientProvider>().GetProvenanceStoreAsync());

    /// <summary>
    /// Builds the text the AI gets for this message, with the content of its attached documents.
    /// </summary>
    /// <remarks>
    /// A document stored at its first send comes from the provenance store, also when its file
    /// changed or is gone by now, see FileAttachment.ProvenanceId: the AI gets the same text with
    /// every request. Only a document without a stored text is read from the disk, as before.
    /// </remarks>
    /// <param name="reportingScope">The request this message is part of, which tells the user once about everything filtered out of its attachments, or null when the request reads no documents.</param>
    /// <param name="rustService">The runtime, which reads the documents without a stored text.</param>
    /// <param name="getProvenanceStore">Gets the provenance store, which is asked only when a document has an entry.</param>
    /// <returns>The text for the AI.</returns>
    internal async Task<string> PrepareTextContentForAI(PromptInjectionReportingScope? reportingScope, RustService rustService, Func<Task<ProvenanceStoreClient>> getProvenanceStore)
    {
        var sb = new StringBuilder();
        var attachmentWebAddresses = new HashSet<string>(StringComparer.Ordinal);
        sb.AppendLine(this.Text);

        if(this.FileAttachments.Count > 0)
        {
            var normalizedAttachments = this.FileAttachments
                .Select(attachment => attachment.Normalize())
                .ToList();

            var storedTexts = await ReadStoredDocumentTextsAsync(normalizedAttachments, getProvenanceStore);

            // Get the list of existing documents, those with a stored text included, whether their file is still there or not:
            var existingDocuments = normalizedAttachments.Where(x => x.Type is FileAttachmentType.DOCUMENT && (StoredTextOf(x) is not null || x.Exists)).ToList();

            //
            // Report missing files. We tell the user about them instead of only logging: on a
            // network drive, a file which is temporarily unreachable looks exactly like a deleted
            // one, and silently dropping it would let the AI answer without that document.
            //
            var missingDocuments = normalizedAttachments.Except(existingDocuments).Where(x => x.Type is FileAttachmentType.DOCUMENT).ToList();
            foreach (var missingDocument in missingDocuments)
            {
                LOGGER.LogWarning("File attachment no longer exists and will be skipped: '{MissingDocument}'.", missingDocument.FilePath);
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.FindInPage, string.Format(TB("The file '{0}' is currently not available and was not sent."), missingDocument.FileName)));
            }

            // Only proceed if there are existing, allowed documents:
            if (existingDocuments.Count > 0)
            {
                //
                // Pandoc is only needed for the few formats we convert with it. PDFs, text files,
                // spreadsheets, and presentations are read by the runtime itself, so a missing
                // Pandoc installation must not stop them.
                //
                var pandocIsUsable = true;
                if (existingDocuments.Any(document => StoredTextOf(document) is null && FileTypes.RequiresPandoc(document.FilePath)))
                {
                    var pandocState = await Pandoc.CheckAvailabilityAsync(rustService, showMessages: true, showSuccessMessage: false);
                    pandocIsUsable = pandocState is { IsAvailable: true, CheckWasSuccessful: true };

                    if (!pandocState.IsAvailable)
                        LOGGER.LogWarning("File attachments which need Pandoc could not be processed because Pandoc is not available.");
                    else if (!pandocState.CheckWasSuccessful)
                        LOGGER.LogWarning("File attachments which need Pandoc could not be processed because the Pandoc version check failed.");
                }

                //
                // The document blocks are collected separately, so we only announce attached
                // files when at least one of them could actually be read. Announcing files we
                // then hand over as empty blocks makes the AI answer about an empty document.
                //
                var documentBlocks = new StringBuilder();
                foreach(var document in existingDocuments)
                {
                    var content = StoredTextOf(document);
                    if (content is null)
                    {
                        if (document.IsForbidden)
                        {
                            LOGGER.LogWarning("File attachment '{FilePath}' has a forbidden file type and will be skipped.", document.FilePath);
                            continue;
                        }

                        if (!pandocIsUsable && FileTypes.RequiresPandoc(document.FilePath))
                        {
                            LOGGER.LogWarning("The file attachment '{FilePath}' needs Pandoc and will be skipped.", document.FilePath);
                            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Description, FileExtractionErrorCode.PANDOC_UNAVAILABLE.ToUserMessage(document.FileName)));
                            continue;
                        }

                        var extraction = await rustService.ReadArbitraryFileData(document.FilePath, int.MaxValue, reportingScope: reportingScope);
                        if (!extraction.HasUsableContent)
                        {
                            LOGGER.LogError("Reading the file attachment '{FilePath}' failed and it will not be sent: code={ErrorCode}, message='{ErrorMessage}'.", document.FilePath, extraction.ErrorCode, extraction.ErrorMessage);
                            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Description, extraction.ToUserMessage(document.FileName)));
                            continue;
                        }

                        await ReportIncompleteReadingAsync(document, extraction);
                        content = extraction.Content;
                    }

                    documentBlocks.AppendLine();
                    documentBlocks.AppendLine("---------------------------------------");
                    documentBlocks.AppendLine($"File path: {document.FilePath}");
                    documentBlocks.AppendLine("File content:");
                    documentBlocks.AppendLine("````");
                    documentBlocks.AppendLine(content);
                    documentBlocks.AppendLine("````");

                    foreach (var address in WebAddresses.Find(content))
                        if (WebAddresses.TryCreateRequestKey(address, out var requestKey))
                            attachmentWebAddresses.Add(requestKey);
                }

                if (documentBlocks.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("The following files are attached to this message:");
                    sb.Append(documentBlocks);
                }

                var numImages = normalizedAttachments.Count(x => x is { IsImage: true, Exists: true });
                if (numImages > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Additionally, there are {numImages} image file(s) attached to this message. ");
                    sb.AppendLine("Please consider them as part of the message content and use them to answer accordingly.");
                }
            }

            string? StoredTextOf(FileAttachment document) => document.ProvenanceId is { } provenanceId ? storedTexts.GetValueOrDefault(provenanceId) : null;
        }

        this.RuntimeAttachmentWebAddresses = attachmentWebAddresses;
        return sb.ToString();
    }
    
    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}