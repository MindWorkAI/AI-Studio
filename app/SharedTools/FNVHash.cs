// ReSharper disable MemberCanBePrivate.Global
namespace SharedTools;

/// <summary>
/// Implements the Fowler–Noll–Vo hash function for 32-bit and 64-bit hashes.
/// </summary>
public static class FNVHash
{
    private const uint FNV_OFFSET_BASIS_32_BIT = 2_166_136_261;
    private const ulong FNV_OFFSET_BASIS_64_BIT = 14_695_981_039_346_656_037;
    
    private const uint FNV_PRIME_32_BIT = 16_777_619;
    private const ulong FNV_PRIME_64_BIT = 1_099_511_628_211;

    /// <param name="text">The string to hash.</param>
    extension(string text)
    {
        /// <summary>
        /// Computes the 32bit FNV-1a hash of a string.
        /// </summary>
        /// <returns>The 32bit FNV-1a hash of the string.</returns>
        public uint ToFNV32() => ToFNV32(text.AsSpan());

        /// <summary>
        /// Computes the 64bit FNV-1a hash of a string.
        /// </summary>
        /// <returns>The 64bit FNV-1a hash of the string.</returns>
        public ulong ToFNV64() => ToFNV64(text.AsSpan());
    }

    /// <param name="text">The string to hash.</param>
    extension(ReadOnlySpan<char> text)
    {
        /// <summary>
        /// Computes the 32bit FNV-1a hash of a string.
        /// </summary>
        /// <returns>The 32bit FNV-1a hash of the string.</returns>
        public uint ToFNV32()
        {
            var hash = FNV_OFFSET_BASIS_32_BIT;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= FNV_PRIME_32_BIT;
            }

            return hash;
        }

        /// <summary>
        /// Computes the 64bit FNV-1a hash of a string.
        /// </summary>
        /// <returns>The 64bit FNV-1a hash of the string.</returns>
        public ulong ToFNV64()
        {
            var hash = FNV_OFFSET_BASIS_64_BIT;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= FNV_PRIME_64_BIT;
            }

            return hash;
        }
    }
}