using AIStudio.Models.Matching;

namespace AIStudio.Models;

/// <summary>
/// Everything the app knows about one family of models, in one place.
/// </summary>
/// <remarks>
/// A family is a class, and adding one is all it takes: the source generator finds it at compile
/// time and the registry asks it for its rules. There is no list to remember to add it to, which is
/// what the old code got wrong in the other direction -- there, a new family meant editing a file
/// which had already grown past a thousand lines, and putting the block in the wrong place changed
/// the answer for models nobody was thinking about.
///
/// The source is an abstract member, so the compiler asks for it. That is deliberate: a rule
/// without a page behind it is a guess, and a guess which nobody can check ages into a defect.
/// </remarks>
public abstract class ModelFamily
{
    private IReadOnlyList<ModelRule>? declaredRules;

    /// <summary>
    /// Who builds the models of this family.
    /// </summary>
    public abstract ModelVendor Vendor { get; }

    /// <summary>
    /// Where the statements below were read, and when.
    /// </summary>
    public abstract ModelSource Source { get; }

    /// <summary>
    /// The other pages this family was read from, where one was not enough.
    /// </summary>
    /// <remarks>
    /// A vendor keeps what a model can do, how much it reads and how many images it takes on three
    /// different pages often enough. The source above stays the one to start from; these are the
    /// rest, and the same is asked of them -- a page and a day, so that every number in the family
    /// leads back to something somebody can open.
    /// </remarks>
    public virtual IReadOnlyList<ModelSource> FurtherSources => [];

    /// <summary>
    /// What this family is called, which is what its rules name as their origin.
    /// </summary>
    public string Name => this.GetType().Name;

    /// <summary>
    /// The rules this family states, worked out once.
    /// </summary>
    public IReadOnlyList<ModelRule> Rules => this.declaredRules ??= this.BuildRules();

    /// <summary>
    /// Adjusts a profile in a way no pattern can express.
    /// </summary>
    /// <remarks>
    /// The way out for the handful of families whose capabilities are computed from the name rather
    /// than looked up: Mistral encodes a release date as four digits and gains abilities from a
    /// certain date onwards, and Z AI marks its vision models by putting a "v" behind the version
    /// number. Writing one rule per possible date is not a rule set, it is a table of everything.
    ///
    /// Everything which can be said with a pattern belongs in a pattern, where the specificity can
    /// see it. This runs afterwards, on the family whose rule won.
    /// </remarks>
    /// <param name="id">The model name.</param>
    /// <param name="selected">What the rules made of it.</param>
    /// <returns>The profile, adjusted.</returns>
    public virtual ModelProfile Refine(in ModelId id, in ModelProfile selected) => selected;

    /// <summary>
    /// States the rules of this family.
    /// </summary>
    /// <param name="builder">What to state them with.</param>
    protected abstract void Declare(ModelFamilyBuilder builder);

    private IReadOnlyList<ModelRule> BuildRules()
    {
        var builder = new ModelFamilyBuilder(this.Name);
        this.Declare(builder);
        return builder.Build();
    }
}