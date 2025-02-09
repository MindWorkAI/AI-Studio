namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// A chat thread, which is a list of content blocks.
/// </summary>
/// <param name="ContentBlocks">The content blocks in this chat thread.</param>
public readonly record struct ChatThread(List<ContentBlock> ContentBlocks);