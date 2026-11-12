namespace AIStudio.Chat.Compression;

/// <summary>
/// Tells what compressing a chat is busy with right now.
/// </summary>
/// <param name="Step">What compressing is busy with.</param>
/// <param name="Current">The image being described, or the part being summarized, counted from 1; 0 while reading the files.</param>
/// <param name="Total">How many images there are to describe, or how many parts the conversation was split into; 0 while reading the files.</param>
public readonly record struct ChatCompressionProgress(ChatCompressionStep Step, int Current, int Total);