namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A document the person added to a preparation, e.g., notes, a talk draft, a paper, or an invitation, which the
/// chats of every step read.
/// </summary>
/// <remarks>
/// The lab reads a document once, when the person adds it, and keeps the text which passed the prompt injection
/// filter beside this record, as materials/ID.md in the folder of the preparation; this record lies beside it as
/// materials/ID.json. Neither keeps the path of the document: the person may move or delete it afterward, and the
/// preparation still works on another computer after a backup.
/// </remarks>
public sealed class BflMaterial
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes. Raise it with every new property.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets the ID of the material, which also names its files.
    /// </summary>
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the name of the document as the person knows it, e.g., "Invitation.docx", never its path.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the person added the material.
    /// </summary>
    public DateTimeOffset AddedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the size of the document in bytes.
    /// </summary>
    public long SourceBytes { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the document, in lowercase hexadecimal, to recognize a document the person
    /// added already.
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many characters the stored text has, so that the lab can share out the room in a prompt
    /// without reading every text.
    /// </summary>
    public int TextLength { get; set; }

    /// <summary>
    /// Gets or sets whether parts of the document could not be read, e.g., single pages of a PDF.
    /// </summary>
    public bool IsIncomplete { get; set; }

    /// <summary>
    /// Gets or sets whether the lab kept only the beginning of the text, because the document holds more than a
    /// conversation can ever take of one material.
    /// </summary>
    public bool IsShortened { get; set; }

    /// <summary>
    /// Gets or sets the pages which could not be read, when they are known.
    /// </summary>
    public List<int> FailedPages { get; set; } = [];

    /// <summary>
    /// Gets or sets how many passages the prompt injection filter removed from the text.
    /// </summary>
    public int FilteredPassages { get; set; }
}