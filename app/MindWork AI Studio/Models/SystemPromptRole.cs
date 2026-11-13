namespace AIStudio.Models;

/// <summary>
/// Which role the system prompt is sent in.
/// </summary>
/// <remarks>
/// A question only OpenAI's own cloud asks. Its models took their instructions as "system" first,
/// then as "developer", and the early reasoning models took none at all, so that the instructions
/// had to travel as a message of the person instead. Every other provider has one way to send a
/// system prompt, and for their models the rules say nothing about this.
///
/// That the vendor's name is not in the members is on purpose: they say what the model accepts,
/// and the provider decides how to spell that in its request.
/// </remarks>
public enum SystemPromptRole
{
    /// <summary>
    /// No rule says anything about it, so the provider decides.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// The model takes its instructions in the system role.
    /// </summary>
    SYSTEM,

    /// <summary>
    /// The model takes its instructions in the developer role.
    /// </summary>
    DEVELOPER,

    /// <summary>
    /// The model takes no instructions of its own, so they go as a message of the person.
    /// </summary>
    USER,
}