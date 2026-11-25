namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What became of a material the person added.
/// </summary>
/// <param name="Material">The new material, or the one with the same content which the preparation had already.</param>
/// <param name="IsDuplicate">Whether the preparation had a material with the same content already, so that nothing was stored.</param>
public sealed record BflMaterialAddition(BflMaterial Material, bool IsDuplicate);