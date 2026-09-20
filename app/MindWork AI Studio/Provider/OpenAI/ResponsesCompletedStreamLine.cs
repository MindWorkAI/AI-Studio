namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The closing line of a streamed Responses API call, which repeats the whole response.
/// </summary>
/// <remarks>
/// Everything the round produced comes back here, reasoning items included, in the same shape a
/// non-streamed call would have returned. That is why a streamed tool calling round needs no
/// reassembly: this line is the round.
/// </remarks>
/// <param name="Type">The type of the stream event.</param>
/// <param name="Response">The response as a non-streamed call would have returned it.</param>
public sealed record ResponsesCompletedStreamLine(string Type, ResponsesResponse? Response);