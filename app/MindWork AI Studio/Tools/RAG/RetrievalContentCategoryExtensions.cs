using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.RAG;

public static class RetrievalContentCategoryExtensions
{
    /// <param name="contentType">The content type yielded by the ERI server.</param>
    extension(ContentType contentType)
    {
        /// <summary>
        /// Converts an ERI content type to a common retrieval content category.
        /// </summary>
        /// <value>The corresponding retrieval content category.</value>
        public RetrievalContentCategory RetrievalContentCategory => contentType switch
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
}