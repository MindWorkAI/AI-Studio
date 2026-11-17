using System.Text.Json.Nodes;

using AIStudio.Tools.RAG;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// A mail found, written for the model and for the user once its texts went through the filter.
/// </summary>
/// <param name="Description">What the model reads of the mail.</param>
/// <param name="Source">The source of the mail, listed below the answer.</param>
/// <param name="Passage">What the user is shown of the mail: what the model received of it.</param>
/// <param name="Characters">How long the description is, as the model receives it.</param>
internal sealed record DescribedMail(JsonObject Description, Source Source, SourcePassage Passage, int Characters);