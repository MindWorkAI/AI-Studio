using System.Text.Json.Serialization;

namespace AIStudio.Tools.Feedback;

/// <summary>
/// The JSON body which the feedback server expects at <c>POST /feedback</c>.
/// </summary>
/// <remarks>
/// The server rejects unknown fields, so these names must match its Feedback struct exactly.
/// </remarks>
public sealed record FeedbackPayload(
    [property: JsonPropertyName("weekOf")] string WeekOf,
    [property: JsonPropertyName("performance")] string Performance,
    [property: JsonPropertyName("timeSaved")] string TimeSaved,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("comment")] string Comment,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("appVersion")] string AppVersion);