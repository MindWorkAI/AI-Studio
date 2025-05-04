using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

public readonly record struct Profile(uint Num, string Id, string Name, string NeedToKnow, string Actions)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(Profile).Namespace, nameof(Profile));
    
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
}