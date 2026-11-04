namespace AIStudio.Tools;

/// <summary>
/// Icons we draw ourselves, because the Material icon set MudBlazor ships does not contain them.
/// </summary>
/// <remarks>
/// The strings follow the same convention as the MudBlazor icons: they contain the SVG child
/// elements only, drawn on a 24 by 24 canvas. MudIcon and every component taking an icon wrap them
/// into the svg element themselves, which is why there must be no svg root element here.
/// </remarks>
public static class AppIcons
{
    /// <summary>
    /// The classic database symbol: a cylinder made of three stacked discs.
    /// </summary>
    public const string DATABASE =
        """
        <path d="M5 4.6A7 2.6 0 0 1 19 4.6L19 8.9A7 2.6 0 0 1 5 8.9Z"/><path d="M5 9.9A7 2.6 0 0 0 19 9.9L19 14.1A7 2.6 0 0 1 5 14.1Z"/><path d="M5 15.1A7 2.6 0 0 0 19 15.1L19 19.4A7 2.6 0 0 1 5 19.4Z"/>
        """;

    /// <summary>
    /// A square with a pencil writing from its open corner, the common symbol for starting something new to write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built from two Material icons, so that it looks like one of them: a square with an open
    /// corner like the one of <see cref="Icons.Material.Outlined.OpenInNew"/>, and the pencil of
    /// <see cref="Icons.Material.Filled.Edit"/>, scaled to 60 percent and moved into that corner.
    /// </para>
    /// <para>
    /// The square is 16 units wide instead of the 18 of Material's squares. An outlined square
    /// fills its box more than any other shape, and at 18 units it looks larger than the folders
    /// and the bin next to it, whose outlines are about 16 units.
    /// </para>
    /// </remarks>
    public const string EDIT_SQUARE_OUTLINED =
        """
        <path d="M18 18H6V6h6V4H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2v-6h-2v6z"/><path d="M9 12.75V15h2.25l6.64-6.64-2.25-2.25L9 12.75zM19.63 6.62c.23-.23.23-.61 0-.85l-1.4-1.4c-.23-.23-.61-.23-.85 0l-1.1 1.1 2.25 2.25 1.1-1.1z"/>
        """;

    /// <summary>
    /// Two overlapping squares, the common symbol for copying.
    /// </summary>
    /// <remarks>
    /// The shape of <see cref="Icons.Material.Outlined.FilterNone"/>, drawn 18 units wide instead
    /// of 22, the size Material gives squares, with the same two units wide borders and gaps.
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
    /// corners, the two units wide borders, and the outer bounds of the closed Material folder, and
    /// puts a slanted front flap in front of it, after <see cref="Icons.Custom.Uncategorized.FolderOpen"/>.
    /// The even-odd rule makes the inside of the flap a hole, whichever direction its points run.
    /// </remarks>
    public const string FOLDER_OPEN_OUTLINED =
        """
        <path fill-rule="evenodd" d="M17.79 20H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2h6l2 2h8c1.1 0 2 .9 2 2H11.17l-2-2H4v12l2.14-8H22l-2.28 8.5c-.23.87-1.01 1.5-1.93 1.5zM17.79 18H6l1.6-6h11.79z"/>
        """;
}