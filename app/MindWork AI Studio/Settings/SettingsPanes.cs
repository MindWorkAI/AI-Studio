using AIStudio.Components.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

/// <summary>
/// All panes of the settings window, and which of them the window shows for a path.
/// </summary>
/// <remarks>
/// A new pane is one entry in <see cref="ALL"/>, under a path of <see cref="SettingsPath"/>. The panes
/// of the first level appear in the window in the order of this list.
/// </remarks>
public static class SettingsPanes
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SettingsPanes).Namespace, nameof(SettingsPanes));

    /// <summary>
    /// Every pane of the settings window.
    /// </summary>
    public static readonly IReadOnlyList<SettingsPane> ALL =
    [
        new(SettingsPath.CHAT, () => TB("Chat"), Icons.Material.Filled.ChatBubble, Color.Primary, typeof(SettingsPaneChat)),
        new(SettingsPath.WORKSPACES, () => TB("Workspaces"), Icons.Material.Filled.Folder, Color.Info, typeof(SettingsPaneWorkspaces)),
    ];

    /// <summary>
    /// The pane at exactly this path, if there is one.
    /// </summary>
    /// <param name="path">The path of the pane.</param>
    /// <returns>The pane, or null when no pane lives at this path.</returns>
    public static SettingsPane? Find(SettingsPath path) => ALL.FirstOrDefault(pane => pane.Path == path);

    /// <summary>
    /// The pane which the window shows for a path.
    /// </summary>
    /// <remarks>
    /// A path which leads nowhere, or to a pane which cannot be shown right now, opens the nearest pane
    /// above it which can. Without one, the window opens its first pane. A place which offers settings
    /// thus never opens an empty window, also when a preview feature hides its pane.
    /// </remarks>
    /// <param name="path">The path the window was asked to show.</param>
    /// <param name="isAvailable">Whether a pane can be shown right now, see <see cref="SettingsPane.IsAvailable"/>.</param>
    /// <returns>The pane to show.</returns>
    public static SettingsPane Resolve(SettingsPath path, Func<SettingsPane, bool> isAvailable)
    {
        foreach (var candidate in path.SelfAndAncestors())
        {
            if (Find(candidate) is { } pane && IsShown(pane, isAvailable))
                return pane;
        }

        // The pane of the chat options is always available, so the window always finds one:
        return FirstLevel(isAvailable).First();
    }

    /// <summary>
    /// The panes of the first level which can be shown right now, in the order of the list on the left
    /// of the window.
    /// </summary>
    /// <param name="isAvailable">Whether a pane can be shown right now, see <see cref="SettingsPane.IsAvailable"/>.</param>
    public static IEnumerable<SettingsPane> FirstLevel(Func<SettingsPane, bool> isAvailable) => ALL.Where(pane => pane.Path.Parent is null && isAvailable(pane));

    /// <summary>
    /// The panes from the first level down to this one: the window marks the first of them in its list,
    /// and its back button leads to the one before the last.
    /// </summary>
    /// <param name="pane">The pane shown.</param>
    /// <returns>The panes above it and the pane itself, starting at the first level.</returns>
    public static IReadOnlyList<SettingsPane> Trail(SettingsPane pane) => pane.Path.SelfAndAncestors()
        .Select(Find)
        .OfType<SettingsPane>()
        .Reverse()
        .ToList();

    /// <summary>
    /// Whether a pane can be shown: it is available itself, and so is every pane above it.
    /// </summary>
    private static bool IsShown(SettingsPane pane, Func<SettingsPane, bool> isAvailable) => Trail(pane).All(isAvailable);
}