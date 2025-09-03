namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data structure for a line in the response stream of the Responses API, containing an annotation.
/// </summary>
/// <param name="Type">The type of the annotation.</param>
/// <param name="AnnotationIndex">The continuous index of the annotation in the response.</param>
/// <param name="Annotation">The annotation details.</param>
public sealed record ResponsesAnnotationStreamLine(string Type, int AnnotationIndex, Annotation Annotation) : IAnnotationStreamLine
{
    #region Implementation of IAnnotationStreamLine

    /// <inheritdoc />
    public bool ContainsSources()
    {
        return this.Annotation is not AnnotatingUnknown;
    }

    /// <inheritdoc />
    public IList<ISource> GetSources()
    {
        //
        // Check for the unexpected annotation type of the chat completion API.
        //
        // This seems weird at first. But there are two possibilities why this could happen:
        // - Anyone of the open source providers such as ollama, LM Studio, etc. could
        //   implement and use the chat completion API data structures for annotations in their
        //   Responses API endpoint.
        //
        // - Our custom JSON converter checks for all possible annotation data types. So,
        //   when the streamed data is valid for any annotation type, it will be deserialized
        //   into that type, even though we are calling the Responses API endpoint.
        //
        if (this.Annotation is ChatCompletionAnnotatingURL urlAnnotation)
            return [new Source(urlAnnotation.UrlCitation.Title, urlAnnotation.UrlCitation.URL)];
        
        // Check for the expected annotation type of the Responses API:
        if (this.Annotation is ResponsesAnnotatingUrlCitationData urlCitationData)
            return [new Source(urlCitationData.Title, urlCitationData.URL)];

        return [];
    }

    #endregion
}