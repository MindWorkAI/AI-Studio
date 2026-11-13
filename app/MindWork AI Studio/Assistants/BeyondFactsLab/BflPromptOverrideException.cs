namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Tells that a file in the folder for prompt overrides contains a placeholder which the lab does not fill in it.
/// </summary>
/// <remarks>
/// The lab fills placeholders in the core and in the prompt of the onboarding only, and only those it knows; in any
/// other file, a placeholder would reach the model as text. Whoever tests a changed prompt needs to learn which file
/// and which placeholder it is, so the message names both.
/// </remarks>
/// <param name="fileName">The file, relative to the folder for prompt overrides, with forward slashes.</param>
/// <param name="placeholder">The name of the placeholder, without braces.</param>
public sealed class BflPromptOverrideException(string fileName, string placeholder) : FormatException($"The file '{fileName}' in the folder for prompt overrides contains the placeholder '{placeholder}', which the lab does not fill in this file.")
{
    /// <summary>
    /// Gets the file, relative to the folder for prompt overrides, with forward slashes.
    /// </summary>
    public string FileName { get; } = fileName;

    /// <summary>
    /// Gets the name of the placeholder, without braces.
    /// </summary>
    public string Placeholder { get; } = placeholder;
}