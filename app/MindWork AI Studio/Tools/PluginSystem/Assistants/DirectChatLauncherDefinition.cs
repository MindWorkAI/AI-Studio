namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// Everything a user may change about an installed direct chat launcher.
/// </summary>
/// <param name="PluginName">The plugin name, shown on the plugins page.</param>
/// <param name="Title">The assistant title, shown on the tile.</param>
/// <param name="Description">The description, used for both the plugin and the assistant.</param>
/// <param name="Launch">The workspace and the chat settings the tile starts its chat with.</param>
public sealed record DirectChatLauncherDefinition(string PluginName, string Title, string Description, AssistantChatLaunchConfiguration Launch);