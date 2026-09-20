using System.Globalization;

namespace AIStudio.Chat;

/// <summary>
/// Writes a number of tokens the way a person reads it next to their input field.
/// </summary>
/// <remarks>
/// Written out in full up to a million, because below that the short form saves nothing: "11.86k"
/// and "11,860" are both six characters, and "999.99k" and "999,990" are both seven. All the
/// prefix does in that range is ask the reader to know what it stands for, and not every reader
/// does. From a million on it earns its place -- nine characters of digits become five -- and two
/// decimals there keep the resolution a person acts on.
///
/// The culture is passed in rather than taken from the thread. AI Studio's language is chosen in
/// its settings and does not move the thread's culture along with it, so a German who picked German
/// would otherwise read English separators inside a German sentence.
/// </remarks>
public static class TokenAmount
{
    /// <summary>
    /// Below this, the exact number is shown.
    /// </summary>
    private const int EXACT_BELOW = 1_000_000;

    /// <summary>
    /// Writes a number of tokens.
    /// </summary>
    /// <param name="tokens">The number of tokens.</param>
    /// <param name="culture">The culture whose separators the number is written with.</param>
    /// <returns>The number, shortened from a thousand on.</returns>
    public static string Format(int tokens, CultureInfo culture)
    {
        if (tokens < EXACT_BELOW)
            return tokens.ToString("N0", culture);

        return $"{(tokens / 1_000_000d).ToString("N2", culture)}M";
    }
}