namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How much of a material the prompt of a step carries.
/// </summary>
/// <param name="Material">The material.</param>
/// <param name="Characters">How many characters of its text, from the beginning, the prompt carries at most.</param>
public sealed record BflMaterialShare(BflMaterial Material, int Characters)
{
    /// <summary>
    /// Gets whether the prompt carries the whole text.
    /// </summary>
    public bool IsComplete => this.Characters >= this.Material.TextLength;

    /// <summary>
    /// Gets which share of the text the prompt carries, in percent: 100 for the whole text, 0 for none of it, and
    /// between 1 and 99 for a part of it, however small or large.
    /// </summary>
    public int Percent
    {
        get
        {
            if (this.IsComplete)
                return 100;

            if (this.Characters <= 0)
                return 0;

            return (int)Math.Clamp(100L * this.Characters / this.Material.TextLength, 1, 99);
        }
    }
}