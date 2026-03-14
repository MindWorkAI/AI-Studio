namespace AIStudio.Settings;

public readonly record struct ProfilePreselection
{
    public ProfilePreselectionMode Mode { get; }

    public string SpecificProfileId { get; }

    public bool UseAppDefault => this.Mode == ProfilePreselectionMode.USE_APP_DEFAULT;

    public bool DoNotPreselectProfile => this.Mode == ProfilePreselectionMode.USE_NO_PROFILE;

    public bool UseSpecificProfile => this.Mode == ProfilePreselectionMode.USE_SPECIFIC_PROFILE;

    public static ProfilePreselection AppDefault => new(ProfilePreselectionMode.USE_APP_DEFAULT, string.Empty);

    public static ProfilePreselection NoProfile => new(ProfilePreselectionMode.USE_NO_PROFILE, Profile.NO_PROFILE.Id);

    private ProfilePreselection(ProfilePreselectionMode mode, string specificProfileId)
    {
        this.Mode = mode;
        this.SpecificProfileId = specificProfileId;
    }

    public static ProfilePreselection Specific(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("A specific profile preselection requires a profile ID.", nameof(profileId));

        if (profileId.Equals(Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use NoProfile for the NO_PROFILE selection.", nameof(profileId));

        return new(ProfilePreselectionMode.USE_SPECIFIC_PROFILE, profileId);
    }

    public static ProfilePreselection FromStoredValue(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
            return AppDefault;

        if (storedValue.Equals(Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase))
            return NoProfile;

        return new(ProfilePreselectionMode.USE_SPECIFIC_PROFILE, storedValue);
    }

    public static implicit operator string(ProfilePreselection preselection) => preselection.Mode switch
    {
        ProfilePreselectionMode.USE_APP_DEFAULT => string.Empty,
        ProfilePreselectionMode.USE_NO_PROFILE => Profile.NO_PROFILE.Id,
        ProfilePreselectionMode.USE_SPECIFIC_PROFILE => preselection.SpecificProfileId,
        
        _ => string.Empty,
    };
}