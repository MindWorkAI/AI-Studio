namespace AIStudio.Assistants.ERI;

/// <summary>
/// Tells the user why the ERI Server assistant did not write a file.
/// </summary>
/// <remarks>
/// Up to v26.10.1, such a file only showed up in the log, and the user found out by comparing the
/// base directory with the list of files in the answer.
/// </remarks>
internal static class ERIServerFileOutcomeExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(ERIServerFileOutcomeExtensions).Namespace, nameof(ERIServerFileOutcomeExtensions));

    /// <summary>
    /// Gets the localized message which explains why a file was not written.
    /// </summary>
    /// <param name="outcome">What became of the file.</param>
    /// <param name="file">The path of the file, as the model named it.</param>
    /// <returns>The localized message.</returns>
    internal static string ToUserMessage(this ERIServerFileOutcome outcome, string file) => string.Format(ToUserMessageFormat(outcome), file);

    private static string ToUserMessageFormat(ERIServerFileOutcome outcome) => outcome switch
    {
        ERIServerFileOutcome.OUTSIDE_BASE_DIRECTORY => TB("The file '{0}' was not written, because its path leads outside the base directory."),
        ERIServerFileOutcome.NO_COMPLETE_CODE_BLOCK => TB("The file '{0}' was not written, because the answer of the model contains no complete code block for it. You find the answer in the conversation."),
        ERIServerFileOutcome.WRITE_FAILED => TB("The file '{0}' could not be written. The log of AI Studio names the reason."),

        _ => TB("The file '{0}' was not written."),
    };
}