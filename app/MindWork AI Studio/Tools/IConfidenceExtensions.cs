namespace AIStudio.Tools;

public static class IConfidenceExtensions
{
    public static TargetWindow DetermineTargetWindow<T>(this IReadOnlyList<T> items, TargetWindowStrategy strategy, int numMaximumItems = 30) where T : IConfidence 
    {
        switch (strategy)
        {
            case TargetWindowStrategy.A_FEW_GOOD_ONES:
                return new(1, 2, 3, 0f);
            
            case TargetWindowStrategy.TOP10_BETTER_THAN_GUESSING:
                var numItemsBetterThanGuessing = items.Count(x => x.Confidence > 0.5f);
                if(numItemsBetterThanGuessing < 3)
                    return new(1, 2, 3, 0.5f);

                // We want the top 10% of items better than guessing:
                var numTop10Percent = (int) MathF.Floor(numItemsBetterThanGuessing * 0.1f);

                // When these 10% are just a few items, we take them all:
                if (numTop10Percent <= 10)
                {
                    var diff = numItemsBetterThanGuessing - numTop10Percent;
                    var num50Percent = (int) MathF.Floor(numItemsBetterThanGuessing * 0.5f);
                    return new(num50Percent, num50Percent + 1, Math.Max(numItemsBetterThanGuessing, diff), 0.5f);
                }

                // Let's define the size of the window:
                const int MIN_NUM_ITEMS = 3;
                var windowMin = Math.Max(MIN_NUM_ITEMS + 1, numTop10Percent);
                windowMin = Math.Min(windowMin, numMaximumItems - 1);
                var totalMin = Math.Max(MIN_NUM_ITEMS, windowMin - 3);
                var windowSize = (int)MathF.Max(MathF.Floor(numTop10Percent * 0.1f), MathF.Min(10, numTop10Percent));
                var windowMax = Math.Min(numMaximumItems, numTop10Percent + windowSize);
                return new(totalMin, windowMin, windowMax, 0.5f);

            case TargetWindowStrategy.NONE:
            default:
                return new(-1, -1, -1, 0f);
        }
    }

    /// <summary>
    /// Determine the optimal confidence threshold for a list of items
    /// in order to match a target window of number of items.
    /// </summary>
    /// <param name="items">The list of confidence items to analyze.</param>
    /// <param name="targetWindow">The target window for the number of items.</param>
    /// <param name="maxSteps">The maximum number of steps to search for the threshold.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The confidence threshold.</returns>
    public static float GetConfidenceThreshold<T>(this IReadOnlyList<T> items, TargetWindow targetWindow, int maxSteps = 10) where T : IConfidence
    {
        if(!targetWindow.IsValid())
        {
            var logger = Program.SERVICE_PROVIDER.GetService<ILogger<IConfidence>>()!;
            logger.LogWarning("The target window is invalid. Returning 0f as threshold.");
            return 0f;
        }

        var confidenceValues = items.Select(x => x.Confidence).ToList();
        var minConfidence = confidenceValues.Min();
        var lowerBound = MathF.Max(minConfidence, targetWindow.MinThreshold);
        var upperBound = confidenceValues.Max();

        //
        // We search for a threshold so that we have between
        // targetWindowMin and targetWindowMax items. When not
        // possible, we take all items (e.g., threshold = 0f; depends on the used window strategy)
        //
        var threshold = 0.0f;

        // Check the case where the confidence values are too close:
        if (upperBound - minConfidence >= 0.01)
        {
            var previousThreshold = threshold;
            for (var i = 0; i < maxSteps; i++)
            {
                threshold = lowerBound + (upperBound - lowerBound) * i / maxSteps;
                var numMatches = items.Count(x => x.Confidence >= threshold);
                if (numMatches <= targetWindow.NumMinItems)
                {
                    threshold = previousThreshold;
                    break;
                }

                if (targetWindow.InsideWindow(numMatches))
                    break;

                previousThreshold = threshold;
            }
        }
        else
        {
            var logger = Program.SERVICE_PROVIDER.GetService<ILogger<IConfidence>>()!;
            logger.LogWarning("The confidence values are too close. Returning 0f as threshold.");
        }
        
        return threshold;
    }
}