using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace AIStudio.Tools.Security;

public sealed class PromptInjectionScanner(ILogger<PromptInjectionScanner> logger)
{
    private const int MAX_DECODED_CANDIDATES_PER_ENCODING = 12;
    private const int MAX_DECODED_TEXT_LENGTH = 12_000;
    private const int MAX_FINDINGS = 8;
    private const int MAX_SNIPPET_LENGTH = 240;
    private const int LEXICAL_WINDOW_SIZE = 12;

    private static readonly IReadOnlyDictionary<(int Length, char First, char Last), string[]> TYPOGLYCEMIA_KEYWORDS =
        CreateTypoglycemiaKeywordIndex();

    public PromptInjectionScanResult Scan(string text, PromptInjectionSource source)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new(source, []);

        var findings = new List<PromptInjectionFinding>();
        var findingKeys = new HashSet<string>(StringComparer.Ordinal);

        this.ScanVariant(text, "raw", findings, findingKeys, scanTypoglycemia: true);
        if (findings.Count >= MAX_FINDINGS)
            return new(source, findings);

        var collapsed = CollapseCharacterSpacedContent(text);
        if (!string.Equals(text, collapsed, StringComparison.Ordinal))
            this.ScanVariant(collapsed, "character_spacing", findings, findingKeys);

        if (findings.Count < MAX_FINDINGS)
            this.ScanDecodedCandidates(text, findings, findingKeys);

        return new(source, findings);
    }

    private void ScanVariant(
        string text,
        string stage,
        List<PromptInjectionFinding> findings,
        HashSet<string> findingKeys,
        bool scanTypoglycemia = false)
    {
        var shouldScanRegexRules = true;
        try
        {
            shouldScanRegexRules = PromptInjectionPatterns.AnyRuleRegex().IsMatch(text);
        }
        catch (RegexMatchTimeoutException exception)
        {
            logger.LogWarning(exception, "Prompt-injection regex prefilter timed out during stage '{Stage}'. Falling back to individual rules.", stage);
        }

        if (shouldScanRegexRules)
        {
            foreach (var rule in PromptInjectionPatterns.RULES)
            {
                if (findings.Count >= MAX_FINDINGS)
                    return;

                Match match;
                try
                {
                    match = rule.Regex.Match(text);
                }
                catch (RegexMatchTimeoutException exception)
                {
                    logger.LogWarning(exception, "Prompt-injection regex '{RuleId}' timed out during stage '{Stage}'.", rule.Id, stage);
                    continue;
                }

                if (!match.Success)
                    continue;

                var snippet = ExtractSnippet(text, match.Index, match.Length);
                AddFinding(findings, findingKeys, new(rule.Id, rule.Category, snippet));
            }
        }

        if (findings.Count < MAX_FINDINGS)
            ScanLexicalSignals(text, scanTypoglycemia, findings, findingKeys);
    }

    private void ScanDecodedCandidates(string text, List<PromptInjectionFinding> findings, HashSet<string> findingKeys)
    {
        var processed = 0;
        var seenDecodedTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in PromptInjectionPatterns.Base64Regex().EnumerateMatches(text))
        {
            if (processed++ >= MAX_DECODED_CANDIDATES_PER_ENCODING || findings.Count >= MAX_FINDINGS)
                break;

            var decoded = TryDecodeBase64(text.AsSpan(match.Index, match.Length));
            if (decoded is not null && seenDecodedTexts.Add(decoded))
                this.ScanVariant(decoded, "decoded_base64", findings, findingKeys);
        }

        processed = 0;
        seenDecodedTexts.Clear();
        foreach (var match in PromptInjectionPatterns.HexPairRegex().EnumerateMatches(text))
        {
            if (processed++ >= MAX_DECODED_CANDIDATES_PER_ENCODING || findings.Count >= MAX_FINDINGS)
                break;

            var decoded = TryDecodeHex(text.AsSpan(match.Index, match.Length));
            if (decoded is not null && seenDecodedTexts.Add(decoded))
                this.ScanVariant(decoded, "decoded_hex_pairs", findings, findingKeys);
        }

        processed = 0;
        seenDecodedTexts.Clear();
        foreach (var match in PromptInjectionPatterns.HexCompactRegex().EnumerateMatches(text))
        {
            if (processed++ >= MAX_DECODED_CANDIDATES_PER_ENCODING || findings.Count >= MAX_FINDINGS)
                break;

            var decoded = TryDecodeHex(text.AsSpan(match.Index, match.Length));
            if (decoded is not null && seenDecodedTexts.Add(decoded))
                this.ScanVariant(decoded, "decoded_hex", findings, findingKeys);
        }
    }

    private static void ScanLexicalSignals(
        string text,
        bool scanTypoglycemia,
        List<PromptInjectionFinding> findings,
        HashSet<string> findingKeys)
    {
        Span<int> lastTokenPositions = stackalloc int[PromptInjectionLexicon.SIGNAL_COUNT];
        Span<int> lastCharacterPositions = stackalloc int[PromptInjectionLexicon.SIGNAL_COUNT];
        Span<int> lastTokenLengths = stackalloc int[PromptInjectionLexicon.SIGNAL_COUNT];
        Span<bool> matchedRules = stackalloc bool[PromptInjectionLexicon.RULES.Count];
        lastTokenPositions.Fill(int.MinValue);
        matchedRules.Clear();

        var tokenIndex = 0;
        var characterIndex = 0;
        while (characterIndex < text.Length)
        {
            while (characterIndex < text.Length && !IsAsciiLetter(text[characterIndex]))
                characterIndex++;

            if (characterIndex >= text.Length)
                break;

            var tokenStart = characterIndex;
            while (characterIndex < text.Length &&
                   (IsAsciiLetter(text[characterIndex]) || text[characterIndex] == '\''))
            {
                characterIndex++;
            }

            var token = text.AsSpan(tokenStart, characterIndex - tokenStart);
            var signals = PromptInjectionLexicon.Classify(token);
            if (signals != PromptInjectionLexicalSignal.NONE)
            {
                UpdateSignalOccurrences(
                    signals,
                    tokenIndex,
                    tokenStart,
                    token.Length,
                    lastTokenPositions,
                    lastCharacterPositions,
                    lastTokenLengths);

                for (var ruleIndex = 0; ruleIndex < PromptInjectionLexicon.RULES.Count; ruleIndex++)
                {
                    if (matchedRules[ruleIndex])
                        continue;

                    var rule = PromptInjectionLexicon.RULES[ruleIndex];
                    if (!TryMatchLexicalRule(
                            rule,
                            tokenIndex,
                            lastTokenPositions,
                            lastCharacterPositions,
                            lastTokenLengths,
                            out var matchStart,
                            out var matchLength))
                    {
                        continue;
                    }

                    matchedRules[ruleIndex] = true;
                    var snippet = ExtractSnippet(text, matchStart, matchLength);
                    AddFinding(findings, findingKeys, new(rule.Id, rule.Category, snippet));
                    if (findings.Count >= MAX_FINDINGS)
                        return;
                }
            }

            if (scanTypoglycemia && IsTypoglycemiaCandidate(token))
            {
                ScanTypoglycemiaToken(text, token, tokenStart, findings, findingKeys);
                if (findings.Count >= MAX_FINDINGS)
                    return;
            }

            tokenIndex++;
        }
    }

    private static void UpdateSignalOccurrences(
        PromptInjectionLexicalSignal signals,
        int tokenIndex,
        int tokenStart,
        int tokenLength,
        Span<int> lastTokenPositions,
        Span<int> lastCharacterPositions,
        Span<int> lastTokenLengths)
    {
        var remainingSignals = (uint)signals;
        while (remainingSignals != 0)
        {
            var signalIndex = BitOperations.TrailingZeroCount(remainingSignals);
            lastTokenPositions[signalIndex] = tokenIndex;
            lastCharacterPositions[signalIndex] = tokenStart;
            lastTokenLengths[signalIndex] = tokenLength;
            remainingSignals &= remainingSignals - 1;
        }
    }

    private static bool TryMatchLexicalRule(
        PromptInjectionLexicalRule rule,
        int currentTokenIndex,
        ReadOnlySpan<int> lastTokenPositions,
        ReadOnlySpan<int> lastCharacterPositions,
        ReadOnlySpan<int> lastTokenLengths,
        out int matchStart,
        out int matchLength)
    {
        matchStart = int.MaxValue;
        var matchEnd = 0;

        if (!TryIncludeMostRecentSignal(
                rule.First,
                currentTokenIndex,
                lastTokenPositions,
                lastCharacterPositions,
                lastTokenLengths,
                ref matchStart,
                ref matchEnd) ||
            !TryIncludeMostRecentSignal(
                rule.Second,
                currentTokenIndex,
                lastTokenPositions,
                lastCharacterPositions,
                lastTokenLengths,
                ref matchStart,
                ref matchEnd) ||
            rule.Third != PromptInjectionLexicalSignal.NONE &&
            !TryIncludeMostRecentSignal(
                rule.Third,
                currentTokenIndex,
                lastTokenPositions,
                lastCharacterPositions,
                lastTokenLengths,
                ref matchStart,
                ref matchEnd))
        {
            matchStart = 0;
            matchLength = 0;
            return false;
        }

        matchLength = matchEnd - matchStart;
        return true;
    }

    private static bool TryIncludeMostRecentSignal(
        PromptInjectionLexicalSignal allowedSignals,
        int currentTokenIndex,
        ReadOnlySpan<int> lastTokenPositions,
        ReadOnlySpan<int> lastCharacterPositions,
        ReadOnlySpan<int> lastTokenLengths,
        ref int matchStart,
        ref int matchEnd)
    {
        var mostRecentSignalIndex = -1;
        var mostRecentTokenIndex = int.MinValue;
        var remainingSignals = (uint)allowedSignals;
        while (remainingSignals != 0)
        {
            var signalIndex = BitOperations.TrailingZeroCount(remainingSignals);
            if (lastTokenPositions[signalIndex] > mostRecentTokenIndex)
            {
                mostRecentSignalIndex = signalIndex;
                mostRecentTokenIndex = lastTokenPositions[signalIndex];
            }

            remainingSignals &= remainingSignals - 1;
        }

        if (mostRecentSignalIndex < 0 ||
            mostRecentTokenIndex < currentTokenIndex - (LEXICAL_WINDOW_SIZE - 1))
        {
            return false;
        }

        var signalStart = lastCharacterPositions[mostRecentSignalIndex];
        matchStart = Math.Min(matchStart, signalStart);
        matchEnd = Math.Max(matchEnd, signalStart + lastTokenLengths[mostRecentSignalIndex]);
        return true;
    }

    private static bool IsTypoglycemiaCandidate(ReadOnlySpan<char> token)
    {
        if (token.Length is < 5 or > 12)
            return false;

        foreach (var character in token)
        {
            if (!IsAsciiLetter(character))
                return false;
        }

        return true;
    }

    private static void ScanTypoglycemiaToken(
        string text,
        ReadOnlySpan<char> token,
        int tokenStart,
        List<PromptInjectionFinding> findings,
        HashSet<string> findingKeys)
    {
        var key = (token.Length, char.ToLowerInvariant(token[0]), char.ToLowerInvariant(token[^1]));
        if (!TYPOGLYCEMIA_KEYWORDS.TryGetValue(key, out var keywords))
            return;

        foreach (var keyword in keywords)
        {
            if (!IsTypoglycemiaVariant(token, keyword))
                continue;

            var snippet = ExtractSnippet(text, tokenStart, token.Length);
            AddFinding(findings, findingKeys, new($"typoglycemia:{keyword}", "evasion", snippet));
            return;
        }
    }

    private static bool IsAsciiLetter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsTypoglycemiaVariant(ReadOnlySpan<char> token, string keyword)
    {
        if (token.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return false;

        Span<int> characterCounts = stackalloc int[26];
        for (var index = 1; index < token.Length - 1; index++)
        {
            characterCounts[char.ToLowerInvariant(token[index]) - 'a']++;
            characterCounts[keyword[index] - 'a']--;
        }

        foreach (var count in characterCounts)
        {
            if (count != 0)
                return false;
        }

        return true;
    }

    private static string CollapseCharacterSpacedContent(string text)
    {
        return PromptInjectionPatterns.SpacedLetterSequenceRegex().Replace(text, static match =>
        {
            var builder = new StringBuilder(match.Value.Length);
            foreach (var character in match.Value)
            {
                if (char.IsLetter(character))
                    builder.Append(character);
            }

            return builder.ToString();
        });
    }

    private static string? TryDecodeBase64(ReadOnlySpan<char> candidate)
    {
        var encodedLength = Math.Min(candidate.Length, ((MAX_DECODED_TEXT_LENGTH + 2) / 3) * 4);
        encodedLength -= encodedLength % 4;
        if (encodedLength == 0)
            return null;

        var bytes = ArrayPool<byte>.Shared.Rent(MAX_DECODED_TEXT_LENGTH);
        try
        {
            if (!Convert.TryFromBase64Chars(candidate[..encodedLength], bytes, out var bytesWritten))
                return null;

            return ConvertDecodedBytesToText(bytes.AsSpan(0, bytesWritten));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static string? TryDecodeHex(ReadOnlySpan<char> candidate)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(MAX_DECODED_TEXT_LENGTH);
        try
        {
            var bytesWritten = 0;
            var highNibble = -1;
            foreach (var character in candidate)
            {
                var nibble = HexValue(character);
                if (nibble < 0)
                    continue;

                if (highNibble < 0)
                {
                    highNibble = nibble;
                    continue;
                }

                bytes[bytesWritten++] = (byte)((highNibble << 4) | nibble);
                highNibble = -1;
                if (bytesWritten >= MAX_DECODED_TEXT_LENGTH)
                    break;
            }

            return bytesWritten == 0 ? null : ConvertDecodedBytesToText(bytes.AsSpan(0, bytesWritten));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static int HexValue(char character)
    {
        if (character is >= '0' and <= '9')
            return character - '0';
        if (character is >= 'A' and <= 'F')
            return character - 'A' + 10;
        if (character is >= 'a' and <= 'f')
            return character - 'a' + 10;
        return -1;
    }

    private static string? ConvertDecodedBytesToText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return null;

        var text = Encoding.UTF8.GetString(bytes);
        return LooksTextLike(text) ? text : null;
    }

    private static bool LooksTextLike(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var printableCount = 0;
        foreach (var character in text)
        {
            if (!char.IsControl(character) || character is '\r' or '\n' or '\t')
                printableCount++;
        }

        return printableCount >= text.Length * 0.85;
    }

    private static void AddFinding(List<PromptInjectionFinding> findings, HashSet<string> findingKeys, PromptInjectionFinding finding)
    {
        var key = $"{finding.Category}|{finding.Snippet}";
        if (findingKeys.Add(key))
            findings.Add(finding);
    }

    private static string ExtractSnippet(string text, int index, int length)
    {
        var matchStart = Math.Clamp(index, 0, text.Length);
        var matchEnd = Math.Clamp(index + length, matchStart, text.Length);
        var sentenceStart = FindSentenceStart(text, matchStart);
        var sentenceEnd = FindSentenceEnd(text, matchEnd);

        while (sentenceStart < matchStart && char.IsWhiteSpace(text[sentenceStart]))
            sentenceStart++;

        while (sentenceEnd > matchEnd && char.IsWhiteSpace(text[sentenceEnd - 1]))
            sentenceEnd--;

        if (sentenceEnd - sentenceStart <= MAX_SNIPPET_LENGTH)
            return NormalizeSnippet(text[sentenceStart..sentenceEnd]);

        var matchLength = matchEnd - matchStart;
        if (matchLength >= MAX_SNIPPET_LENGTH - 6)
            return NormalizeSnippet(text[matchStart..matchEnd]);

        var contextBudget = MAX_SNIPPET_LENGTH - 6 - matchLength;
        var leftAvailable = matchStart - sentenceStart;
        var rightAvailable = sentenceEnd - matchEnd;
        var leftLength = Math.Min(leftAvailable, contextBudget / 2);
        var rightLength = Math.Min(rightAvailable, contextBudget - leftLength);
        var remainingBudget = contextBudget - leftLength - rightLength;

        leftLength += Math.Min(leftAvailable - leftLength, remainingBudget);
        remainingBudget = contextBudget - leftLength - rightLength;
        rightLength += Math.Min(rightAvailable - rightLength, remainingBudget);

        var snippetStart = matchStart - leftLength;
        var snippetEnd = matchEnd + rightLength;
        var snippet = NormalizeSnippet(text[snippetStart..snippetEnd]);
        var prefix = snippetStart > sentenceStart ? "..." : string.Empty;
        var suffix = snippetEnd < sentenceEnd ? "..." : string.Empty;
        return $"{prefix}{snippet}{suffix}";
    }

    private static int FindSentenceStart(string text, int matchStart)
    {
        for (var index = matchStart - 1; index >= 0; index--)
        {
            if (IsSentenceBoundary(text[index]))
                return index + 1;
        }

        return matchStart;
    }

    private static int FindSentenceEnd(string text, int matchEnd)
    {
        for (var index = matchEnd; index < text.Length; index++)
        {
            if (IsSentenceBoundary(text[index]))
                return index + 1;
        }

        return matchEnd;
    }

    private static bool IsSentenceBoundary(char character) => character is '.' or '!' or '?' or '\r' or '\n';

    private static string NormalizeSnippet(ReadOnlySpan<char> snippet)
    {
        var normalized = new StringBuilder(snippet.Length);
        var previousCharacterWasWhitespace = false;
        foreach (var character in snippet)
        {
            if (char.IsWhiteSpace(character))
            {
                if (normalized.Length > 0 && !previousCharacterWasWhitespace)
                    normalized.Append(' ');

                previousCharacterWasWhitespace = true;
                continue;
            }

            normalized.Append(character);
            previousCharacterWasWhitespace = false;
        }

        return normalized.ToString().Trim();
    }

    private static IReadOnlyDictionary<(int Length, char First, char Last), string[]> CreateTypoglycemiaKeywordIndex()
    {
        string[] keywords =
        [
            "ignore", "bypass", "override", "reveal", "forget", "disregard", "delete", "reset", "expose",
            "system", "prompt", "policy", "safety", "developer", "instructions", "admin", "secret", "token", "credential",
        ];

        return keywords
            .GroupBy(keyword => (keyword.Length, keyword[0], keyword[^1]))
            .ToDictionary(group => group.Key, group => group.ToArray());
    }
}