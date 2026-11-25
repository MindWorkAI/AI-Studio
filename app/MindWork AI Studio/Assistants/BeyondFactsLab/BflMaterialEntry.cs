namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A material in the list of a preparation, also when it cannot be opened.
/// </summary>
/// <param name="MaterialId">The ID of the material.</param>
/// <param name="Material">The material, or null when its files are damaged or missing, or a newer version of AI Studio stored it.</param>
public sealed record BflMaterialEntry(Guid MaterialId, BflMaterial? Material)
{
    /// <summary>
    /// Gets whether the material can be opened.
    /// </summary>
    public bool IsAvailable => this.Material is not null;
}