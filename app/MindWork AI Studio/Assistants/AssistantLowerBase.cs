using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants;

public abstract class AssistantLowerBase : MSGComponentBase
{
    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    internal const string RESULT_DIV_ID = "assistantResult";
    internal const string BEFORE_RESULT_DIV_ID = "beforeAssistantResult";
    internal const string AFTER_RESULT_DIV_ID = "afterAssistantResult";

    /// <summary>
    /// The minimum time between two renderings of the assistant while an answer streams in.
    /// </summary>
    /// <remarks>
    /// The stream waits for each rendering, and an assistant renders its whole form, which may hold
    /// more than a hundred fields. Ten renderings a second still read as a live stream.
    /// </remarks>
    private protected static readonly TimeSpan STREAMING_RENDER_MIN_TIME = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The minimum time between two checkpoints of the assistant session while an answer streams in,
    /// as in the chat.
    /// </summary>
    private protected static readonly TimeSpan STREAMING_CHECKPOINT_MIN_TIME = TimeSpan.FromSeconds(3);

    protected static readonly AssistantSessionStateKey<AIStudio.Settings.Provider> PROVIDER_SETTINGS_STATE_KEY = new(nameof(ProviderSettings));
    protected static readonly AssistantSessionStateKey<bool> INPUT_IS_VALID_STATE_KEY = new(nameof(InputIsValid));
    protected static readonly AssistantSessionStateKey<Profile> CURRENT_PROFILE_STATE_KEY = new(nameof(CurrentProfile));
    protected static readonly AssistantSessionStateKey<ChatTemplate> CURRENT_CHAT_TEMPLATE_STATE_KEY = new(nameof(CurrentChatTemplate));
    protected static readonly AssistantSessionStateKey<ChatThread?> CHAT_THREAD_STATE_KEY = new(nameof(ChatThread));
    protected static readonly AssistantSessionStateKey<IContent?> LAST_USER_PROMPT_STATE_KEY = new(nameof(LastUserPrompt));
    protected static readonly AssistantSessionStateKey<ContentBlock?> RESULTING_CONTENT_BLOCK_STATE_KEY = new(nameof(ResultingContentBlock));
    protected static readonly AssistantSessionStateKey<string[]> INPUT_ISSUES_STATE_KEY = new(nameof(InputIssues));
    protected static readonly AssistantSessionStateKey<bool> IS_PROCESSING_STATE_KEY = new(nameof(IsProcessing));
    protected static readonly AssistantSessionStateKey<HashSet<string>> SELECTED_TOOL_IDS_STATE_KEY = new("SelectedToolIds");
    
    protected AIStudio.Settings.Provider ProviderSettings = Settings.Provider.NONE;
    protected bool InputIsValid;
    protected Profile CurrentProfile = Profile.NO_PROFILE;
    protected ChatTemplate CurrentChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE;
    protected ChatThread? ChatThread;
    protected IContent? LastUserPrompt;
    
    protected ContentBlock? ResultingContentBlock;
    protected string[] InputIssues = [];
    protected bool IsProcessing;

    /// <summary>
    /// Clears everything one assistant run has produced.
    /// </summary>
    /// <remarks>
    /// Assistants call this whenever the previous run must not carry over: a follow-up run would
    /// otherwise append to the old chat thread, and the old result would stay on screen.
    /// </remarks>
    protected void ClearConversationState()
    {
        this.ChatThread = null;
        this.LastUserPrompt = null;
        this.ResultingContentBlock = null;
    }
}