namespace AIStudio.Provider;

/// <summary>
/// A marker record indicating that no chat completion annotation line is expected in that stream.
/// </summary>
public sealed record NoChatCompletionAnnotationStreamLine : IAnnotationStreamLine
{
    #region Implementation of IAnnotationStreamLine

    public bool ContainsSources() => false;

    public IList<ISource> GetSources() => [];

    #endregion
}