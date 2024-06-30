using System.Text.Json.Serialization;

namespace AIStudio.Tools;

/// <summary>
/// The response of the update check.
/// </summary>
/// <param name="UpdateIsAvailable">True if an update is available.</param>
/// <param name="NewVersion">The new version, when available.</param>
/// <param name="Changelog">The changelog of the new version, when available.</param>
public readonly record struct UpdateResponse(
    [property:JsonPropertyName("update_is_available")] bool UpdateIsAvailable, 
    [property:JsonPropertyName("error")] bool Error, 
    [property:JsonPropertyName("new_version")] string NewVersion, 
    string Changelog
);