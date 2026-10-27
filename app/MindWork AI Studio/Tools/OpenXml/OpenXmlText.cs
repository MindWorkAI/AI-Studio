using System.Text;
using System.Xml;

namespace AIStudio.Tools.OpenXml;

/// <summary>
/// Prepares texts for the Office documents AI Studio writes.
/// </summary>
internal static class OpenXmlText
{
    /// <summary>
    /// Leaves out the characters XML cannot carry, which a model may produce now and then. The file
    /// could not be saved with them.
    /// </summary>
    /// <remarks>
    /// A character outside the Basic Multilingual Plane, such as an emoji, takes two chars; such a
    /// pair stays, whereas a half of one alone goes.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <returns>The text without the characters XML cannot carry.</returns>
    public static string XmlSafe(string text)
    {
        if (text.All(XmlConvert.IsXmlChar))
            return text;

        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (XmlConvert.IsXmlChar(text[index]))
                result.Append(text[index]);
            else if (index + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[index + 1], text[index]))
                result.Append(text[index]).Append(text[++index]);
        }

        return result.ToString();
    }
}