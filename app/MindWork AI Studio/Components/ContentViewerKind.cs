namespace AIStudio.Components;

/// <summary>
/// What kind of content a ContentViewer shows.
/// </summary>
public enum ContentViewerKind
{
    /// <summary>
    /// Text, shown as rendered Markdown and as plain text.
    /// </summary>
    TEXT,

    /// <summary>
    /// An image, shown from its address.
    /// </summary>
    IMAGE,

    /// <summary>
    /// A recording, played from its address.
    /// </summary>
    AUDIO,

    /// <summary>
    /// A video, played from its address.
    /// </summary>
    VIDEO,
}