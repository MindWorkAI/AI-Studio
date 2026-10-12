namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Contains the exported presentation path or a user-safe rendering issue.
/// </summary>
/// <param name="OutputPath">The generated presentation path.</param>
/// <param name="Issue">The issue when generation failed.</param>
internal sealed record PowerPointRenderResult(
    string OutputPath,
    string Issue)
{
    internal bool Success => !string.IsNullOrEmpty(this.OutputPath);
}
