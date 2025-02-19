namespace AIStudio.Tools;

public static class IConfidenceExtensions
{
    /// <summary>
    /// Determine the optimal confidence threshold for a list of items
    /// in order to match a target window of number of items.
    /// </summary>
    /// <param name="items">The list of confidence items to analyze.</param>
    /// <param name="targetWindowMin">The minimum number of items in the target window. Should be at least 2 and more than numMinItems.</param>
    /// <param name="targetWindowMax">The maximum number of items in the target window.</param>
    /// <param name="numMinItems">The minimum number of items to match the threshold. Should be at least 1 and less than targetWindowMin.</param>
    /// <param name="maxSteps">The maximum number of steps to search for the threshold.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The confidence threshold.</returns>
    public static float GetConfidenceThreshold<T>(this IList<T> items, int targetWindowMin = 2, int targetWindowMax = 3, int numMinItems = 1, int maxSteps = 10) where T : IConfidence
    {
        var confidenceValues = items.Select(x => x.Confidence).ToList();
        var lowerBound = confidenceValues.Min();
        var upperBound = confidenceValues.Max();

        //
        // We search for a threshold so that we have between
        // targetWindowMin and targetWindowMax items. When not
        // possible, we take all items (i.e., threshold = 0f)
        //
        var threshold = 0.0f;

        // Check the case where the confidence values are too close:
        if (upperBound - lowerBound >= 0.01)
        {
            var previousThreshold = 0.0f;
            for (var i = 0; i < maxSteps; i++)
            {
                threshold = lowerBound + (upperBound - lowerBound) * i / maxSteps;
                var numMatches = items.Count(x => x.Confidence >= threshold);
                if (numMatches <= numMinItems)
                {
                    threshold = previousThreshold;
                    break;
                }

                if (numMatches <= targetWindowMax && numMatches >= targetWindowMin)
                    break;

                previousThreshold = threshold;
            }
        }
        
        return threshold;
    }
}