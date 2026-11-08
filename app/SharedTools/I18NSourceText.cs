namespace SharedTools;

/// <summary>
/// A text of T() or TB() which the I18N collector found in a source file.
/// </summary>
/// <param name="Offset">Where its literal starts in the file.</param>
/// <param name="Length">How many characters its literal takes in the file.</param>
/// <param name="Text">The string the compiler makes of the literal.</param>
public sealed record I18NSourceText(int Offset, int Length, string Text);