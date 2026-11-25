namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A public detail about the event, as the research received it.
/// </summary>
/// <remarks>
/// Only fields marked with BflFieldDefinition.IsSharedWithResearch become such a detail, see
/// BflPromptComposer.PublicDetailsOf. A research keeps the details it received, so that the person can see them, and
/// so that the lab can tell when the details changed since.
/// </remarks>
public sealed class BflResearchDetail
{
    /// <summary>
    /// Gets or sets the ID of the field, see BflFieldCatalog.
    /// </summary>
    public string FieldId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value of the field, as the research received it.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}