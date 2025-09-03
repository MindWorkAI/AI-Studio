namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Represents a line of a chat completion annotation stream.
/// </summary>
/// <param name="Id">The unique identifier of the chat completion.</param>
/// <param name="Object">The type of object returned, typically "chat.completion".</param>
/// <param name="Created">The creation timestamp of the chat completion in Unix epoch format.</param>
/// <param name="Model">The model used for the chat completion.</param>
/// <param name="SystemFingerprint">The system fingerprint associated with the chat completion.</param>
/// <param name="Choices">The list of choices returned in the chat completion.</param>
public record ChatCompletionAnnotationStreamLine(string Id, string Object, uint Created, string Model, string SystemFingerprint, IList<ChatCompletionAnnotationChoice> Choices) : IAnnotationStreamLine
{
    #region Implementation of IAnnotationStreamLine

    /// <inheritdoc />
    public bool ContainsSources() => this.Choices.Any(choice => choice.Delta.Annotations is not null && choice.Delta.Annotations.Any(annotation => annotation is not AnnotatingUnknown));

    /// <inheritdoc />
    public IList<ISource> GetSources()
    {
        var sources = new List<ISource>();
        foreach (var choice in this.Choices)
        {
            if (choice.Delta.Annotations is null)
                continue;

            // Iterate through all annotations:
            foreach (var annotation in choice.Delta.Annotations)
            {
                // Check if the annotation is of the expected type and extract the source information:
                if (annotation is ChatCompletionAnnotatingURL urlAnnotation)
                    sources.Add(new Source(urlAnnotation.UrlCitation.Title, urlAnnotation.UrlCitation.URL));

                //
                // Check for the unexpected annotation type of the Responses API.
                //
                // This seems weird at first. But there are two possibilities why this could happen:
                // - Anyone of the open source providers such as ollama, LM Studio, etc. could
                //   implement & use the Responses API data structures for annotations in their
                //   chat completion endpoint.
                //
                // - Our custom JSON converter checks for the Responses API data type first. If it
                //   fails, it checks for the chat completion API data type. So, when the Responses
                //   API data type is valid, it will be deserialized into that type, even though
                //   we are calling the chat completion endpoint.
                //
                if (annotation is ResponsesAnnotatingUrlCitationData citationData)
                    sources.Add(new Source(citationData.Title, citationData.URL));
            }
        }

        return sources;
    }

    #endregion
}