using System.Globalization;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One slide a template carries, which the presentation may keep as it is or have the model edit.
/// </summary>
/// <remarks>
/// A template made for PowerPoint's "New" dialog carries samples or hints, which a presentation
/// leaves out. A presentation used as a template, though, often carries slides worth keeping: the
/// title slide, a picture opening each chapter, the closing slide. Which ones the presentation
/// keeps is up to the user; what a slide holds tells the model what it is about.
/// </remarks>
/// <param name="Number">The number of the slide in the template, starting at 1, as PowerPoint counts it; the model refers to the slide by it.</param>
/// <param name="PartUri">Where the slide is stored inside the file, which identifies it when the presentation is written.</param>
/// <param name="LayoutPartUri">Where the layout of the slide is stored; null when the slide names none.</param>
/// <param name="FamilyId">The ID of the layout family the slide is based on; null when its layout belongs to none, e.g., because it has no room for content.</param>
/// <param name="Title">The title of the slide on a single line; empty when it has none.</param>
/// <param name="Contents">What the slots of the slide hold, in the order of <paramref name="Slots"/>, a table which is no placeholder and the copies of slots included. Slots left empty are missing.</param>
/// <param name="MovedSlots">The slots whose placeholders the slide moved or resized, e.g., to make room for a picture of its own, with their place and capacity on the slide; empty when every placeholder stays where the layout puts it.</param>
/// <param name="Slots">The slots the slide offers, in reading order: those of its layout, where the slide puts them, and the copies the slide carries; empty when the layout of the slide is unknown.</param>
/// <param name="SlotCopies">The shapes which repeat a slot of the layout, e.g., a second heading with its text, numbered after the slots of the layout; empty when the slide carries none.</param>
/// <param name="Elements">What the slide carries besides the slots of its layout and their copies, from back to front.</param>
/// <param name="IsHidden">Whether the slide is hidden in the slide show.</param>
/// <param name="IsImprint">Whether the slide is an imprint, which closes the presentation when the user keeps or edits it.</param>
internal sealed record TemplateSlide(
    int Number,
    Uri PartUri,
    Uri? LayoutPartUri,
    string? FamilyId,
    string Title,
    IReadOnlyList<TemplateSlideContent> Contents,
    IReadOnlyList<TemplateSlot> MovedSlots,
    IReadOnlyList<TemplateSlot> Slots,
    IReadOnlyList<TemplateSlotCopy> SlotCopies,
    IReadOnlyList<TemplateSlideElement> Elements,
    bool IsHidden,
    bool IsImprint)
{
    /// <summary>
    /// Finds an element of the slide by the name the model used for it.
    /// </summary>
    /// <param name="name">The name, e.g., "element 2"; a bare number is understood as well.</param>
    /// <returns>The element, or null when the slide has no element of that name.</returns>
    public TemplateSlideElement? FindElement(string name)
    {
        var number = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) switch
        {
            [var single] => single,
            [var word, var single] when string.Equals(word, "element", StringComparison.OrdinalIgnoreCase) => single,

            _ => null,
        };

        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? this.Elements.FirstOrDefault(element => element.Number == parsed)
            : null;
    }

    /// <summary>
    /// Where a slot of the slide's layout family sits on this slide, and how much fits there.
    /// </summary>
    /// <remarks>
    /// The slot is matched by its role and number, which every variant of a family shares, so this
    /// works whichever color the slide takes in the presentation.
    /// </remarks>
    /// <param name="slot">A slot of the family, or of any of its layouts.</param>
    /// <returns>The slot as the slide moved it, or the given one when the slide keeps it where the layout puts it.</returns>
    public TemplateSlot OnSlide(TemplateSlot slot) => this.MovedSlots.FirstOrDefault(moved => moved.Role == slot.Role && moved.Ordinal == slot.Ordinal) ?? slot;

    /// <summary>
    /// Whether the slide moved or resized the placeholder of a slot.
    /// </summary>
    public bool Moves(TemplateSlot slot) => this.MovedSlots.Any(moved => moved.Role == slot.Role && moved.Ordinal == slot.Ordinal);

    /// <summary>
    /// Finds the copy which offers a slot of the slide.
    /// </summary>
    /// <param name="role">The role of the slot.</param>
    /// <param name="ordinal">The number of the slot among the slots of its role, starting at 1.</param>
    /// <returns>The copy, or null when the slot is one of the layout, or the slide has none of that number.</returns>
    public TemplateSlotCopy? FindCopy(PlaceholderRole role, int ordinal) => this.SlotCopies.FirstOrDefault(copy => copy.Slot.Role == role && copy.Slot.Ordinal == ordinal);
}