using System.Text;

namespace AIStudio.Tools.PowerPoint;

internal sealed partial class PresentationTemplate
{
    /// <summary>
    /// A slot at least this wide, as a share of the slide, spans the whole width.
    /// </summary>
    private const double FULL_WIDTH_SHARE = 0.7;

    /// <summary>
    /// A label this close to a text, picture or table, as a share of the slide height, belongs to it.
    /// </summary>
    private const double LABEL_DISTANCE_SHARE = 0.05;

    /// <summary>
    /// Describes the layout families for the model, one line per family.
    /// </summary>
    /// <remarks>
    /// The model never sees the slide in front of it, so the catalog says in words what a designer sees
    /// at a glance: where each slot sits, what a label belongs to, and how much text fits. The layout
    /// names stay as the template's author wrote them, since they often say what a layout is meant
    /// for, in whatever language the template speaks.
    /// </remarks>
    /// <returns>The catalog, e.g., <c>- L3 "Inhalt/Content 1": title (up to 2 lines of about 48 characters); text 1 (full width, middle, up to 15 lines of about 85 characters)</c>.</returns>
    public string ToCatalogPrompt()
    {
        var catalog = new StringBuilder();
        foreach (var family in this.Families)
        {
            if (catalog.Length > 0)
                catalog.Append('\n');

            catalog.Append("- ").Append(family.Id).Append(" \"").Append(family.Name).Append('"');
            if (PurposeOf(family.Variants[0].Layout.LayoutType) is { } purpose)
                catalog.Append(" (").Append(purpose).Append(')');

            catalog.Append(": ").AppendJoin("; ", family.Slots.Select(slot => this.DescribeSlot(slot, family.Slots)));
        }

        return catalog.ToString();
    }

    /// <summary>
    /// Describes the template slides the user chose for the model, one line per slide.
    /// </summary>
    /// <remarks>
    /// The model learns what each slide says, so that a new slide does not repeat a kept one, and
    /// which slots an edited slide offers, by the layout it shares with the catalog. Where an edited
    /// slide moved or resized a slot, the entry gives its place and capacity on the slide, since
    /// those of the layout would make the model write too much. Slides the user leaves out are not
    /// mentioned at all. The texts come from the template file, so the caller passes them through
    /// the prompt injection filter.
    /// </remarks>
    /// <param name="uses">What the user chose for each slide of the template, by its number.</param>
    /// <returns>The list, e.g., <c>- Template slide 3, to edit, with the slots of layout L3: title "News"; text 1 (left, middle, up to 6 lines of about 40 characters, on this slide) "AI Studio: / EINSTEIN:"</c>; empty when the user chose no slide.</returns>
    public string ToTemplateSlidesPrompt(IReadOnlyList<TemplateSlideUse> uses)
    {
        var list = new StringBuilder();
        foreach (var slide in this.Slides)
        {
            var use = slide.Number <= uses.Count ? uses[slide.Number - 1] : TemplateSlideUse.OMIT;
            if (use is TemplateSlideUse.OMIT)
                continue;

            if (list.Length > 0)
                list.Append('\n');

            list.Append("- Template slide ").Append(slide.Number).Append(use is TemplateSlideUse.KEEP ? ", to keep unchanged" : ", to edit");
            if (slide.IsImprint)
                list.Append(", the imprint");

            var family = this.FindFamily(slide.FamilyId ?? string.Empty);
            if (use is TemplateSlideUse.EDIT && family is not null)
                list.Append(", with the slots of layout ").Append(family.Id);

            var slots = family?.Slots ?? slide.Contents.Select(content => content.Slot).ToList();
            var entries = use is TemplateSlideUse.EDIT && family is not null
                ? this.DescribeSlotsToEdit(slide, slots)
                : slide.Contents.Select(content => $"{NameOf(content.Slot, slots)} {DescribeContent(content)}").ToList();

            list.Append(": ");
            if (entries.Count == 0)
                list.Append("no content");
            else
                list.AppendJoin("; ", entries);
        }

        return list.ToString();
    }

    /// <summary>
    /// Describes the slots of a slide to edit: what each one holds, and where the slide moved one,
    /// its place and capacity on the slide.
    /// </summary>
    /// <param name="slide">The slide.</param>
    /// <param name="slots">The slots of its layout family.</param>
    /// <returns>One entry per slot which holds content or which the slide moved, in reading order.</returns>
    private List<string> DescribeSlotsToEdit(TemplateSlide slide, IReadOnlyList<TemplateSlot> slots)
    {
        var slotsOnSlide = slots.Select(slide.OnSlide).ToList();
        var entries = new List<string>();
        foreach (var slot in slotsOnSlide)
        {
            var content = slide.Contents.FirstOrDefault(candidate => candidate.Slot.Role == slot.Role && candidate.Slot.Ordinal == slot.Ordinal);
            if (!slide.Moves(slot))
            {
                if (content is not null)
                    entries.Add($"{NameOf(slot, slots)} {DescribeContent(content)}");

                continue;
            }

            entries.Add(content is null
                ? this.DescribeSlot(slot, slotsOnSlide, "on this slide, empty")
                : $"{this.DescribeSlot(slot, slotsOnSlide, "on this slide")} {DescribeContent(content)}");
        }

        return entries;
    }

    private static string DescribeContent(TemplateSlideContent content) => content.Slot.Role is PlaceholderRole.PICTURE
        ? "(a picture)"
        : $"\"{content.Text.ReplaceLineEndings(" / ")}\"";

    /// <summary>
    /// What the layout types a template may state mean; most templates state none.
    /// </summary>
    private static string? PurposeOf(string layoutType) => layoutType switch
    {
        "title" => "title slide",
        "secHead" => "section header",

        _ => null,
    };

    /// <summary>
    /// Describes a slot for the model: its place, and how much text fits.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <param name="slots">All slots of the layout or the slide, which tell what a label belongs to.</param>
    /// <param name="note">What the description adds at the end, e.g., "on this slide"; null for nothing.</param>
    private string DescribeSlot(TemplateSlot slot, IReadOnlyList<TemplateSlot> slots, string? note = null)
    {
        var details = new List<string>();
        switch (slot.Role)
        {
            case PlaceholderRole.LABEL:
                details.Add(this.DescribeLabelPlace(slot, slots));
                break;

            case PlaceholderRole.TEXT or PlaceholderRole.PICTURE or PlaceholderRole.TABLE:
                details.Add(this.DescribePlace(slot.Bounds));
                break;
        }

        if (slot.Role is PlaceholderRole.PICTURE)
            details.Add(OrientationOf(slot.Bounds));

        if (slot.Capacity is { } capacity)
            details.Add(slot.Role is PlaceholderRole.TABLE
                ? $"up to {capacity.Lines} rows"
                : $"up to {capacity.Lines} {(capacity.Lines == 1 ? "line" : "lines")} of about {capacity.CharactersPerLine} characters");

        if (note is not null)
            details.Add(note);

        return $"{NameOf(slot, slots)} ({string.Join(", ", details)})";
    }

    /// <summary>
    /// How the model refers to a slot: a title alone is "title", while several of a kind are numbered.
    /// </summary>
    private static string NameOf(TemplateSlot slot, IReadOnlyList<TemplateSlot> slots)
    {
        var isOnlyOne = slot.Role is (PlaceholderRole.TITLE or PlaceholderRole.SUBTITLE) && slots.Count(other => other.Role == slot.Role) == 1;
        return isOnlyOne ? slot.Role.CatalogName() : $"{slot.Role.CatalogName()} {slot.Ordinal}";
    }

    private string DescribePlace(SlotBounds bounds)
    {
        var horizontal = bounds.Width >= this.SlideWidth * FULL_WIDTH_SHARE
            ? "full width"
            : bounds.CenterX < this.SlideWidth * 0.4
                ? "left"
                : bounds.CenterX > this.SlideWidth * 0.6
                    ? "right"
                    : "center";

        var vertical = bounds.CenterY < this.SlideHeight / 3.0
            ? "top"
            : bounds.CenterY > this.SlideHeight * 2 / 3.0
                ? "bottom"
                : "middle";

        return $"{horizontal}, {vertical}";
    }

    /// <summary>
    /// Says what a label belongs to: the text, picture or table right below or right above it.
    /// </summary>
    /// <remarks>
    /// A label above a text area is its heading, and a label below a picture its source or caption.
    /// A label with nothing next to it is described by its place, e.g., a chapter number in a corner.
    /// </remarks>
    private string DescribeLabelPlace(TemplateSlot label, IReadOnlyList<TemplateSlot> slots)
    {
        var tolerance = this.SlideHeight * SLOT_TOLERANCE;
        var nearDistance = this.SlideHeight * LABEL_DISTANCE_SHARE;
        string? description = null;
        var smallestGap = double.MaxValue;
        foreach (var target in slots)
        {
            if (target.Role is not (PlaceholderRole.TEXT or PlaceholderRole.PICTURE or PlaceholderRole.TABLE) || !target.Bounds.OverlapsHorizontally(label.Bounds))
                continue;

            var gapAbove = target.Bounds.Y - label.Bounds.Bottom;
            if (gapAbove >= -tolerance && gapAbove <= nearDistance && Math.Abs(gapAbove) < smallestGap)
            {
                smallestGap = Math.Abs(gapAbove);
                description = $"above {NameOf(target, slots)}";
            }

            var gapBelow = label.Bounds.Y - target.Bounds.Bottom;
            if (gapBelow >= -tolerance && gapBelow <= nearDistance && Math.Abs(gapBelow) < smallestGap)
            {
                smallestGap = Math.Abs(gapBelow);
                description = $"below {NameOf(target, slots)}";
            }
        }

        return description ?? this.DescribePlace(label.Bounds);
    }

    private static string OrientationOf(SlotBounds bounds)
    {
        var ratio = (double)bounds.Width / Math.Max(1, bounds.Height);
        return ratio switch
        {
            > 1.2 => "landscape",
            < 0.8 => "portrait",

            _ => "square",
        };
    }
}