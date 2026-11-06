namespace AIStudio.Settings;

/// <summary>
/// A pane of the settings window: what the window shows at one path.
/// </summary>
/// <remarks>
/// A pane of the first level appears in the list on the left of the window; a deeper pane opens in
/// place of its parent, and a back button left of its title leads there again. The window gives every pane what a panel of the
/// settings page gets as well, so <see cref="Component"/> derives from
/// <see cref="AIStudio.Components.Settings.SettingsPanelBase"/>.
/// </remarks>
/// <param name="Path">Where the pane lives in the window.</param>
/// <param name="Title">The title of the pane in the language the user chose.</param>
/// <param name="Icon">The icon of the pane in the list of the window: the filled form of the icon the app shows elsewhere for the same thing, such as the chat bubble of the workspace list.</param>
/// <param name="IconColor">The theme color of the avatar behind the icon, as the avatars of the messages in a chat have one. Each pane of the first level gets a color of its own, so the eye finds it again in the list. Only Primary, Secondary, Tertiary, Info, or Dark: red, orange, and green stand for errors, warnings, and success elsewhere in the app, and a pane in one of them would look like it reported a state.</param>
/// <param name="Component">The component which shows the options of the pane.</param>
/// <param name="IsAvailable">Whether the pane can be shown right now, for example only while a preview feature is on. Without it, the pane is always available.</param>
/// <param name="IconScale">How large the icon is drawn, relative to the others. Material draws some icons fuller than others: the chat bubble fills its square from top to bottom, while the folder leaves a quarter of it empty. Below 1, such an icon looks as large as the others.</param>
public sealed record SettingsPane(SettingsPath Path, Func<string> Title, string Icon, Color IconColor, Type Component, Func<SettingsManager, bool>? IsAvailable = null, double IconScale = 1);