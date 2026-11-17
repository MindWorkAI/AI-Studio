using AIStudio.Settings;

namespace AIStudio.Tools;

public static class ProfileExtensions
{
    extension(IEnumerable<Profile> profiles)
    {
        public IEnumerable<Profile> GetAllProfiles()
        {
            yield return Profile.NO_PROFILE;
            foreach (var profile in profiles)
                yield return profile;
        }
    }
}