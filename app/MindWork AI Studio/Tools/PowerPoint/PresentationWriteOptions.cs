namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What the user chose for the presentation as a whole, beyond the slides themselves.
/// </summary>
/// <param name="VariantLabel">The variant all slides are written in, e.g., "A"; null for the first one of the template.</param>
/// <param name="FooterText">The text of the footer on every slide whose layout has one; empty for no footer.</param>
/// <param name="KeepEmptyPicturePlaceholders">Whether a new slide keeps the picture placeholders of its layout which no picture fills, so that the user can insert pictures of their own.</param>
internal sealed record PresentationWriteOptions(string? VariantLabel, string FooterText, bool KeepEmptyPicturePlaceholders);