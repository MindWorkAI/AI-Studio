namespace AIStudio.Chat;

/// <summary>
/// Represents different types of file attachments.
/// </summary>
public enum FileAttachmentType
{
    /// <summary>
    /// Document file types, such as .pdf, .docx, .txt, etc.
    /// </summary>
    DOCUMENT,
    
    /// <summary>
    /// All image file types, such as .jpg, .png, .gif, etc.
    /// </summary>
    IMAGE,
    
    /// <summary>
    /// All audio file types, such as .mp3, .wav, .aac, etc.
    /// </summary>
    AUDIO,
}