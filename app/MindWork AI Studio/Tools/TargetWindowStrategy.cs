namespace AIStudio.Tools;

public enum TargetWindowStrategy
{
    /// <summary>
    /// Means no target window strategy, which will effectively return all items.
    /// </summary>
    NONE,
    
    /// <summary>
    /// Searches for two up-to-three items but at least one.
    /// </summary>
    A_FEW_GOOD_ONES,
    
    /// <summary>
    /// Searches for the top 10% items that are better than guessing, i.e., with confidence greater than 0.5f.
    /// </summary>
    TOP10_BETTER_THAN_GUESSING,
}