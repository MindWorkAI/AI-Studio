using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Tools.Services;

/// <summary>
/// The provider a chat started by an assistant chat launcher begins with.
/// </summary>
/// <param name="Provider">The provider of the chat, or NONE when there is none to start with.</param>
/// <param name="IsExplicit">Whether the chat template or the launcher named this provider, as opposed to the chat defaults.</param>
/// <param name="ErrorMessage">Why the named provider cannot be used, or an empty string when it can.</param>
internal readonly record struct ChatLauncherProviderResult(ProviderSettings Provider, bool IsExplicit, string ErrorMessage);