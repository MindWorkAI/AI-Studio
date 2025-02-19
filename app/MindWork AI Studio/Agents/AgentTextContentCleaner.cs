using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Services;

namespace AIStudio.Agents;

public sealed class AgentTextContentCleaner(ILogger<AgentBase> logger, SettingsManager settingsManager, DataSourceService dataSourceService, ThreadSafeRandom rng) : AgentBase(logger, settingsManager, dataSourceService, rng)
{
    private readonly List<ContentBlock> context = new();
    private readonly List<ContentBlock> answers = new();
    
    #region Overrides of AgentBase

    public override AIStudio.Settings.Provider? ProviderSettings { get; set; }

    protected override Type Type => Type.SYSTEM;

    public override string Id => "Text Content Cleaner";

    protected override string JobDescription => 
        """
        You receive a Markdown document as input. Your goal is to identify the main content of the document
        and return it including Markdown formatting. Remove areas that do not belong to the main part of the
        document. For a blog article, return only the text of the article with its formatting. For a scientific
        paper, only the contents of the paper. Delete elements of navigation, advertisements, HTML artifacts,
        cookie banners, etc. If the content contains images, these images remain. The same applies to links.
        Ensure that links and images are present as valid Markdown:
        
        - Syntax of links: [link text](URL)
        - Syntax of images: ![alt text](URL)
        
        If you find relative links or images with relative paths, correct them to absolute paths. For this
        purpose, here is the source URL:
        """;

    protected override string SystemPrompt(string additionalData) => $"{this.JobDescription} `{additionalData}`.";
    
    /// <inheritdoc />
    public override async Task<ChatThread> ProcessContext(ChatThread chatThread, IDictionary<string, string> additionalData)
    {
        // We process the last block of the chat thread. Then, we add the result
        // to the chat thread as the last block:
        var answer = await this.ProcessInput(chatThread.Blocks[^1], additionalData);
        chatThread.Blocks.Add(answer);
        
        this.context.Clear();
        this.context.AddRange(chatThread.Blocks);
        
        return chatThread;
    }

    // <inheritdoc />
    public override async Task<ContentBlock> ProcessInput(ContentBlock input, IDictionary<string, string> additionalData)
    {
        if (input.Content is not ContentText text)
            return EMPTY_BLOCK;
        
        if(text.InitialRemoteWait || text.IsStreaming)
            return EMPTY_BLOCK;
        
        if(string.IsNullOrWhiteSpace(text.Text))
            return EMPTY_BLOCK;
        
        if(!additionalData.TryGetValue("sourceURL", out var sourceURL) || string.IsNullOrWhiteSpace(sourceURL))
            return EMPTY_BLOCK;
        
        var thread = this.CreateChatThread(this.SystemPrompt(sourceURL));
        var userRequest = this.AddUserRequest(thread, text.Text);
        await this.AddAIResponseAsync(thread, userRequest.UserPrompt, userRequest.Time);
        
        var answer = thread.Blocks[^1];
        this.answers.Add(answer);
        return answer;
    }

    // <inheritdoc />
    public override Task<bool> MadeDecision(ContentBlock input) => Task.FromResult(true);

    // <inheritdoc />
    public override IReadOnlyCollection<ContentBlock> GetContext() => this.context;

    // <inheritdoc />
    public override IReadOnlyCollection<ContentBlock> GetAnswers() => this.answers;

    #endregion
}