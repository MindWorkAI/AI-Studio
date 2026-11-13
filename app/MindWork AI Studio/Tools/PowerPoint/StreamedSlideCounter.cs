namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Counts the slides an answer has started while it still streams in, without reading it as JSON.
/// </summary>
/// <remarks>
/// A slide starts with the key of its layout, or with the key of the template slide it is taken
/// from. JSON escapes the quotation marks within a text, so no text of a slide looks like such a
/// key. The answer grows by thousands of characters, and the count runs with every rendering, so
/// each count starts where the last one ended. One counter belongs to one answer.
/// </remarks>
public sealed class StreamedSlideCounter
{
    private static readonly string[] SLIDE_KEYS = ["\"layout\"", "\"template_slide\""];

    private int countedLength;
    private int countedSlides;

    /// <summary>
    /// Counts the slides which the answer has started so far.
    /// </summary>
    /// <param name="answerSoFar">The answer as far as it has streamed in, which only grows.</param>
    /// <returns>How many slides the answer has started.</returns>
    public int Count(string answerSoFar)
    {
        // An answer which got shorter was replaced; it is counted anew:
        if (answerSoFar.Length < this.countedLength)
        {
            this.countedLength = 0;
            this.countedSlides = 0;
        }

        foreach (var key in SLIDE_KEYS)
        {
            //
            // A key which the last count saw only in part ends after the end of what it counted,
            // so the count goes back by less than one key:
            //
            var index = Math.Max(0, this.countedLength - key.Length + 1);
            while ((index = answerSoFar.IndexOf(key, index, StringComparison.Ordinal)) >= 0)
            {
                this.countedSlides++;
                index += key.Length;
            }
        }

        this.countedLength = answerSoFar.Length;
        return this.countedSlides;
    }
}
