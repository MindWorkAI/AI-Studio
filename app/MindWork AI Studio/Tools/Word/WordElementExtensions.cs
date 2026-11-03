using DocumentFormat.OpenXml;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Finds what a block of a Word document holds itself.
/// </summary>
internal static class WordElementExtensions
{
    /// <summary>
    /// Returns the elements of a kind within an element, in the order of the document, apart from those
    /// in text boxes, which float elsewhere, and from the older markup Word reads only when it
    /// understands no newer one.
    /// </summary>
    /// <param name="element">The element, e.g., a paragraph.</param>
    /// <typeparam name="T">The kind of the elements.</typeparam>
    /// <returns>The elements.</returns>
    public static IEnumerable<T> OwnDescendants<T>(this OpenXmlElement element) where T : OpenXmlElement
    {
        foreach (var child in element.ChildElements)
        {
            if (child is W.TextBoxContent or AlternateContentFallback)
                continue;

            if (child is T match)
                yield return match;

            foreach (var descendant in child.OwnDescendants<T>())
                yield return descendant;
        }
    }
}