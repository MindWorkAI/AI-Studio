using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

/// <summary>
/// Whose data source options a chat started by a launcher begins with, decided by ChatTemplate.ChooseDataSourceOptions.
/// </summary>
/// <param name="Options">The options to start with, or null when neither side names any, which leaves the chat default in place.</param>
/// <param name="LauncherChoiceDropped">Whether the data sources the launcher names itself were dropped for the options of the chat template.</param>
public readonly record struct ChatTemplateDataSourceChoice(DataSourceOptions? Options, bool LauncherChoiceDropped)
{
    /// <summary>
    /// Neither side names any data sources, so the chat keeps its default options.
    /// </summary>
    public static readonly ChatTemplateDataSourceChoice NONE = new(null, false);
}