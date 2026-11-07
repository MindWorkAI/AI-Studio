namespace AIStudio.Chat.Compression;

/// <summary>
/// Tells which part of the conversation the AI is summarizing right now.
/// </summary>
/// <param name="Part">The part being summarized, counted from 1.</param>
/// <param name="Parts">How many parts the conversation was split into.</param>
public readonly record struct ChatCompressionProgress(int Part, int Parts);