using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Tools;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that the augmented data of the classic RAG process stays with the request it was made for.
/// </summary>
/// <remarks>
/// The process writes it anew for every message, so a stored copy was never read again, while it
/// often made up the largest part of a chat file. What the AI read of it is kept with the answer as
/// its passages.
/// </remarks>
[TestFixture]
public sealed class ChatThreadAugmentedDataTests
{
    [Test]
    public void TheAugmentedDataIsNotSaved()
    {
        var thread = new ChatThread { ChatId = Guid.NewGuid(), AugmentedData = "# Retrieval context 1 of 1\nThe mixing console is described here." };

        var json = JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS);
        using var document = JsonDocument.Parse(json);

        Assert.That(document.RootElement.TryGetProperty("augmented_data", out _), Is.False);
    }

    [Test]
    public void AChatSavedWithAugmentedDataLoadsWithout()
    {
        const string CHAT_WITH_AUGMENTED_DATA = """
                                                {
                                                  "chat_id": "2b7c9e1f-4a6d-4c8b-9e3f-1d5a7c9b2e4f",
                                                  "name": "An older chat",
                                                  "augmented_data": "# Retrieval context 1 of 1\nThe mixing console is described here.",
                                                  "blocks": []
                                                }
                                                """;

        var loaded = JsonSerializer.Deserialize<ChatThread>(CHAT_WITH_AUGMENTED_DATA, WorkspaceBehaviour.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded, Is.Not.Null, "An older chat file must still load.");
            Assert.That(loaded!.AugmentedData, Is.Empty, "It belonged to a request long over; the next message retrieves anew.");
            Assert.That(loaded.Name, Is.EqualTo("An older chat"));
        }
    }
}