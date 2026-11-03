using AIStudio.Settings;

namespace AIStudio.Tools.Services;

/// <summary>
/// The chat template a chat started by an assistant chat launcher begins with.
/// </summary>
/// <param name="ChatTemplate">The chat template of the chat, or null when the template the launcher names does not exist.</param>
/// <param name="ErrorMessage">Why no chat template could be chosen, or an empty string when one was.</param>
internal readonly record struct ChatLauncherTemplateResult(ChatTemplate? ChatTemplate, string ErrorMessage);