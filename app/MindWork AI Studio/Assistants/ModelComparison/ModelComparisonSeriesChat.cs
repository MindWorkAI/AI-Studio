using System.Text.Json.Serialization;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// One of the two chats of a task of a comparison series, as the series keeps it beside the chat itself.
/// </summary>
public sealed record ModelComparisonSeriesChat
{
    /// <summary>
    /// The ID of the chat, which names its file in the folder of the series.
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// When the user began to work in the chat, in UTC, or null before.
    /// </summary>
    public DateTimeOffset? StartedAtUtc { get; set; }

    /// <summary>
    /// When the user was done with the chat, in UTC, or null before.
    /// </summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>
    /// How many messages the user wrote in the chat, the first one included.
    /// </summary>
    public int UserMessageCount { get; set; }

    /// <summary>
    /// How long the user worked in the chat, or null while it is not done.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.StartedAtUtc is { } started && this.FinishedAtUtc is { } finished ? finished - started : null;
}