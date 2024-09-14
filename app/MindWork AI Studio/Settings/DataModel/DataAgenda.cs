using AIStudio.Assistants.Agenda;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataAgenda
{
    /// <summary>
    /// Preselect any agenda options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    public string PreselectName { get; set; } = string.Empty;

    public string PreselectTopic { get; set; } = string.Empty;

    public string PreselectObjective { get; set; } = string.Empty;

    public string PreselectModerator { get; set; } = string.Empty;

    public string PreselectDuration { get; set; } = string.Empty;

    public string PreselectStartTime { get; set; } = string.Empty;

    public bool PreselectIntroduceParticipants { get; set; }

    public NumberParticipants PreselectNumberParticipants { get; set; } = NumberParticipants.NOT_SPECIFIED;

    public bool PreselectActiveParticipation { get; set; }

    public bool PreselectIsMeetingVirtual { get; set; } = true;

    public string PreselectLocation { get; set; } = string.Empty;

    public bool PreselectJointDinner { get; set; }

    public bool PreselectSocialActivity { get; set; }

    public bool PreselectArriveAndDepart { get; set; }

    public int PreselectLunchTime { get; set; }

    public int PreselectBreakTime { get; set; }
    
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; }
    
    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Preselect a agenda provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}