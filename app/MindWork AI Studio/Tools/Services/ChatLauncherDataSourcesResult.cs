using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services;

/// <summary>
/// The data source options a chat started by an assistant chat launcher begins with.
/// </summary>
/// <param name="Options">The checked options of the chat, or null when the chosen data sources cannot be used.</param>
/// <param name="ErrorMessage">Why no chat can be created with the chosen data sources, or an empty string when it can.</param>
internal readonly record struct ChatLauncherDataSourcesResult(DataSourceOptions? Options, string ErrorMessage);