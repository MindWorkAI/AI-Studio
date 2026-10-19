namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Where a placeholder sits on the slide, in English Metric Units (EMU).
/// </summary>
/// <param name="X">The distance of the left edge from the left edge of the slide.</param>
/// <param name="Y">The distance of the top edge from the top edge of the slide.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal readonly record struct SlotBounds(long X, long Y, long Width, long Height)
{
    public long Right => this.X + this.Width;

    public long Bottom => this.Y + this.Height;

    public long CenterX => this.X + this.Width / 2;

    public long CenterY => this.Y + this.Height / 2;

    public double Area => (double)this.Width * this.Height;

    /// <summary>
    /// Whether the two areas share a part of their horizontal extent.
    /// </summary>
    public bool OverlapsHorizontally(SlotBounds other) => this.X < other.Right && other.X < this.Right;

    /// <summary>
    /// Whether the other area lies within this one, give or take the given tolerance on each edge.
    /// </summary>
    public bool Contains(SlotBounds other, long horizontalTolerance, long verticalTolerance) =>
        other.X >= this.X - horizontalTolerance &&
        other.Right <= this.Right + horizontalTolerance &&
        other.Y >= this.Y - verticalTolerance &&
        other.Bottom <= this.Bottom + verticalTolerance;

    /// <summary>
    /// Whether both areas are the same, give or take the given tolerance on each edge.
    /// </summary>
    public bool IsCloseTo(SlotBounds other, long horizontalTolerance, long verticalTolerance) =>
        Math.Abs(this.X - other.X) <= horizontalTolerance &&
        Math.Abs(this.Width - other.Width) <= horizontalTolerance &&
        Math.Abs(this.Y - other.Y) <= verticalTolerance &&
        Math.Abs(this.Height - other.Height) <= verticalTolerance;
}