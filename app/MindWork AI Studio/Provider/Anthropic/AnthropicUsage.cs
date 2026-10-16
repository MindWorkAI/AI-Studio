// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Provider.Anthropic;

/// <summary>
/// What Anthropic reports a messages call carried, as it opens the stream.
/// </summary>
/// <remarks>
/// The input arrives in up to three parts, and only their sum is what the request carried: the
/// input tokens are just those after the last cache breakpoint, the other two are what was written
/// to and read from the cache before it. Read on 2026-09-24 at
/// https://platform.claude.com/docs/en/build-with-claude/prompt-caching.
///
/// A missing cache part means that nothing was cached, not that its size is unknown: the API
/// caches only for a request which asks for it with cache_control, which AI Studio never does on
/// its own -- somebody could, though, through the additional API parameters. A missing input part
/// is different, and without it the block states nothing.
///
/// The output tokens are left unread on purpose, for the reason given at TokenUsage: they include
/// the model's thinking, which no later request carries.
/// </remarks>
public sealed record AnthropicUsage
{
    /// <summary>
    /// What the request carried after its last cache breakpoint, which is all of it without caching.
    /// </summary>
    public int? InputTokens { get; init; }

    /// <summary>
    /// What the request wrote to the cache.
    /// </summary>
    public int? CacheCreationInputTokens { get; init; }

    /// <summary>
    /// What the request read from the cache.
    /// </summary>
    public int? CacheReadInputTokens { get; init; }

    /// <summary>
    /// States what this block reports, as far as it can be believed.
    /// </summary>
    /// <remarks>
    /// The one way from the wire to a usage, shared by the plain text path and the tool calling
    /// path, so that what counts as believable is decided in a single place.
    /// </remarks>
    /// <returns>The usage, or TokenUsage.UNKNOWN when the block states nothing usable.</returns>
    public TokenUsage ToTokenUsage() => this.InputTokens is { } inputTokens
        ? TokenUsage.OfReported(inputTokens + (this.CacheCreationInputTokens ?? 0) + (this.CacheReadInputTokens ?? 0))
        : TokenUsage.UNKNOWN;
}