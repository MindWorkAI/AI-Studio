using System.Runtime.CompilerServices;
using System.Text;

namespace SharedTools;

/// <summary>
/// Appends lines with LF, the same line break on every system.
/// </summary>
/// <remarks>
/// StringBuilder.AppendLine writes Environment.NewLine, which is CR LF on Windows: a text built with it differs between
/// the systems, and it mixes two kinds of line breaks as soon as one of its parts comes with LF. These members carry a
/// name of their own, because C# always prefers the instance method AppendLine over an extension of the same name.
/// </remarks>
public static class StringBuilderExtensions
{
    private const char LINE_FEED = '\n';

    /// <param name="builder">The builder to append to.</param>
    extension(StringBuilder builder)
    {
        /// <summary>
        /// Appends a line break, LF.
        /// </summary>
        /// <returns>The builder, to append more.</returns>
        public StringBuilder AppendLineLF() => builder.Append(LINE_FEED);

        /// <summary>
        /// Appends a text and a line break, LF.
        /// </summary>
        /// <param name="value">The text; for null, only the line break.</param>
        /// <returns>The builder, to append more.</returns>
        public StringBuilder AppendLineLF(string? value) => builder.Append(value).Append(LINE_FEED);

        /// <summary>
        /// Appends an interpolated text and a line break, LF.
        /// </summary>
        /// <remarks>
        /// Like StringBuilder.AppendLine, the handler writes the text right into the builder, without a string in
        /// between; afterward, only the line break is missing.
        /// </remarks>
        /// <param name="handler">The handler which the compiler creates for the interpolated text.</param>
        /// <returns>The builder, to append more.</returns>
        public StringBuilder AppendLineLF([InterpolatedStringHandlerArgument("builder")] ref StringBuilder.AppendInterpolatedStringHandler handler) => builder.Append(LINE_FEED);
    }
}