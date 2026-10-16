using System.Collections.Concurrent;
using System.Security.Cryptography;

using Microsoft.AspNetCore.WebUtilities;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Issues and validates short-lived, non-guessable preview grants.
/// </summary>
public sealed class VisualBriefingPreviewTokenService
{
    /// <summary>
    /// Defines the maximum preview-grant lifetime.
    /// </summary>
    private static readonly TimeSpan TOKEN_LIFETIME = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Stores active grants by opaque token.
    /// </summary>
    private readonly ConcurrentDictionary<string, PreviewGrant> grants = new(StringComparer.Ordinal);

    /// <summary>
    /// Issues a preview token bound to one briefing revision.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="revisionId">The revision identifier.</param>
    /// <returns>The opaque preview token.</returns>
    public string Issue(Guid briefingId, Guid revisionId)
    {
        this.RemoveExpired();
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        this.grants[token] = new(briefingId, revisionId, DateTimeOffset.UtcNow.Add(TOKEN_LIFETIME));
        return token;
    }

    /// <summary>
    /// Validates a token and its briefing/revision binding.
    /// </summary>
    /// <param name="token">The opaque preview token.</param>
    /// <param name="briefingId">The requested briefing identifier.</param>
    /// <param name="revisionId">The requested revision identifier.</param>
    /// <returns>Whether the grant is valid and unexpired.</returns>
    public bool Validate(string? token, Guid briefingId, Guid revisionId)
    {
        if (string.IsNullOrWhiteSpace(token) || !this.grants.TryGetValue(token, out var grant))
            return false;

        if (grant.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            this.grants.TryRemove(token, out _);
            return false;
        }

        return grant.BriefingId == briefingId && grant.RevisionId == revisionId;
    }

    /// <summary>
    /// Removes expired grants.
    /// </summary>
    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (token, grant) in this.grants)
            if (grant.ExpiresAtUtc <= now)
                this.grants.TryRemove(token, out _);
    }

    /// <summary>
    /// Stores one token binding and expiry.
    /// </summary>
    /// <param name="BriefingId">The bound briefing identifier.</param>
    /// <param name="RevisionId">The bound revision identifier.</param>
    /// <param name="ExpiresAtUtc">The token expiry.</param>
    private sealed record PreviewGrant(Guid BriefingId, Guid RevisionId, DateTimeOffset ExpiresAtUtc);
}
