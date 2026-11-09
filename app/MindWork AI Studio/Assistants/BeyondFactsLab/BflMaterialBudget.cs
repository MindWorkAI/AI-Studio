using AIStudio.Models;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Decides how much of the material of a preparation the prompt of a step carries.
/// </summary>
/// <remarks>
/// <para>
/// Every request of a step carries the guide and the material in its system prompt, so the material must leave the
/// conversation room in the context of the model. It gets half of what the prompt without material leaves free, and
/// the lab shares that out evenly: a short material comes whole, and the long ones are shortened alike. The budget
/// counts characters, about four per token like the limits of the prompts, so that putting a prompt together needs
/// no tokenizer; the token budget of the chat still counts what a request really carries.
/// </para>
/// <para>
/// A shortened material is the beginning of the text which passed the prompt injection filter when the person added
/// it. It does not pass the filter again: cutting off the end of a text adds nothing which the filter did not see,
/// unlike IRetrievalContextExtensions.ShortenToFitAsync, which cuts the unfiltered text of a data source.
/// </para>
/// </remarks>
public static class BflMaterialBudget
{
    /// <summary>
    /// The share of what the prompt without material leaves free in the context window, which the material may take.
    /// </summary>
    /// <remarks>
    /// The other half stays for the conversation, so that a long material does not end it after a few messages.
    /// </remarks>
    public const double SHARE_OF_THE_FREE_CONTEXT = 0.5;

    /// <summary>
    /// How many characters the budget counts for one token, about as many as a German text takes.
    /// </summary>
    public const int CHARACTERS_PER_TOKEN = 4;

    /// <summary>
    /// How many characters the material takes at most, whatever the model reads at once.
    /// </summary>
    /// <remarks>
    /// About 100,000 tokens. Every message of a conversation carries the material again, so a book in the material
    /// of a model with a huge context window would make every message slow and expensive.
    /// </remarks>
    public const int MAX_CHARACTERS = 400_000;

    /// <summary>
    /// How many characters the material takes when nobody knows the context window of the model.
    /// </summary>
    /// <remarks>
    /// About 10,000 tokens: together with the guide and a conversation, that fits a model which reads 64,000 tokens
    /// at once. The token budget of the chat tells the person when a conversation grows too long.
    /// </remarks>
    public const int CHARACTERS_WITHOUT_A_KNOWN_CONTEXT_WINDOW = 40_000;

    /// <summary>
    /// How much of what fits a shortened material gives up at most to end at a line end, as a share.
    /// </summary>
    private const double MAX_SHARE_GIVEN_UP_FOR_A_LINE_END = 0.1;

    /// <summary>
    /// Gets how many characters the material of a preparation may take in the prompt of a step.
    /// </summary>
    /// <param name="contextWindow">The context window of the model which answers.</param>
    /// <param name="promptCharacters">How many characters the system prompt has without the material.</param>
    /// <returns>The characters, between 0 and MAX_CHARACTERS.</returns>
    public static int CharactersFor(ContextWindow contextWindow, int promptCharacters)
    {
        if (!contextWindow.IsKnown)
            return CHARACTERS_WITHOUT_A_KNOWN_CONTEXT_WINDOW;

        var freeCharacters = ((long)contextWindow.DefaultTokens - BflTokenBudget.RESERVE_TOKENS) * CHARACTERS_PER_TOKEN - promptCharacters;
        return (int)Math.Clamp((long)(freeCharacters * SHARE_OF_THE_FREE_CONTEXT), 0, MAX_CHARACTERS);
    }

    /// <summary>
    /// Shares out the characters among the materials evenly: what a short material does not need goes to the others.
    /// </summary>
    /// <param name="materials">The materials, in the order the person added them.</param>
    /// <param name="characters">How many characters the materials may take together.</param>
    /// <returns>The share of each material, in the same order.</returns>
    public static IReadOnlyList<BflMaterialShare> Share(IReadOnlyList<BflMaterial> materials, int characters)
    {
        var shares = new int[materials.Count];
        var remaining = Math.Max(0, characters);
        var open = materials.Count;
        foreach (var index in Enumerable.Range(0, materials.Count).OrderBy(index => materials[index].TextLength).ThenBy(index => index))
        {
            var share = Math.Min(Math.Max(0, materials[index].TextLength), remaining / open);
            shares[index] = share;
            remaining -= share;
            open--;
        }

        return [.. materials.Select((material, index) => new BflMaterialShare(material, shares[index]))];
    }

    /// <summary>
    /// Ends the beginning of a longer text at a line end, so that a shortened material keeps whole lines, such as
    /// the rows of a table.
    /// </summary>
    /// <remarks>
    /// A text whose last line end lies too far from its end is cut where it ends, though never between the two halves
    /// of a character outside the Basic Multilingual Plane.
    /// </remarks>
    /// <param name="beginning">The beginning of the text, as long as its share.</param>
    /// <returns>The beginning, ending at a line end where possible.</returns>
    public static string EndAtLineEnd(string beginning)
    {
        var lineEnd = beginning.LastIndexOf('\n');
        if (lineEnd >= 0 && lineEnd + 1 >= beginning.Length * (1 - MAX_SHARE_GIVEN_UP_FOR_A_LINE_END))
            return beginning[..lineEnd].TrimEnd();

        var end = beginning.Length;
        if (end > 0 && char.IsHighSurrogate(beginning[end - 1]))
            end--;

        return beginning[..end].TrimEnd();
    }
}