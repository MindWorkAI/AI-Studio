using AIStudio.Settings;

namespace AIStudio.Tools.Services;

/// <summary>
/// The profile a chat started by an assistant chat launcher begins with.
/// </summary>
/// <param name="Profile">The profile of the chat, or null when the profile the launcher names does not exist.</param>
/// <param name="ErrorMessage">Why no profile could be chosen, or an empty string when one was.</param>
internal readonly record struct ChatLauncherProfileResult(Profile? Profile, string ErrorMessage);