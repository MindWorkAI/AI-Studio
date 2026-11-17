namespace SharedTools;

/// <summary>
/// A text of T(), TB(), or TA() which the I18N collector found in a source file.
/// </summary>
/// <param name="Offset">Where its literal starts in the file.</param>
/// <param name="Length">How many characters its literal takes in the file.</param>
/// <param name="Text">The string the compiler makes of the literal.</param>
/// <param name="HasAddressVariants">Whether the text comes from TA(), i.e., addresses the reader and gets a variant with the informal address, see I18NAddressVariant.</param>
public sealed record I18NSourceText(int Offset, int Length, string Text, bool HasAddressVariants);