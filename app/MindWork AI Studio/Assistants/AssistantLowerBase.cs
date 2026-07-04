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

    protected static readonly AssistantSessionStateKey<AIStudio.Settings.Provider> PROVIDER_SETTINGS_STATE_KEY = new(nameof(ProviderSettings));
    protected static readonly AssistantSessionStateKey<bool> INPUT_IS_VALID_STATE_KEY = new(nameof(InputIsValid));
    protected static readonly AssistantSessionStateKey<Profile> CURRENT_PROFILE_STATE_KEY = new(nameof(CurrentProfile));
    protected static readonly AssistantSessionStateKey<ChatTemplate> CURRENT_CHAT_TEMPLATE_STATE_KEY = new(nameof(CurrentChatTemplate));
    protected static readonly AssistantSessionStateKey<ChatThread?> CHAT_THREAD_STATE_KEY = new(nameof(ChatThread));
    protected static readonly AssistantSessionStateKey<IContent?> LAST_USER_PROMPT_STATE_KEY = new(nameof(LastUserPrompt));
    protected static readonly AssistantSessionStateKey<ContentBlock?> RESULTING_CONTENT_BLOCK_STATE_KEY = new(nameof(ResultingContentBlock));
    protected static readonly AssistantSessionStateKey<string[]> INPUT_ISSUES_STATE_KEY = new(nameof(InputIssues));
    protected static readonly AssistantSessionStateKey<bool> IS_PROCESSING_STATE_KEY = new(nameof(IsProcessing));

    protected AIStudio.Settings.Provider ProviderSettings = Settings.Provider.NONE;
    protected bool InputIsValid;
    protected Profile CurrentProfile = Profile.NO_PROFILE;
    protected ChatTemplate CurrentChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE;
    protected ChatThread? ChatThread;
    protected IContent? LastUserPrompt;

    protected ContentBlock? ResultingContentBlock;
    protected string[] InputIssues = [];
    protected bool IsProcessing;
}
