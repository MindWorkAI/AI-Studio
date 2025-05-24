using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

public readonly record struct ChatTemplate(uint Num, string Id, string Name, string SystemPrompt, List<ContentBlock> ExampleConversation, bool AllowProfileUsage)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatTemplate).Namespace, nameof(ChatTemplate));
    
    public static readonly ChatTemplate NO_CHAT_TEMPLATE = new()
    {
        Name = TB("Use no chat template"),
        SystemPrompt = string.Empty,
        Id = Guid.Empty.ToString(),
        Num = uint.MaxValue,
        ExampleConversation = [],
        AllowProfileUsage = true,
    };
    
    #region Overrides of ValueType

    /// <summary>
    /// Returns a string that represents the profile in a human-readable format.
    /// </summary>
    /// <returns>A string that represents the profile in a human-readable format.</returns>
    public override string ToString() => this.Name;

    #endregion
    
    public string ToSystemPrompt()
    {
        if(this.Num == uint.MaxValue)
            return string.Empty;
        
        return this.SystemPrompt;
    }
}