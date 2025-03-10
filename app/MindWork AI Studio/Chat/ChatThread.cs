using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Chat;

/// <summary>
/// Data structure for a chat thread.
/// </summary>
public sealed record ChatThread
{
    /// <summary>
    /// The unique identifier of the chat thread.
    /// </summary>
    public Guid ChatId { get; init; }
    
    /// <summary>
    /// The unique identifier of the workspace.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Specifies the provider selected for the chat thread.
    /// </summary>
    public string SelectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Specifies the profile selected for the chat thread.
    /// </summary>
    public string SelectedProfile { get; set; } = string.Empty;

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
    /// The name of the chat thread. Usually generated by an AI model or manually edited by the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The seed for the chat thread. Some providers use this to generate deterministic results.
    /// </summary>
    public int Seed { get; init; }
    
    /// <summary>
    /// The current system prompt for the chat thread.
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// The content blocks of the chat thread.
    /// </summary>
    public List<ContentBlock> Blocks { get; init; } = [];

    /// <summary>
    /// Prepares the system prompt for the chat thread.
    /// </summary>
    /// <remarks>
    /// The actual system prompt depends on the selected profile. If no profile is selected,
    /// the system prompt is returned as is. When a profile is selected, the system prompt
    /// is extended with the profile chosen.
    /// </remarks>
    /// <param name="settingsManager">The settings manager instance to use.</param>
    /// <param name="chatThread">The chat thread to prepare the system prompt for.</param>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>The prepared system prompt.</returns>
    public string PrepareSystemPrompt(SettingsManager settingsManager, ChatThread chatThread, ILogger logger)
    {
        var isAugmentedDataAvailable = !string.IsNullOrWhiteSpace(chatThread.AugmentedData);
        var systemPromptWithAugmentedData = isAugmentedDataAvailable switch
        {
            true => $"""
                     {chatThread.SystemPrompt}
                     
                     {chatThread.AugmentedData}
                     """,

            false => chatThread.SystemPrompt,
        };
        
        if(isAugmentedDataAvailable)
            logger.LogInformation("Augmented data is available for the chat thread.");
        else
            logger.LogInformation("No augmented data is available for the chat thread.");
        
        //
        // Prepare the system prompt:
        //
        string systemPromptText;
        var logMessage = $"Using no profile for chat thread '{chatThread.Name}'.";
        if (string.IsNullOrWhiteSpace(chatThread.SelectedProfile))
            systemPromptText = systemPromptWithAugmentedData;
        else
        {
            if(!Guid.TryParse(chatThread.SelectedProfile, out var profileId))
                systemPromptText = systemPromptWithAugmentedData;
            else
            {
                if(chatThread.SelectedProfile == Profile.NO_PROFILE.Id || profileId == Guid.Empty)
                    systemPromptText = systemPromptWithAugmentedData;
                else
                {
                    var profile = settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == chatThread.SelectedProfile);
                    if(profile == default)
                        systemPromptText = systemPromptWithAugmentedData;
                    else
                    {
                        logMessage = $"Using profile '{profile.Name}' for chat thread '{chatThread.Name}'.";
                        systemPromptText = $"""
                                            {systemPromptWithAugmentedData}

                                            {profile.ToSystemPrompt()}
                                            """;
                    }
                }
            }
        }
        
        logger.LogInformation(logMessage);
        return systemPromptText;
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
                    this.Blocks.Remove(previousBlock);
            }
        }

        // Remove the block from the chat thread:
        this.Blocks.Remove(block);
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
                ContentImage image => (await image.AsBase64(token), Tools.ERIClient.DataModel.ContentType.IMAGE),
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
                    
                    _ => Role.UNKNOW,
                },
                
                Content = contentData,
                Type = contentType,
            });
        }
        
        return new Tools.ERIClient.DataModel.ChatThread { ContentBlocks = contentBlocks };
    }
}