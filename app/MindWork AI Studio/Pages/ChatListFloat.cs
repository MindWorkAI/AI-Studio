namespace AIStudio.Pages;

/// <summary>
/// Why the list of the chat page floats over the chat instead of standing beside it.
/// </summary>
internal enum ChatListFloat
{
    /// <summary>
    /// The list does not float.
    /// </summary>
    NONE,

    /// <summary>
    /// The pointer rested on the toggle of the hidden list, which shows the list until the pointer
    /// leaves both.
    /// </summary>
    PEEK,

    /// <summary>
    /// A click on the toggle showed the list in a window too narrow for it beside the chat. It stays
    /// until the user opens a chat, clicks outside of it, or clicks the toggle again.
    /// </summary>
    OPEN,
}