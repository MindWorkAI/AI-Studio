using System.Runtime.CompilerServices;
using System.Text;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Finds the entries of a glossary whose term, or one of its aliases, occurs in a text.
/// </summary>
/// <remarks>
/// A term counts as soon as it is part of the text, ignoring case and line breaks. This is on purpose:
/// German compounds such as "Arbeitszeitregelung" contain the term "Arbeitszeit", and a match on whole
/// words alone would miss them. A needless entry costs a few tokens, a missing one the exact translation
/// the user asked for.<br/><br/>
///
/// The matcher builds an Aho-Corasick automaton from the normalized terms, once per list of entries: a
/// trie of all terms, in which every node also knows the longest end of its path that begins another
/// term (its failure link), and the next node along these links at which a term ends (its output link).
/// A search reads the text exactly once, character by character, and never copies it, so it takes time
/// linear in the length of the text, however many terms the glossary holds. The automaton takes memory
/// linear in the total length of the terms.<br/><br/>
///
/// The GlossaryStore hands out the same list until the glossary is saved again, so a matcher lives
/// exactly as long as the entries it was made for.
/// </remarks>
public sealed class GlossaryMatcher
{
    /// <summary>
    /// Terms shorter than this would match almost every text, e.g. a single letter.
    /// </summary>
    private const int MIN_TERM_LENGTH = 2;

    private const int ROOT = 0;

    private const int NONE = -1;

    private static readonly ConditionalWeakTable<IReadOnlyList<GlossaryEntry>, GlossaryMatcher> MATCHERS = new();

    private readonly IReadOnlyList<GlossaryEntry> entries;

    //
    // The edges leaving node k are edgeCharacters[edgeStart[k] .. edgeStart[k + 1]], sorted by their
    // character, which leads to edgeTargets at the same index. Flat arrays keep the automaton compact,
    // where a dictionary per node would cost far more memory than the terms themselves:
    //
    private readonly int[] edgeStart;
    private readonly char[] edgeCharacters;
    private readonly int[] edgeTargets;

    private readonly int[] failureLinks;
    private readonly int[] outputLinks;

    // The term which ends at a node, or NONE:
    private readonly int[] termAtNode;

    // The indices of the entries which use a term; several entries may share one:
    private readonly int[][] entriesOfTerm;

    private GlossaryMatcher(IReadOnlyList<GlossaryEntry> entries)
    {
        this.entries = entries;

        //
        // First, the trie: one path per distinct normalized term, the paths of terms with the same
        // beginning sharing their nodes:
        //
        var termIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var entriesOfTerms = new List<List<int>>();
        var children = new List<Dictionary<char, int>> { new() };
        var termAtNodes = new List<int> { NONE };
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var terms = new[] { entry.Term }
                .Concat(entry.Aliases ?? [])
                .Select(Normalize)
                .Where(term => term.Length >= MIN_TERM_LENGTH)
                .Distinct(StringComparer.Ordinal);

            foreach (var term in terms)
            {
                if (!termIds.TryGetValue(term, out var termId))
                {
                    termId = termIds.Count;
                    termIds[term] = termId;
                    entriesOfTerms.Add([]);

                    var node = ROOT;
                    foreach (var character in term)
                    {
                        if (!children[node].TryGetValue(character, out var next))
                        {
                            next = children.Count;
                            children.Add(new());
                            termAtNodes.Add(NONE);
                            children[node][character] = next;
                        }

                        node = next;
                    }

                    termAtNodes[node] = termId;
                }

                entriesOfTerms[termId].Add(index);
            }
        }

        var nodeCount = children.Count;
        this.termAtNode = termAtNodes.ToArray();
        this.entriesOfTerm = entriesOfTerms.Select(entryIndices => entryIndices.ToArray()).ToArray();
        this.edgeStart = new int[nodeCount + 1];
        this.edgeCharacters = new char[nodeCount - 1];
        this.edgeTargets = new int[nodeCount - 1];

        var position = 0;
        for (var node = 0; node < nodeCount; node++)
        {
            this.edgeStart[node] = position;
            foreach (var (character, target) in children[node].OrderBy(edge => edge.Key))
            {
                this.edgeCharacters[position] = character;
                this.edgeTargets[position] = target;
                position++;
            }
        }

        this.edgeStart[nodeCount] = position;

        //
        // Then the links, level by level from the root, so the links of every shorter path are known
        // before a longer one needs them. A child of the root falls back to the root; any other node
        // follows the failure links of its parent until one of them continues with the same character:
        //
        this.failureLinks = new int[nodeCount];
        this.outputLinks = new int[nodeCount];
        Array.Fill(this.outputLinks, NONE);

        var queue = new Queue<int>();
        for (var edge = this.edgeStart[ROOT]; edge < this.edgeStart[ROOT + 1]; edge++)
            queue.Enqueue(this.edgeTargets[edge]);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            for (var edge = this.edgeStart[node]; edge < this.edgeStart[node + 1]; edge++)
            {
                var child = this.edgeTargets[edge];
                var fallback = this.Step(this.failureLinks[node], this.edgeCharacters[edge]);
                this.failureLinks[child] = fallback;
                this.outputLinks[child] = this.termAtNode[fallback] != NONE ? fallback : this.outputLinks[fallback];
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Gets the matcher for a list of entries, reusing the one made before for the same list.
    /// </summary>
    /// <param name="entries">The entries of a glossary, as the GlossaryStore hands them out.</param>
    /// <returns>The matcher for these entries.</returns>
    public static GlossaryMatcher For(IReadOnlyList<GlossaryEntry> entries) => MATCHERS.GetValue(entries, list => new GlossaryMatcher(list));

    /// <summary>
    /// Finds the entries which occur in a text.
    /// </summary>
    /// <param name="text">The text to search, e.g. the text which is going to be translated.</param>
    /// <returns>The matching entries, in the order of the glossary; none when nothing matched.</returns>
    public IReadOnlyList<GlossaryEntry> FindEntries(string text)
    {
        var termCount = this.entriesOfTerm.Length;
        if (termCount == 0)
            return [];

        var foundTerms = new bool[termCount];
        var remainingTerms = termCount;
        var node = ROOT;

        //
        // The text is normalized on the fly, exactly as Normalize would: whitespace before the first
        // other character is dropped, a run of whitespace becomes a single space only once another
        // character follows, and every other character is lowercased:
        //
        var hasStarted = false;
        var pendingSpace = false;
        foreach (var character in text.AsSpan())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = hasStarted;
                continue;
            }

            if (pendingSpace)
            {
                node = this.Step(node, ' ');
                remainingTerms -= this.CollectTerms(node, foundTerms);
                pendingSpace = false;
            }

            node = this.Step(node, char.ToLowerInvariant(character));
            remainingTerms -= this.CollectTerms(node, foundTerms);
            hasStarted = true;

            // Nothing left to find, so the rest of a long text needs no reading:
            if (remainingTerms == 0)
                break;
        }

        var isMatched = new bool[this.entries.Count];
        for (var termId = 0; termId < termCount; termId++)
            if (foundTerms[termId])
                foreach (var entryIndex in this.entriesOfTerm[termId])
                    isMatched[entryIndex] = true;

        var matches = new List<GlossaryEntry>();
        for (var index = 0; index < this.entries.Count; index++)
            if (isMatched[index])
                matches.Add(this.entries[index]);

        return matches;
    }

    /// <summary>
    /// Moves on from a node with the next character of the text.
    /// </summary>
    /// <remarks>
    /// Without an edge for the character, the automaton falls back to the longest end of what it read
    /// which still begins a term, and tries again there; at the root, it stays when no term begins with
    /// the character either.
    /// </remarks>
    private int Step(int node, char character)
    {
        while (true)
        {
            var next = this.FindEdge(node, character);
            if (next != NONE)
                return next;

            if (node == ROOT)
                return ROOT;

            node = this.failureLinks[node];
        }
    }

    private int FindEdge(int node, char character)
    {
        var low = this.edgeStart[node];
        var high = this.edgeStart[node + 1] - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var middleCharacter = this.edgeCharacters[middle];
            if (middleCharacter == character)
                return this.edgeTargets[middle];

            if (middleCharacter < character)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return NONE;
    }

    /// <summary>
    /// Marks the terms which end at a node: its own term and those along its output links.
    /// </summary>
    /// <remarks>
    /// Once a term was found, so were all terms along its output links, since they were collected along
    /// with it. The walk therefore stops at the first term found before, and all walks of a search take
    /// no more steps than there are terms.
    /// </remarks>
    /// <returns>The number of terms found for the first time.</returns>
    private int CollectTerms(int node, bool[] foundTerms)
    {
        var newlyFound = 0;
        for (var hit = this.termAtNode[node] != NONE ? node : this.outputLinks[node]; hit != NONE; hit = this.outputLinks[hit])
        {
            var termId = this.termAtNode[hit];
            if (foundTerms[termId])
                break;

            foundTerms[termId] = true;
            newlyFound++;
        }

        return newlyFound;
    }

    //
    // A term may break across lines in the text, or carry a double space in the glossary; neither must
    // keep it from matching. Lowercasing both sides once is much faster than comparing every term while
    // ignoring the case. FindEntries normalizes the text the same way while reading it:
    //
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}