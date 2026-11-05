namespace AIStudio.Components;

/// <summary>
/// The small mark in the corner of a chat's icon, see <see cref="ChatIndicator.Badge"/>.
/// </summary>
internal enum ChatIndicatorBadge
{
    /// <summary>
    /// No mark.
    /// </summary>
    NONE,

    /// <summary>
    /// A small spinner: an answer is being written, or a transcript made.
    /// </summary>
    WORK_GOING_ON,

    /// <summary>
    /// A small red exclamation mark: an answer or a transcript failed.
    /// </summary>
    FAILED,
}