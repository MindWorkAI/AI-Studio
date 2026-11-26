namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Puts the words of a paragraph into its segment, with a pair of tags around each change of formatting, for the
/// formats which keep the formatting in the properties of their runs, e.g., Word and PowerPoint.
/// </summary>
/// <remarks>
/// <para>
/// The words in the most common formatting stay words, and the new words there get that formatting; the words in
/// any other formatting get a pair of their own, see FormatSpans. A paragraph whose formatting changes more often
/// than FormatSpans.MAX_PAIRS allows is read once more without pairs of formatting: all its new words then get the
/// most common formatting, and the paragraph counts as simplified.
/// </para>
/// <para>
/// The most common formatting is the one of the most letters, not the one of the first words. Otherwise, a glossary
/// with a term in bold at the start of each line would turn bold as a whole.
/// </para>
/// </remarks>
/// <param name="builder">The builder of the segment, which the reader of the paragraph shares.</param>
/// <param name="usesFormatPairs">Whether changes of formatting become pairs; false when a paragraph is read once more because it has too many of them.</param>
/// <param name="keyOf">Tells a formatting apart from others: the same key for formatting which looks the same, e.g., apart from its language, and empty for formatting which changes nothing.</param>
/// <typeparam name="TFormat">The properties of a run.</typeparam>
internal sealed class FormatSpanReader<TFormat>(TaggedTextBuilder builder, bool usesFormatPairs, Func<TFormat, string> keyOf) where TFormat : class
{
    private readonly Dictionary<TFormat, string> keys = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, TFormat?> baseFormats = [];
    private readonly HashSet<int> pairsWithBaseFormat = [];

    /// <summary>
    /// How many changes of formatting became pairs.
    /// </summary>
    public int FormatPairCount { get; private set; }

    /// <summary>
    /// Whether words read as a tag, so that a model could not tell them apart from the tags of the paragraph.
    /// </summary>
    public bool HasTagLikeWords { get; private set; }

    /// <summary>
    /// Whether words outside of pairs of formatting differ in their formatting, which the new words do not keep.
    /// </summary>
    public bool HasMixedFormatting { get; private set; }

    /// <summary>
    /// The formatting of new words within each pair which holds words, and with 0 outside of any pair: the most
    /// common formatting there.
    /// </summary>
    public IReadOnlyDictionary<int, TFormat?> BaseFormats => this.baseFormats;

    /// <summary>
    /// Tells a formatting apart from others.
    /// </summary>
    /// <param name="format">The properties of a run; null for a run without any.</param>
    public string KeyOf(TFormat? format)
    {
        if (format is null)
            return string.Empty;

        if (!this.keys.TryGetValue(format, out var key))
        {
            key = keyOf(format);
            this.keys[format] = key;
        }

        return key;
    }

    /// <summary>
    /// Finds the most common formatting of words, by their letters.
    /// </summary>
    /// <param name="words">The words, with their formatting.</param>
    /// <returns>The key of the most common formatting, see FormatSpans.BaseKeyOf.</returns>
    public string BaseKeyOf(IEnumerable<(string Text, TFormat? Format)> words) => FormatSpans.BaseKeyOf(words.Select(word => (word.Text, this.KeyOf(word.Format))));

    /// <summary>
    /// Adds words which follow each other to the segment.
    /// </summary>
    /// <param name="words">The words, with their formatting, in their order.</param>
    /// <param name="pairId">The number of the pair the words lie in; 0 for none.</param>
    /// <param name="baseFormatKey">The key of the most common formatting within this pair, see BaseKeyOf.</param>
    /// <param name="addFormatPair">Notes what a new pair of formatting stands for, given its number and its formatting.</param>
    public void AddWords(IEnumerable<(string Text, TFormat? Format)> words, int pairId, string baseFormatKey, Action<int, TFormat?> addFormatPair)
    {
        foreach (var span in FormatSpans.Of(words.Select(word => (word.Text, word.Format, this.KeyOf(word.Format))), baseFormatKey))
        {
            this.HasTagLikeWords |= TaggedText.ContainsTagLikeText(span.Text);
            if (!usesFormatPairs || span.FormatKey == baseFormatKey)
            {
                builder.Words(span.Text);
                this.NoteFormat(pairId, span, baseFormatKey);
                continue;
            }

            var formatPair = builder.Open();
            addFormatPair(formatPair, span.Format);
            builder.Words(span.Text);
            builder.Close(formatPair);
            this.FormatPairCount++;
        }
    }

    /// <summary>
    /// Notes the formatting of words outside of a pair of formatting.
    /// </summary>
    /// <remarks>
    /// The first words in the most common formatting decide. Words in another formatting only get here without
    /// pairs of formatting; they stand in until such words come, e.g., when spaces alone are in the most common
    /// formatting, and they make the formatting mixed.
    /// </remarks>
    private void NoteFormat(int pairId, FormatSpan<TFormat?> span, string baseFormatKey)
    {
        if (span.FormatKey == baseFormatKey)
        {
            if (this.pairsWithBaseFormat.Add(pairId))
                this.baseFormats[pairId] = span.Format;

            return;
        }

        this.baseFormats.TryAdd(pairId, span.Format);
        if (!string.IsNullOrWhiteSpace(span.Text))
            this.HasMixedFormatting = true;
    }
}