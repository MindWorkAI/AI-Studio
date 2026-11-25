namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The file the lab reads for a preparation right now, to show the person what is going on.
/// </summary>
/// <param name="Name">The name of the file as the person knows it.</param>
/// <param name="Kind">What the file becomes: a recording takes long, since it is transcribed first.</param>
public sealed record BflMaterialInProgress(string Name, BflMaterialKind Kind);