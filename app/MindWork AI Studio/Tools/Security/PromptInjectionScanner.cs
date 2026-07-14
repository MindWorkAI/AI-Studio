using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace AIStudio.Tools.Security;

public sealed class PromptInjectionScanner(ILogger<PromptInjectionScanner> logger)
{
    private const int MAX_DECODED_CANDIDATES_PER_ENCODING = 12;
    private const int MAX_DECODED_TEXT_LENGTH = 12_000;
    private const int MAX_FINDINGS = 8;
    private const int SNIPPET_RADIUS = 80;

    private static readonly IReadOnlyDictionary<(int Length, char First, char Last), string[]> TYPOGLYCEMIA_KEYWORDS =
        CreateTypoglycemiaKeywordIndex();

    public PromptInjectionScanResult Scan(string text, PromptInjectionSource source)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new(source, []);

        var findings = new List<PromptInjectionFinding>();
        var findingKeys = new HashSet<string>(StringComparer.Ordinal);

        this.ScanVariant(text, "raw", findings, findingKeys);
        if (findings.Count >= MAX_FINDINGS)
            return new(source, findings);

        var collapsed = CollapseCharacterSpacedContent(text);
        if (!string.Equals(text, collapsed, StringComparison.Ordinal))
            this.ScanVariant(collapsed, "character_spacing", findings, findingKeys);

        if (findings.Count < MAX_FINDINGS)
            this.ScanDecodedCandidates(text, findings, findingKeys);

        if (findings.Count < MAX_FINDINGS)
            this.ScanTypoglycemia(text, findings, findingKeys);

        return new(source, findings);
    }

    private void ScanVariant(string text, string stage, List<PromptInjectionFinding> findings, HashSet<string> findingKeys)
    {
        try
        {
            if (!PromptInjectionPatterns.AnyRuleRegex().IsMatch(text))
                return;
        }
        catch (RegexMatchTimeoutException exception)
        {
            logger.LogWarning(exception, "Prompt-injection regex prefilter timed out during stage '{Stage}'. Falling back to individual rules.", stage);
        }

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

            var snippet = ExtractSnippet(text, match.Index, match.Length, SNIPPET_RADIUS);
            AddFinding(findings, findingKeys, new(rule.Id, rule.Category, stage, snippet));
        }
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

    private void ScanTypoglycemia(string text, List<PromptInjectionFinding> findings, HashSet<string> findingKeys)
    {
        foreach (var match in PromptInjectionPatterns.WordRegex().EnumerateMatches(text))
        {
            if (findings.Count >= MAX_FINDINGS)
                return;

            var token = text.AsSpan(match.Index, match.Length);
            var key = (token.Length, char.ToLowerInvariant(token[0]), char.ToLowerInvariant(token[^1]));
            if (!TYPOGLYCEMIA_KEYWORDS.TryGetValue(key, out var keywords))
                continue;

            foreach (var keyword in keywords)
            {
                if (!IsTypoglycemiaVariant(token, keyword))
                    continue;

                var snippet = ExtractSnippet(text, match.Index, match.Length, SNIPPET_RADIUS);
                AddFinding(findings, findingKeys, new($"typoglycemia:{keyword}", "evasion", "typoglycemia", snippet));
                break;
            }
        }
    }

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
        var key = $"{finding.RuleId}|{finding.DetectionStage}|{finding.Snippet}";
        if (findingKeys.Add(key))
            findings.Add(finding);
    }

    private static string ExtractSnippet(string text, int index, int length, int radius)
    {
        var start = Math.Max(0, index - radius);
        var end = Math.Min(text.Length, index + length + radius);
        var snippet = text[start..end].Replace('\r', ' ').Replace('\n', ' ').Trim();
        return snippet.Length > radius * 2 ? $"{snippet[..(radius * 2)]}..." : snippet;
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