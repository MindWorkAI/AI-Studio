using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.RAG;

public static class RetrievalContentCategoryExtensions
{
    /// <summary>
    /// Converts an ERI content type to a common retrieval content category.
    /// </summary>
    /// <param name="contentType">The content type yielded by the ERI server.</param>
    /// <returns>The corresponding retrieval content category.</returns>
    public static RetrievalContentCategory ToRetrievalContentCategory(this ContentType contentType) => contentType switch
    {
        ContentType.NONE => RetrievalContentCategory.NONE,
        ContentType.UNKNOWN => RetrievalContentCategory.UNKNOWN,
        ContentType.TEXT => RetrievalContentCategory.TEXT,
        ContentType.IMAGE => RetrievalContentCategory.IMAGE,
        ContentType.VIDEO => RetrievalContentCategory.VIDEO,
        ContentType.AUDIO => RetrievalContentCategory.AUDIO,
        ContentType.SPEECH => RetrievalContentCategory.AUDIO,
        
        _ => RetrievalContentCategory.UNKNOWN,
    };
}