using System.Text;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Tells whether words hold letters, i.e., whether a model has anything to work on.
/// </summary>
/// <remarks>
/// Letters count by Unicode scalar values rather than by chars: a letter beyond the Basic Multilingual Plane, e.g.,
/// of Adlam or a rare CJK character, takes two chars, neither of which is a letter on its own.
/// </remarks>
internal static class TextLetterExtensions
{
    /// <param name="text">The words.</param>
    extension(string text)
    {
        /// <summary>
        /// Whether the words hold any letter.
        /// </summary>
        public bool HasLetters => text.EnumerateRunes().Any(Rune.IsLetter);

        /// <summary>
        /// How many letters the words hold.
        /// </summary>
        public int LetterCount => text.EnumerateRunes().Count(Rune.IsLetter);
    }
}