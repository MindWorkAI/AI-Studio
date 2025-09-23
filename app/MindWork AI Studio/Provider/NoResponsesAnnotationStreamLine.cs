namespace AIStudio.Provider;

/// <summary>
/// A marker record indicating that no annotation line is expected in that Responses API stream.
/// </summary>
public sealed record NoResponsesAnnotationStreamLine : IAnnotationStreamLine
{
    #region Implementation of IAnnotationStreamLine

    public bool ContainsSources() => false;

    public IList<ISource> GetSources() => [];

    #endregion
}