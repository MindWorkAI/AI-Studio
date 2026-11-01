using System.Text.Json.Nodes;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Finds the values of one JSON document which another one lacks.
/// </summary>
public static class JsonContainment
{
    /// <summary>
    /// Returns the path of every value in the expected document which the actual one lacks or holds otherwise.
    /// </summary>
    /// <remarks>
    /// Properties only the actual document has don't count, e.g., one added to a type after the expected document
    /// was written. A property which is null in the expected document may be missing in the actual one, since
    /// some properties are only written with a value. Arrays have to hold the same elements, in any order: an
    /// ImmutableHashSet of strings orders them by their hash codes, which differ from process to process.
    /// </remarks>
    /// <param name="expected">The document whose values have to be there.</param>
    /// <param name="actual">The document which has to hold them.</param>
    /// <returns>One line per value, with its path and both values.</returns>
    public static IReadOnlyList<string> FindMissing(JsonNode? expected, JsonNode? actual)
    {
        var missing = new List<string>();
        Compare(expected, actual, "$", missing);
        return missing;
    }

    private static void Compare(JsonNode? expected, JsonNode? actual, string path, List<string> missing)
    {
        switch (expected)
        {
            case JsonObject expectedObject when actual is JsonObject actualObject:
                foreach (var (name, expectedValue) in expectedObject)
                {
                    if (actualObject.TryGetPropertyValue(name, out var actualValue))
                        Compare(expectedValue, actualValue, $"{path}.{name}", missing);
                    else if (expectedValue is not null)
                        missing.Add($"{path}.{name}: missing, expected {expectedValue.ToJsonString()}");
                }

                break;

            case JsonArray expectedArray when actual is JsonArray actualArray && expectedArray.Count == actualArray.Count:
                var unmatched = actualArray.ToList();
                for (var index = 0; index < expectedArray.Count; index++)
                {
                    var match = unmatched.FindIndex(candidate => FindMissing(expectedArray[index], candidate).Count is 0);
                    if (match < 0)
                    {
                        missing.Add($"{path}[{index}]: no element holds {expectedArray[index]?.ToJsonString() ?? "null"}");
                        continue;
                    }

                    unmatched.RemoveAt(match);
                }

                break;

            default:
                if (!JsonNode.DeepEquals(expected, actual))
                    missing.Add($"{path}: {actual?.ToJsonString() ?? "null"}, expected {expected?.ToJsonString() ?? "null"}");

                break;
        }
    }
}