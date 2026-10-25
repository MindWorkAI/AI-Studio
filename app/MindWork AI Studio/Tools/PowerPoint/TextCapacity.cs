namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// How much text fits into a slot, estimated from its size and its font size.
/// </summary>
/// <remarks>
/// An estimate on purpose: the real width of a line depends on the font and the words. It tells the
/// model how much to write, and the check of its answer how much is clearly too much.
/// </remarks>
/// <param name="Lines">How many lines fit, or how many rows for a table.</param>
/// <param name="CharactersPerLine">How many characters fit into one line, across the whole width.</param>
internal readonly record struct TextCapacity(int Lines, int CharactersPerLine);