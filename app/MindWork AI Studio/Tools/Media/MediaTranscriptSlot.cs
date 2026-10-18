namespace AIStudio.Tools.Media;

/// <summary>
/// The place of the persistent transcript of one audio or video file.
/// </summary>
/// <param name="Path">The absolute path of the Markdown transcript.</param>
/// <param name="Key">What identifies the transcript at its location, e.g. the hash of the media content.</param>
internal sealed record MediaTranscriptSlot(string Path, string Key);