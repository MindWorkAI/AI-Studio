using System.Text.Json;

using AIStudio.Assistants.VisualBriefing;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Writes and reads filled values of the roots which matter most, since some parts of a contract only come
/// into play once there is a value, e.g., the factory which builds an immutable collection.
/// </summary>
public static class JsonRoundTrips
{
    private const string TEXT_WITH_WEB_ADDRESS = "The details are on https://example.org/chosen.";

    /// <summary>
    /// Returns a description of every value which does not survive being written and read again.
    /// </summary>
    /// <returns>One line per failure, naming the type which failed.</returns>
    public static IReadOnlyList<string> FindLosses()
    {
        var restrictions = ChatRestrictions.NONE.WithWebAddressesWrittenByModelIn(TEXT_WITH_WEB_ADDRESS);
        var chat = new ChatThread
        {
            Name = "Trim smoke test",
            Blocks =
            [
                new ContentBlock
                {
                    Time = DateTimeOffset.UnixEpoch,
                    ContentType = ContentType.TEXT,
                    Content = new ContentText { Text = TEXT_WITH_WEB_ADDRESS },
                    Role = ChatRole.USER,
                },
            ],
        };

        chat.RequireRestrictions(restrictions);

        var failures = new List<string>();
        AddLoss(chat, WorkspaceBehaviour.JSON_OPTIONS, failures);
        AddLoss(new Data(), SettingsManager.JSON_OPTIONS, failures);
        AddLoss(new VisualBriefingManifest { Name = "Trim smoke test", Restrictions = restrictions }, VisualBriefingJson.Persistence, failures);
        return failures;
    }

    private static void AddLoss<T>(T value, JsonSerializerOptions options, List<string> failures)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, options);
            var jsonAgain = JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(json, options), options);
            if (jsonAgain != json)
                failures.Add($"{typeof(T).Name}: written again after reading, it gives other JSON than before.");
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or JsonException)
        {
            failures.Add($"{typeof(T).Name}: {exception.Message}");
        }
    }
}