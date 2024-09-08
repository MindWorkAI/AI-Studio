using AIStudio.Settings;

namespace AIStudio.Tools;

public static class ProfileExtensions
{
    public static IEnumerable<Profile> GetAllProfiles(this IEnumerable<Profile> profiles)
    {
        yield return Profile.NO_PROFILE;
        foreach (var profile in profiles)
            yield return profile;
    }
}