namespace AIStudio.Settings;

public readonly record struct ProfilePreselection
{
    public ProfilePreselectionMode Mode { get; }

    public IReadOnlySet<string> SpecificProfileIds { get; }

    public bool UseAppDefault => this.Mode == ProfilePreselectionMode.USE_APP_DEFAULT;

    public bool DoNotPreselectProfiles => this.Mode == ProfilePreselectionMode.USE_NO_PROFILES;

    public bool UseSpecificProfiles => this.Mode == ProfilePreselectionMode.USE_SPECIFIC_PROFILES;

    public static ProfilePreselection AppDefault => new(ProfilePreselectionMode.USE_APP_DEFAULT, new HashSet<string>());

    public static ProfilePreselection NoProfiles => new(ProfilePreselectionMode.USE_NO_PROFILES, new HashSet<string>());

    private ProfilePreselection(ProfilePreselectionMode mode, IReadOnlySet<string> specificProfileIds)
    {
        this.Mode = mode;
        this.SpecificProfileIds = specificProfileIds;
    }

    public static ProfilePreselection Specific(IEnumerable<string> profileIds)
    {
        var normalizedIds = profileIds
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId) && !profileId.Equals(Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedIds.Count == 0)
            throw new ArgumentException("A specific profile preselection requires at least one profile ID.", nameof(profileIds));

        return new(ProfilePreselectionMode.USE_SPECIFIC_PROFILES, normalizedIds);
    }

    public static ProfilePreselection FromStoredValue(IEnumerable<string>? storedValue)
    {
        if (storedValue is null)
            return AppDefault;

        var profileIds = storedValue
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId) && !profileId.Equals(Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return profileIds.Count == 0 ? NoProfiles : Specific(profileIds);
    }

    public static implicit operator HashSet<string>?(ProfilePreselection preselection) => preselection.Mode switch
    {
        ProfilePreselectionMode.USE_APP_DEFAULT => null,
        ProfilePreselectionMode.USE_NO_PROFILES => [],
        ProfilePreselectionMode.USE_SPECIFIC_PROFILES => [..preselection.SpecificProfileIds],

        _ => null,
    };
}
