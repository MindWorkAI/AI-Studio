using System.Runtime.CompilerServices;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Loads the mails in the Fixtures folder next to this file.
/// </summary>
/// <remarks>
/// The fixtures are real mails, encoded the way mail programs send them, so that a test sees what
/// MimeKit makes of the encodings rather than text which was never encoded at all.
/// </remarks>
internal static class MailFixtures
{
    public static MimeMessage Load(string fileName, [CallerFilePath] string sourceFilePath = "") =>
        MimeMessage.Load(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "Fixtures", fileName));
}