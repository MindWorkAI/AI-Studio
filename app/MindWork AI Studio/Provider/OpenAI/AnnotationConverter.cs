using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Custom JSON converter for the annotation class to handle polymorphic deserialization.
/// </summary>
/// <remarks>
/// We use this converter for chat completion API and responses API annotation deserialization.
/// </remarks>
public sealed class AnnotationConverter : JsonConverter<Annotation>
{
    public override Annotation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("type", out var typeElement))
            return null;
        
        var type = typeElement.GetString();
        var rawText = root.GetRawText();
        
        Annotation? annotation;
        switch (type)
        {
            case "url_citation":
                
                // Let's check the responses API data type first:
                var responsesAnnotation = JsonSerializer.Deserialize<ResponsesAnnotatingUrlCitationData>(rawText, options);
                
                // If it fails, let's try the chat completion API data type:
                if(responsesAnnotation is null || string.IsNullOrWhiteSpace(responsesAnnotation.Title) || string.IsNullOrWhiteSpace(responsesAnnotation.URL))
                {
                    // Try chat completion API data type:
                    var chatCompletionAnnotation = JsonSerializer.Deserialize<ChatCompletionAnnotatingURL>(rawText, options);
                    
                    // If both fail, we return the unknown type:
                    if(chatCompletionAnnotation is null)
                        annotation = new AnnotatingUnknown(type);
                    else
                        annotation = chatCompletionAnnotation;
                }
                else
                    annotation = responsesAnnotation;
                
                break;
            
            default:
                annotation = new AnnotatingUnknown(type ?? "unknown");
                break;
        }

        return annotation;
    }

    public override void Write(Utf8JsonWriter writer, Annotation value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}