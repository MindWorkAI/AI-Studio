using AIStudio.Components;
using AIStudio.Tools.Imports;

namespace AIStudio.Tools.Web;

/// <summary>
/// A copy of the state of a running web content import, see WebContentImportService.GetSnapshot.
/// </summary>
/// <param name="Target">The field the import fills.</param>
/// <param name="Step">The step the import is at: loading, parsing, or cleaning.</param>
public sealed record WebContentImportSnapshot(ImportTarget Target, ReadWebContentSteps Step);