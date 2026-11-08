namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A preparation in the BeyondFacts Lab: one event, for which a person works through the steps of the lab.
/// </summary>
/// <remarks>
/// The lab stores it as project.json in the folder of the preparation. Everything else the preparation
/// holds lies in files beside it, which the lab finds by the ID of the preparation, never by a stored path.
/// </remarks>
public sealed class BflProject
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes.
    /// </summary>
    /// <remarks>
    /// A preparation of a newer form is listed but neither opened nor written, so that an older version of
    /// AI Studio cannot lose what a newer one stored. Raise it with every field a released version does not
    /// know yet: that version refuses fields it does not know, and only the higher number tells the person
    /// that a newer version stored the preparation.
    /// </remarks>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets the ID of the preparation, which is also the name of its folder.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the name the person gave the preparation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the preparation was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the preparation was changed last.
    /// </summary>
    public DateTimeOffset ModifiedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the person marked the event as having taken place, or null while it has not.
    /// </summary>
    public DateTimeOffset? HappenedAtUtc { get; set; }
}