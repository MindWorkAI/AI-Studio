using AIStudio.Tools.OpenXml;

using DocumentFormat.OpenXml;

using A = DocumentFormat.OpenXml.Drawing;

namespace AIStudio.Tools.DocumentSegments.PowerPoint;

/// <summary>
/// Puts a paragraph of a slide together again around its new words.
/// </summary>
/// <remarks>
/// The properties of the paragraph and those of its end stay, the latter as its last element, as the schema
/// demands. The elements its tags stand for, e.g., line breaks and fields, move into the new paragraph. New words
/// get the formatting of their pair, or the most common formatting of the paragraph outside of any pair.
/// </remarks>
internal static class SlideParagraphWriter
{
    /// <summary>
    /// Replaces the content of a paragraph with its new words.
    /// </summary>
    /// <param name="paragraph">The paragraph, as the reader read it from this presentation.</param>
    /// <param name="newText">The new words with tags, as the model wrote them.</param>
    /// <param name="languageTag">The IETF tag of the language of the new words; empty to leave the languages as they are.</param>
    /// <returns>Whether the paragraph kept all of its inline structure and formatting.</returns>
    public static bool Write(SlideTextParagraph paragraph, string newText, string languageTag)
    {
        var element = paragraph.Paragraph;
        var end = element.GetFirstChild<A.EndParagraphRunProperties>();
        foreach (var child in element.ChildElements.Where(child => child is not (A.ParagraphProperties or A.EndParagraphRunProperties)).ToList())
            child.Remove();

        var writtenPlaceholders = new HashSet<int>();
        bool keepsTags;
        if (TaggedText.TryRead(newText, out var taggedText, out _))
        {
            keepsTags = taggedText.HasSameTagsAs(paragraph.Segment.Source, out _);

            // Only pairs whose both tags arrived go back; TryRead made sure that they nest:
            var openIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.OPEN).Select(token => token.Id).ToHashSet();
            var closeIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.CLOSE).Select(token => token.Id).ToHashSet();
            var openPairs = new Stack<int>();
            foreach (var token in taggedText.Tokens)
            {
                switch (token.Kind)
                {
                    case TaggedTokenKind.TEXT:
                        AppendWords(element, end, token.Text, openPairs.Count == 0 ? paragraph.BaseFormat : paragraph.FormatPairs[openPairs.Peek()], languageTag);
                        break;

                    case TaggedTokenKind.OPEN when closeIds.Contains(token.Id) && paragraph.FormatPairs.ContainsKey(token.Id):
                        openPairs.Push(token.Id);
                        break;

                    case TaggedTokenKind.CLOSE when openIds.Contains(token.Id) && paragraph.FormatPairs.ContainsKey(token.Id):
                        openPairs.Pop();
                        break;

                    case TaggedTokenKind.PLACEHOLDER when paragraph.Placeholders.TryGetValue(token.Id, out var placeholder) && writtenPlaceholders.Add(token.Id):
                        Insert(element, end, Detached(placeholder));
                        break;
                }
            }
        }
        else
        {
            keepsTags = false;
            AppendWords(element, end, TaggedText.RemoveTags(newText), paragraph.BaseFormat, languageTag);
        }

        // What the model dropped follows its words, so that no line break or field gets lost:
        foreach (var (id, placeholder) in paragraph.Placeholders.OrderBy(entry => entry.Key))
        {
            if (!writtenPlaceholders.Contains(id))
                Insert(element, end, Detached(placeholder));
        }

        return keepsTags && !paragraph.HasMixedFormatting;
    }

    /// <summary>
    /// Appends words as runs, with line breaks as PowerPoint writes them.
    /// </summary>
    private static void AppendWords(A.Paragraph paragraph, A.EndParagraphRunProperties? end, string words, A.RunProperties? format, string languageTag)
    {
        var lines = words.Replace("\r", string.Empty).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
                Insert(paragraph, end, new A.Break { RunProperties = FormatOf(format, languageTag) });

            if (lines[index].Length > 0)
                Insert(paragraph, end, new A.Run(FormatOf(format, languageTag), new A.Text(OpenXmlText.XmlSafe(lines[index]))));
        }
    }

    /// <summary>
    /// Copies the formatting for new words, in their language, and without the marks of an earlier spell check.
    /// </summary>
    private static A.RunProperties FormatOf(A.RunProperties? format, string languageTag)
    {
        var properties = format?.CloneNode(true) as A.RunProperties ?? new A.RunProperties();
        properties.SpellingError = null;
        properties.Dirty = null;
        if (languageTag.Length > 0)
            properties.Language = languageTag;

        return properties;
    }

    /// <summary>
    /// Adds an element before the properties of the end of the paragraph, which have to come last.
    /// </summary>
    private static void Insert(A.Paragraph paragraph, A.EndParagraphRunProperties? end, OpenXmlElement element)
    {
        if (end is null)
            paragraph.AppendChild(element);
        else
            paragraph.InsertBefore(element, end);
    }

    private static T Detached<T>(T element) where T : OpenXmlElement
    {
        if (element.Parent is not null)
            element.Remove();

        return element;
    }
}