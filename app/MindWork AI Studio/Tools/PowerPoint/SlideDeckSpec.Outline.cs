using System.Text;

namespace AIStudio.Tools.PowerPoint;

internal sealed partial record SlideDeckSpec
{
    /// <summary>
    /// Writes the slide deck as a Markdown outline, which the user reads before creating the file.
    /// </summary>
    /// <remarks>
    /// The outline is what the slide planner always produced, and it keeps working wherever Markdown
    /// does: copying it, exporting it to Word, or taking it into a chat. Slides starting a chapter
    /// get a larger heading. Within a slide, the content follows the reading order of its layout, so
    /// a heading above a text comes before that text, and the source of a picture after the picture.
    /// </remarks>
    /// <param name="template">The template the deck was written for, which gives the reading order.</param>
    /// <param name="pictureNames">The names of the attached pictures, in the order the model numbered them.</param>
    /// <returns>The outline.</returns>
    public string ToMarkdownOutline(PresentationTemplate template, IReadOnlyList<string> pictureNames)
    {
        var outline = new StringBuilder();
        outline.Append("# ").Append(this.Title).Append('\n');
        foreach (var slide in this.Slides)
        {
            outline.Append("\n***\n");
            if (slide.Title.Length > 0)
                outline.Append('\n').Append(slide.StartsChapter ? "## " : "### ").Append(slide.Title).Append('\n');

            var slots = template.FindFamily(slide.Layout)?.Slots ?? [];
            foreach (var slot in slots)
                AppendSlot(outline, slide, slot, pictureNames);
        }

        return outline.ToString().TrimEnd('\n');
    }

    private static void AppendSlot(StringBuilder outline, SlideSpec slide, TemplateSlot slot, IReadOnlyList<string> pictureNames)
    {
        var index = slot.Ordinal - 1;
        switch (slot.Role)
        {
            case PlaceholderRole.SUBTITLE when slide.Subtitle.Length > 0:
                outline.Append('\n').Append('_').Append(slide.Subtitle).Append("_\n");
                break;

            case PlaceholderRole.TEXT when index < slide.Texts.Count && slide.Texts[index].Paragraphs.Count > 0:
                outline.Append('\n');
                foreach (var paragraph in slide.Texts[index].Paragraphs)
                    outline.Append(' ', 2 * paragraph.Level).Append("- ").Append(paragraph.Text).Append('\n');

                break;

            case PlaceholderRole.LABEL when index < slide.Labels.Count && slide.Labels[index].Length > 0:
                outline.Append('\n').Append('_').Append(slide.Labels[index]).Append("_\n");
                break;

            case PlaceholderRole.PICTURE when index < slide.Pictures.Count:
                var number = slide.Pictures[index];
                var name = number >= 1 && number <= pictureNames.Count ? pictureNames[number - 1] : $"#{number}";
                outline.Append('\n').Append("*Picture: ").Append(name).Append("*\n");
                break;

            case PlaceholderRole.TABLE when index == 0 && slide.Table is { Headers.Count: > 0 } table:
                outline.Append('\n');
                AppendTableRow(outline, table.Headers);
                AppendTableRow(outline, table.Headers.Select(_ => "---").ToList());
                foreach (var row in table.Rows)
                    AppendTableRow(outline, row);

                break;
        }
    }

    private static void AppendTableRow(StringBuilder outline, IReadOnlyList<string> cells)
    {
        outline.Append('|');
        foreach (var cell in cells)
            outline.Append(' ').Append(cell.ReplaceLineEndings(" ").Replace("|", "\\|")).Append(" |");

        outline.Append('\n');
    }
}