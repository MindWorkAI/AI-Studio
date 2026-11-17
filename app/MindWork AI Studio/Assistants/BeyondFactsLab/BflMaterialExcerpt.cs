namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The part of a material which the prompt of a step carries.
/// </summary>
/// <param name="Share">How much of the material the prompt carries.</param>
/// <param name="Text">The beginning of its text, or the whole text; empty when nothing of it fits.</param>
public sealed record BflMaterialExcerpt(BflMaterialShare Share, string Text);