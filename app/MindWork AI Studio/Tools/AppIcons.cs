namespace AIStudio.Tools;

/// <summary>
/// Icons we draw ourselves, because the Material icon set MudBlazor ships does not contain them, or
/// draws them off its own grid.
/// </summary>
/// <remarks>
/// <para>
/// The strings follow the same convention as the MudBlazor icons: they contain the SVG child
/// elements only, drawn on a 24 by 24 canvas. MudIcon and every component taking an icon wrap them
/// into the svg element themselves, which is why there must be no svg root element here.
/// </para>
/// <para>
/// Every icon here is drawn on the grid of Material's icons, so that it looks as large as the
/// Material icons next to it: its outer bounds are one of Material's keyline shapes, centered on
/// the canvas, and its lines are two units wide. Nothing is sized by eye. <c>AppIconsTests</c>
/// checks the bounds, and documentation/Icons.md explains the grid and when to draw an icon at all.
/// </para>
/// </remarks>
public static class AppIcons
{
    /// <summary>
    /// The classic database symbol: a cylinder made of three stacked discs.
    /// </summary>
    /// <remarks>
    /// It fills the upright rectangle of Material's grid, 16 units wide and 20 tall.
    /// </remarks>
    public const string DATABASE =
        """
        <path d="M4 4.6A8 2.6 0 0 1 20 4.6L20 8.9A8 2.6 0 0 1 4 8.9Z"/><path d="M4 9.9A8 2.6 0 0 0 20 9.9L20 14.1A8 2.6 0 0 1 4 14.1Z"/><path d="M4 15.1A8 2.6 0 0 0 20 15.1L20 19.4A8 2.6 0 0 1 4 19.4Z"/>
        """;

    /// <summary>
    /// A square with a pencil writing from its open corner, the common symbol for starting something new to write.
    /// </summary>
    /// <remarks>
    /// Built from two Material icons, so that it looks like one of them: the square with an open
    /// corner of <see cref="Icons.Material.Outlined.OpenInNew"/>, 18 units wide like every square of
    /// Material's grid, and the pencil of <see cref="Icons.Material.Filled.Edit"/>, scaled to 60
    /// percent and moved into that corner.
    /// </remarks>
    public const string EDIT_SQUARE_OUTLINED =
        """
        <path d="M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7z"/><path d="M10 11.75V14h2.25l6.64-6.64-2.25-2.25L10 11.75zM20.63 5.62c.23-.23.23-.61 0-.85l-1.4-1.4c-.23-.23-.61-.23-.85 0l-1.1 1.1 2.25 2.25 1.1-1.1z"/>
        """;

    /// <summary>
    /// Two overlapping squares, the common symbol for copying.
    /// </summary>
    /// <remarks>
    /// The shape of <see cref="Icons.Material.Outlined.FilterNone"/>, drawn 18 units wide instead
    /// of 22, the square of Material's grid, with the same two units wide borders and gaps.
    /// Material's own copy icon, <see cref="Icons.Material.Outlined.ContentCopy"/>, shows upright
    /// rectangles instead of squares.
    /// </remarks>
    public const string COPY_SQUARE_OUTLINED =
        """
        <path d="M5 7H3v12c0 1.1.9 2 2 2h12v-2H5V7z"/><path d="M19 3H9c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 12H9V5h10v10z"/>
        """;

    /// <summary>
    /// An open folder in perspective, the counterpart of <see cref="Icons.Material.Outlined.Folder"/>.
    /// </summary>
    /// <remarks>
    /// The Material icon of an open folder shows no front flap. This one keeps the tab, the rounded
    /// corners, the two units wide borders, and the outer bounds of the closed Material folder, the
    /// wide rectangle of Material's grid, and puts a slanted front flap in front of it, after
    /// <see cref="Icons.Custom.Uncategorized.FolderOpen"/>. The even-odd rule makes the inside of the
    /// flap a hole, whichever direction its points run.
    /// </remarks>
    public const string FOLDER_OPEN_OUTLINED =
        """
        <path fill-rule="evenodd" d="M17.79 20H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2h6l2 2h8c1.1 0 2 .9 2 2H11.17l-2-2H4v12l2.14-8H22l-2.28 8.5c-.23.87-1.01 1.5-1.93 1.5zM17.79 18H6l1.6-6h11.79z"/>
        """;

    /// <summary>
    /// A speech bubble, the symbol of a chat.
    /// </summary>
    /// <remarks>
    /// The bubble of <see cref="Icons.Material.Outlined.ChatBubbleOutline"/>, drawn on the square of
    /// Material's grid, 18 units wide and tall. Material draws it 20 units wide and tall, the size of
    /// its circle, so next to a folder it looks larger. Only the shape shrinks, its tail included: the
    /// lines stay two units wide, as scaling the whole icon would thin them.
    /// </remarks>
    public const string CHAT_BUBBLE_OUTLINED =
        """
        <path d="M19 3H5c-1.1 0-2 .9-2 2v16l3.6-3.6H19c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 12.4H6.6L5 17V5h14v10.4z"/>
        """;

    /// <summary>
    /// The speech bubble of <see cref="CHAT_BUBBLE_OUTLINED"/> with a dot in its upper right corner, for a
    /// chat with an answer which the user has not read yet.
    /// </summary>
    /// <remarks>
    /// After <see cref="Icons.Material.Outlined.MarkChatUnread"/>, whose dot reaches beyond its bubble.
    /// Here, the dot stays within the square of the bubble, and the bubble leaves a gap of two units
    /// around it, as Material's icon does. So the icon of a chat keeps its size when an answer arrives.
    /// </remarks>
    public const string CHAT_BUBBLE_UNREAD_OUTLINED =
        """
        <path d="M14.76 3H5c-1.1 0-2 .9-2 2v16l3.6-3.6H19c1.1 0 2-.9 2-2V9.24A4.5 4.5 0 0 1 19 9.97v5.43H6.6L5 17V5h9.03A4.5 4.5 0 0 1 14.76 3z"/><path d="M16 5.5c0 1.38 1.12 2.5 2.5 2.5s2.5-1.12 2.5-2.5-1.12-2.5-2.5-2.5S16 4.12 16 5.5z"/>
        """;

    /// <summary>
    /// The speech bubble of <see cref="CHAT_BUBBLE_OUTLINED"/> with an exclamation mark inside, for a chat
    /// whose answer failed.
    /// </summary>
    /// <remarks>
    /// After <see cref="Icons.Material.Outlined.SmsFailed"/>, drawn on the square of Material's grid like
    /// the plain bubble. The mark stays two units wide, and its length shrinks with the bubble.
    /// </remarks>
    public const string CHAT_BUBBLE_FAILED_OUTLINED =
        """
        <path d="M19 3H5c-1.1 0-2 .9-2 2v16l3.6-3.6H19c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 12.4H6.6L5 17V5h14v10.4zM11 6.5h2v3.6h-2zm0 5.4h2v2h-2z"/>
        """;
}