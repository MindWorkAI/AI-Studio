using System.Text.Json.Nodes;

using AIStudio.Tools.RAG;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// What the model and the user get of a mail which the model read, cf. ReadMailTool.
/// </summary>
/// <param name="Json">The result for the model, with every text filtered for prompt injections.</param>
/// <param name="Source">The source of the mail, which the answer lists.</param>
/// <param name="Passage">What the user reads of the mail under its source.</param>
internal sealed record MailReadingResult(JsonObject Json, Source Source, SourcePassage Passage);