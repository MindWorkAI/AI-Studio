using AIStudio.Components;

namespace AIStudio.Assistants;

public abstract class AssistantLowerBase : MSGComponentBase
{
    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    internal const string RESULT_DIV_ID = "assistantResult";
    internal const string BEFORE_RESULT_DIV_ID = "beforeAssistantResult";
    internal const string AFTER_RESULT_DIV_ID = "afterAssistantResult";
}