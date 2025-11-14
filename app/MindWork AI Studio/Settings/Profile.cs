using AIStudio.Tools.PluginSystem;
using Lua;

namespace AIStudio.Settings;

public record Profile(
    uint Num, 
    string Id, 
    string Name, 
    string NeedToKnow, 
    string Actions,
    bool IsEnterpriseConfiguration = false, 
    Guid EnterpriseConfigurationPluginId = default): ConfigurationBaseObject
{
    public Profile() : this(0, Guid.Empty.ToString(), string.Empty, string.Empty, string.Empty)
    {
    }
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(Profile).Namespace, nameof(Profile));
    
    private static readonly ILogger<Profile> LOGGER = Program.LOGGER_FACTORY.CreateLogger<Profile>();
    
    public static readonly Profile NO_PROFILE = new()
    {
        Name = TB("Use no profile"),
        NeedToKnow = string.Empty,
        Actions = string.Empty,
        Id = Guid.Empty.ToString(),
        Num = uint.MaxValue,
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
        
        var needToKnow =  
            $"""
             What should you know about the user?

             ```
             {this.NeedToKnow}
             ```
             """;
        
        var actions = 
            $"""
             The user wants you to consider the following things.

             ```
             {this.Actions}
             ```
             """;

        if (string.IsNullOrWhiteSpace(this.NeedToKnow))
            return actions;

        if (string.IsNullOrWhiteSpace(this.Actions))
            return needToKnow;
        
        return $"""
                {needToKnow}

                {actions}
                """;
    }
    
    public static bool TryParseProfileTable(int idx, LuaTable table, Guid configPluginId, out ConfigurationBaseObject template)
    {
        LOGGER.LogInformation($"\n Profile table parsing {idx}.\n");
        template = NO_PROFILE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured profile {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }
        
        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name))
        {
            LOGGER.LogWarning($"The configured profile {idx} does not contain a valid name.");
            return false;
        }
        
        if (!table.TryGetValue("NeedToKnow", out var needToKnowValue) || !needToKnowValue.TryRead<string>(out var needToKnow))
        {
            LOGGER.LogWarning($"The configured profile {idx} does not contain valid NeedToKnow data.");
            return false;
        }
        
        if (!table.TryGetValue("Actions", out var actionsValue) || !actionsValue.TryRead<string>(out var actions))
        {
            LOGGER.LogWarning($"The configured profile {idx} does not contain valid actions data.");
            return false;
        }
        
        template = new Profile
        {
            Num = 0,
            Id = id.ToString(),
            Name = name,
            NeedToKnow = needToKnow,
            Actions = actions,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };
        
        return true;
    }
}