namespace AIStudio.Settings;

/// <summary>
/// The address of a pane in the settings window, such as <c>chat</c> or, one level deeper,
/// <c>assistants/synonyms</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every place which offers settings opens the same window at the path of what it offers, see
/// <see cref="AIStudio.Dialogs.Settings.SettingsWindow.OpenAsync"/>. The paths of the first level are
/// written down here, and only here, and so are the deeper paths which places outside the window open.
/// A deeper path is built from its parent with <see cref="Append"/>, so that a segment can also be an ID,
/// such as the one of a plugin.
/// </para>
/// <para>
/// A path belongs to the window, not to the routes of the app: the window opens over whatever page is
/// shown, which a change of the route would replace.
/// </para>
/// </remarks>
public readonly record struct SettingsPath
{
    private const char SEPARATOR = '/';

    /// <summary>
    /// The options of all chats.
    /// </summary>
    public static readonly SettingsPath CHAT = new("chat");

    /// <summary>
    /// How the workspaces store and show the chats.
    /// </summary>
    public static readonly SettingsPath WORKSPACES = new("workspaces");

    /// <summary>
    /// The chat templates, below the options of all chats.
    /// </summary>
    public static readonly SettingsPath CHAT_TEMPLATES = CHAT.Append("templates");

    private readonly string? value;

    private SettingsPath(string value) => this.value = value;

    /// <summary>
    /// The path as text, its segments separated by slashes.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// The path one level up, or null for a path of the first level.
    /// </summary>
    public SettingsPath? Parent
    {
        get
        {
            var lastSeparator = this.Value.LastIndexOf(SEPARATOR);
            return lastSeparator < 0 ? null : new SettingsPath(this.Value[..lastSeparator]);
        }
    }

    /// <summary>
    /// The path of a pane one level below this one.
    /// </summary>
    /// <param name="segment">The last segment of the new path, such as the name of an assistant or the ID of a plugin.</param>
    /// <returns>The path of the pane below this one.</returns>
    /// <exception cref="ArgumentException">The segment is empty or holds a slash.</exception>
    public SettingsPath Append(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Contains(SEPARATOR))
            throw new ArgumentException($"A segment of a settings path must not be empty or hold a '{SEPARATOR}'.", nameof(segment));

        return new SettingsPath($"{this.Value}{SEPARATOR}{segment}");
    }

    /// <summary>
    /// The path one level below the given one on the way down to this path, such as <c>chat/templates</c>
    /// below <c>chat</c> on the way to <c>chat/templates/abc</c>.
    /// </summary>
    /// <remarks>
    /// Going up from this path to the given one, the window returns to the part of the page there which
    /// leads down again, see <see cref="AIStudio.Components.Settings.SettingsAnchor"/>, and that part
    /// carries this path.
    /// </remarks>
    /// <param name="ancestor">A path above this one.</param>
    /// <returns>The path one level below the given one, or null when the given path is not above this one.</returns>
    public SettingsPath? StepBelow(SettingsPath ancestor)
    {
        foreach (var path in this.SelfAndAncestors())
        {
            if (path.Parent == ancestor)
                return path;
        }

        return null;
    }

    /// <summary>
    /// This path and the ones above it, from this one up to the first level.
    /// </summary>
    public IReadOnlyList<SettingsPath> SelfAndAncestors()
    {
        var paths = new List<SettingsPath>();
        SettingsPath? path = this;
        while (path is { } current)
        {
            paths.Add(current);
            path = current.Parent;
        }

        return paths;
    }

    /// <inheritdoc />
    public override string ToString() => this.Value;
}