namespace AIStudio.Tools.Rust;

/// <summary>
/// The cursor position of a drag and drop event.
/// </summary>
/// <remarks>
/// The coordinates are viewport-relative CSS pixels, on every platform. The Rust runtime has already
/// dealt with the platform differences -- device pixels on Windows, logical points on macOS and Linux --
/// so these numbers can be handed to the browser for a hit test without any further conversion.
/// </remarks>
/// <param name="X">The distance from the left edge of the viewport, in CSS pixels.</param>
/// <param name="Y">The distance from the top edge of the viewport, in CSS pixels.</param>
public readonly record struct DropPosition(double X, double Y);