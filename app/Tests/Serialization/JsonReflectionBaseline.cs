using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// The options of AI Studio as System.Text.Json uses them without source generation: the same settings, but
/// with contracts which reflection builds.
/// </summary>
/// <remarks>
/// AI Studio stored all its files with such contracts, so they are the baseline every other contract has to
/// match. Each baseline is built once per options, since reflection builds its contracts once per options, too.
/// </remarks>
public static class JsonReflectionBaseline
{
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> BASELINES = new();

    /// <summary>
    /// Returns a copy of the given options, whose contracts reflection builds.
    /// </summary>
    /// <param name="options">The options AI Studio uses.</param>
    public static JsonSerializerOptions Of(JsonSerializerOptions options) => BASELINES.GetValue(options, static original => new JsonSerializerOptions(original)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    });
}