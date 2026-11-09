using System.Text;

using AIStudio.Tools.OpenXml;

using DocumentFormat.OpenXml;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// Puts a paragraph of a Word document together again around its new words.
/// </summary>
/// <remarks>
/// The properties of the paragraph stay, and so do all elements its tags stand for: the writer moves them into
/// the new paragraph rather than copying them, so that a text box keeps the new words of its own paragraphs,
/// whichever paragraph is written first. New words get the properties of the first words of their pair.
/// </remarks>
internal static class WordParagraphWriter
{
    /// <summary>
    /// Replaces the content of a paragraph with its new words.
    /// </summary>
    /// <param name="paragraph">The paragraph, as the reader read it from this document.</param>
    /// <param name="newText">The new words with tags, as the model wrote them.</param>
    /// <param name="languageTag">The IETF tag of the language of the new words; empty to leave the languages as they are.</param>
    /// <returns>Whether the paragraph kept all of its inline structure and formatting.</returns>
    public static bool Write(WordParagraph paragraph, string newText, string languageTag)
    {
        var element = paragraph.Paragraph;
        foreach (var child in element.ChildElements.Where(child => child is not W.ParagraphProperties).ToList())
            child.Remove();

        foreach (var marker in paragraph.StartMarkers)
            element.AppendChild(Detached(marker));

        var writtenAtoms = new HashSet<WordAtom>();
        bool keepsTags;
        if (TaggedText.TryRead(newText, out var taggedText, out _))
        {
            keepsTags = taggedText.HasSameTagsAs(paragraph.Segment.Source, out _);
            WriteTokens(paragraph, taggedText, languageTag, writtenAtoms);
        }
        else
        {
            keepsTags = false;
            AppendWords(element, TaggedText.RemoveTags(newText), paragraph.TextFormatOf(0), languageTag);
        }

        // What the model dropped follows its words, so that no picture or reference gets lost:
        foreach (var atom in paragraph.AtomsInOrder.Where(atom => !writtenAtoms.Contains(atom)))
            AppendAtom(element, atom);

        foreach (var marker in paragraph.EndMarkers)
            element.AppendChild(Detached(marker));

        return keepsTags && !paragraph.HasMixedFormatting;
    }

    private static void WriteTokens(WordParagraph paragraph, TaggedText taggedText, string languageTag, HashSet<WordAtom> writtenAtoms)
    {
        // Only pairs whose both tags arrived go back; TryRead made sure that they nest:
        var openIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.OPEN).Select(token => token.Id).ToHashSet();
        var closeIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.CLOSE).Select(token => token.Id).ToHashSet();

        OpenXmlElement target = paragraph.Paragraph;
        var openPairs = new Stack<(int Id, OpenXmlElement Target)>();
        foreach (var token in taggedText.Tokens)
        {
            switch (token.Kind)
            {
                case TaggedTokenKind.TEXT:
                    AppendWords(target, token.Text, paragraph.TextFormatOf(openPairs.Count == 0 ? 0 : openPairs.Peek().Id), languageTag);
                    break;

                case TaggedTokenKind.OPEN when closeIds.Contains(token.Id) && paragraph.Pairs.TryGetValue(token.Id, out var openedPair):
                    openPairs.Push((token.Id, target));
                    switch (openedPair)
                    {
                        case WordContainerPair container:
                            var (shell, content) = ShellOf(container.Container);
                            target.AppendChild(shell);
                            target = content;
                            break;

                        case WordFieldPair field:
                            AppendAtom(target, field.Opening);
                            writtenAtoms.Add(field.Opening);
                            break;
                    }

                    break;

                case TaggedTokenKind.CLOSE when openIds.Contains(token.Id) && paragraph.Pairs.TryGetValue(token.Id, out var closedPair):
                    target = openPairs.Pop().Target;
                    if (closedPair is WordFieldPair { Closing: var closing })
                    {
                        AppendAtom(target, closing);
                        writtenAtoms.Add(closing);
                    }

                    break;

                case TaggedTokenKind.PLACEHOLDER when paragraph.Placeholders.TryGetValue(token.Id, out var atom) && writtenAtoms.Add(atom):
                    AppendAtom(target, atom);
                    break;
            }
        }
    }

    /// <summary>
    /// Creates an empty copy of an element which encloses runs, with its attributes and properties.
    /// </summary>
    /// <returns>The copy, and the element within it which takes the runs.</returns>
    private static (OpenXmlElement Shell, OpenXmlElement Content) ShellOf(OpenXmlElement container)
    {
        var shell = container.CloneNode(false);
        switch (container)
        {
            case W.SdtRun contentControl:
                if (contentControl.SdtProperties is { } contentControlProperties)
                    shell.AppendChild(contentControlProperties.CloneNode(true));

                if (contentControl.SdtEndCharProperties is { } endProperties)
                    shell.AppendChild(endProperties.CloneNode(true));

                return (shell, shell.AppendChild(new W.SdtContentRun()));

            case W.CustomXmlRun { CustomXmlProperties: { } customXmlProperties }:
                shell.AppendChild(customXmlProperties.CloneNode(true));
                break;
        }

        return (shell, shell);
    }

    private static void AppendAtom(OpenXmlElement target, WordAtom atom)
    {
        foreach (var piece in atom.Pieces)
        {
            var element = Detached(piece.Element);
            if (!piece.InRun)
            {
                target.AppendChild(element);
                continue;
            }

            var run = new W.Run();
            if (piece.RunProperties is { } format)
                run.AppendChild(format.CloneNode(true));

            run.AppendChild(element);
            target.AppendChild(run);
        }
    }

    /// <summary>
    /// Appends words as a run, with tabs and line breaks as Word writes them.
    /// </summary>
    private static void AppendWords(OpenXmlElement target, string words, W.RunProperties? format, string languageTag)
    {
        if (words.Length == 0)
            return;

        var run = new W.Run();
        if (format is not null)
            run.AppendChild((W.RunProperties)format.CloneNode(true));

        var text = new StringBuilder();
        foreach (var character in words)
        {
            switch (character)
            {
                case '\t':
                    AppendText(run, text);
                    run.AppendChild(new W.TabChar());
                    break;

                case '\n':
                    AppendText(run, text);
                    run.AppendChild(new W.Break());
                    break;

                case '\r':
                    break;

                default:
                    text.Append(character);
                    break;
            }
        }

        AppendText(run, text);
        WordLanguage.Set(run, languageTag);
        target.AppendChild(run);
    }

    private static void AppendText(W.Run run, StringBuilder text)
    {
        if (text.Length == 0)
            return;

        run.AppendChild(new W.Text(OpenXmlText.XmlSafe(text.ToString())) { Space = SpaceProcessingModeValues.Preserve });
        text.Clear();
    }

    private static T Detached<T>(T element) where T : OpenXmlElement
    {
        if (element.Parent is not null)
            element.Remove();

        return element;
    }
}