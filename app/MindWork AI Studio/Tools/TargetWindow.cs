namespace AIStudio.Tools;

/// <summary>
/// Represents a target window for the number of items to match a threshold.
/// </summary>
/// <param name="NumMinItems">The minimum number of items to match the threshold. Should be at least one and less than targetWindowMin.</param>
/// <param name="TargetWindowMin">The minimum number of items in the target window. Should be at least 2 and more than numMinItems.</param>
/// <param name="TargetWindowMax">The maximum number of items in the target window.</param>
public readonly record struct TargetWindow(int NumMinItems, int TargetWindowMin, int TargetWindowMax, float MinThreshold)
{
    /// <summary>
    /// Determines if the target window is valid.
    /// </summary>
    /// <returns>True when the target window is valid; otherwise, false.</returns>
    public bool IsValid()
    {
        if(this.NumMinItems < 1)
            return false;
        
        if(this.TargetWindowMin < this.NumMinItems)
            return false;
        
        if(this.TargetWindowMax < this.TargetWindowMin)
            return false;
        
        if(this.MinThreshold is < 0f or > 1f)
            return false;
        
        return true;
    }

    /// <summary>
    /// Determines if the number of items is inside the target window.
    /// </summary>
    /// <param name="numItems">The number of items to check.</param>
    /// <returns>True when the number of items is inside the target window; otherwise, false.</returns>
    public bool InsideWindow(int numItems) => numItems >= this.TargetWindowMin && numItems <= this.TargetWindowMax;
}