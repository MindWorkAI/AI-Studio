namespace AIStudio.Chat.Compression;

/// <summary>
/// Tells what compressing a chat is busy with right now.
/// </summary>
/// <param name="Step">What compressing is busy with.</param>
/// <param name="Current">The part being summarized, counted from 1; 0 while reading the files.</param>
/// <param name="Total">How many parts the conversation was split into, as far as known by now; 0 while reading the files.</param>
public readonly record struct ChatCompressionProgress(ChatCompressionStep Step, int Current, int Total);