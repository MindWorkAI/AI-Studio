namespace AIStudio.Tools.Services;

/// <summary>
/// The plugin and the requested change, as the prompt for a revision hands them to the model.
/// </summary>
/// <param name="PluginId">The ID of the plugin.</param>
/// <param name="PluginName">The name of the plugin.</param>
/// <param name="AssistantTitle">The title of the assistant.</param>
/// <param name="ChangeRequest">The change the user requested.</param>
/// <param name="TestContext">What the user observed while testing, or null when the user left it empty.</param>
internal sealed record AssistantBuilderRevisionPromptData(Guid PluginId, string PluginName, string AssistantTitle, string ChangeRequest, string? TestContext);