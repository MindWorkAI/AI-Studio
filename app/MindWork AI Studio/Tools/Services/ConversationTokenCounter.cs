using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.Services;

//
// Inside the namespace on purpose. A "Provider" written here would otherwise be the namespace
// AIStudio.Provider, which every namespace below AIStudio sees before it sees a file's aliases.
//
using Provider = AIStudio.Settings.Provider;

/// <summary>
/// Counts what a conversation takes out of a model's context window.
/// </summary>
/// <remarks>
/// Asked from the chat while somebody types, so what it must not do is as important as what it
/// does. Every document is read and measured once and then remembered, because extracting a
/// thousand-page PDF on each keystroke would be unusable. The conversation so far is remembered the
/// same way, so typing measures the sentence being typed rather than the whole chat again.
///
/// The numbers are estimates and are shown as such. Unless somebody configured the model's own
/// tokenizer for their provider, the built-in one does the counting, and two tokenizers disagree by
/// a few percent on prose and by more than that on code.
/// </remarks>
public sealed class ConversationTokenCounter(RustService rustService, ILogger<ConversationTokenCounter> logger)
{
    /// <summary>
    /// How much text goes into one counting request.
    /// </summary>
    /// <remarks>
    /// The same bound the rest of the app uses when it hands text to the tokenizer. Longer
    /// conversations are counted in several pieces and added up, which costs a handful of special
    /// tokens per piece -- a rounding error against a window of hundreds of thousands.
    /// </remarks>
    private const int CHUNK_SIZE = RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH;

    /// <summary>
    /// What separates the parts of a key.
    /// </summary>
    /// <remarks>
    /// A character which cannot occur in a path, a tokenizer name or a hash, so that no two
    /// different keys can be spelled the same way by accident. Written as an escape rather than as
    /// the character itself: a source file carrying a raw zero byte is a binary file as far as Git
    /// is concerned, and stops being reviewable.
    /// </remarks>
    private const string KEY_SEPARATOR = "\0";

    private readonly ConcurrentDictionary<string, int> counted = new(StringComparer.Ordinal);

    /// <summary>
    /// What the texts which were still being written cost during the previous count.
    /// </summary>
    /// <remarks>
    /// One run's worth, replaced by the next -- so at most the draft and the answer being streamed
    /// stand in here. It exists for the case where nothing about them changed: a draft somebody left
    /// standing while they think would otherwise be measured again on every heartbeat, and that is a
    /// call to the tokenizer for an answer we already have.
    /// </remarks>
    private IReadOnlyDictionary<string, int> stillGrowing = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Counts what the next request would carry.
    /// </summary>
    /// <param name="provider">The configured provider, which decides both the tokenizer and the window.</param>
    /// <param name="parts">What the conversation would send, collected beforehand.</param>
    /// <param name="token">Ends the counting when nobody needs the answer anymore.</param>
    /// <returns>What the conversation costs, or that nothing could be counted.</returns>
    public async Task<ConversationTokens> CountAsync(Provider provider, ConversationParts parts, CancellationToken token = default)
    {
        if (provider.UsedLLMProvider is LLMProviders.NONE)
            return ConversationTokens.UNAVAILABLE;

        var profile = provider.GetModelProfile();
        var previouslyGrowing = this.stillGrowing;
        var growing = new Dictionary<string, int>(StringComparer.Ordinal);
        var tokens = 0;

        try
        {
            foreach (var text in parts.Texts)
                tokens += await this.CountTextAsync(provider, text, token);

            //
            // A text which is still being written is measured whole every time rather than by its
            // increment. Two counts meet at a token boundary, and adding up the pieces drifts
            // further from the truth with every three seconds an answer goes on.
            //
            foreach (var text in parts.GrowingTexts)
            {
                var key = Key(provider, text);
                if (!previouslyGrowing.TryGetValue(key, out var known))
                    known = await this.MeasureAsync(provider, text, token);

                growing[key] = known;
                tokens += known;
            }

            foreach (var document in parts.Documents)
                tokens += await this.CountDocumentAsync(provider, document, token);
        }
        catch (OperationCanceledException)
        {
            return ConversationTokens.UNAVAILABLE;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not count the tokens of this conversation.");
            return ConversationTokens.UNAVAILABLE;
        }

        this.stillGrowing = growing;

        return new()
        {
            IsKnown = true,
            Tokens = tokens,
            IsEstimate = string.IsNullOrWhiteSpace(provider.TokenizerPath),
            Window = profile.Context,
            UncountedImages = parts.Images,
            ImageLimits = profile.Images,
        };
    }

    /// <summary>
    /// Forgets everything counted so far.
    /// </summary>
    /// <remarks>
    /// Needed when a file changed behind our back in a way its size and time do not show, which is
    /// rare enough that nothing calls this today. It exists so that the cache has a way out other
    /// than restarting the app.
    /// </remarks>
    public void Forget()
    {
        this.counted.Clear();
        this.stillGrowing = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Counts one text, remembering the answer under a fingerprint of it.
    /// </summary>
    /// <remarks>
    /// This is what makes typing affordable. A message which was sent an hour ago says exactly what
    /// it said then, and its tokens are the same number every time -- so the whole conversation is
    /// measured once and every keystroke afterwards measures the sentence being written.
    ///
    /// Keyed by a hash rather than by the text, because the key of the cache would otherwise be a
    /// second copy of the whole conversation in memory. Hashing is not free, but it is two orders of
    /// magnitude cheaper than tokenizing the same bytes, so the trade pays for itself on the first
    /// repeat.
    /// </remarks>
    private async Task<int> CountTextAsync(Provider provider, string text, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var key = Key(provider, text);
        if (this.counted.TryGetValue(key, out var known))
            return known;

        var tokens = await this.MeasureAsync(provider, text, token);
        this.counted[key] = tokens;
        return tokens;
    }

    /// <summary>
    /// Measures one text without remembering the answer.
    /// </summary>
    /// <remarks>
    /// A text longer than one request is split. Cutting between characters rather than between
    /// words costs a token or two where the cut falls, which is the cheapest way to stay inside the
    /// bound without pretending to know the language.
    /// </remarks>
    private async Task<int> MeasureAsync(Provider provider, string text, CancellationToken token)
    {
        var tokens = 0;
        for (var start = 0; start < text.Length; start += CHUNK_SIZE)
            tokens += await this.AskTokenizerAsync(provider, text.Substring(start, Math.Min(CHUNK_SIZE, text.Length - start)), token);

        return tokens;
    }

    /// <summary>
    /// Under which name one text is remembered.
    /// </summary>
    /// <remarks>
    /// The tokenizer travels in the key: the same text counted for two providers is two different
    /// numbers, and handing one of them to the other would be wrong in exactly the case somebody
    /// switches providers to see whether their chat fits.
    /// </remarks>
    private static string Key(Provider provider, string text) => $"{provider.TokenizerPath}{KEY_SEPARATOR}{Fingerprint(text)}";

    /// <summary>
    /// A short, stable name for a piece of text.
    /// </summary>
    /// <remarks>
    /// The length travels along with the hash. Two texts colliding on the hash and agreeing on
    /// their length as well is not something which happens by accident, and nothing here is a
    /// security decision: the worst a collision could do is show a number which is a few tokens off.
    /// </remarks>
    private static string Fingerprint(string text) => $"{text.Length}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}";

    /// <summary>
    /// Counts one document, reading it the first time and remembering it afterwards.
    /// </summary>
    /// <remarks>
    /// The key carries the tokenizer as well as the file: the same document counted for two
    /// providers is two different numbers, and handing one of them to the other would be wrong in
    /// exactly the case somebody switches providers to see whether their chat fits.
    /// </remarks>
    private async Task<int> CountDocumentAsync(Provider provider, FileAttachment document, CancellationToken token)
    {
        var file = new FileInfo(document.FilePath);
        if (!file.Exists)
            return 0;

        var key = $"{provider.TokenizerPath}{KEY_SEPARATOR}{file.FullName}{KEY_SEPARATOR}{file.Length}{KEY_SEPARATOR}{file.LastWriteTimeUtc.Ticks}";
        if (this.counted.TryGetValue(key, out var known))
            return known;

        //
        // Read without telling the user about filtered passages. Nothing here is sent anywhere: the
        // text is measured and dropped, and the warning belongs to the moment the document actually
        // travels -- where it is still given.
        //
        var extraction = await rustService.ReadArbitraryFileData(document.FilePath, int.MaxValue, reportPromptInjections: false, token: token);
        if (!extraction.HasUsableContent)
        {
            //
            // A document which cannot be read is not sent either, so it costs nothing. Remembered
            // as zero so that a broken file is not read again on every keystroke.
            //
            logger.LogInformation("The attachment '{FilePath}' could not be read and is therefore counted as nothing.", document.FilePath);
            this.counted[key] = 0;
            return 0;
        }

        var tokens = await this.CountTextAsync(provider, extraction.Content, token);
        this.counted[key] = tokens;
        return tokens;
    }

    private async Task<int> AskTokenizerAsync(Provider provider, string text, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var response = await rustService.GetTokenCount(provider, text, token);
        if (response is null || !response.Value.Success)
            throw new InvalidOperationException($"The tokenizer did not answer: {response?.Message}");

        return response.Value.TokenCount;
    }
}