namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// A block of content of a chat thread.
/// </summary>
/// <remarks>
/// Images and other media are base64 encoded.
/// </remarks>
/// <param name="Content">The content of the block. Remember that images and other media are base64 encoded.</param>
/// <param name="Role">The role of the content in the chat thread.</param>
/// <param name="Type">The type of the content, e.g., text, image, video, etc.</param>
public readonly record struct ContentBlock(string Content, Role Role, ContentType Type);