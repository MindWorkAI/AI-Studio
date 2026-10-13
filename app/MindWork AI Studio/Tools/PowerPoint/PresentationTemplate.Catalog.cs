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
    /// What the layout types a template may state mean; most templates state none.
    /// </summary>
    private static string? PurposeOf(string layoutType) => layoutType switch
    {
        "title" => "title slide",
        "secHead" => "section header",

        _ => null,
    };

    private string DescribeSlot(TemplateSlot slot, IReadOnlyList<TemplateSlot> slots)
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