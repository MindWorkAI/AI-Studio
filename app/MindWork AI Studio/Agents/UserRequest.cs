using AIStudio.Chat;

namespace AIStudio.Agents;

/// <summary>
/// The created user request.
/// </summary>
public sealed class UserRequest
{
    /// <summary>
    /// The time when the request was created.
    /// </summary>
    public required DateTimeOffset Time { get; init; }

    /// <summary>
    /// The user prompt.
    /// </summary>
    public required IContent UserPrompt { get; init; }
}