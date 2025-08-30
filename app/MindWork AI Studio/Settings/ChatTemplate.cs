using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Settings;

public record ChatTemplate(
    uint Num,
    string Id,
    string Name,
    string SystemPrompt,
    string PredefinedUserPrompt,
    List<ContentBlock> ExampleConversation,
    bool AllowProfileUsage,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default) : ConfigurationBaseObject
{
    public ChatTemplate() : this(0, Guid.Empty.ToString(), string.Empty, string.Empty, string.Empty, [], false)
    {
    }
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatTemplate).Namespace, nameof(ChatTemplate));
    
    private static readonly ILogger<ChatTemplate> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ChatTemplate>();
    
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
    
    public static bool TryParseChatTemplateTable(int idx, LuaTable table, Guid configPluginId, out ConfigurationBaseObject template)
    {
        template = NO_CHAT_TEMPLATE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }
        
        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid name.");
            return false;
        }
        
        if (!table.TryGetValue("SystemPrompt", out var sysPromptValue) || !sysPromptValue.TryRead<string>(out var systemPrompt))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid system prompt.");
            return false;
        }
        
        var predefinedUserPrompt = string.Empty;
        if (table.TryGetValue("PredefinedUserPrompt", out var preUserValue) && preUserValue.TryRead<string>(out var preUser))
            predefinedUserPrompt = preUser;
        
        var allowProfileUsage = false;
        if (table.TryGetValue("AllowProfileUsage", out var allowProfileValue) && allowProfileValue.TryRead<bool>(out var allow))
            allowProfileUsage = allow;
        
        template = new ChatTemplate
        {
            Num = 0,
            Id = id.ToString(),
            Name = name,
            SystemPrompt = systemPrompt,
            PredefinedUserPrompt = predefinedUserPrompt,
            ExampleConversation = ParseExampleConversation(idx, table),
            AllowProfileUsage = allowProfileUsage,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };
        
        return true;
    }
    
    private static List<ContentBlock> ParseExampleConversation(int idx, LuaTable table)
    {
        var exampleConversation = new List<ContentBlock>();
        if (!table.TryGetValue("ExampleConversation", out var exConvValue) || !exConvValue.TryRead<LuaTable>(out var exConvTable))
            return exampleConversation;
        
        var numBlocks = exConvTable.ArrayLength;
        for (var j = 1; j <= numBlocks; j++)
        {
            var blockValue = exConvTable[j];
            if (!blockValue.TryRead<LuaTable>(out var blockTable))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} is not a valid table.");
                continue;
            }
            
            if (!blockTable.TryGetValue("Role", out var roleValue) || !roleValue.TryRead<string>(out var roleText) || !Enum.TryParse<ChatRole>(roleText, true, out var parsedRole))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} does not contain a valid role.");
                continue;
            }

            if (!blockTable.TryGetValue("Content", out var contentValue) || !contentValue.TryRead<string>(out var content))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} does not contain a valid content message.");
                continue;
            }
                
            if (string.IsNullOrWhiteSpace(content))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} contains an empty content message.");
                continue;
            }
                
            exampleConversation.Add(new ContentBlock
            {
                Time = DateTimeOffset.UtcNow,
                Role = parsedRole,
                Content = new ContentText { Text = content },
                ContentType = ContentType.TEXT,
                HideFromUser = true,
            });
        }

        return exampleConversation;
    }
}