using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Rust;

/// <summary>
/// Translates why an image could not be prepared for an AI model into user-facing text.
/// </summary>
/// <remarks>
/// The runtime writes the technical details into the log. The texts here name what the user can
/// act on, such as converting the image into another format.
/// </remarks>
public static class ModelImageErrorCodeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ModelImageErrorCodeExtensions).Namespace, nameof(ModelImageErrorCodeExtensions));

    /// <summary>
    /// Gets the localized message which explains why an image was not sent to the AI.
    /// </summary>
    /// <param name="code">Why the image could not be prepared.</param>
    /// <param name="fileName">The name of the image file, as shown to the user.</param>
    /// <returns>The localized message.</returns>
    public static string ToUserMessage(this ModelImageErrorCode code, string fileName) => string.Format(ToUserMessageFormat(code), fileName);

    private static string ToUserMessageFormat(ModelImageErrorCode code) => code switch
    {
        ModelImageErrorCode.FILE_NOT_FOUND => TB("The image '{0}' does not exist anymore and was not sent."),
        ModelImageErrorCode.FILE_NOT_READABLE => TB("The image '{0}' could not be opened and was not sent. When the image is stored on a network drive, the drive might be unavailable."),
        ModelImageErrorCode.UNSUPPORTED_FORMAT => TB("The image '{0}' uses a format which AI Studio cannot give to an AI model, so it was not sent. Please convert the image to JPG or PNG."),
        ModelImageErrorCode.CONTENT_MISMATCH => TB("The image '{0}' is not what its file extension claims, so it was not sent. Please correct its file extension."),
        ModelImageErrorCode.NOT_DECODABLE => TB("The image '{0}' could not be read and was not sent. It might be damaged or transferred incompletely."),
        ModelImageErrorCode.TOO_MANY_PIXELS => TB("The image '{0}' has too many pixels for AI Studio to prepare it, so it was not sent. Please scale the image down first."),
        ModelImageErrorCode.BUDGET_NOT_REACHABLE => TB("The image '{0}' could not be made small enough for the selected provider, so it was not sent."),

        _ => TB("AI Studio was not able to prepare the image '{0}' for the AI, so it was not sent."),
    };
}