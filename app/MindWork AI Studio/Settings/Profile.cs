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
        Name = TB("Use no profile"), // Cannot be localized due to being a static readonly field
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
    
    /// <summary>
    /// Gets the name of this profile. If it is the NO_PROFILE, it returns a localized string.
    /// </summary>
    /// <remarks>
    /// Why not using the Name property directly? Because the Name property of NO_PROFILE cannot be
    /// localized because it is a static readonly field. So we need this method to return a localized
    /// string instead.
    /// </remarks>
    /// <returns>The name of this profile.</returns>
    public string GetSafeName()
    {
        if(this == NO_PROFILE)
            return TB("Use no profile");
        
        return this.Name;
    }
    
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
            Num = 0, // will be set later by the PluginConfigurationObject
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