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
    /// <param name="templateSlideUses">What the user chose for each slide of the template, by its number; null or empty when the deck uses no template slides.</param>
    /// <returns>The issues, written for the model; empty when the deck fits.</returns>
    public static IReadOnlyList<string> Validate(SlideDeckSpec spec, PresentationTemplate template, int pictureCount, int expectedSlideCount, IReadOnlyList<TemplateSlideUse>? templateSlideUses = null)
    {
        var uses = templateSlideUses ?? [];
        var issues = new List<string>();
        if (spec.Title.Length == 0)
            issues.Add("The presentation has no title.");

        if (spec.Slides.Count == 0)
        {
            issues.Add("The presentation has no slides.");
            return issues;
        }

        // The imprint of the template closes the presentation without being part of its content, so it does not count:
        var slideCount = spec.Slides.Count(slide => !template.IsImprint(slide));
        if (expectedSlideCount > 0 && slideCount != expectedSlideCount)
            issues.Add($"The presentation has {slideCount} slides, but exactly {expectedSlideCount} were asked for.");

        for (var index = 0; index < spec.Slides.Count; index++)
            ValidateSlide(spec.Slides[index], index + 1, template, uses, pictureCount, issues);

        ValidateTemplateSlides(spec, template, uses, issues);
        return issues;
    }

    /// <summary>
    /// Says whether a slide can be written at all.
    /// </summary>
    /// <remarks>
    /// A slide on a layout the template does not have, or one referring to a template slide the user
    /// did not choose, cannot be written. Any other issue, such as a text which runs long, leaves a
    /// slide worth writing and looking at.
    /// </remarks>
    /// <param name="slide">The slide.</param>
    /// <param name="template">The template whose layout families and slides the deck uses.</param>
    /// <param name="templateSlideUses">What the user chose for each slide of the template, by its number.</param>
    public static bool CanWrite(SlideSpec slide, PresentationTemplate template, IReadOnlyList<TemplateSlideUse> templateSlideUses) => slide.TemplateSlide is { } number
        ? UseOf(templateSlideUses, number) is not TemplateSlideUse.OMIT && template.FindSlide(number) is not null && (!slide.HasContent || template.FamilyOf(slide) is not null)
        : template.FindFamily(slide.Layout) is not null;

    private static void ValidateSlide(SlideSpec slide, int number, PresentationTemplate template, IReadOnlyList<TemplateSlideUse> uses, int pictureCount, List<string> issues)
    {
        if (slide.TemplateSlide is { } templateSlideNumber)
        {
            ValidateTemplateSlideEntry(slide, number, templateSlideNumber, template, uses, pictureCount, issues);
            return;
        }

        if (slide.Removals.Count > 0)
            issues.Add($"Slide {number} is a new slide, which has nothing to remove. Leave out \"remove\".");

        var family = template.FindFamily(slide.Layout);
        if (family is null)
        {
            issues.Add($"Slide {number} uses the layout \"{slide.Layout}\", which does not exist. Use one of {string.Join(", ", template.Families.Select(candidate => candidate.Id))}.");
            return;
        }

        ValidateContent(slide, number, family.Id, family.Slots, pictureCount, issues);
    }

    /// <summary>
    /// Checks a slide which refers to a template slide: a kept one takes no content, an edited one
    /// needs some, and it has to fit the slots of the template slide where the slide puts them.
    /// </summary>
    private static void ValidateTemplateSlideEntry(SlideSpec slide, int number, int templateSlideNumber, PresentationTemplate template, IReadOnlyList<TemplateSlideUse> uses, int pictureCount, List<string> issues)
    {
        var use = UseOf(uses, templateSlideNumber);
        if (use is TemplateSlideUse.OMIT || template.FindSlide(templateSlideNumber) is not { } templateSlide)
        {
            var offered = OfferedNumbersOf(uses, template);
            issues.Add(offered.Count > 0
                ? $"Slide {number} refers to template slide {templateSlideNumber}, which is not offered. Refer only to template slides {string.Join(", ", offered)}."
                : $"Slide {number} refers to template slide {templateSlideNumber}, but no template slides are offered. Leave out \"template_slide\".");

            return;
        }

        if (use is TemplateSlideUse.KEEP)
        {
            if (slide.HasContent)
                issues.Add($"Slide {number} keeps template slide {templateSlideNumber} unchanged, so it takes no content. Write only \"template_slide\", and \"starts_chapter\" where it opens a chapter.");

            return;
        }

        if (!slide.HasContent)
        {
            issues.Add($"Slide {number} is to edit template slide {templateSlideNumber}, but writes no content. Write the new content of the slide, or what it removes.");
            return;
        }

        if (template.FamilyOf(slide) is not { } family)
        {
            issues.Add($"Template slide {templateSlideNumber} has no slots to fill. Keep it unchanged: write only \"template_slide\".");
            return;
        }

        ValidateContent(slide, number, family.Id, family.Slots.Select(templateSlide.OnSlide).ToList(), pictureCount, issues);
        ValidateRemovals(slide, number, templateSlide, family, issues);
    }

    /// <summary>
    /// Checks what an edited slide removes from its template slide: slots and elements the slide
    /// has, and neither the title nor a slot the slide fills anew.
    /// </summary>
    /// <remarks>
    /// The title stays, since it names the slide in the outline and in the sections of the
    /// presentation; an edited slide changes it by writing a new one.
    /// </remarks>
    private static void ValidateRemovals(SlideSpec slide, int number, TemplateSlide templateSlide, LayoutFamily family, List<string> issues)
    {
        foreach (var name in slide.Removals)
        {
            if (templateSlide.FindElement(name) is not null)
                continue;

            if (!PlaceholderRoleExtensions.TryParseSlotName(name, out var role, out var ordinal) || SlotOf(family.Slots, role, ordinal) is null)
            {
                issues.Add($"Slide {number} removes \"{name}\", which template slide {templateSlide.Number} does not have. Remove only the slots and elements its entry in TEMPLATE_SLIDES names, e.g., \"element 1\" or \"label 1\".");
                continue;
            }

            if (role is PlaceholderRole.TITLE)
                issues.Add($"Slide {number} removes the title of template slide {templateSlide.Number}. Write a new title instead, or keep the old one.");
            else if (slide.Fills(role, ordinal))
                issues.Add($"Slide {number} fills {name} and removes it as well. Either fill it or remove it.");
        }
    }

    /// <summary>
    /// Checks that every template slide the user chose appears exactly once, in the order of the
    /// template, and that the imprint of the template closes the presentation.
    /// </summary>
    /// <remarks>
    /// References to slides the user did not choose are reported with the slide which makes them.
    /// </remarks>
    private static void ValidateTemplateSlides(SlideDeckSpec spec, PresentationTemplate template, IReadOnlyList<TemplateSlideUse> uses, List<string> issues)
    {
        var references = spec.Slides
            .Select(slide => slide.TemplateSlide)
            .OfType<int>()
            .Where(number => UseOf(uses, number) is not TemplateSlideUse.OMIT && template.FindSlide(number) is not null)
            .ToList();

        foreach (var number in OfferedNumbersOf(uses, template))
        {
            var count = references.Count(reference => reference == number);
            if (count == 0)
            {
                issues.Add(UseOf(uses, number) is TemplateSlideUse.KEEP
                    ? $"Template slide {number} is missing. Add it at its place as {{ \"template_slide\": {number} }}."
                    : $"Template slide {number} is missing. Add it at its place with \"template_slide\": {number} and its new content.");
            }
            else if (count > 1)
                issues.Add($"Template slide {number} appears {count} times. Use every template slide exactly once.");
        }

        var order = references.Distinct().ToList();
        if (!order.SequenceEqual(order.Order()))
            issues.Add($"The template slides are out of order. Keep them in the order {string.Join(", ", order.Order())}, and put new slides before, between, or after them.");

        // The imprint of the template closes the presentation:
        for (var index = 0; index < spec.Slides.Count - 1; index++)
        {
            if (spec.Slides[index] is { TemplateSlide: { } number } slide && template.IsImprint(slide) && UseOf(uses, number) is not TemplateSlideUse.OMIT)
                issues.Add($"Slide {index + 1} is the imprint of the template, which closes the presentation. Move it to the end.");
        }
    }

    /// <summary>
    /// The numbers of the template slides the user chose to keep or to edit, in the order of the template.
    /// </summary>
    private static List<int> OfferedNumbersOf(IReadOnlyList<TemplateSlideUse> uses, PresentationTemplate template) => Enumerable.Range(1, uses.Count)
        .Where(number => uses[number - 1] is not TemplateSlideUse.OMIT && template.FindSlide(number) is not null)
        .ToList();

    private static TemplateSlideUse UseOf(IReadOnlyList<TemplateSlideUse> uses, int number) => number >= 1 && number <= uses.Count ? uses[number - 1] : TemplateSlideUse.OMIT;

    /// <summary>
    /// Checks that the content of a slide fits the slots it goes into.
    /// </summary>
    /// <param name="slide">The slide.</param>
    /// <param name="number">The number of the slide in the deck.</param>
    /// <param name="layoutId">The ID of the layout family, which the issues name.</param>
    /// <param name="slots">The slots, as the layout offers them or as a template slide moved them.</param>
    /// <param name="pictureCount">How many pictures are attached.</param>
    /// <param name="issues">The issues found so far; new ones are added.</param>
    private static void ValidateContent(SlideSpec slide, int number, string layoutId, IReadOnlyList<TemplateSlot> slots, int pictureCount, List<string> issues)
    {
        ValidateSingleText(slide.Title, PlaceholderRole.TITLE, number, layoutId, slots, issues);
        ValidateSingleText(slide.Subtitle, PlaceholderRole.SUBTITLE, number, layoutId, slots, issues);

        //
        // The n-th text goes into "text n", and an empty entry keeps its slot empty: a model may
        // well want the source below a picture without a heading above the text. Only empty
        // entries at the end take no slot:
        //
        var textCount = CountUpToLastFilled(slide.Texts, text => text.Paragraphs.Count > 0);
        if (ValidateCount(textCount, PlaceholderRole.TEXT, number, layoutId, slots, issues))
        {
            for (var index = 0; index < textCount; index++)
            {
                if (SlotOf(slots, PlaceholderRole.TEXT, index + 1)?.Capacity is { } capacity)
                    ValidateCapacity(slide.Texts[index].Paragraphs, capacity, $"Slide {number}: text {index + 1}", issues);
            }
        }

        var labelCount = CountUpToLastFilled(slide.Labels, label => label.Length > 0);
        if (ValidateCount(labelCount, PlaceholderRole.LABEL, number, layoutId, slots, issues))
        {
            for (var index = 0; index < labelCount; index++)
            {
                if (slide.Labels[index].Length > 0 && SlotOf(slots, PlaceholderRole.LABEL, index + 1)?.Capacity is { } capacity)
                    ValidateCapacity([new SlideParagraph(slide.Labels[index], 0)], capacity, $"Slide {number}: label {index + 1}", issues);
            }
        }

        if (ValidateCount(slide.Pictures.Count, PlaceholderRole.PICTURE, number, layoutId, slots, issues))
        {
            foreach (var picture in slide.Pictures.Where(candidate => candidate < 1 || candidate > pictureCount))
            {
                issues.Add(pictureCount == 0
                    ? $"Slide {number} refers to picture {picture}, but no pictures are attached. Leave \"pictures\" empty."
                    : $"Slide {number} refers to picture {picture}, but only pictures 1 to {pictureCount} are attached.");
            }
        }

        if (slide.Table is { } table)
            ValidateTable(table, number, layoutId, slots, issues);
    }

    private static void ValidateSingleText(string text, PlaceholderRole role, int number, string layoutId, IReadOnlyList<TemplateSlot> slots, List<string> issues)
    {
        if (text.Length == 0)
            return;

        var slot = SlotOf(slots, role, 1);
        if (slot is null)
        {
            issues.Add($"Slide {number} has a {role.CatalogName()}, but layout {layoutId} has none. Choose a layout with a {role.CatalogName()}, or leave it empty.");
            return;
        }

        if (slot.Capacity is { } capacity)
            ValidateCapacity([new SlideParagraph(text, 0)], capacity, $"Slide {number}: the {role.CatalogName()}", issues);
    }

    /// <summary>
    /// Checks that the layout has a slot for every piece of content of one role.
    /// </summary>
    /// <returns>Whether the content fits, so that its slots can be checked one by one.</returns>
    private static bool ValidateCount(int count, PlaceholderRole role, int number, string layoutId, IReadOnlyList<TemplateSlot> slots, List<string> issues)
    {
        var slotCount = slots.Count(slot => slot.Role == role);
        if (count <= slotCount)
            return true;

        issues.Add(slotCount == 0
            ? $"Slide {number} has {count} {PluralOf(role, count)}, but layout {layoutId} has no {role.CatalogName()}. Choose a layout which has one."
            : $"Slide {number} has {count} {PluralOf(role, count)}, but layout {layoutId} has only {slotCount}. Merge them, or choose a layout with more.");

        return false;
    }

    private static void ValidateCapacity(IEnumerable<SlideParagraph> paragraphs, TextCapacity capacity, string what, List<string> issues)
    {
        var lines = paragraphs.Sum(paragraph => LinesOf(paragraph, capacity.CharactersPerLine));
        if (lines > AcceptedLinesOf(capacity))
            issues.Add($"{what} needs about {lines} lines, but only about {capacity.Lines} fit. Shorten it, or spread the content over more slides.");
    }

    private static void ValidateTable(SlideTableSpec table, int number, string layoutId, IReadOnlyList<TemplateSlot> slots, List<string> issues)
    {
        if (SlotOf(slots, PlaceholderRole.TABLE, 1) is not { } slot)
        {
            issues.Add($"Slide {number} has a table, but layout {layoutId} has none. Choose a layout with a table.");
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

    private static TemplateSlot? SlotOf(IReadOnlyList<TemplateSlot> slots, PlaceholderRole role, int ordinal) => slots.FirstOrDefault(slot => slot.Role == role && slot.Ordinal == ordinal);

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