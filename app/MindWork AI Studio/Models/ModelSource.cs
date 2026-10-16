namespace AIStudio.Models;

/// <summary>
/// Where the statements about a model were read, and when somebody last looked.
/// </summary>
/// <remarks>
/// Model cards change without telling anybody. A vendor adds tool calling to a checkpoint, raises a
/// context window, or quietly stops offering an API, and the rule written from the old page keeps
/// answering as if nothing happened. Naming the page and the day it was read is what turns "this is
/// what the rules say" into something a person can check in a minute.
///
/// This is not optional: a family has to state it, and the compiler asks for it. The verification
/// run reports the ones which have gone stale.
/// </remarks>
/// <param name="Url">The page the statements were read from.</param>
/// <param name="CheckedOn">The day somebody last read it.</param>
/// <param name="Note">What that page actually said, in a sentence, so a reader knows what to look for.</param>
public sealed record ModelSource(string Url, DateOnly CheckedOn, string Note)
{
    /// <summary>
    /// Whether this source names a page and a day.
    /// </summary>
    /// <remarks>
    /// The compiler can insist that a family states a source; it cannot insist that the source says
    /// anything. This is what the verification run asks.
    /// </remarks>
    public bool IsStated => !string.IsNullOrWhiteSpace(this.Url) && this.CheckedOn != default;
}