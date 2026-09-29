namespace AIStudio.Tools;

/// <summary>
/// Hands the dropped paths to the drop zone which was under the cursor.
/// </summary>
/// <remarks>
/// The zone is named rather than addressed, because the message bus broadcasts. Only the zone whose
/// own ID matches acts on this, and every other zone ignores it -- including the zones of circuits
/// whose browser is long gone, because an ID belongs to one instance in one circuit. What the paths
/// mean is the receiving zone's business: they may lead to files just as well as to folders.
/// </remarks>
/// <param name="ZoneId">The ID of the zone the paths were dropped on.</param>
/// <param name="Paths">The dropped paths, in the order the runtime delivered them.</param>
public readonly record struct DroppedPaths(string ZoneId, List<string> Paths);