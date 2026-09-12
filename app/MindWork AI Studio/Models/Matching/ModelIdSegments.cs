namespace AIStudio.Models.Matching;

/// <summary>
/// Walks the parts of a normalized model name without cutting it into strings.
/// </summary>
/// <remarks>
/// The index looks up every part of a name to find the rules which could possibly apply to it. That
/// happens for every model of every configured provider, so the walk itself must not allocate: the
/// parts stay slices of the name they came from. This is both the enumerable and the enumerator,
/// which is what lets foreach use it without an interface in between.
/// </remarks>
/// <param name="normalizedId">The normalized model name to walk.</param>
public ref struct ModelIdSegments(ReadOnlySpan<char> normalizedId)
{
    private ReadOnlySpan<char> remaining = normalizedId;

    /// <summary>
    /// The part the walk currently stands on.
    /// </summary>
    public ReadOnlySpan<char> Current { get; private set; } = default;

    /// <summary>
    /// Hands foreach the walk itself.
    /// </summary>
    /// <returns>This walk, at its beginning.</returns>
    public readonly ModelIdSegments GetEnumerator() => this;

    /// <summary>
    /// Steps to the next part of the name.
    /// </summary>
    /// <returns>True, as long as there was one.</returns>
    public bool MoveNext()
    {
        while (!this.remaining.IsEmpty)
        {
            var separator = this.remaining.IndexOf(ModelId.SEGMENT_SEPARATOR);
            if (separator is -1)
            {
                this.Current = this.remaining;
                this.remaining = default;
                return true;
            }

            this.Current = this.remaining[..separator];
            this.remaining = this.remaining[(separator + 1)..];

            //
            // Normalizing leaves no empty part behind, so this only guards against a name which
            // never went through it. Skipping is the right answer: an empty part matches nothing.
            //
            if (!this.Current.IsEmpty)
                return true;
        }

        return false;
    }
}