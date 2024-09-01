namespace AIStudio.Tools.Rust;

/// <summary>
/// The response of the update check.
/// </summary>
/// <param name="UpdateIsAvailable">True if an update is available.</param>
/// <param name="NewVersion">The new version, when available.</param>
/// <param name="Changelog">The changelog of the new version, when available.</param>
public readonly record struct UpdateResponse(
    bool UpdateIsAvailable, 
    bool Error, 
    string NewVersion, 
    string Changelog
);