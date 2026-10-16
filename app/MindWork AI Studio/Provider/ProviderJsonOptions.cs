using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Provider.Anthropic;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider;

/// <summary>
/// The JSON options every provider request and response is read and written with.
/// </summary>
/// <remarks>
/// They sit outside the provider base class so that the types which interpret a stream can share
/// them without being a provider themselves. Those types are the ones worth testing, and a
/// provider cannot be constructed in a test at all -- it reaches for the service provider in its
/// constructor. Options rebuilt inside a test would be a second set of rules drifting away from
/// the one that actually reads the wire.
/// </remarks>
public static class ProviderJsonOptions
{
    /// <summary>
    /// The shared options.
    /// </summary>
    public static readonly JsonSerializerOptions OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
            new AnnotationConverter(),
            new MessageBaseConverter(),
            new SubContentConverter(),
            new SubContentImageSourceConverter(),
            new SubContentImageUrlConverter(),
        },
        AllowTrailingCommas = false
    };
}