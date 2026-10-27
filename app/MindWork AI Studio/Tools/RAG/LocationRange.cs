namespace AIStudio.Tools.RAG;

/// <summary>
/// Where a passage is in its document, counted from one: the pages it spans, or the part it is of a
/// document without pages.
/// </summary>
/// <param name="First">The page the passage starts on, or its part.</param>
/// <param name="Last">The page the passage ends on; the first one again when it stays on one, and always for a part.</param>
public readonly record struct LocationRange(int First, int Last)
{
    /// <summary>
    /// The range as it is written, e.g., "4" or "4–5".
    /// </summary>
    public override string ToString() => this.First == this.Last ? $"{this.First}" : $"{this.First}–{this.Last}";
}