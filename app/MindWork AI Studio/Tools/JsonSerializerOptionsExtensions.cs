using System.Text.Json;

namespace AIStudio.Tools;

public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Makes the options read-only and returns them, so that a static field gets its options in one expression.
    /// </summary>
    /// <remarks>
    /// Options with contracts of their own, i.e., with a TypeInfoResolver, should never change after they were
    /// declared: whoever used them before such a change would have read or written with other contracts.
    /// </remarks>
    /// <param name="options">The options, with their TypeInfoResolver set.</param>
    /// <returns>The same options, now read-only.</returns>
    public static JsonSerializerOptions AsReadOnly(this JsonSerializerOptions options)
    {
        options.MakeReadOnly();
        return options;
    }
}