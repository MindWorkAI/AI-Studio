using System.Globalization;
using System.Text.Json.Serialization;

using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Chat;

/// <summary>
/// Data structure for a chat thread.
/// </summary>
public sealed record ChatThread
{
    private static readonly ILogger<ChatThread> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ChatThread>();
    
    /// <summary>
    /// The unique identifier of the chat thread.
    /// </summary>
    public Guid ChatId { get; init; }
    
    /// <summary>
    /// The unique identifier of the workspace.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// The monotonically increasing number used for managed media transcript filenames.
    /// </summary>
    public ulong LastMediaTranscriptNumber { get; set; }

    /// <summary>
    /// Managed transcript attachments prepared for the composer but not sent yet.
    /// Empty by default so older serialized threads require no migration.
    /// </summary>
    public List<ManagedTranscriptAttachment> PendingMediaTranscripts { get; set; } = [];

    /// <summary>
    /// Specifies the provider selected for the chat thread.
    /// </summary>
    public string SelectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Specifies the profile selected for the chat thread.
    /// </summary>
    public string SelectedProfile { get; set; } = string.Empty;
    
    /// <summary>
    /// Specifies the profile selected for the chat thread.
    /// </summary>
    public string SelectedChatTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Specifies the tools selected for the chat thread, as the user chose them.
    /// </summary>
    /// <remarks>
    /// Null means the thread never stored a selection, which is the case for every chat written
    /// before tools existed: those open with the defaults of their component. An empty set is the
    /// opposite statement — the user switched every tool off and wants it to stay that way.<br/><br/>
    /// This is the unfiltered selection. What a provider may actually run is decided per request,
    /// because a provider with too little confidence must not cost the user a tool permanently.
    /// </remarks>
    public HashSet<string>? SelectedToolIds { get; set; }

    /// <summary>
    /// Indicates whether to include the current date and time in the system prompt.
    /// False by default for backward compatibility.
    /// </summary>
    public bool IncludeDateTime { get; set; } = false;

    /// <summary>
    /// The data source options for this chat thread.
    /// </summary>
    public DataSourceOptions DataSourceOptions { get; set; } = new();

    /// <summary>
    /// The AI-selected data sources for this chat thread.
    /// </summary>
    public IReadOnlyList<DataSourceAgentSelected> AISelectedDataSources { get; set; } = [];

    /// <summary>
    /// The augmented data for this chat thread. Will be inserted into the system prompt.
    /// </summary>
    public string AugmentedData { get; set; } = string.Empty;

    /// <summary>
    /// The data security to use, derived from the data sources used so far.
    /// </summary>
    public DataSourceSecurity DataSecurity { get; set; } = DataSourceSecurity.NOT_SPECIFIED;

    /// <summary>
    /// The minimum confidence required for providers that continue this chat. It is raised whenever
    /// a tool returned sensitive data, and whenever a data source was used which demands a higher
    /// level. Both cases share one rule: once such data is in the thread, every provider which
    /// continues it must meet the level.
    /// </summary>
    [JsonInclude]
    public ConfidenceLevel RequiredProviderConfidence { get; private set; } = ConfidenceLevel.NONE;

    public void RequireProviderConfidence(ConfidenceLevel minimumProviderConfidence)
    {
        if (minimumProviderConfidence > this.RequiredProviderConfidence)
            this.RequiredProviderConfidence = minimumProviderConfidence;
    }

    /// <summary>
    /// Tightens the data security of this chat to what the data brought in demands, and never
    /// loosens it.
    /// </summary>
    /// <remarks>
    /// Data which may only be used with self-hosted providers keeps the chat restricted to them,
    /// no matter what comes in later: the data was seen by this chat. Data which may be used with
    /// any provider marks the chat as one which holds data of a data source, while a restriction set
    /// earlier stays. NOT_SPECIFIED demands nothing and changes nothing.<br/><br/>
    /// Shared by the RAG process and by tools which search the data sources, so both tighten a chat
    /// the same way.
    /// </remarks>
    /// <param name="dataSecurity">What the data brought in demands.</param>
    public void RequireDataSecurity(DataSourceSecurity dataSecurity) => this.DataSecurity = (this.DataSecurity, dataSecurity) switch
    {
        (DataSourceSecurity.SELF_HOSTED, _) or (_, DataSourceSecurity.SELF_HOSTED) => DataSourceSecurity.SELF_HOSTED,
        (_, DataSourceSecurity.ALLOW_ANY) => DataSourceSecurity.ALLOW_ANY,
        _ => this.DataSecurity,
    };

    /// <summary>
    /// The name of the chat thread. Usually generated by an AI model or manually edited by the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The current system prompt for the chat thread.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// The content blocks of the chat thread.
    /// </summary>
    public List<ContentBlock> Blocks { get; init; } = [];

    [JsonIgnore]
    public AIStudio.Tools.Components RuntimeComponent { get; set; } = AIStudio.Tools.Components.CHAT;

    [JsonIgnore]
    public HashSet<string> RuntimeSelectedToolIds { get; set; } = [];

    /// <summary>
    /// Whether the tools of this run were named by the assistant's own rules instead of chosen by
    /// the user.
    /// </summary>
    /// <remarks>
    /// A user who cannot see a tool selection must not get tools they never picked, which is why
    /// running tools normally requires a visible selection. That rule misses the case where nobody
    /// asked the user in the first place: a document analysis policy or an assistant plugin names
    /// its tools, and hiding the selection is the point rather than an obstacle. This flag tells
    /// the providers which of the two they are looking at.
    /// </remarks>
    [JsonIgnore]
    public bool RuntimeToolsAreAssistantManaged { get; set; }

    /// <summary>
    /// Whether this thread may run tools at all.
    /// </summary>
    public bool MayRunTools(SettingsManager settingsManager) => this.RuntimeToolsAreAssistantManaged || settingsManager.IsToolSelectionVisible(this.RuntimeComponent);
    
    /// <summary>
    /// Prepares the system prompt for the chat thread, and remembers what it was built from.
    /// </summary>
    /// <remarks>
    /// The actual system prompt depends on the selected profile. If no profile is selected,
    /// the system prompt is returned as is. When a profile is selected, the system prompt
    /// is extended with the profile chosen.
    /// </remarks>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="runnableToolDefinitions">The tools which may run in this thread. Their instructions become part of the system prompt. Null when the thread runs without tools.</param>
    /// <returns>The prepared system prompt.</returns>
    public string PrepareSystemPrompt(SettingsManager settingsManager, IEnumerable<ToolDefinition>? runnableToolDefinitions = null)
    {
        var prepared = this.BuildSystemPrompt(settingsManager, runnableToolDefinitions);

        // We need a way to save the changed system prompt in our chat thread.
        // Otherwise, the chat thread will always tell us that it is using the
        // default system prompt:
        this.SystemPrompt = prepared.BasePrompt;
        LOGGER.LogInformation(prepared.Explanation);

        return prepared.Text;
    }

    /// <summary>
    /// Works out the system prompt without changing anything about the thread.
    /// </summary>
    /// <remarks>
    /// Split off from the preparation above so that somebody can ask how long the next request
    /// would be. Counting the tokens of a conversation has to ask the same question the request
    /// asks -- a count against the prompt a person typed, rather than against the one a chat
    /// template, a data source, a profile and the tool policy make of it, is a number about a
    /// request which is never sent.
    ///
    /// Nothing here writes to the thread and nothing logs, because this runs while somebody types.
    /// </remarks>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="runnableToolDefinitions">The tools which may run in this thread. Null when the thread runs without tools.</param>
    /// <returns>The system prompt and what building it decided.</returns>
    public PreparedSystemPrompt BuildSystemPrompt(SettingsManager settingsManager, IEnumerable<ToolDefinition>? runnableToolDefinitions = null)
    {
        var allowProfile = true;

        //
        // Use the information from the chat template, if provided. Otherwise, use the default system prompt
        //
        string systemPromptTextWithChatTemplate;
        var logMessage = $"Using no chat template for chat thread '{this.Name}'.";
        if (string.IsNullOrWhiteSpace(this.SelectedChatTemplate))
            systemPromptTextWithChatTemplate = this.SystemPrompt;
        else
        {
            if(!Guid.TryParse(this.SelectedChatTemplate, out var chatTemplateId))
                systemPromptTextWithChatTemplate = this.SystemPrompt;
            else
            {
                if(this.SelectedChatTemplate == ChatTemplate.NO_CHAT_TEMPLATE.Id || chatTemplateId == Guid.Empty)
                    systemPromptTextWithChatTemplate = this.SystemPrompt;
                else
                {
                    var chatTemplate = settingsManager.GetChatTemplateById(this.SelectedChatTemplate);
                    if(chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE)
                        systemPromptTextWithChatTemplate = this.SystemPrompt;
                    else
                    {
                        logMessage = $"Using chat template '{chatTemplate.Name}' for chat thread '{this.Name}'.";
                        allowProfile = chatTemplate.AllowProfileUsage;
                        systemPromptTextWithChatTemplate = chatTemplate.ToSystemPrompt();
                    }
                }
            }
        }

        //
        // Add augmented data, if available:
        //
        var isAugmentedDataAvailable = !string.IsNullOrWhiteSpace(this.AugmentedData);
        var systemPromptWithAugmentedData = isAugmentedDataAvailable switch
        {
            true => $"""
                     {systemPromptTextWithChatTemplate}
                     
                     {this.AugmentedData}
                     """,

            false => systemPromptTextWithChatTemplate,
        };
        
        logMessage = isAugmentedDataAvailable
            ? $"{logMessage} Augmented data is available for the chat thread."
            : $"{logMessage} No augmented data is available for the chat thread.";

        //
        // Add information from the profile if available and allowed:
        //
        string systemPromptText;
        var profileNote = $"Using no profile for chat thread '{this.Name}'.";
        if (string.IsNullOrWhiteSpace(this.SelectedProfile) || !allowProfile)
            systemPromptText = systemPromptWithAugmentedData;
        else
        {
            if(!Guid.TryParse(this.SelectedProfile, out var profileId))
                systemPromptText = systemPromptWithAugmentedData;
            else
            {
                if(this.SelectedProfile == Profile.NO_PROFILE.Id || profileId == Guid.Empty)
                    systemPromptText = systemPromptWithAugmentedData;
                else
                {
                    var profile = settingsManager.GetProfileById(this.SelectedProfile);
                    if(profile == Profile.NO_PROFILE)
                        systemPromptText = systemPromptWithAugmentedData;
                    else
                    {
                        profileNote = $"Using profile '{profile.Name}' for chat thread '{this.Name}'.";
                        systemPromptText = $"""
                                            {systemPromptWithAugmentedData}

                                            {profile.ToSystemPrompt()}
                                            """;
                    }
                }
            }
        }

        var toolPolicy = ToolSelectionRules.BuildToolPolicyPrompt(runnableToolDefinitions ?? []);
        if (!string.IsNullOrWhiteSpace(toolPolicy))
        {
            systemPromptText = $"""
                                {systemPromptText}

                                {toolPolicy}
                                """;
        }

        var explanation = $"{logMessage} {profileNote}";
        if(!this.IncludeDateTime)
            return new(systemPromptText, systemPromptTextWithChatTemplate, allowProfile, explanation);

        //
        // Prepend the current date and time to the system prompt:
        //
        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now;
        var currentDateTime = string.Create(
            new CultureInfo("en-US"),
            $"Today is {nowUtc:dddd, MMMM d, yyyy h:mm tt} (UTC) and {nowLocal:dddd, MMMM d, yyyy h:mm tt} (local time)."
        );

        var withDateTime = $"""
                            {currentDateTime}

                            {systemPromptText}
                            """;

        return new(withDateTime, systemPromptTextWithChatTemplate, allowProfile, explanation);
    }

    /// <summary>
    /// Removes a content block from this chat thread.
    /// </summary>
    /// <param name="content">The content block to remove.</param>
    /// <param name="removeForRegenerate">Indicates whether the content block is removed for
    /// regeneration purposes. True, when the content block is removed for regeneration purposes,
    /// which will not remove the previous user block if it is hidden from the user.</param>
    public void Remove(IContent content, bool removeForRegenerate = false)
    {
        var block = this.Blocks.FirstOrDefault(x => x.Content == content);
        if(block is null)
            return;

        //
        // Remove the previous user block if it is hidden from the user. Otherwise,
        // the experience might be confusing for the user.
        //
        // Explanation, using the ERI assistant as an example:
        // - The ERI assistant generates for every file a hidden user prompt.
        // - In the UI, the user can only see the AI's responses, not the hidden user prompts.
        // - Now, the user removes one AI response
        // - The hidden user prompt is still there, but the user can't see it.
        // - Since the user prompt is hidden, neither is it possible to remove nor edit it.
        // - This method solves this issue by removing the hidden user prompt when the AI response is removed.
        //
        if (block.Role is ChatRole.AI && !removeForRegenerate)
        {
            var sortedBlocks = this.Blocks.OrderBy(x => x.Time).ToList();
            var index = sortedBlocks.IndexOf(block);
            if (index > 0)
            {
                var previousBlock = sortedBlocks[index - 1];
                if (previousBlock.Role is ChatRole.USER && previousBlock.HideFromUser)
                {
                    DeleteManagedAttachments(previousBlock);
                    this.Blocks.Remove(previousBlock);
                }
            }
        }

        DeleteManagedAttachments(block);

        // Remove the block from the chat thread:
        this.Blocks.Remove(block);
    }

    /// <summary>
    /// Rolls this chat thread back, so that the given content becomes the last block of the conversation.
    /// </summary>
    /// <remarks>
    /// Every later block is removed in conversation order, which is the order of the time stamps and
    /// the order in which the chat shows the blocks. That includes the blocks hidden from the user,
    /// such as the prompts an assistant sends into a chat: the user can neither see nor remove them,
    /// so leaving them behind would continue the chat with messages nobody knows about. Hidden blocks
    /// before the content stay, e.g. the example conversation of a chat template. The managed
    /// transcripts of the removed blocks are deleted with them.<br/><br/>
    ///
    /// The augmented data and the AI-selected data sources are reset, too. Both describe the last
    /// retrieval, not a certain message, so after a rollback nobody knows whether they belong to a
    /// kept or to a removed one. The next message with active data sources retrieves anew; without
    /// active data sources, the chat continues without this context. The data source options stay,
    /// because they are the user's choice rather than the result of a message.<br/><br/>
    ///
    /// What stays as well is everything the thread ratchets for security reasons, namely the data
    /// security and the required provider confidence. Both only ever tighten, because the data which
    /// raised them was seen by this thread. Removing the message that brought it in does not unsee
    /// it, so the chat keeps demanding the same of every provider which continues it.
    /// </remarks>
    /// <param name="content">The content to keep as the last block.</param>
    /// <returns>True when one or more later blocks were removed. False when the content is unknown or already the last block; the thread stays unchanged then.</returns>
    public bool RollBackTo(IContent content)
    {
        var sortedBlocks = this.Blocks.OrderBy(x => x.Time).ToList();
        var blockIndex = sortedBlocks.FindIndex(block => ReferenceEquals(block.Content, content));
        if (blockIndex < 0 || blockIndex == sortedBlocks.Count - 1)
            return false;

        foreach (var block in sortedBlocks.Skip(blockIndex + 1))
        {
            DeleteManagedAttachments(block);
            this.Blocks.Remove(block);
        }

        this.AugmentedData = string.Empty;
        this.AISelectedDataSources = [];
        return true;
    }

    /// <summary>
    /// Finds what a provider reported for this conversation, as long as the report still describes it.
    /// </summary>
    /// <remarks>
    /// A report describes one request: the conversation up to the answer which carries it. It is
    /// worth something only while the thread still is that conversation, so only the last block is
    /// asked, and no earlier answer ever stands in for it. Whatever came after an older report -- a
    /// message whose request was turned down, an answer which is still being written, an answer
    /// without a report of its own -- is missing from that report's number, and the estimate is
    /// closer to the truth than a figure which leaves it out.<br/><br/>
    ///
    /// A report also stops counting when the thread holds a different number of blocks than the
    /// request did, which means an earlier message was deleted, and when the next request goes to
    /// another model, which counts the same conversation with another tokenizer. Editing the last
    /// message or rolling the chat back makes an earlier answer the last block again, with exactly
    /// the blocks it was reported for, so its report counts once more.<br/><br/>
    ///
    /// What goes unnoticed is a change beside the messages: another system prompt, profile, or
    /// selection of tools. That shows only with the next answer. Noticing it would take a
    /// fingerprint of the system prompt as it was sent, after the data sources added to it, which
    /// is a lot of machinery for a number which corrects itself one answer later.
    /// </remarks>
    /// <param name="model">The model the next request would go to.</param>
    /// <returns>
    /// What the provider reported, together with the answer which followed it, or
    /// ReportedHistory.UNKNOWN when no report describes this conversation.
    /// </returns>
    public ReportedHistory ReportedHistoryFor(Model model)
    {
        if (this.Blocks.Count is 0)
            return ReportedHistory.UNKNOWN;

        if (this.Blocks[^1].Content is not ContentText { IsStreaming: false, ReportedUsage: { } reported } answer)
            return ReportedHistory.UNKNOWN;

        if (reported.BlockCount != this.Blocks.Count)
            return ReportedHistory.UNKNOWN;

        if (!string.Equals(reported.ModelId, model.Id, StringComparison.Ordinal))
            return ReportedHistory.UNKNOWN;

        return ReportedHistory.Of(reported.ToTokenUsage(), answer.Text);
    }

    private static void DeleteManagedAttachments(ContentBlock block)
    {
        if (block.Content is not ContentText textContent)
            return;
            
        foreach (var attachment in textContent.FileAttachments)
            ManagedTranscriptAttachment.TryDeleteOwnedFile(attachment);
    }

    /// <summary>
    /// Transforms this chat thread to an ERI chat thread.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The ERI chat thread.</returns>
    public async Task<Tools.ERIClient.DataModel.ChatThread> ToERIChatThread(CancellationToken token = default)
    {
        //
        // Transform the content blocks:
        //
        var contentBlocks = new List<Tools.ERIClient.DataModel.ContentBlock>(this.Blocks.Count);
        foreach (var block in this.Blocks)
        {
            var (contentData, contentType) = block.Content switch
            {
                ContentImage image => (await image.TryAsBase64(token) is (success: true, { } base64Image) ? base64Image : string.Empty, Tools.ERIClient.DataModel.ContentType.IMAGE),
                ContentText text => (text.Text, Tools.ERIClient.DataModel.ContentType.TEXT),
                
                _ => (string.Empty, Tools.ERIClient.DataModel.ContentType.UNKNOWN),
            };
            
            contentBlocks.Add(new Tools.ERIClient.DataModel.ContentBlock
            {
                Role = block.Role switch
                {
                    ChatRole.AI => Role.AI,
                    ChatRole.USER => Role.USER,
                    ChatRole.AGENT => Role.AGENT,
                    ChatRole.SYSTEM => Role.SYSTEM,
                    ChatRole.NONE => Role.NONE,
                    
                    _ => Role.UNKNOWN,
                },
                
                Content = contentData,
                Type = contentType,
            });
        }
        
        return new Tools.ERIClient.DataModel.ChatThread { ContentBlocks = contentBlocks };
    }
}
