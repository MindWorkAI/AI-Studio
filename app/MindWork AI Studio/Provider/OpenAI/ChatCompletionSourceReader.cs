using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Reads the sources a provider puts into its Chat Completions stream.
/// </summary>
/// <remarks>
/// Where those sit differs per provider: OpenAI announces them on annotation lines of their own,
/// Perplexity puts its search results into the very line that carries the text. The plain text
/// path reads both through the provider's own stream line types, and so does this -- otherwise
/// the tool calling rounds would be the one place where a citation link goes missing.
/// </remarks>
public static class ChatCompletionSourceReader
{
    private const string DONE = "[DONE]";
    
    /// <summary>
    /// Reads whatever sources one line of the stream announced.
    /// </summary>
    /// <param name="serverSentEvent">The event to read.</param>
    /// <typeparam name="TDelta">The provider's delta stream line type.</typeparam>
    /// <typeparam name="TAnnotation">The provider's annotation stream line type.</typeparam>
    /// <returns>The sources of this line, empty when it announced none.</returns>
    public static IList<ISource> Read<TDelta, TAnnotation>(ServerSentEvent serverSentEvent)
        where TDelta : IResponseStreamLine
        where TAnnotation : IAnnotationStreamLine
    {
        if (serverSentEvent.Data.Length is 0 || serverSentEvent.Data is DONE)
            return [];

        //
        // The same split the plain text path makes, and for the same reason: a line is either an
        // annotation line or a delta line, and reading it as both would count its sources twice.
        //
        var annotationSupported = typeof(TAnnotation) != typeof(NoResponsesAnnotationStreamLine) && typeof(TAnnotation) != typeof(NoChatCompletionAnnotationStreamLine);
        if (annotationSupported && serverSentEvent.Line.Contains("""
                                                                 "annotations":[
                                                                 """, StringComparison.InvariantCulture))
        {
            var annotationLine = TryDeserialize<TAnnotation>(serverSentEvent.Data);
            return annotationLine is not null && annotationLine.ContainsSources() ? annotationLine.GetSources() : [];
        }

        var deltaLine = TryDeserialize<TDelta>(serverSentEvent.Data);
        return deltaLine is not null && deltaLine.ContainsSources() ? deltaLine.GetSources() : [];
    }
    
    private static T? TryDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, ProviderJsonOptions.OPTIONS);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}