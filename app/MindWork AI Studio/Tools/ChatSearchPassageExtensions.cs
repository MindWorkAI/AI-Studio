using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class ChatSearchPassageExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ChatSearchPassageExtensions).Namespace, nameof(ChatSearchPassageExtensions));

    /// <summary>
    /// Where in the chat the search found the words, short enough for a narrow sidebar. The passage
    /// itself shows only where there is room for it, such as in a tooltip.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <param name="isAlso">Whether the chat was found by its name, so that this place holds the words as well.</param>
    public static string Label(this ChatSearchPassage passage, bool isAlso) => (passage.Kind, isAlso) switch
    {
        (ChatSearchPassageKind.ATTACHED_FILE, false) => TB("Found in the name of an attached file"),
        (ChatSearchPassageKind.ATTACHED_FILE, true) => TB("Found in its name and in an attached file"),
        (ChatSearchPassageKind.SOURCE, false) => TB("Found in one of its sources"),
        (ChatSearchPassageKind.SOURCE, true) => TB("Found in its name and in a source"),
        (_, false) => TB("Found in its messages"),
        (_, true) => TB("Found in its name and its messages"),
    };
}