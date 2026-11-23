using System.Text;

namespace AIStudio.Settings.DataModel;

public static class MailboxPasswordEncodingExtensions
{
    /// <summary>
    /// UTF-8 which refuses what it cannot encode, such as a lone surrogate, instead of replacing it.
    /// </summary>
    private static readonly Encoding UTF8_STRICT = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Windows-1252 which refuses a character it lacks, such as ẞ, instead of sending a question mark.
    /// </summary>
    /// <remarks>
    /// .NET knows the code pages of Windows only once their provider is registered.
    /// </remarks>
    private static readonly Lazy<Encoding> WINDOWS_1252_STRICT = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    });

    extension(MailboxPasswordEncoding passwordEncoding)
    {
        /// <summary>
        /// The encoding to hand to the IMAP client.
        /// </summary>
        /// <value>The encoding, or null for one this version does not know.</value>
        public Encoding? TextEncoding => passwordEncoding switch
        {
            MailboxPasswordEncoding.UTF8 => UTF8_STRICT,
            MailboxPasswordEncoding.WINDOWS_1252 => WINDOWS_1252_STRICT.Value,

            _ => null,
        };

        /// <summary>
        /// Prepares a username or a password for this encoding.
        /// </summary>
        /// <remarks>
        /// For Windows-1252, the text is normalized first (NFC): an ä may arrive as an a followed by a
        /// combining diaeresis, e.g., from a password manager, and only the composed ä exists in
        /// Windows-1252. UTF-8 takes the text as it is, as it always did.
        /// </remarks>
        /// <param name="text">The username or the password.</param>
        /// <param name="prepared">The text to sign in with.</param>
        /// <returns>Whether the encoding has every character of the text.</returns>
        public bool TryPrepare(string text, out string prepared)
        {
            prepared = text;
            if (passwordEncoding.TextEncoding is not { } encoding)
                return false;

            try
            {
                if (passwordEncoding is MailboxPasswordEncoding.WINDOWS_1252)
                    prepared = text.Normalize(NormalizationForm.FormC);

                encoding.GetByteCount(prepared);
                return true;
            }

            // Thrown for a character the encoding lacks, and for a lone surrogate, which no encoding has.
            // EncoderFallbackException is one of them:
            catch (ArgumentException)
            {
                prepared = text;
                return false;
            }
        }
    }
}