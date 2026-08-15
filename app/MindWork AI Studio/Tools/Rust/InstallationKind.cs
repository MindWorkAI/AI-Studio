namespace AIStudio.Tools.Rust;

/// <summary>
/// Tells whether this installation is able to update itself, and if not, why.
/// </summary>
public enum InstallationKind
{
    /// <summary>
    /// An installation the current user owns and which AI Studio may update itself. This is also
    /// the fallback when the runtime reports a kind we do not know yet.
    /// </summary>
    USER,

    /// <summary>
    /// An installation someone else deployed and maintains, for example, an IT department. Whoever
    /// deployed it distributes new versions instead.
    /// </summary>
    MANAGED,

    /// <summary>
    /// An installation the current user owns, but which the updater cannot replace. Its owner has
    /// to install a new version themselves.
    /// </summary>
    UNSUPPORTED_LOCATION,
}