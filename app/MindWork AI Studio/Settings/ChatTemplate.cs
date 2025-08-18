using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

public record ChatTemplate(uint Num, string Id, string Name, string SystemPrompt, string PredefinedUserPrompt, List<ContentBlock> ExampleConversation, bool AllowProfileUsage, bool IsEnterpriseConfiguration = false, Guid EnterpriseConfigurationPluginId = default)
{
    public ChatTemplate() : this(0, Guid.Empty.ToString(), string.Empty, string.Empty, string.Empty, [], false)
    {
    }
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatTemplate).Namespace, nameof(ChatTemplate));
    
    public static readonly ChatTemplate NO_CHAT_TEMPLATE = new()
    {
        Name = TB("Use no chat template"),
        SystemPrompt = string.Empty,
        PredefinedUserPrompt = string.Empty,
        Id = Guid.Empty.ToString(),
        Num = uint.MaxValue,
        ExampleConversation = [],
        AllowProfileUsage = true,
        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
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