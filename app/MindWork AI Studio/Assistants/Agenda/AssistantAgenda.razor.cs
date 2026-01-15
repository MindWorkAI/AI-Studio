using System.Text;

using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.Agenda;

public partial class AssistantAgenda : AssistantBaseCore<SettingsDialogAgenda>
{
    public override Tools.Components Component => Tools.Components.AGENDA_ASSISTANT;
    
    protected override string Title => T("Agenda Planner");
    
    protected override string Description => T("This agenda planner helps you create a structured agenda for your meeting or seminar. Just provide some basic information about the event, and the assistant will generate an agenda for you. You can also specify the duration, the start time, the location, the target language, and other details.");

    protected override string SystemPrompt =>
        $"""
        You are a friendly assistant who supports the planning and consultation of events. You receive
        metadata about an event and create an agenda from it. Additionally, you provide advice on what
        the organizer and moderator should consider. When creating the agenda, you take into account that
        time for questions and discussions needs to be scheduled. For large events, you take into account
        the logistical challenges that come with an increasing number of participants.

        If the user is planning interactions with participants, you know various methods. For example:
        
        - **Townhall Meeting**: An open assembly where participants can ask questions and provide feedback directly to leaders or speakers. Good moderation is necessary to queue contributions and hand the floor to the respective person. Additionally, one person should take notes visible to all. Ideally, this should not be the moderator.
        
        - **Speed Dating**: Short, structured interaction rounds where participants get the chance to quickly introduce themselves and make initial connections. A long row of tables is needed so that two people can sit opposite each other. A moderator with a stopwatch must ensure that participants rotate and change seats every 3 to 5 minutes.
        
        - **World Café**: Participants discuss in small groups at tables and rotate after set intervals to exchange thoughts and develop ideas further. Each table needs a host. Notes should be taken on a flipchart or digitally at each table. At the end, the table hosts could present their insights to all participants in a plenary session.
        
        - **Fishbowl**: A group discusses in an inner circle while a larger group listens from the outside. Occasionally, listeners can move into the inner circle to actively participate.
        
        - **BarCamp (Unconference)**: Participants contribute to part of the agenda themselves and can act as both presenters and listeners. Anyone can propose and lead a session. However, there should be initial moderation to guide this meta-process.
        
        - **Open Space Technology**: A method for large groups where participants bring up their own topics and then work intensively in small groups. At the end, the groups can present their findings to all participants in a plenary session.
        
        - **Panel Discussion**: A group of experts discusses a specific topic in front of an audience, followed by a question-and-answer session.
        
        - **Breakout Sessions**: Larger groups are divided into smaller, topic-related groups to discuss specific issues more deeply and interactively.
        
        - **Interactive Polling**: Real-time surveys during the meeting or seminar using digital technology to collect immediate feedback from participants. With an increasing number of participants, technical requirements also become more complex: not every Wi-Fi router can handle several hundred or thousand devices. Additionally, the moderator or speaker must have a stable network connection to display the results. It would be problematic if the speaker depends on the same Wi-Fi as the participants: if the Wi-Fi crashes due to the high number of participants, the speaker cannot show the results. Therefore, a LAN for the speaker or the use of a mobile hotspot would be a better choice.
        
        - **Peer Learning Groups**: Small groups of participants work together to learn from each other and collaboratively develop solutions for specific challenges.
        
        - **Role Play/Simulation**: Participants take on roles to simulate certain scenarios, gaining practical experience and promoting dynamic discussions.
        
        - **Round Table Discussions**: Small group discussions on specific topics, often without formal moderation, to encourage open and equal dialogue.
        
        - **Mind Mapping**: A method for visualizing ideas and concepts, which can foster creative exchange within groups.
        
        - **Case Study Analysis**: Groups analyze real or hypothetical case studies and present their solutions or insights.
        
        - **Silent Meeting/Writing**: Participants write down their thoughts and questions, which are then collected and discussed to balance out loud or dominant voices.
        
        - **Debate**: Participants argue for or against a specific topic, often with a moderator who ensures that the discussion remains constructive.
        
        - **Workshops**: Interactive sessions where participants work together on specific tasks or challenges.
        
        - **Brainstorming**: A method for generating ideas in a group setting, often with a focus on quantity and creativity.
        
        - **Design Thinking**: A structured approach to innovation solutions. Design thinking is structured in six phases: understand, observe, point of view, ideate, prototype, and test. Depending on the number of participants, there are two moderates needed. One moderator leads the process, the other supports the participants in their work. Design thinking may takes up to two days.
        
        You output the agenda in the following Markdown format:
        
        # [Agenda Title]
        - 09:00 - 09:05 (5 minutes): Welcome
        - 09:05 - 09:15 (10 minutes): Introduction
        - ...
        {this.SystemPromptSocialActivity()}
        {this.SystemPromptDinner()}
        
        # Organizational notes
        [Your advice for the organizer goes here, like room setup, technical requirements, etc.]
        
        # Moderation notes
        [Your advice for the moderator goes here]
        
        Output the agenda in the following language: {this.PromptLanguage()}.
        """;

    private const string PLACEHOLDER_CONTENT = """
                                              - Project status
                                              - We need to discuss how to proceed
                                              - Problem solving in work package 3 possible?
                                              - Concerns of the members
                                              """;

    private const string PLACEHOLDER_WHO_IS_PRESENTING = """
                                                         - John Doe: Project status
                                                         - Mary Jane: Work package 3
                                                         """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];

    protected override string SubmitText => T("Create Agenda");

    protected override Func<Task> SubmitAction => this.CreateAgenda;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetForm()
    {
        this.inputContent = string.Empty;
        this.contentLines.Clear();
        this.selectedFoci = [];
        this.justBriefly = [];
        this.inputWhoIsPresenting = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.inputTopic = string.Empty;
            this.inputName = string.Empty;
            this.inputDuration = string.Empty;
            this.inputStartTime = string.Empty;
            this.inputObjective = string.Empty;
            this.inputModerator = string.Empty;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.introduceParticipants = false;
            this.isMeetingVirtual = true;
            this.inputLocation = string.Empty;
            this.goingToDinner = false;
            this.doingSocialActivity = false;
            this.needToArriveAndDepart = false;
            this.durationLunchBreak = 0;
            this.durationBreaks = 0;
            this.activeParticipation = false;
            this.numberParticipants = NumberParticipants.NOT_SPECIFIED;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.Agenda.PreselectOptions)
        {
            this.inputTopic = this.SettingsManager.ConfigurationData.Agenda.PreselectTopic;
            this.inputName = this.SettingsManager.ConfigurationData.Agenda.PreselectName;
            this.inputDuration = this.SettingsManager.ConfigurationData.Agenda.PreselectDuration;
            this.inputStartTime = this.SettingsManager.ConfigurationData.Agenda.PreselectStartTime;
            this.inputObjective = this.SettingsManager.ConfigurationData.Agenda.PreselectObjective;
            this.inputModerator = this.SettingsManager.ConfigurationData.Agenda.PreselectModerator;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.Agenda.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.Agenda.PreselectedOtherLanguage;
            this.introduceParticipants = this.SettingsManager.ConfigurationData.Agenda.PreselectIntroduceParticipants;
            this.isMeetingVirtual = this.SettingsManager.ConfigurationData.Agenda.PreselectIsMeetingVirtual;
            this.inputLocation = this.SettingsManager.ConfigurationData.Agenda.PreselectLocation;
            this.goingToDinner = this.SettingsManager.ConfigurationData.Agenda.PreselectJointDinner;
            this.doingSocialActivity = this.SettingsManager.ConfigurationData.Agenda.PreselectSocialActivity;
            this.needToArriveAndDepart = this.SettingsManager.ConfigurationData.Agenda.PreselectArriveAndDepart;
            this.durationLunchBreak = this.SettingsManager.ConfigurationData.Agenda.PreselectLunchTime;
            this.durationBreaks = this.SettingsManager.ConfigurationData.Agenda.PreselectBreakTime;
            this.activeParticipation = this.SettingsManager.ConfigurationData.Agenda.PreselectActiveParticipation;
            this.numberParticipants = this.SettingsManager.ConfigurationData.Agenda.PreselectNumberParticipants;
            return true;
        }
        
        return false;
    }
    
    private string inputTopic = string.Empty;
    private string inputName = string.Empty;
    private string inputContent = string.Empty;
    private string inputDuration = string.Empty;
    private string inputStartTime = string.Empty;
    private IEnumerable<string> selectedFoci = new HashSet<string>();
    private IEnumerable<string> justBriefly = new HashSet<string>();
    private string inputObjective = string.Empty;
    private string inputModerator = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private bool introduceParticipants;
    private bool isMeetingVirtual = true;
    private string inputLocation = string.Empty;
    private bool goingToDinner;
    private bool doingSocialActivity;
    private bool needToArriveAndDepart;
    private int durationLunchBreak;
    private int durationBreaks;
    private bool activeParticipation;
    private NumberParticipants numberParticipants;
    private string inputWhoIsPresenting = string.Empty;
    
    private readonly List<string> contentLines = [];

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_AGENDA_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputContent = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion

    private void OnContentChanged(string content)
    {
        var previousSelectedFoci = new HashSet<string>();
        var previousJustBriefly = new HashSet<string>();
        
        this.contentLines.Clear();
        foreach (var line in content.AsSpan().EnumerateLines())
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("-"))
                trimmedLine = trimmedLine[1..].Trim();
            
            if (trimmedLine.Length == 0)
                continue;
            
            var finalLine = trimmedLine.ToString();
            if(this.selectedFoci.Any(x => x.StartsWith(finalLine, StringComparison.InvariantCultureIgnoreCase)))
                previousSelectedFoci.Add(finalLine);
            
            if(this.justBriefly.Any(x => x.StartsWith(finalLine, StringComparison.InvariantCultureIgnoreCase)))
                previousJustBriefly.Add(finalLine);
            
            this.contentLines.Add(finalLine);
        }

        this.selectedFoci = previousSelectedFoci;
        this.justBriefly = previousJustBriefly;
    }
    
    private string? ValidateLocation(string location)
    {
        if(!this.isMeetingVirtual && string.IsNullOrWhiteSpace(location))
            return T("Please provide a location for the meeting or the seminar.");
        
        return null;
    }
    
    private string? ValidateNumberParticipants(NumberParticipants selectedSize)
    {
        if(selectedSize is NumberParticipants.NOT_SPECIFIED)
            return T("Please select the number of participants.");
        
        return null;
    }
    
    private string? ValidateTargetLanguage(CommonLanguages language)
    {
        if(language is CommonLanguages.AS_IS)
            return T("Please select a target language for the agenda.");
        
        return null;
    }
    
    private string? ValidateDuration(string duration)
    {
        if(string.IsNullOrWhiteSpace(duration))
            return T("Please provide a duration for the meeting or the seminar, e.g. '2 hours', or '2 days (8 hours and 4 hours)', etc.");
        
        return null;
    }
    
    private string? ValidateStartTime(string startTime)
    {
        if(string.IsNullOrWhiteSpace(startTime))
            return T("Please provide a start time for the meeting or the seminar. When the meeting is a multi-day event, specify the start time for each day, e.g. '9:00 AM, 10:00 AM', etc.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }
    
    private string? ValidateTopic(string topic)
    {
        if(string.IsNullOrWhiteSpace(topic))
            return T("Please provide a topic for the agenda. What is the meeting or the seminar about?");
        
        return null;
    }
    
    private string? ValidateName(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            return T("Please provide a name for the meeting or the seminar.");
        
        return null;
    }
    
    private string? ValidateContent(string content)
    {
        if(string.IsNullOrWhiteSpace(content))
            return T("Please provide some content for the agenda. What are the main points of the meeting or the seminar?");

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            if(!line.TrimStart().StartsWith('-'))
                return T("Please start each line of your content list with a dash (-) to create a bullet point list.");
        
        return null;
    }
    
    private string? ValidateObjective(string objective)
    {
        if(string.IsNullOrWhiteSpace(objective))
            return T("Please provide an objective for the meeting or the seminar. What do you want to achieve?");
        
        return null;
    }
    
    private string? ValidateModerator(string moderator)
    {
        if(string.IsNullOrWhiteSpace(moderator))
            return T("Please provide a moderator for the meeting or the seminar. Who will lead the discussion?");
        
        return null;
    }

    private async Task CreateAgenda()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                Create an agenda for the meeting or seminar wit the title '{this.inputName}' using the following details:
                
                - The overall topic is '{this.inputTopic}'.
                - The objective of the meeting is '{this.inputObjective}'.
                - The moderator is '{this.inputModerator}'.
                - The planned duration is '{this.inputDuration}'.
                - The planned start time is '{this.inputStartTime}'.
                {this.PromptGettingToKnowEachOther()}
                - The number of participants is '{this.numberParticipants.Name()}'.
                {this.PromptActiveParticipation()}
                {this.PromptVirtualMeeting()}
                {this.PromptPhysicalMeeting()}
                
                Here is draft what was planned for the meeting:
                {this.inputContent}
                
                Please do not follow this draft strictly. It is just a suggestion.
                {this.PromptFoci()}
                {this.PromptBriefly()}
                {this.PromptWhoIsPresenting()}
             """);

        await this.AddAIResponseAsync(time);
    }

    private string PromptLanguage()
    {
        if(this.selectedTargetLanguage is CommonLanguages.AS_IS)
            return "Use the same language as the input.";
        
        if(this.selectedTargetLanguage is CommonLanguages.OTHER)
            return this.customTargetLanguage;
        
        return this.selectedTargetLanguage.Name();
    }
    
    private string PromptPhysicalMeeting()
    {
        if(this.isMeetingVirtual)
            return string.Empty;

        return $"""
               - The meeting takes place at the following location: '{this.inputLocation}'.
               {this.PromptJoiningDinner()}
               {this.PromptSocialActivity()}
               {this.PromptArriveAndDepart()}
               - In case a lunch break is necessary, the break should about {this.durationLunchBreak} minutes long.
               - In case any breaks are necessary, the breaks should be about {this.durationBreaks} minutes long.
               """;
    }
    
    private string PromptArriveAndDepart()
    {
        if (this.needToArriveAndDepart)
            return "- The participants should have time to arrive and depart.";

        return "- The participants do not need to arrive and depart.";
    }
    
    private string PromptSocialActivity()
    {
        if (this.doingSocialActivity)
            return "- The participants will engage in a social activity after the meeting or seminar. This can be a team-building exercise, a sightseeing tour, or a visit to a local attraction. Please make a recommendation for an activity at '{this.inputLocation}'.";

        return "- The participants will not engage in a social activity after the meeting or seminar.";
    }

    private string SystemPromptSocialActivity()
    {
        if(this.doingSocialActivity)
            return """
                   - 16:00 Departure to the social event [Consider the time needed to get to the location].
                   - 17:00 [Insert your advice for the social activity here].
                   """;
        
        return string.Empty;
    }
    
    private string SystemPromptDinner()
    {
        if(this.goingToDinner)
            return """
                   - 19:00 Departure to the restaurant [Consider the time needed to get to the restaurant].
                   - 20:00 [Insert your advice for the dinner here.].
                   """;
        
        return string.Empty;
    }
    
    private string PromptJoiningDinner()
    {
        if (this.goingToDinner)
            return $"- The participants will join for a dinner after the meeting or seminar. Please make a recommendation for a restaurant at '{this.inputLocation}'.";

        return "- The participants will not join for a dinner after the meeting or seminar.";
    }
    
    private string PromptVirtualMeeting()
    {
        if (this.isMeetingVirtual)
            return "- The meeting or seminar will be held virtually, e.g, a call or webinar. The participants will join online.";

        return "- The meeting or seminar will be held in person.";
    }
    
    private string PromptActiveParticipation()
    {
        if (this.activeParticipation)
            return "- The participants should actively participate in the meeting or seminar. This can be done through discussions, questions, and contributions to the content.";

        return "- The participants do not need to actively participate.";
    }
    
    private string PromptGettingToKnowEachOther()
    {
        if (this.introduceParticipants)
            return "- The participants should introduce themselves to get to know each other. This can be done in a round or in pairs. The moderator should ensure that everyone has the opportunity to speak.";

        return "- The participants do not need to introduce themselves.";
    }

    private string PromptFoci()
    {
        if (this.selectedFoci.Any())
        {
            return $"""
                    
                    Out of this content, the following points should be focused on:
                    {this.ConvertItemsToMarkdown(this.selectedFoci)}
                    """;
        }

        return string.Empty;
    }
    
    private string PromptBriefly()
    {
        if (this.justBriefly.Any())
        {
            return $"""
                    
                    The following points should be just briefly mentioned:
                    {this.ConvertItemsToMarkdown(this.justBriefly)}
                    """;
        }

        return string.Empty;
    }
    
    private string PromptWhoIsPresenting()
    {
        if (!string.IsNullOrWhiteSpace(this.inputWhoIsPresenting))
        {
            return $"""
                    
                    The following persons will present the content:
                    {this.inputWhoIsPresenting}
                    """;
        }

        return string.Empty;
    }
    
    private string ConvertItemsToMarkdown(IEnumerable<string> items)
    {
        var markdown = new StringBuilder();
        foreach (var item in items)
            markdown.AppendLine($"- {item}");
        
        return markdown.ToString();
    }
}