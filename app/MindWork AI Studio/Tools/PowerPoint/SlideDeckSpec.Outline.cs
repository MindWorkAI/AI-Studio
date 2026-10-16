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
    ///
    /// A slide taken from the template says so, and shows what it keeps of the template: all of it
    /// when kept unchanged, and every slot the model left empty when edited.
    /// </remarks>
    /// <param name="template">The template the deck was written for, which gives the reading order and the template slides.</param>
    /// <param name="pictureNames">The names of the attached pictures, in the order the model numbered them.</param>
    /// <returns>The outline.</returns>
    public string ToMarkdownOutline(PresentationTemplate template, IReadOnlyList<string> pictureNames)
    {
        var outline = new StringBuilder();
        outline.Append("# ").Append(this.Title).Append('\n');
        foreach (var slide in this.Slides)
        {
            outline.Append("\n***\n");
            var templateSlide = slide.TemplateSlide is { } number ? template.FindSlide(number) : null;
            var title = slide.Title.Length > 0 ? slide.Title : templateSlide?.Title ?? string.Empty;
            if (title.Length > 0)
                outline.Append('\n').Append(slide.StartsChapter ? "## " : "### ").Append(title).Append('\n');

            if (templateSlide is null)
            {
                foreach (var slot in template.FindFamily(slide.Layout)?.Slots ?? [])
                    AppendSlot(outline, slide, slot, pictureNames);

                continue;
            }

            outline.Append('\n').Append("*Slide ").Append(templateSlide.Number).Append(slide.HasContent ? " of the template, edited*" : " of the template, unchanged*").Append('\n');

            // A slide on a layout without room for content shows what it holds all the same:
            var slots = template.FamilyOf(slide)?.Slots ?? templateSlide.Contents.Select(content => content.Slot).ToList();
            foreach (var slot in slots)
            {
                if (AppendSlot(outline, slide, slot, pictureNames))
                    continue;

                if (templateSlide.Contents.FirstOrDefault(content => content.Slot.Role == slot.Role && content.Slot.Ordinal == slot.Ordinal) is { } templateContent)
                    AppendTemplateContent(outline, templateContent);
            }
        }

        return outline.ToString().TrimEnd('\n');
    }

    /// <returns>Whether the slide has content for the slot, which was appended.</returns>
    private static bool AppendSlot(StringBuilder outline, SlideSpec slide, TemplateSlot slot, IReadOnlyList<string> pictureNames)
    {
        var index = slot.Ordinal - 1;
        switch (slot.Role)
        {
            case PlaceholderRole.SUBTITLE when slide.Subtitle.Length > 0:
                outline.Append('\n').Append('_').Append(slide.Subtitle).Append("_\n");
                return true;

            case PlaceholderRole.TEXT when index < slide.Texts.Count && slide.Texts[index].Paragraphs.Count > 0:
                outline.Append('\n');
                foreach (var paragraph in slide.Texts[index].Paragraphs)
                    outline.Append(' ', 2 * paragraph.Level).Append("- ").Append(paragraph.Text).Append('\n');

                return true;

            case PlaceholderRole.LABEL when index < slide.Labels.Count && slide.Labels[index].Length > 0:
                outline.Append('\n').Append('_').Append(slide.Labels[index]).Append("_\n");
                return true;

            case PlaceholderRole.PICTURE when index < slide.Pictures.Count:
                var number = slide.Pictures[index];
                var name = number >= 1 && number <= pictureNames.Count ? pictureNames[number - 1] : $"#{number}";
                outline.Append('\n').Append("*Picture: ").Append(name).Append("*\n");
                return true;

            case PlaceholderRole.TABLE when index == 0 && slide.Table is { Headers.Count: > 0 } table:
                outline.Append('\n');
                AppendTableRow(outline, table.Headers);
                AppendTableRow(outline, table.Headers.Select(_ => "---").ToList());
                foreach (var row in table.Rows)
                    AppendTableRow(outline, row);

                return true;
        }

        return false;
    }

    /// <summary>
    /// Shows what a slot of a template slide holds, where the deck keeps it as it is.
    /// </summary>
    /// <remarks>
    /// The title is the heading of the slide already. The rows of a table become list items, since
    /// the template slide tells their cells apart only by " | ".
    /// </remarks>
    private static void AppendTemplateContent(StringBuilder outline, TemplateSlideContent content)
    {
        switch (content.Slot.Role)
        {
            case PlaceholderRole.SUBTITLE or PlaceholderRole.LABEL:
                outline.Append('\n').Append('_').Append(content.Text.ReplaceLineEndings(" ")).Append("_\n");
                break;

            case PlaceholderRole.TEXT or PlaceholderRole.TABLE:
                outline.Append('\n');
                foreach (var line in content.Text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
                    outline.Append("- ").Append(line).Append('\n');

                break;

            case PlaceholderRole.PICTURE:
                outline.Append('\n').Append("*Picture of the template*\n");
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