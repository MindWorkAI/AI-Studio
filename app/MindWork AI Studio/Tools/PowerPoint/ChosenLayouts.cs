using System.Text;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// The layouts the user chose for kinds of slides, found in one template.
/// </summary>
/// <remarks>
/// The user chooses layouts by their names, which stay valid across sessions, computers, and new
/// versions of a template, while the IDs the model uses follow from the order of the layouts. A name
/// the template does not have counts as no choice, just as a kind of slide the user left to the
/// model. The rules for the model name the layouts by their IDs only, so that no text of the file
/// reaches the system prompt.
/// </remarks>
/// <param name="Families">The family chosen for each kind of slide; a kind without a choice is missing.</param>
internal sealed record ChosenLayouts(IReadOnlyDictionary<LayoutPurpose, LayoutFamily> Families)
{
    /// <summary>
    /// No choices: the model decides every layout on its own.
    /// </summary>
    public static readonly ChosenLayouts NONE = new(new Dictionary<LayoutPurpose, LayoutFamily>());

    /// <summary>
    /// Finds the chosen layouts in a template.
    /// </summary>
    /// <param name="template">The template the slides are planned for.</param>
    /// <param name="layoutNames">The name of the layout chosen for each kind of slide.</param>
    /// <returns>The choices the template can serve.</returns>
    public static ChosenLayouts Of(PresentationTemplate template, IReadOnlyDictionary<LayoutPurpose, string> layoutNames)
    {
        var families = new Dictionary<LayoutPurpose, LayoutFamily>();
        foreach (var (purpose, name) in layoutNames)
            if (template.FindFamilyByName(name) is { } family)
                families[purpose] = family;

        return families.Count == 0 ? NONE : new ChosenLayouts(families);
    }

    /// <summary>
    /// The family chosen for a kind of slide.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    /// <returns>The family, or null when the model decides.</returns>
    public LayoutFamily? For(LayoutPurpose purpose) => this.Families.GetValueOrDefault(purpose);

    /// <summary>
    /// The kinds of slides the presentation has to have, because the user chose a layout for them.
    /// </summary>
    public IEnumerable<LayoutPurpose> AddedSlides => Enum.GetValues<LayoutPurpose>().Where(purpose => purpose.ChoiceAddsSlide() && this.Families.ContainsKey(purpose));

    /// <summary>
    /// Writes the choices as rules for the model, one line per kind of slide, in the order of the kinds.
    /// </summary>
    /// <remarks>
    /// The first rule says which layout each kind of slide uses. The second one lists the slides the
    /// presentation has to have because of a choice, each with its place.
    /// </remarks>
    /// <returns>The rules, indented for the layout section of the system prompt; empty without choices.</returns>
    public string ToPromptRules()
    {
        if (this.Families.Count == 0)
            return string.Empty;

        var rules = new StringBuilder("    - The user chose layouts for some kinds of slides. A choice decides which layout such a slide uses, and it comes before every other rule on choosing layouts. Whether the presentation has such a slide follows from the content and the occasion, except for the slides the next rule requires. Vary the layouts only where no choice applies:");
        foreach (var purpose in Enum.GetValues<LayoutPurpose>())
            if (this.For(purpose) is { } family)
                rules.Append('\n').Append("        - ").Append(family.Id).Append(" for ").Append(purpose.PromptDescription()).Append('.');

        var addedSlides = this.AddedSlides.ToList();
        if (addedSlides.Count > 0)
        {
            rules.Append('\n').Append("    - The presentation has at least one slide of each of these kinds, on its layout:");
            foreach (var purpose in addedSlides)
                rules.Append('\n').Append("        - ").Append(this.Families[purpose].Id).Append(": ").Append(purpose.PromptDescription()).Append(", ").Append(purpose.PromptPlacement()).Append('.');
        }

        return rules.ToString();
    }
}