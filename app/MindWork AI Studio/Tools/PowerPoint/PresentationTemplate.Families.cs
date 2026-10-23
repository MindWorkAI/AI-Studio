namespace AIStudio.Tools.PowerPoint;

internal sealed partial class PresentationTemplate
{
    /// <summary>
    /// Two layouts offer the same slots when every edge of every slot differs by no more than this
    /// share of the slide.
    /// </summary>
    private const double SLOT_TOLERANCE = 0.01;

    /// <summary>
    /// Groups the layouts with room for content into families of variants.
    /// </summary>
    /// <remarks>
    /// Two layouts are variants of each other when they offer the same slots at the same places and
    /// their names differ in one word only. Same slots alone are not enough: "Table 1" and "Table 2"
    /// may well look alike to the reading code and still mean different things to the template's
    /// author.
    ///
    /// Templates put that word in front ("A Content 1", "B Content 1") or at the end ("Title blue",
    /// "Title green"). Which one a template uses is decided for the whole template, by the reading
    /// which finds more variants. Within a single template, a word in front and a word at the end
    /// never mean the same thing.
    /// </remarks>
    private static GroupedLayouts GroupIntoFamilies(IReadOnlyList<TemplateLayout> layouts, long slideWidth, long slideHeight)
    {
        var usableLayouts = layouts.Where(layout => layout.Slots.Any(slot => slot.Role.CarriesContent())).ToList();
        var byFirstWord = Group(usableLayouts, VariantWord.FIRST, slideWidth, slideHeight);
        var byLastWord = Group(usableLayouts, VariantWord.LAST, slideWidth, slideHeight);
        var groups = CountVariants(byLastWord) > CountVariants(byFirstWord) ? byLastWord : byFirstWord;

        var families = new List<LayoutFamily>(groups.Count);
        var variantLabels = new List<string>();
        foreach (var group in groups)
        {
            var id = $"L{families.Count + 1}";
            if (group.Variants.Count == 1)
            {
                // Alone, the layout keeps its full name and needs no label:
                families.Add(new LayoutFamily(id, group.Variants[0].Layout.Name, [new LayoutVariant(null, group.Variants[0].Layout)]));
                continue;
            }

            families.Add(new LayoutFamily(id, group.Stem, group.Variants));
            foreach (var variant in group.Variants)
                if (variant.Label is { } label && !variantLabels.Contains(label))
                    variantLabels.Add(label);
        }

        return new GroupedLayouts(families, variantLabels);
    }

    private static List<FamilyCandidate> Group(IReadOnlyList<TemplateLayout> layouts, VariantWord variantWord, long slideWidth, long slideHeight)
    {
        var candidates = new List<FamilyCandidate>();
        foreach (var layout in layouts)
        {
            if (!TrySplitName(layout.Name, variantWord, out var label, out var stem))
            {
                candidates.Add(new FamilyCandidate(layout.Name, [new LayoutVariant(null, layout)]));
                continue;
            }

            var family = candidates.FirstOrDefault(candidate =>
                candidate.Stem == stem &&
                candidate.Variants[0].Label is not null &&
                candidate.Variants.All(variant => variant.Label != label) &&
                HaveSameSlots(candidate.Variants[0].Layout, layout, slideWidth, slideHeight));

            if (family is null)
                candidates.Add(new FamilyCandidate(stem, [new LayoutVariant(label, layout)]));
            else
                family.Variants.Add(new LayoutVariant(label, layout));
        }

        return candidates;
    }

    private static bool TrySplitName(string name, VariantWord variantWord, out string label, out string stem)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length < 2)
        {
            label = string.Empty;
            stem = string.Empty;
            return false;
        }

        if (variantWord is VariantWord.FIRST)
        {
            label = words[0];
            stem = string.Join(' ', words[1..]);
        }
        else
        {
            label = words[^1];
            stem = string.Join(' ', words[..^1]);
        }

        return true;
    }

    private static bool HaveSameSlots(TemplateLayout first, TemplateLayout second, long slideWidth, long slideHeight)
    {
        if (first.Slots.Count != second.Slots.Count)
            return false;

        var horizontalTolerance = (long)(slideWidth * SLOT_TOLERANCE);
        var verticalTolerance = (long)(slideHeight * SLOT_TOLERANCE);
        return first.Slots.Zip(second.Slots).All(pair => pair.First.Role == pair.Second.Role && pair.First.Bounds.IsCloseTo(pair.Second.Bounds, horizontalTolerance, verticalTolerance));
    }

    /// <summary>
    /// How many layouts a grouping puts into families of more than one variant.
    /// </summary>
    private static int CountVariants(IEnumerable<FamilyCandidate> candidates) => candidates.Where(candidate => candidate.Variants.Count > 1).Sum(candidate => candidate.Variants.Count);

    /// <summary>
    /// Which word of a layout name tells the variants apart.
    /// </summary>
    private enum VariantWord
    {
        FIRST,
        LAST,
    }

    /// <summary>
    /// A family while it is being gathered.
    /// </summary>
    /// <param name="Stem">The name without the word which tells the variants apart.</param>
    /// <param name="Variants">The variants found so far.</param>
    private sealed record FamilyCandidate(string Stem, List<LayoutVariant> Variants);

    /// <summary>
    /// The layouts with room for content, grouped into families of variants.
    /// </summary>
    /// <param name="Families">The families, in the order of the template.</param>
    /// <param name="VariantLabels">The labels which tell the variants apart; empty when the template carries every layout once.</param>
    private sealed record GroupedLayouts(IReadOnlyList<LayoutFamily> Families, IReadOnlyList<string> VariantLabels);
}