using System.Text.Json;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// A type which AI Studio reads or writes as JSON on its own, together with the options it uses for that.
/// </summary>
/// <param name="Type">The type which gets serialized. Whatever it holds gets checked along with it.</param>
/// <param name="Options">The options AI Studio passes to System.Text.Json for this type.</param>
public sealed record JsonRoot(Type Type, JsonSerializerOptions Options);