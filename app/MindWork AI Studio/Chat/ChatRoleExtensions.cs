using AIStudio.Tools.PluginSystem;

namespace AIStudio.Chat;

public static class ChatRoleExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatRoleExtensions).Namespace, nameof(ChatRoleExtensions));
    
    /// <param name="role">The role.</param>
    extension(ChatRole role)
    {
        /// <summary>
        /// Returns the name of the role.
        /// </summary>
        /// <value>The name of the role.</value>
        public string Name => role switch
        {
            ChatRole.SYSTEM => TB("System"),
            ChatRole.USER => TB("You"),
            ChatRole.AI => TB("AI"),
        
            _ => TB("Unknown"),
        };

        /// <summary>
        /// Returns the color of the role.
        /// </summary>
        /// <value>The color of the role.</value>
        public Color Color => role switch
        {
            ChatRole.SYSTEM => Color.Info,
            ChatRole.USER => Color.Primary,
            ChatRole.AI => Color.Tertiary,
        
            _ => Color.Error,
        };

        /// <summary>
        /// Returns the icon of the role.
        /// </summary>
        /// <value>The icon of the role.</value>
        public string Icon => role switch
        {
            ChatRole.SYSTEM => Icons.Material.Filled.Settings,
            ChatRole.USER => Icons.Material.Filled.Person,
            ChatRole.AI => Icons.Material.Filled.AutoAwesome,
        
            _ => Icons.Material.Filled.Help,
        };

        /// <summary>
        /// Returns the specific name of the role for the chat template.
        /// </summary>
        /// <value>The name of the role.</value>
        public string ChatTemplateName => role switch
        {
            ChatRole.SYSTEM => TB("System"),
            ChatRole.USER => TB("User"),
            ChatRole.AI => TB("Assistant"),

            _ => TB("Unknown"),
        };

        /// <summary>
        /// Selects the next role for a chat template.
        /// </summary>
        /// <returns>The next role for the chat template.</returns>
        public ChatRole SelectNextRoleForTemplate() => role switch
        {
            ChatRole.USER => ChatRole.AI,
            _ => ChatRole.USER,
        };
    }
}