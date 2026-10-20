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
    /// An element overlaps a slot when they share at least this share of the smaller one's area.
    /// </summary>
    /// <remarks>
    /// Edges which merely touch, or a text box reaching a little into a slot, do not count: what the
    /// model should hear about is an element which a filled slot would cover, or which would cover it.
    /// </remarks>
    private const double OVERLAP_AREA_SHARE = 0.2;

    /// <summary>
    /// An element covering at least this share of the slide is described as a background.
    /// </summary>
    private const double BACKGROUND_AREA_SHARE = 0.5;

    /// <summary>
    /// How much of the text of an element the model reads, which is enough to tell what it is.
    /// </summary>
    private const int MAX_EXCERPT_CHARACTERS = 80;

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
    /// those of the layout would make the model write too much. A copy of a slot, e.g., a second
    /// heading with its text, is offered as one more slot of its kind, with its place and capacity
    /// as well. An edited slide also lists what it carries besides its slots, which the model may
    /// remove. Slides the user leaves out are not
    /// mentioned at all. The texts come from the template file, so the caller passes them through
    /// the prompt injection filter.
    /// </remarks>
    /// <param name="uses">What the user chose for each slide of the template, by its number.</param>
    /// <returns>The list, e.g., <c>- Template slide 3, to edit, with the slots of layout L3: title "News"; text 1 (left, middle, up to 6 lines of about 40 characters, on this slide) "AI Studio: / EINSTEIN:"; element 1 (picture, right, middle)</c>; empty when the user chose no slide.</returns>
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

            var slots = family is not null ? slide.Slots : slide.Contents.Select(content => content.Slot).ToList();
            var entries = use is TemplateSlideUse.EDIT && family is not null
                ? this.DescribeSlideToEdit(slide)
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
    /// Describes a slide to edit: what each slot holds, and where the slide moved one or carries a
    /// copy of one, its place and capacity on the slide; then every element the slide carries
    /// besides its slots.
    /// </summary>
    /// <param name="slide">The slide.</param>
    /// <returns>One entry per slot which holds content, which the slide moved, or which a copy offers, in reading order, and one per element.</returns>
    private List<string> DescribeSlideToEdit(TemplateSlide slide)
    {
        var slotsOnSlide = slide.Slots;
        var entries = new List<string>();
        foreach (var slot in slotsOnSlide)
        {
            var content = slide.Contents.FirstOrDefault(candidate => candidate.Slot.Role == slot.Role && candidate.Slot.Ordinal == slot.Ordinal);
            if (!slide.Moves(slot) && slide.FindCopy(slot.Role, slot.Ordinal) is null)
            {
                if (content is not null)
                    entries.Add($"{NameOf(slot, slotsOnSlide)} {DescribeContent(content)}");

                continue;
            }

            entries.Add(content is null
                ? this.DescribeSlot(slot, slotsOnSlide, "on this slide, empty")
                : $"{this.DescribeSlot(slot, slotsOnSlide, "on this slide")} {DescribeContent(content)}");
        }

        entries.AddRange(slide.Elements.Select(element => this.DescribeElement(element, slotsOnSlide)));
        return entries;
    }

    /// <summary>
    /// Describes an element of a slide to edit: its kind, its place, the slots it overlaps, and the
    /// beginning of its text.
    /// </summary>
    /// <remarks>
    /// The slots include those the slide leaves out, at the place of the layout: filling one of them
    /// puts a new shape there, on top of whatever the slide shows in its place.
    /// </remarks>
    /// <param name="element">The element.</param>
    /// <param name="slotsOnSlide">The slots of the slide, where the slide puts them.</param>
    /// <returns>The entry, e.g., <c>element 2 (text box, right, middle, overlaps picture 1) "Image source"</c>.</returns>
    private string DescribeElement(TemplateSlideElement element, IReadOnlyList<TemplateSlot> slotsOnSlide)
    {
        var details = new List<string> { element.Kind.CatalogName() };
        if (element.Bounds is { } bounds)
        {
            if (bounds.Area >= (double)this.SlideWidth * this.SlideHeight * BACKGROUND_AREA_SHARE)
                details.Add("covering most of the slide");
            else
            {
                details.Add(this.DescribePlace(bounds));
                var overlapped = slotsOnSlide.Where(slot => Overlaps(bounds, slot.Bounds)).Select(slot => NameOf(slot, slotsOnSlide)).ToList();
                if (overlapped.Count > 0)
                    details.Add($"overlaps {string.Join(" and ", overlapped)}");
            }
        }

        var description = $"{element.Name} ({string.Join(", ", details)})";
        if (element.Text.Length == 0)
            return description;

        var text = element.Text.ReplaceLineEndings(" / ");
        if (text.Length > MAX_EXCERPT_CHARACTERS)
            text = $"{text[..MAX_EXCERPT_CHARACTERS].TrimEnd()}…";

        return $"{description} \"{text}\"";
    }

    /// <summary>
    /// Whether an element and a slot share enough of their areas to be in each other's way.
    /// </summary>
    private static bool Overlaps(SlotBounds element, SlotBounds slot)
    {
        // A straight line has no area, so it counts as one EMU thick:
        var thick = element with { Width = Math.Max(1, element.Width), Height = Math.Max(1, element.Height) };
        var shared = thick.IntersectionArea(slot);
        return shared > 0 && shared >= OVERLAP_AREA_SHARE * Math.Min(thick.Area, slot.Area);
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
        if (this.NeighborOf(label, slots, true) is { } below)
            return $"above {NameOf(below, slots)}";

        return this.NeighborOf(label, slots, false) is { } above
            ? $"below {NameOf(above, slots)}"
            : this.DescribePlace(label.Bounds);
    }

    /// <summary>
    /// The text, picture or table right below or right above a label, which the label belongs to.
    /// </summary>
    /// <remarks>
    /// A label right below one text and right above the next one heads the next one: on a slide
    /// stacking headings with their texts, a heading sits closer to the text before it than to its
    /// own, yet belongs to the text it introduces. Hence the caller asks for what lies below a label
    /// first.
    /// </remarks>
    /// <param name="label">The label.</param>
    /// <param name="slots">All slots of the layout or the slide.</param>
    /// <param name="below">Whether to look below the label rather than above it.</param>
    /// <returns>The nearest slot on that side; null when there is none close enough.</returns>
    private TemplateSlot? NeighborOf(TemplateSlot label, IReadOnlyList<TemplateSlot> slots, bool below)
    {
        var tolerance = this.SlideHeight * SLOT_TOLERANCE;
        var nearDistance = this.SlideHeight * LABEL_DISTANCE_SHARE;
        TemplateSlot? neighbor = null;
        var smallestGap = double.MaxValue;
        foreach (var target in slots)
        {
            if (target.Role is not (PlaceholderRole.TEXT or PlaceholderRole.PICTURE or PlaceholderRole.TABLE) || !target.Bounds.OverlapsHorizontally(label.Bounds))
                continue;

            var gap = below ? target.Bounds.Y - label.Bounds.Bottom : label.Bounds.Y - target.Bounds.Bottom;
            if (gap >= -tolerance && gap <= nearDistance && Math.Abs(gap) < smallestGap)
            {
                smallestGap = Math.Abs(gap);
                neighbor = target;
            }
        }

        return neighbor;
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