namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Checks a slide deck the model wrote against the template it was written for.
/// </summary>
/// <remarks>
/// Every issue is written for the model: the slide planner sends the issues back in a repair
/// round, so each one names the slide and the slot, and says what to do instead. What fits the
/// template is checked here and nowhere else, so that the writer of the file can rely on it.
/// </remarks>
internal static class SlideDeckSpecValidator
{
    /// <summary>
    /// How far the estimated text may exceed the estimated capacity of a slot; at least one line more
    /// is always accepted.
    /// </summary>
    /// <remarks>
    /// Both are rough estimates, and the template shrinks text which runs a little long. Only text
    /// clearly beyond the slot is sent back. The extra line keeps a two-line title in a box made for
    /// one from being rejected, which PowerPoint simply sets a little smaller.
    /// </remarks>
    private const double CAPACITY_TOLERANCE = 1.2;

    /// <summary>
    /// A table column narrower than this many characters is hardly readable.
    /// </summary>
    private const int MIN_COLUMN_CHARACTERS = 8;

    /// <summary>
    /// Each list level indents a paragraph by about this many characters.
    /// </summary>
    private const int INDENT_CHARACTERS_PER_LEVEL = 4;

    /// <summary>
    /// Checks a slide deck.
    /// </summary>
    /// <param name="spec">The slide deck.</param>
    /// <param name="template">The template whose layout families the deck uses.</param>
    /// <param name="pictureCount">How many pictures are attached, which the slides may refer to by number.</param>
    /// <param name="expectedSlideCount">How many slides the user asked for, or 0 when the model may choose.</param>
    /// <returns>The issues, written for the model; empty when the deck fits.</returns>
    public static IReadOnlyList<string> Validate(SlideDeckSpec spec, PresentationTemplate template, int pictureCount, int expectedSlideCount)
    {
        var issues = new List<string>();
        if (spec.Title.Length == 0)
            issues.Add("The presentation has no title.");

        if (spec.Slides.Count == 0)
        {
            issues.Add("The presentation has no slides.");
            return issues;
        }

        if (expectedSlideCount > 0 && spec.Slides.Count != expectedSlideCount)
            issues.Add($"The presentation has {spec.Slides.Count} slides, but exactly {expectedSlideCount} were asked for.");

        for (var index = 0; index < spec.Slides.Count; index++)
            ValidateSlide(spec.Slides[index], index + 1, template, pictureCount, issues);

        return issues;
    }

    private static void ValidateSlide(SlideSpec slide, int number, PresentationTemplate template, int pictureCount, List<string> issues)
    {
        var family = template.FindFamily(slide.Layout);
        if (family is null)
        {
            issues.Add($"Slide {number} uses the layout \"{slide.Layout}\", which does not exist. Use one of {string.Join(", ", template.Families.Select(candidate => candidate.Id))}.");
            return;
        }

        ValidateSingleText(slide.Title, PlaceholderRole.TITLE, number, family, issues);
        ValidateSingleText(slide.Subtitle, PlaceholderRole.SUBTITLE, number, family, issues);

        //
        // The n-th text goes into "text n", and an empty entry keeps its slot empty: a model may
        // well want the source below a picture without a heading above the text. Only empty
        // entries at the end take no slot:
        //
        var textCount = CountUpToLastFilled(slide.Texts, text => text.Paragraphs.Count > 0);
        if (ValidateCount(textCount, PlaceholderRole.TEXT, number, family, issues))
        {
            for (var index = 0; index < textCount; index++)
            {
                if (SlotOf(family, PlaceholderRole.TEXT, index + 1)?.Capacity is { } capacity)
                    ValidateCapacity(slide.Texts[index].Paragraphs, capacity, $"Slide {number}: text {index + 1}", issues);
            }
        }

        var labelCount = CountUpToLastFilled(slide.Labels, label => label.Length > 0);
        if (ValidateCount(labelCount, PlaceholderRole.LABEL, number, family, issues))
        {
            for (var index = 0; index < labelCount; index++)
            {
                if (slide.Labels[index].Length > 0 && SlotOf(family, PlaceholderRole.LABEL, index + 1)?.Capacity is { } capacity)
                    ValidateCapacity([new SlideParagraph(slide.Labels[index], 0)], capacity, $"Slide {number}: label {index + 1}", issues);
            }
        }

        if (ValidateCount(slide.Pictures.Count, PlaceholderRole.PICTURE, number, family, issues))
        {
            foreach (var picture in slide.Pictures.Where(candidate => candidate < 1 || candidate > pictureCount))
            {
                issues.Add(pictureCount == 0
                    ? $"Slide {number} refers to picture {picture}, but no pictures are attached. Leave \"pictures\" empty."
                    : $"Slide {number} refers to picture {picture}, but only pictures 1 to {pictureCount} are attached.");
            }
        }

        if (slide.Table is { } table)
            ValidateTable(table, number, family, issues);
    }

    private static void ValidateSingleText(string text, PlaceholderRole role, int number, LayoutFamily family, List<string> issues)
    {
        if (text.Length == 0)
            return;

        var slot = SlotOf(family, role, 1);
        if (slot is null)
        {
            issues.Add($"Slide {number} has a {role.CatalogName()}, but layout {family.Id} has none. Choose a layout with a {role.CatalogName()}, or leave it empty.");
            return;
        }

        if (slot.Capacity is { } capacity)
            ValidateCapacity([new SlideParagraph(text, 0)], capacity, $"Slide {number}: the {role.CatalogName()}", issues);
    }

    /// <summary>
    /// Checks that the layout has a slot for every piece of content of one role.
    /// </summary>
    /// <returns>Whether the content fits, so that its slots can be checked one by one.</returns>
    private static bool ValidateCount(int count, PlaceholderRole role, int number, LayoutFamily family, List<string> issues)
    {
        var slots = family.Slots.Count(slot => slot.Role == role);
        if (count <= slots)
            return true;

        issues.Add(slots == 0
            ? $"Slide {number} has {count} {PluralOf(role, count)}, but layout {family.Id} has no {role.CatalogName()}. Choose a layout which has one."
            : $"Slide {number} has {count} {PluralOf(role, count)}, but layout {family.Id} has only {slots}. Merge them, or choose a layout with more.");

        return false;
    }

    private static void ValidateCapacity(IEnumerable<SlideParagraph> paragraphs, TextCapacity capacity, string what, List<string> issues)
    {
        var lines = paragraphs.Sum(paragraph => LinesOf(paragraph, capacity.CharactersPerLine));
        if (lines > AcceptedLinesOf(capacity))
            issues.Add($"{what} needs about {lines} lines, but only about {capacity.Lines} fit. Shorten it, or spread the content over more slides.");
    }

    private static void ValidateTable(SlideTableSpec table, int number, LayoutFamily family, List<string> issues)
    {
        if (SlotOf(family, PlaceholderRole.TABLE, 1) is not { } slot)
        {
            issues.Add($"Slide {number} has a table, but layout {family.Id} has none. Choose a layout with a table.");
            return;
        }

        var columns = table.Headers.Count;
        if (columns == 0)
        {
            issues.Add($"Slide {number}: the table has no headers. Name every column.");
            return;
        }

        for (var index = 0; index < table.Rows.Count; index++)
            if (table.Rows[index].Count != columns)
                issues.Add($"Slide {number}: row {index + 1} of the table has {table.Rows[index].Count} cells, but the table has {columns} columns.");

        if (slot.Capacity is not { } capacity)
            return;

        var maxColumns = Math.Max(1, capacity.CharactersPerLine / MIN_COLUMN_CHARACTERS);
        if (columns > maxColumns)
            issues.Add($"Slide {number}: the table has {columns} columns, but only about {maxColumns} fit. Leave out or merge columns.");

        // The header takes a row as well:
        var rows = table.Rows.Count + 1;
        if (rows > AcceptedLinesOf(capacity))
            issues.Add($"Slide {number}: the table has {rows} rows including the header, but only about {capacity.Lines} fit. Split it across slides.");
    }

    private static TemplateSlot? SlotOf(LayoutFamily family, PlaceholderRole role, int ordinal) => family.Slots.FirstOrDefault(slot => slot.Role == role && slot.Ordinal == ordinal);

    /// <summary>
    /// How many slots the entries take: all of them up to the last one with content.
    /// </summary>
    private static int CountUpToLastFilled<T>(IReadOnlyList<T> entries, Func<T, bool> isFilled)
    {
        for (var index = entries.Count - 1; index >= 0; index--)
            if (isFilled(entries[index]))
                return index + 1;

        return 0;
    }

    private static double AcceptedLinesOf(TextCapacity capacity) => Math.Max(capacity.Lines * CAPACITY_TOLERANCE, capacity.Lines + 1);

    private static int LinesOf(SlideParagraph paragraph, int charactersPerLine)
    {
        var width = Math.Max(MIN_COLUMN_CHARACTERS, charactersPerLine - paragraph.Level * INDENT_CHARACTERS_PER_LEVEL);
        return Math.Max(1, (int)Math.Ceiling(paragraph.Text.Length / (double)width));
    }

    private static string PluralOf(PlaceholderRole role, int count) => count == 1 ? role.CatalogName() : $"{role.CatalogName()}s";
}