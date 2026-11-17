using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Gives each chat of the BeyondFacts Lab an ID which follows from where it lives.
/// </summary>
/// <remarks>
/// The provenance store keeps what a chat received by the ID of the chat, and its cleanup at the start removes what
/// belongs to no chat on the disk. With IDs which follow from the place of a chat, such as the preparation and the
/// step, the cleanup finds the chats of the lab from the folders alone, without reading a single chat; see
/// ProvenanceCleanup. A chat started over keeps its ID, so what the old conversation received becomes unreferenced
/// and goes at the next cleanup. The ID is a version 8 UUID after RFC 9562, built from a SHA-256 hash.
/// </remarks>
public static class BflThreadId
{
    /// <summary>
    /// What sets the IDs of the lab apart from any other name-based ID.
    /// </summary>
    private const string NAMESPACE = "beyondfacts-lab/thread/";

    /// <summary>
    /// The ID of the conversation in which the lab tells whether it fits a task.
    /// </summary>
    public static readonly Guid ONBOARDING = FromName("onboarding");

    /// <summary>
    /// Gets the ID of the chat of a step.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <returns>The ID, the same on every computer and in every run.</returns>
    public static Guid Of(Guid projectId, BflStep step) => FromName($"{projectId:D}/{step.FolderName}");

    /// <summary>
    /// Builds the ID of a chat from the name of its place.
    /// </summary>
    /// <param name="name">The name of the place, which no other place has.</param>
    /// <returns>The ID, the same on every computer and in every run.</returns>
    private static Guid FromName(string name)
    {
        var bytesOfName = Encoding.UTF8.GetBytes($"{NAMESPACE}{name}");
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytesOfName, hash);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}