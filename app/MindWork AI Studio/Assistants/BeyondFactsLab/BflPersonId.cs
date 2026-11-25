using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Derives the ID under which the BeyondFacts Lab stores the research on one person from the name of the person.
/// </summary>
/// <remarks>
/// The ID follows from the name alone, see BflParticipant.NameKey, so that the research on a person is found again
/// after the list of participants was researched anew, and no path is stored. It is a UUID of version 8 built from
/// SHA-256, like BflThreadId.
/// </remarks>
public static class BflPersonId
{
    /// <summary>
    /// What comes before every name, so that these IDs never meet those of other kinds.
    /// </summary>
    private const string NAMESPACE = "beyondfacts-lab/person/";

    /// <summary>
    /// Gets the ID of a person.
    /// </summary>
    /// <param name="name">The name of the person; case and the spaces between its words do not count.</param>
    public static Guid Of(string name)
    {
        var bytesOfName = Encoding.UTF8.GetBytes($"{NAMESPACE}{BflParticipant.NameKey(name)}");
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytesOfName, hash);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}