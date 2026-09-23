using System.Globalization;

namespace AIStudio.Chat;

/// <summary>
/// Writes a number of tokens the way a person reads it next to their input field.
/// </summary>
/// <remarks>
/// A context window of a million tokens written out in full is eight characters of noise under a
/// text field, and nobody reads the last five of them. So everything from a thousand on is
/// shortened, and two decimals keep the resolution a person acts on: the difference between 1.20k
/// and 1.80k is one they can see, while the last three digits of 1,234 are not.
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
    private const int EXACT_BELOW = 1_000;

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

        //
        // Rounded before the unit is chosen, not after. Otherwise the few hundred tokens just below
        // a million round up inside their own unit and read as "1,000.00k", which is a number
        // nobody writes.
        //
        var thousands = tokens / 1_000d;
        if (Math.Round(thousands, 2) < 1_000d)
            return $"{thousands.ToString("N2", culture)}k";

        return $"{(tokens / 1_000_000d).ToString("N2", culture)}M";
    }
}