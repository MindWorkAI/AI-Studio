using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Services;

// ReSharper disable MemberCanBePrivate.Global

namespace AIStudio.Agents;

public abstract class AgentBase(ILogger<AgentBase> logger, SettingsManager settingsManager, DataSourceService dataSourceService, ThreadSafeRandom rng) : IAgent
{
    protected static readonly ContentBlock EMPTY_BLOCK = new()
    {
        Content = null,
        ContentType = ContentType.NONE,
        Role = ChatRole.AGENT,
        Time = DateTimeOffset.UtcNow,
    };
    
    protected static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    protected DataSourceService DataSourceService { get; init; } = dataSourceService;
    
    protected SettingsManager SettingsManager { get; init; } = settingsManager;

    protected ThreadSafeRandom RNG { get; init; } = rng;
    
    protected ILogger<AgentBase> Logger { get; init; } = logger;
    
    /// <summary>
    /// Represents the type or category of this agent.
    /// </summary>
    protected abstract Type Type { get; }
    
    /// <summary>
    /// The name of the agent.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// The agent's job description. Will be used for the system prompt.
    /// </summary>
    protected abstract string JobDescription { get; }

    /// <summary>
    /// Represents the system prompt provided for the agent.
    /// </summary>
    protected abstract string SystemPrompt(string additionalData);
    
    #region Implementation of IAgent
    
    public abstract AIStudio.Settings.Provider? ProviderSettings { get; set; }
    
    public abstract Task<ChatThread> ProcessContext(ChatThread chatThread, IDictionary<string, string> additionalData);
    
    public abstract Task<ContentBlock> ProcessInput(ContentBlock input, IDictionary<string, string> additionalData);
    
    public abstract Task<bool> MadeDecision(ContentBlock input);
    
    public abstract IReadOnlyCollection<ContentBlock> GetContext();
    
    public abstract IReadOnlyCollection<ContentBlock> GetAnswers();
    
    #endregion
    
    protected ChatThread CreateChatThread(string systemPrompt) => new()
    {
        WorkspaceId = Guid.Empty,
        ChatId = Guid.NewGuid(),
        Name = string.Empty,
        Seed = this.RNG.Next(),
        SystemPrompt = systemPrompt,
        Blocks = [],
    };

    protected UserRequest AddUserRequest(ChatThread thread, string request)
    {
        var time = DateTimeOffset.Now;
        var lastUserPrompt = new ContentText
        {
            Text = request,
        };
        
        thread.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = lastUserPrompt,
        });

        return new()
        {
            Time = time,
            UserPrompt = lastUserPrompt,
        };
    }
    
    protected async Task AddAIResponseAsync(ChatThread thread, IContent lastUserPrompt, DateTimeOffset time)
    {
        if(this.ProviderSettings is null)
            return;
        
        var providerSettings = this.ProviderSettings.Value;
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };

        var resultingContentBlock = new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
        };
        
        thread.Blocks.Add(resultingContentBlock);
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(providerSettings.CreateProvider(this.Logger), providerSettings.Model, lastUserPrompt, thread);
    }
}