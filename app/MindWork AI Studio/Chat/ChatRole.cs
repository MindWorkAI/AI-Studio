using AIStudio.Tools.PluginSystem;

namespace AIStudio.Chat;

/// <summary>
/// Possible roles in the chat.
/// </summary>
public enum ChatRole
{
    NONE,
    UNKNOWN,
    
    SYSTEM,
    USER,
    AI,
    AGENT,
}

/// <summary>
/// Extensions for the ChatRole enum.
/// </summary>
public static class ExtensionsChatRole
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatRole).Namespace, nameof(ChatRole));
    
    /// <summary>
    /// Returns the name of the role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The name of the role.</returns>
    public static string ToName(this ChatRole role) => role switch
    {
        ChatRole.SYSTEM => TB("System"),
        ChatRole.USER => TB("You"),
        ChatRole.AI => TB("AI"),
        
        _ => TB("Unknown"),
    };
    
    /// <summary>
    /// Returns the color of the role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The color of the role.</returns>
    public static Color ToColor(this ChatRole role) => role switch
    {
        ChatRole.SYSTEM => Color.Info,
        ChatRole.USER => Color.Primary,
        ChatRole.AI => Color.Tertiary,
        
        _ => Color.Error,
    };
    
    /// <summary>
    /// Returns the icon of the role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The icon of the role.</returns>
    public static string ToIcon(this ChatRole role) => role switch
    {
        ChatRole.SYSTEM => Icons.Material.Filled.Settings,
        ChatRole.USER => Icons.Material.Filled.Person,
        ChatRole.AI => Icons.Material.Filled.AutoAwesome,
        
        _ => Icons.Material.Filled.Help,
    };
    
    /// <summary>
    /// Returns the specific name of the role for the chat template.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The name of the role.</returns>
    public static string ToChatTemplateName(this ChatRole role) => role switch
    {
        ChatRole.SYSTEM => TB("System"),
        ChatRole.USER => TB("User"),
        ChatRole.AI => TB("Assistant"),
        
        _ => TB("Unknown"),
    };
}