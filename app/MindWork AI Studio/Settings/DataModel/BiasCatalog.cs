namespace AIStudio.Settings.DataModel;

/// <summary>
/// Catalog of biases.
/// </summary>
/// <remarks>
/// Based on the work of Buster Benson, John Manoogian III, and Brian
/// Rene Morrissette. The biases were clustered and organized by
/// Buster Benson. The texts originally come from Wikipedia and
/// were reduced to a short definition by Brian Rene Morrissette.
/// John Manoogian III designed the original poster from Buster
/// Benson's work, which was then supplemented with definitions
/// by Brian Rene Morrissette.
///
/// All texts were checked by Thorsten Sommer against the 2024
/// version of Wikipedia. Most texts were replaced with the latest
/// versions, and long texts were shortened to the essentials.
/// 
/// The texts were revised and, when necessary, supplemented by
/// Thorsten Sommer for integration into AI Studio. Sources and
/// additional links were also researched by Thorsten Sommer.
/// </remarks>
public static class BiasCatalog
{
    public static readonly Bias NONE = new()
    {
        Id = Guid.Empty,
        Category = BiasCategory.NONE,
        Name = "None",
        Description = "No bias selected.",
        Related = [],
        Links = [],
    };
    
    #region WHAT_SHOULD_WE_REMEMBER

    private static readonly Bias MISATTRIBUTION_OF_MEMORY = new()
    {
        Id = new Guid("dd45c762-0599-4c6d-82e0-d10f7ee85bb1"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Misattribution of Memory",
        Description = 
            """
            # Misattribution of Memory
            The ability to remember information correctly, but being wrong about the source of
            that information. Includes the following three sub-effects:

            - Source Confusion:
            Source confusion is an attribute seen in different people’s accounts of the same
            event after hearing people speak about the situation. An example of this would
            be a witness who heard a police officer say he had a gun and then that witness
            later says they saw the gun even though they didn’t. The source of the memory
            is the police officer’s testimony, not actual perception.

            - Cryptomnesia:
            Individuals mistakenly believe that they are the original generators of the
            thought.

            - False Memory:
            False memories occur when a person’s identity and interpersonal relationships
            are strongly centered around a memory of an experience that did not actually
            take place. False memories are often the result of leading questions in a
            therapeutic practice termed Recovered Memory Therapy. In this practice,
            psychiatrists often put their patients under hypnosis to recover repressed
            memories. This can be detrimental, as the individual may recall memories that
            never occurred.
            """,
        
        Related = [],
        Links = [
            "https://en.wikipedia.org/wiki/Misattribution_of_memory",
            "https://en.wikipedia.org/wiki/Cryptomnesia",
            "https://en.wikipedia.org/wiki/Source-monitoring_error",
            "https://en.wikipedia.org/wiki/False_memory",
        ],
    };

    private static readonly Bias LIST_LENGTH_EFFECT = new()
    {
        Id = new Guid("688bba31-0b8e-49c5-8693-aecb37018a08"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "List Length Effect",
        Description =
            """
            # List Length Effect
            The finding that recognition performance for a short list is superior to that for
            a long list.
            """,

        Related = [],
        Links = [
            "https://en.wikipedia.org/wiki/List_of_cognitive_biases#Other_memory_biases",
        ],
    };

    private static readonly Bias MISINFORMATION_EFFECT = new()
    {
        Id = new Guid("2b69b071-6587-4ea1-a4f5-aee4e2fef43c"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Misinformation Effect",
        Description =
            """
            # Misinformation Effect
            When a person's recall of episodic memories becomes less accurate because of
            post-event information. The misinformation effect is an example of retroactive
            interference which occurs when information presented later interferes with the
            ability to retain previously encoded information. Individuals have also been
            shown to be susceptible to incorporating misleading information into their memory
            when it is presented within a question. Essentially, the new information that a
            person receives works backward in time to distort memory of the original event.
            """,

        Related = [
            new Guid("dd45c762-0599-4c6d-82e0-d10f7ee85bb1"), // MISATTRIBUTION_OF_MEMORY -> False Memory
            new Guid("4d377bac-062a-46d3-a1a5-46f3ac804a97"), // SUGGESTIBILITY
        ],
        Links = [
            "https://en.wikipedia.org/wiki/Misinformation_effect",
        ],
    };

    private static readonly Bias LEVELING_AND_SHARPENING = new()
    {
        Id = new Guid("d1ed47f9-2415-4fa3-8ca3-151e9e4ee3ca"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Leveling and Sharpening",
        Description =
            """
            # Leveling and Sharpening
            Leveling occurs when you hear or remember something, and drop details which do not fit cognitive
            categories and/or assumptions; sharpening occurs when you hear or remember something, and emphasize
            details which do fit cognitive categories and/or assumptions.
            """,

        Related = [],
        Links = [
            "https://en.wikipedia.org/wiki/Leveling_and_sharpening",
        ],
    };

    private static readonly Bias PEAK_END_RULE = new()
    {
        Id = new Guid("cf71d1e1-f49e-4d8f-a6c3-37056297bf13"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Peak-End Rule",
        Description =
            """
            # Peak-End Rule
            The peak–end rule is a psychological heuristic in which people judge an experience largely based on how
            they felt at its peak (i.e., its most intense point) and at its end, rather than based on the total sum
            or average of every moment of the experience. The effect occurs regardless of whether the experience is
            pleasant or unpleasant. To the heuristic, other information aside from that of the peak and end of the
            experience is not lost, but it is not used. This includes net pleasantness or unpleasantness and how
            long the experience lasted. The peak–end rule is thereby a specific form of the more general extension
            neglect and duration neglect.
            """,

        Related = [],
        Links = [
            "https://en.wikipedia.org/wiki/Peak%E2%80%93end_rule",
        ],
    };

    private static readonly Bias FADING_AFFECT_BIAS = new()
    {
        Id = new Guid("0378a05c-b55b-4451-a7f4-b5e1d6287d83"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Fading Affect Bias",
        Description =
            """
            # Fading Affect Bias
            The fading affect bias, more commonly known as FAB, is a psychological phenomenon in which memories
            associated with negative emotions tend to be forgotten more quickly than those associated with positive
            emotions. FAB only refers to the feelings one has associated with the memories and not the content of
            the memories themselves.
            """,

        Related = [],
        Links = [
            "https://en.wikipedia.org/wiki/Fading_affect_bias",
        ],
    };

    private static readonly Bias NEGATIVITY_BIAS = new()
    {
        Id = new Guid("ef521fbb-c20b-47c9-87f8-a571a06a03eb"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Negativity Bias",
        Description =
            """
            # Negativity Bias
            The negativity bias, also known as the negativity effect, is a cognitive bias that, even when positive
            or neutral things of equal intensity occur, things of a more negative nature (e.g. unpleasant thoughts,
            emotions, or social interactions; harmful/traumatic events) have a greater effect on one's psychological
            state and processes than neutral or positive things. In other words, something very positive will
            generally have less of an impact on a person's behavior and cognition than something equally emotional but
            negative. The negativity bias has been investigated within many different domains, including the formation
            of impressions and general evaluations; attention, learning, and memory; and decision-making and risk
            considerations.
            """,

        Related = [
            new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"), // LOSS_AVERSION
        ],
        Links = [
            "https://en.wikipedia.org/wiki/Negativity_bias",
        ],
    };

    private static readonly Bias PREJUDICE = new()
    {
        Id = new Guid("efb6606f-4629-4e5e-973f-94d5ac496638"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Prejudice",
        Description =
            """
            # Prejudice
            Prejudice can be an affective feeling towards a person based on their perceived group membership. The word
            is often used to refer to a preconceived (usually unfavourable) evaluation or classification of another
            person based on that person's perceived personal characteristics, such as political affiliation, sex,
            gender, gender identity, beliefs, values, social class, age, disability, religion, sexuality, race,
            ethnicity, language, nationality, culture, complexion, beauty, height, body weight, occupation, wealth,
            education, criminality, sport-team affiliation, music tastes or other perceived characteristics.

            The word "prejudice" can also refer to unfounded or pigeonholed beliefs and it may apply to
            "any unreasonable attitude that is unusually resistant to rational influence". Gordon Allport defined
            prejudice as a "feeling, favorable or unfavorable, toward a person or thing, prior to, or not based on,
            actual experience". Auestad defines prejudice as characterized by "symbolic transfer", transfer of
            a value-laden meaning content onto a socially-formed category and then on to individuals who are taken to
            belong to that category, resistance to change, and overgeneralization.

            The United Nations Institute on Globalization, Culture and Mobility has highlighted research considering
            prejudice as a global security threat due to its use in scapegoating some populations and inciting others
            to commit violent acts towards them and how this can endanger individuals, countries, and the international
            community.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Prejudice",
        ],
    };

    private static readonly Bias IMPLICIT_STEREOTYPES = new()
    {
        Id = new Guid("30bd6403-b7f4-4d16-9494-af6a22b349d3"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Implicit Stereotypes",
        Description =
            """
            # Implicit Stereotypes
            The unconscious attribution of particular qualities to a member of a certain social group. Implicit stereotypes are
            influenced by experience, and are based on learned associations between various qualities and social categories,
            including race or gender. Individuals' perceptions and behaviors can be affected by implicit stereotypes, even
            without the individuals' intention or awareness.

            An implicit bias or implicit stereotype is the pre-reflective attribution of particular qualities by an individual
            to a member of some social out group. Implicit stereotypes are thought to be shaped by experience and based on learned
            associations between particular qualities and social categories, including race and/or gender. Individuals' perceptions
            and behaviors can be influenced by the implicit stereotypes they hold, even if they are sometimes unaware they hold such
            stereotypes. Implicit bias is an aspect of implicit social cognition: the phenomenon that perceptions, attitudes, and
            stereotypes can operate prior to conscious intention or endorsement. The existence of implicit bias is supported by a
            variety of scientific articles in psychological literature. Implicit stereotype was first defined by psychologists
            Mahzarin Banaji and Anthony Greenwald in 1995.

            Explicit stereotypes, by contrast, are consciously endorsed, intentional, and sometimes controllable thoughts and beliefs.

            Implicit biases, however, are thought to be the product of associations learned through past experiences. Implicit
            biases can be activated by the environment and operate prior to a person's intentional, conscious endorsement. Implicit
            bias can persist even when an individual rejects the bias explicitly.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Implicit_stereotype",
        ],
    };

    private static readonly Bias IMPLICIT_ASSOCIATIONS = new()
    {
        Id = new Guid("6f1d8a61-cb64-44fe-9a52-5ee66c22fba4"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Implicit Associations",
        Description =
            """
            # Implicit Associations
            A person's automatic association between mental representations of objects (concepts) in memory.
            *Controversial. This is not a bias, it is an association algorithm.*

            Related: The implicit-association test
            The implicit-association test (IAT) is an assessment intended to detect subconscious associations
            between mental representations of objects (concepts) in memory. Its best-known application is the
            assessment of implicit stereotypes held by test subjects, such as associations between particular
            racial categories and stereotypes about those groups. The test has been applied to a variety of
            belief associations, such as those involving racial groups, gender, sexuality, age, and religion
            but also the self-esteem, political views, and predictions of the test taker.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Implicit_stereotype#Activation_of_implicit_stereotypes",
            "https://en.wikipedia.org/wiki/Implicit-association_test",
        ],
    };

    private static readonly Bias SPACING_EFFECT = new()
    {
        Id = new Guid("41e06aaf-73c2-4f48-9962-312d57308468"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Spacing Effect",
        Description =
            """
            # Spacing Effect
            The spacing effect demonstrates that learning is more effective when study sessions are spaced out. This
            effect shows that more information is encoded into long-term memory by spaced study sessions, also known
            as spaced repetition or spaced presentation, than by massed presentation ("cramming").

            The phenomenon was first identified by Hermann Ebbinghaus, and his detailed study of it was published in
            the 1885 book "Über das Gedächtnis. Untersuchungen zur experimentellen Psychologie" (Memory: A Contribution
            to Experimental Psychology), which suggests that active recall with increasing time intervals reduces the
            probability of forgetting information. This robust finding has been supported by studies of many explicit
            memory tasks such as free recall, recognition, cued-recall, and frequency estimation.

            Researchers have offered several possible explanations of the spacing effect, and much research has been
            conducted that supports its impact on recall. In spite of these findings, the robustness of this phenomenon
            and its resistance to experimental manipulation have made empirical testing of its parameters difficult.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Spacing_effect",
        ],
    };

    private static readonly Bias SUGGESTIBILITY = new()
    {
        Id = new Guid("4d377bac-062a-46d3-a1a5-46f3ac804a97"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Suggestibility",
        Description =
            """
            # Suggestibility
            The quality of being inclined to accept and act on the suggestions of others where false but plausible
            information is given and one fills in the gaps in certain memories with false information when recalling
            a scenario or moment. When the subject has been persistently told something about a past event, his or
            her memory of the event conforms to the repeated message.

            Suggestibility can be seen in people's day-to-day lives:

            - Someone witnesses an argument after school. When later asked about the "huge fight" that occurred, he
              recalls the memory, but unknowingly distorts it with exaggerated fabrications, because he now thinks
              of the event as a "huge fight" instead of a simple argument.
              
            - Children are told by their parents they are good singers, so from then on they believe they are talented
              while their parents were in fact being falsely encouraging.
              
            - A teacher could trick his AP Psychology students by saying, "Suggestibility is the distortion of memory
              through suggestion or misinformation, right?" It is likely that the majority of the class would agree
              with him because he is a teacher and what he said sounds correct. However, the term is really the
              definition of the misinformation effect.
              
            However, suggestibility can also be seen in extremes, resulting in negative consequences:

            - A witness' testimony is altered because the police or attorneys make suggestions during the interview,
              which causes their already uncertain observations to become distorted memories.
              
            - A young girl begins suffering migraines which lead to sleep deprivation and depression. Her therapist,
              a specialist in cases involving child abuse, repeatedly asks her whether her father had sexually abused
              her. This suggestion causes the young girl to fabricate memories of her father molesting her, which lead
              to her being placed in foster care and her father being tried on charges of abuse.
            """,

        Related = [
            new Guid("2b69b071-6587-4ea1-a4f5-aee4e2fef43c"), // MISINFORMATION_EFFECT,
            new Guid("dd45c762-0599-4c6d-82e0-d10f7ee85bb1"), // MISATTRIBUTION_OF_MEMORY -> False Memory
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Suggestibility",
        ],
    };

    private static readonly Bias TIP_OF_THE_TONGUE_PHENOMENON = new()
    {
        Id = new Guid("ad341a56-ffa5-4dd1-b3c6-ef2476b89b5a"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Tip of the Tongue Phenomenon",
        Description =
            """
            # Tip of the Tongue Phenomenon
            Tip of the tongue (also known as TOT, or lethologica) is the phenomenon of failing to retrieve a word or term
            from memory, combined with partial recall and the feeling that retrieval is imminent. The phenomenon's name
            comes from the saying, "It's on the tip of my tongue." The tip of the tongue phenomenon reveals that
            lexical access occurs in stages.

            People experiencing the tip-of-the-tongue phenomenon can often recall one or more features of the target word,
            such as the first letter, its syllabic stress, and words similar in sound, meaning, or both sound and meaning.
            Individuals report a feeling of being seized by the state, feeling something like mild anguish while
            searching for the word, and a sense of relief when the word is found. While many aspects of the
            tip-of-the-tongue state remain unclear, there are two major competing explanations for its occurrence:
            the direct-access view and the inferential view. Emotion and the strength of the emotional ties to what
            is trying to be remembered can also have an impact on the TOT phenomenon. The stronger the emotional ties,
            the longer it takes to retrieve the item from memory.

            TOT states should be distinguished from FOK (feeling of knowing) states. FOK, in contrast, is the feeling
            that one will be able to recognize — from a list of items — an item that is currently inaccessible. There
            are still currently opposing hypotheses in the psychological literature regarding the separability of the
            process underlying these concepts. However, there is some evidence that TOTs and FOKs draw on different
            parts of the brain.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Tip_of_the_tongue",
        ],
    };

    private static readonly Bias GOOGLE_EFFECT = new()
    {
        Id = new Guid("aca65269-40cc-4a9f-b850-c0e2eb283987"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Google Effect",
        Description =
            """
            # Google Effect
            The Google effect, also called digital amnesia, is the tendency to forget information that can be found
            readily online by using Internet search engines. According to the first study about the Google effect, people
            are less likely to remember certain details they believe will be accessible online. However, the study also
            claims that people's ability to learn information offline remains the same. This effect may also be seen
            as a change to what information and what level of detail is considered to be important to remember.

            In a big replication study published in Nature 2018, the Google effect was one of the experiments which
            could not be replicated.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Google_effect",
        ],
    };

    private static readonly Bias NEXT_IN_LINE_EFFECT = new()
    {
        Id = new Guid("c0b3d9f9-c0d9-482f-bf6f-d52dffe58205"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Next-In-Line Effect",
        Description =
            """
            # Next-In-Line Effect
            The next-in-line effect is the phenomenon of people being unable to recall information concerning events
            immediately preceding their turn to perform. The effect was first studied experimentally by Malcolm Brenner
            in 1973. In his experiment the participants were each in turn reading a word aloud from an index card, and
            after 25 words were asked to recall as many of all the read words as possible. The results of the experiment
            showed that words read aloud within approximately nine seconds before the subject's own turn were recalled
            worse than other words.

            The reason for the next-in-line effect appears to be a deficit in encoding the perceived information preceding
            a performance. That is, the information is never stored to long-term memory and thus cannot be retrieved later
            after the performance. One finding supporting this theory is that asking the subjects beforehand to pay more
            attention to events preceding their turn to perform can prevent the memory deficit and even result in
            overcompensation, making people remember the events before their turn better than others.

            In addition, the appearance of the next-in-line effect does not seem to be connected to the level of fear of
            negative evaluation. Both people with lower and higher anxiety levels are subject to the memory deficit.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Next-in-line_effect",
        ],
    };

    private static readonly Bias TESTING_EFFECT = new()
    {
        Id = new Guid("2fe5fbe7-3fff-4621-9111-21d8c3b8bcb2"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Testing Effect",
        Description =
            """
            # Testing Effect
            The testing effect (also known as retrieval practice, active recall, practice testing, or test-enhanced learning)
            suggests long-term memory is increased when part of the learning period is devoted to retrieving information from
            memory. It is different from the more general practice effect, defined in the APA Dictionary of Psychology as
            "any change or improvement that results from practice or repetition of task items or activities."

            Cognitive psychologists are working with educators to look at how to take advantage of tests—not as an assessment
            tool, but as a teaching tool since testing prior knowledge is more beneficial for learning when compared to only
            reading or passively studying material (even more so when the test is more challenging for memory).
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Testing_effect",
        ],
    };

    private static readonly Bias ABSENT_MINDEDNESS = new()
    {
        Id = new Guid("f459e613-4f6a-472e-ab9a-0961f5f4a685"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Absent-Mindedness",
        Description =
            """
            # Absent-Mindedness
            In the field of psychology, absent-mindedness is a mental state wherein a person is forgetfully inattentive.
            It is the opposite mental state of mindfulness. Absentmindedness is often caused by things such as boredom,
            sleepiness, rumination, distraction, or preoccupation with one's own internal monologue. When experiencing
            absent-mindedness, people exhibit signs of memory lapses and weak recollection of recent events.

            Absent-mindedness can usually be a result of a variety of other conditions often diagnosed by clinicians,
            such as attention deficit hyperactivity disorder and depression. In addition to absent-mindedness leading
            to an array of consequences affecting daily life, it can have more severe, long-term problems.

            It can have three different causes:
            1) a low level of attention ("blanking" or "zoning out");
            2) intense attention to a single object of focus (hyperfocus) that makes a person oblivious to events around him or her;
            3) unwarranted distraction of attention from the object of focus by irrelevant thoughts or environmental events.

            Absent-mindedness is also noticed as a common characteristic of personalities with schizoid personality
            disorder.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Absent-mindedness",
        ],
    };

    private static readonly Bias LEVELS_OF_PROCESSING_EFFECT = new()
    {
        Id = new Guid("a4027640-1f52-4ff1-ae13-bd14a30d5b8d"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Levels of Processing Effect",
        Description =
            """
            # Levels of Processing Effect
            The Levels of Processing model, created by Fergus I. M. Craik and Robert S. Lockhart in 1972, describes memory recall
            of stimuli as a function of the depth of mental processing. More analysis produce more elaborate and stronger memory
            than lower levels of processing. Depth of processing falls on a shallow to deep continuum. Shallow processing (e.g.,
            processing based on phonemic and orthographic components) leads to a fragile memory trace that is susceptible to rapid
            decay. Conversely, deep processing (e.g., semantic processing) results in a more durable memory trace. There are three
            levels of processing in this model. (1) Structural processing, or visual, is when we remember only the physical quality
            of the word (e.g. how the word is spelled and how letters look). (2) Phonemic processing includes remembering the word
            by the way it sounds (e.g. the word tall rhymes with fall). (3) Lastly, we have semantic processing in which we encode
            the meaning of the word with another word that is similar or has similar meaning. Once the word is perceived, the
            brain allows for a deeper processing.
            """,

        Related = [
            new Guid("4f61b9fa-146a-4b6e-b075-f0ba2ee0d9d0"), // PROCESSING_DIFFICULTY_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Levels_of_Processing_model",
        ],
    };

    private static readonly Bias SUFFIX_EFFECT = new()
    {
        Id = new Guid("1f2b459b-26bc-4a2d-b48e-a9b06d34f924"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Suffix Effect",
        Description =
            """
            # Suffix Effect
            The selective impairment in recall of the final items of a spoken list when the list is followed by a
            nominally irrelevant speech item, or suffix.

            Diminishment of the recency effect because a sound item is appended to the list that the subject is not
            required to recall. A form of serial position effect.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/List_of_cognitive_biases#Other_memory_biases",
        ],
    };

    private static readonly Bias SERIAL_POSITION_EFFECT = new()
    {
        Id = new Guid("fdd1e260-125b-4d06-bab5-a6204f96d5a7"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Serial-Position Effect",
        Description =
            """
            # Serial-Position Effect
            Serial-position effect is the tendency of a person to recall the first and last items in a series best,
            and the middle items worst. When asked to recall a list of items in any order (free recall), people tend
            to begin recall with the end of the list, recalling those items best (the recency effect). Among earlier
            list items, the first few items are recalled more frequently than the middle items (the primacy effect).

            One suggested reason for the primacy effect is that the initial items presented are most effectively stored
            in long-term memory because of the greater amount of processing devoted to them. (The first list item can
            be rehearsed by itself; the second must be rehearsed along with the first, the third along with the first
            and second, and so on.) The primacy effect is reduced when items are presented quickly and is enhanced
            when presented slowly (factors that reduce and enhance processing of each item and thus permanent storage).
            Longer presentation lists have been found to reduce the primacy effect.

            One theorised reason for the recency effect is that these items are still present in working memory when
            recall is solicited. Items that benefit from neither (the middle items) are recalled most poorly. An
            additional explanation for the recency effect is related to temporal context: if tested immediately after
            rehearsal, the current temporal context can serve as a retrieval cue, which would predict more recent items
            to have a higher likelihood of recall than items that were studied in a different temporal context (earlier
            in the list). The recency effect is reduced when an interfering task is given. Intervening tasks involve
            working memory, as the distractor activity, if exceeding 15 to 30 seconds in duration, can cancel out the
            recency effect. Additionally, if recall comes immediately after the test, the recency effect is consistent
            regardless of the length of the studied list, or presentation rate.

            Amnesiacs with poor ability to form permanent long-term memories do not show a primacy effect, but do show
            a recency effect if recall comes immediately after study. People with Alzheimer's disease exhibit a
            reduced primacy effect but do not produce a recency effect in recall.
            """,

        Related = [
            new Guid("741cafef-3f47-45dd-a082-bb243eba124a"), // RECENCY_EFFECT,
            new Guid("78f65dab-ab6d-4c4c-81f5-250edd1e8991"), // PRIMACY_EFFECT 
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Serial-position_effect",
        ],
    };

    private static readonly Bias PART_LIST_CUING = new()
    {
        Id = new Guid("005b650d-74be-4c10-a279-33dcd5c13f84"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Part-List Cuing",
        Description =
            """
            # Part-List Cuing
            The re-exposure of a subset of learned material as a retrieval cue can impair recall of the remaining material.
            """,

        Related = [],
        Links = [],
    };

    private static readonly Bias RECENCY_EFFECT = new()
    {
        Id = new Guid("741cafef-3f47-45dd-a082-bb243eba124a"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Recency Effect",
        Description =
            """
            # Recency Bias
            Recency bias is a cognitive bias that favors recent events over historic ones; a memory bias. Recency bias
            gives "greater importance to the most recent event", such as the final lawyer's closing argument a jury hears
            before being dismissed to deliberate.

            Recency bias should not be confused with anchoring or confirmation bias. Recency bias is related to the
            serial-position effect known as the recency effect. It is not to be confused with recency illusion, the
            belief or impression that a word or language usage is of recent origin when in reality it is long-established.
            """,

        Related = [
            new Guid("fdd1e260-125b-4d06-bab5-a6204f96d5a7"), // SERIAL_POSITION_EFFECT
            new Guid("78f65dab-ab6d-4c4c-81f5-250edd1e8991"), // PRIMACY_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Recency_bias",
            "https://en.wikipedia.org/wiki/Serial-position_effect#Recency_effect",
        ],
    };

    private static readonly Bias PRIMACY_EFFECT = new()
    {
        Id = new Guid("78f65dab-ab6d-4c4c-81f5-250edd1e8991"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Primacy Effect",
        Description =
            """
            # Primacy Effect
            In psychology and sociology, the primacy effect (also known as the primacy bias) is a cognitive bias that results
            in a subject recalling primary information presented better than information presented later on. For example, a
            subject who reads a sufficiently long list of words is more likely to remember words toward the beginning than
            words in the middle.
            """,

        Related = [
            new Guid("fdd1e260-125b-4d06-bab5-a6204f96d5a7"), // SERIAL_POSITION_EFFECT
            new Guid("741cafef-3f47-45dd-a082-bb243eba124a"), // RECENCY_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Serial-position_effect#Primacy_effect",
        ],
    };

    private static readonly Bias MEMORY_INHIBITION = new()
    {
        Id = new Guid("0a370e78-860b-4784-9acf-688b5e1c3148"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Memory Inhibition",
        Description =
            """
            # Memory Inhibition
            In psychology, memory inhibition is the ability not to remember irrelevant information. The scientific concept of memory
            inhibition should not be confused with everyday uses of the word "inhibition". Scientifically speaking, memory inhibition
            is a type of cognitive inhibition, which is the stopping or overriding of a mental process, in whole or in part, with or
            without intention.

            Memory inhibition is a critical component of an effective memory system. While some memories are retained for a lifetime,
            most memories are forgotten. According to evolutionary psychologists, forgetting is adaptive because it facilitates
            selectivity of rapid, efficient recollection. For example, a person trying to remember where they parked their car
            would not want to remember every place they have ever parked. In order to remember something, therefore, it is essential
            not only to activate the relevant information, but also to inhibit irrelevant information.

            *Controversial. This is not a bias, it is a logical information sorting algorithm.*
            """,

        Related = [
            new Guid("4e571eaf-7c2b-44c8-b8cb-0c8da658b82d"), // FREQUENCY_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Memory_inhibition",
        ],
    };

    private static readonly Bias MODALITY_EFFECT = new()
    {
        Id = new Guid("eeca14c3-8710-4522-8991-81db170d7f8b"),
        Category = BiasCategory.WHAT_SHOULD_WE_REMEMBER,
        Name = "Modality Effect",
        Description =
            """
            # Modality Effect
            The modality effect is a term used in experimental psychology, most often in the fields dealing with memory and learning,
            to refer to how learner performance depends on the presentation mode of studied items. Modality can refer to a number of
            characteristics of the presented study material. However, this term is usually used to describe the improved recall of
            the final items of a list when that list is presented verbally in comparison with a visual representation.

            Some studies use the term modality to refer to a general difference in performance based upon the mode of presentation.
            For example, Gibbons demonstrated modality effects in an experiment by making participants count either beeping sounds
            or visually presented dots. In his book about teaching Mathematics, Craig Barton refers to the Modality Effect, arguing
            that students learn better when images or narrations are presented alongside verbal narration, as opposed to being
            presented with on screen text.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Modality_effect",
        ],
    };

    #endregion

    #region TOO_MUCH_INFORMATION

    private static readonly Bias AVAILABILITY_HEURISTIC = new()
    {
        Id = new Guid("d749ce96-32f3-4c3d-86f7-26ff4edabe4a"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Availability Heuristic",
        Description =
            """
            # Availability Heuristic
            A mental shortcut that relies on immediate examples that come to a given person’s mind when evaluating
            a specific topic, concept, method or decision. The availability heuristic operates on the notion that
            if something can be recalled, it must be important, or at least more important than alternative
            solutions which are not as readily recalled. Subsequently, under the availability heuristic, people
            tend to heavily weigh their judgments toward more recent information, making new opinions biased
            toward that latest news.
            
            The mental availability of an action's consequences is positively related to those consequences'
            perceived magnitude. In other words, the easier it is to recall the consequences of something,
            the greater those consequences are often perceived to be. Most notably, people often rely on the
            content of their recall if its implications are not called into question by the difficulty they
            have in recalling it.
            
            After seeing news stories about child abductions, people may judge that the likelihood of this event
            is greater. Media coverage can help fuel a person's example bias with widespread and extensive coverage
            of unusual events, such as homicide or airline accidents, and less coverage of more routine, less
            sensational events, such as common diseases or car accidents. For example, when asked to rate the
            probability of a variety of causes of death, people tend to rate "newsworthy" events as more likely
            because they can more readily recall an example from memory. Moreover, unusual and vivid events
            like homicides, shark attacks, or lightning are more often reported in mass media than common and
            un-sensational causes of death like common diseases.
            
            For example, many people think that the likelihood of dying from shark attacks is greater than that
            of dying from being hit by falling airplane parts when more people actually die from falling airplane
            parts. When a shark attack occurs, the deaths are widely reported in the media whereas deaths as a
            result of being hit by falling airplane parts are rarely reported in the media.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Availability_heuristic",
        ],
    };

    private static readonly Bias ATTENTIONAL_BIAS = new()
    {
        Id = new Guid("368cc51b-a235-4fa4-ad90-446c084ffae8"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Attentional Bias",
        Description =
            """
            # Attentional Bias
            Attentional bias refers to how a person's perception is affected by selective factors in their attention.
            Attentional biases may explain an individual's failure to consider alternative possibilities when occupied
            with an existing train of thought. For example, cigarette smokers have been shown to possess an attentional
            bias for smoking-related cues around them, due to their brain's altered reward sensitivity. Attentional bias
            has also been associated with clinically relevant symptoms such as anxiety and depression.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Attentional_bias",
        ],
    };

    private static readonly Bias ILLUSORY_TRUTH_EFFECT = new()
    {
        Id = new Guid("cadafb8f-d1ed-4c92-9c29-2f1cb0797a66"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Illusory Truth Effect",
        Description =
            """
            # Illusory Truth Effect
            The illusory truth effect (also known as the illusion of truth effect, validity effect, truth effect,
            or the reiteration effect) is the tendency to believe false information to be correct after repeated
            exposure. This phenomenon was first identified in a 1977 study at Villanova University and Temple
            University. When truth is assessed, people rely on whether the information is in line with their
            understanding or if it feels familiar. The first condition is logical, as people compare new information
            with what they already know to be true. Repetition makes statements easier to process relative to new,
            unrepeated statements, leading people to believe that the repeated conclusion is more truthful. The
            illusory truth effect has also been linked to hindsight bias, in which the recollection of confidence
            is skewed after the truth has been received.
            
            In a 2015 study, researchers discovered that familiarity can overpower rationality and that repetitively
            hearing that a certain statement is wrong can paradoxically cause it to feel right. Researchers
            observed the illusory truth effect's impact even on participants who knew the correct answer to begin
            with but were persuaded to believe otherwise through the repetition of a falsehood, to "processing fluency".
            
            The illusory truth effect plays a significant role in fields such as advertising, news media, and
            political propaganda.
            """,

        Related = [
            new Guid("b9edd2f0-8503-4eb5-a4c3-369fcb318894"), // HINDSIGHT_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusory_truth_effect",
        ],
    };

    private static readonly Bias MERE_EXPOSURE_EFFECT = new()
    {
        Id = new Guid("8b6cd991-fcf4-4e45-b3ac-f20987667d94"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Mere-Exposure Effect",
        Description =
            """
            # Mere-Exposure Effect
            The mere-exposure effect is a psychological phenomenon by which people tend to develop liking or disliking for
            things merely because they are familiar with them. In social psychology, this effect is sometimes called the
            familiarity principle. The effect has been demonstrated with many kinds of things, including words, Chinese
            characters, paintings, pictures of faces, geometric figures, and sounds. In studies of interpersonal
            attraction, the more often people see a person, the more pleasing and likeable they find that person.
            
            The most obvious application of the mere-exposure effect is in advertising, but research on its effectiveness at
            enhancing consumer attitudes toward particular companies and products has been mixed.
            
            The mere-exposure effect exists in most areas of human decision-making. For example, many stock traders tend
            to invest in securities of domestic companies merely because they are more familiar with them, even though
            international markets offer similar or better alternatives. The mere-exposure effect also distorts the
            results of journal-ranking surveys; academics who previously published or completed reviews for a particular
            academic journal rate it dramatically higher than those who did not. There are mixed results on the question
            of whether mere exposure can promote good relations between different social groups. When groups already
            have negative attitudes to each other, further exposure can increase hostility. A statistical analysis of
            voting patterns found that candidates' exposure has a strong effect on the number of votes they receive,
            distinct from the popularity of their policies.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Mere-exposure_effect",
        ],
    };

    private static readonly Bias CONTEXT_EFFECT = new()
    {
        Id = new Guid("ccba2bca-8739-4b05-8c88-e54424e441d4"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Context Effect",
        Description =
            """
            # Context Effect
            A context effect is an aspect of cognitive psychology that describes the influence of environmental factors
            on one's perception of a stimulus. The impact of context effects is considered to be part of top-down
            design. The concept is supported by the theoretical approach to perception known as constructive perception.
            Context effects can impact our daily lives in many ways such as word recognition, learning abilities, memory,
            and object recognition. It can have an extensive effect on marketing and consumer decisions. For example,
            research has shown that the comfort level of the floor that shoppers are standing on while reviewing products
            can affect their assessments of product's quality, leading to higher assessments if the floor is comfortable
            and lower ratings if it is uncomfortable. Because of effects such as this, context effects are currently
            studied predominantly in marketing.
            
            Context effects can have a wide range of impacts in daily life. In reading difficult handwriting context
            effects are used to determine what letters make up a word. This helps us analyze potentially ambiguous
            messages and decipher them correctly. It can also affect our perception of unknown sounds based on the noise
            in the environment. For example, we may fill in a word we cannot make out in a sentence based on the
            other words we could understand. Context can prime our attitudes and beliefs about certain topics based
            on current environmental factors and our previous experiences with them.
            
            Context effects also affect memory. We are often better able to recall information in the location in which
            we learned it or studied it. For example, while studying for a test it is better to study in the environment
            that the test will be taken in (i.e. classroom) than in a location where the information was not learned
            and will not need to be recalled. This phenomenon is called transfer-appropriate processing.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Context_effect",
        ],
    };

    private static readonly Bias CUE_DEPENDENT_FORGETTING = new()
    {
        Id = new Guid("944bc142-895e-4c7f-ba00-bbceefc383c9"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Cue-Dependent Forgetting",
        Description =
            """
            # Cue-Dependent Forgetting
            Cue-dependent forgetting, or retrieval failure, is the failure to recall information without memory cues.
            The term either pertains to semantic cues, state-dependent cues or context-dependent cues.
            
            Upon performing a search for files in a computer, its memory is scanned for words. Relevant files containing
            this word or string of words are displayed. This is not how memory in the human mind works. Instead,
            information stored in the memory is retrieved by way of association with other memories. Some memories
            can not be recalled by simply thinking about them. Rather, one must think about something associated
            with it.
            
            For example, if someone tries and fails to recollect the memories they had about a vacation they went on,
            and someone mentions the fact that they hired a classic car during this vacation, this may make them remember
            all sorts of things from that trip, such as what they ate there, where they went and what books they read.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Cue-dependent_forgetting",
        ],
    };

    private static readonly Bias STATE_DEPENDENT_MEMORY = new()
    {
        Id = new Guid("bf83101d-47af-4d81-8306-935d4ab59fd7"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "State-Dependent Memory",
        Description =
            """
            # State-Dependent Memory
            State-dependent memory or state-dependent learning is the phenomenon where people remember more information
            if their physical or mental state is the same at time of encoding and time of recall. State-dependent memory
            is heavily researched in regards to its employment both in regards to synthetic states of consciousness
            (such as under the effects of psychoactive drugs) as well as organic states of consciousness such as mood.
            While state-dependent memory may seem rather similar to context-dependent memory, context-dependent memory
            involves an individual's external environment and conditions (such as the room used for study and to take
            the test) while state-dependent memory applies to the individual's internal conditions (such as use of
            substances or mood).
            """,

        Related = [
            new Guid("6b049d68-9104-4579-a7a4-a744c11bd65f"), // CONTEXT_DEPENDENT_MEMORY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/State-dependent_memory",
        ],
    };

    private static readonly Bias CONTEXT_DEPENDENT_MEMORY = new()
    {
        Id = new Guid("6b049d68-9104-4579-a7a4-a744c11bd65f"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Context-Dependent Memory",
        Description =
            """
            # Context-Dependent Memory
            In psychology, context-dependent memory is the improved recall of specific episodes or information when the
            context present at encoding and retrieval are the same. In a simpler manner, "when events are represented
            in memory, contextual information is stored along with memory targets; the context can therefore cue memories
            containing that contextual information". One particularly common example of context-dependence at work occurs
            when an individual has lost an item (e.g. lost car keys) in an unknown location. Typically, people try to
            systematically "retrace their steps" to determine all of the possible places where the item might be located.
            Based on the role that context plays in determining recall, it is not at all surprising that individuals often
            quite easily discover the lost item upon returning to the correct context. This concept is heavily related to
            the encoding specificity principle.
            
            This example best describes the concept of context-dependent forgetting. However, the research literature on
            context-dependent memory describes a number of different types of contextual information that may affect
            recall such as environmental context-dependent memory, state-dependent learning, cognitive context-dependent
            memory and mood-congruent memory. Research has also shown that context-dependence may play an important role
            in numerous situations, such as memory for studied material, or events that have occurred following the
            consumption of alcohol or other drugs.
            """,

        Related = [
            new Guid("bf83101d-47af-4d81-8306-935d4ab59fd7"), // STATE_DEPENDENT_MEMORY
            new Guid("ccba2bca-8739-4b05-8c88-e54424e441d4"), // CONTEXT_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Context-dependent_memory",
        ],
    };

    private static readonly Bias FREQUENCY_ILLUSION = new()
    {
        Id = new Guid("4e571eaf-7c2b-44c8-b8cb-0c8da658b82d"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Frequency Illusion",
        Description =
            """
            # Frequency Illusion
            The frequency illusion (also known as the Baader–Meinhof phenomenon) is a cognitive bias in which a
            person notices a specific concept, word, or product more frequently after recently becoming aware of it.
            
            The name "Baader–Meinhof phenomenon" was coined in 1994 by Terry Mullen in a letter to the St. Paul
            Pioneer Press. The letter describes how, after mentioning the name of the German terrorist group
            Baader–Meinhof once, he kept noticing it. This led to other readers sharing their own experiences of
            the phenomenon, leading it to gain recognition. It was not until 2005, when Stanford linguistics
            professor Arnold Zwicky wrote about this effect on his blog, that the name "frequency illusion"
            was coined.
            
            The main cause behind frequency illusion, and other related illusions and biases, seems to be
            selective attention. Selective attention refers to the process of selecting and focusing on
            selective objects while ignoring distractions. This means that people have the
            unconscious cognitive ability to filter for what they are focusing on.
            """,

        Related = [
            new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"), // CONFIRMATION_BIAS
            new Guid("0a370e78-860b-4784-9acf-688b5e1c3148"), // MEMORY_INHIBITION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Frequency_illusion",
        ],
    };

    private static readonly Bias HOT_COLD_EMPATHY_GAP = new()
    {
        Id = new Guid("e4e091cf-fed3-4c09-9c21-509db0b2729b"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Hot-Cold Empathy Gap",
        Description =
            """
            # Hot-Cold Empathy Gap
            A hot-cold empathy gap is a cognitive bias in which people underestimate the influences of visceral
            drives on their own attitudes, preferences, and behaviors. It is a type of empathy gap. The most
            important aspect of this idea is that human understanding is "state-dependent". For example, when
            one is angry, it is difficult to understand what it is like for one to be calm, and vice versa;
            when one is blindly in love with someone, it is difficult to understand what it is like for one
            not to be, (or to imagine the possibility of not being blindly in love in the future). Importantly,
            an inability to minimize one's gap in empathy can lead to negative outcomes in medical settings
            (e.g., when a doctor needs to accurately diagnose the physical pain of a patient).
            
            Hot-cold empathy gaps can be analyzed according to their direction:
            
            - Hot-to-cold: People under the influence of visceral factors (hot state) do not fully grasp how
              much their behavior and preferences are being driven by their current state; they think instead
              that these short-term goals reflect their general and long-term preferences.
              
            - Cold-to-hot: People in a cold state have difficulty picturing themselves in hot states, minimizing
              the motivational strength of visceral impulses. This leads to unpreparedness when visceral forces
              inevitably arise.
              
            They can also be classified in regards to their relation with time (past or future) and whether they
            occur intra- or inter-personally:
            
            - intrapersonal prospective: the inability to effectively predict their own future behavior when in
              a different state. See also projection bias.
              
            - intrapersonal retrospective: when people recall or try to understand behaviors that happened in a
              different state.
              
            - interpersonal: the attempt to evaluate behaviors or preferences of another person who is in a
              state different from one's own.
            
            Visceral factors
            Visceral factors are an array of influences which include hunger, thirst, love, sexual arousal,
            drug cravings for the drugs one is addicted to, physical pain, and desire for revenge. These
            drives have a disproportionate effect on decision making and behavior: the mind, when affected
            (i.e., in a hot state), tends to ignore all other goals in an effort to placate these influences.
            These states can lead a person to feel "out of control" and act impulsively.
            """,

        Related = [
            new Guid("61ca5b76-66d0-4ce2-b260-7fd42696000a"), // PROJECTION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Hot-cold_empathy_gap",
        ],
    };

    private static readonly Bias OMISSION_BIAS = new()
    {
        Id = new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Omission Bias",
        Description =
            """
            # Omission Bias
            Omission bias is the phenomenon in which people prefer omission (inaction) over commission (action)
            and people tend to judge harm as a result of commission more negatively than harm as a result of
            omission. It can occur due to a number of processes, including psychological inertia, the perception
            of transaction costs, and the perception that commissions are more causal than omissions. In social
            political terms the Universal Declaration of Human Rights establishes how basic human rights are to
            be assessed in article 2, as "without distinction of any kind, such as race, colour, sex, language,
            religion, political or other opinion, national or social origin, property, birth or other status."
            criteria that are often subject to one or another form of omission bias. It is controversial as to
            whether omission bias is a cognitive bias or is often rational. The bias is often showcased through
            the trolley problem and has also been described as an explanation for the endowment effect and
            status quo bias.
            
            A real-world example is when parents decide not to vaccinate their children because of the potential
            chance of death—even when the probability the vaccination will cause death is much less likely than
            death from the disease prevented.
            """,

        Related = [
            new Guid("b81482f8-b2cf-4b86-a5a4-fcd29aee4e69"), // ENDOWMENT_EFFECT
            new Guid("b9e05a25-ac09-407d-8aee-f54a04decf0b"), // STATUS_QUO_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Omission_bias",
        ],
    };

    private static readonly Bias BASE_RATE_FALLACY = new()
    {
        Id = new Guid("1de0de03-a2a7-4248-b004-4152d84a3c86"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Base Rate Fallacy",
        Description =
            """
            # Base Rate Fallacy
            The base rate fallacy, also called base rate neglect or base rate bias, is a type of fallacy in
            which people tend to ignore the base rate (e.g., general prevalence) in favor of the individuating
            information (i.e., information pertaining only to a specific case). For example, if someone hears
            that a friend is very shy and quiet, they might think the friend is more likely to be a librarian
            than a salesperson, even though there are far more salespeople than librarians overall - hence
            making it more likely that their friend is actually a salesperson. Base rate neglect is a specific
            form of the more general extension neglect.
            
            Another example: Students were asked to estimate the GPAs of hypothetical students. When given
            relevant statistics about GPA distribution, students tended to ignore them if given descriptive
            information about the particular student even if the new descriptive information was obviously
            of little or no relevance to GPA.
            """,

        Related = [
            new Guid("8533edf9-3117-48c5-8f78-efbd996911f0"), // CONSERVATISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Base_rate_fallacy",
        ],
    };

    private static readonly Bias BIZARRENESS_EFFECT = new()
    {
        Id = new Guid("7f2d9bd2-96e5-4100-85f8-a13b37e91a9f"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Bizarreness Effect",
        Description =
            """
            # Bizarreness Effect
            Bizarreness effect is the tendency of bizarre material to be better remembered than common material.
            The scientific evidence for its existence is contested. Some research suggests it does exist, some
            suggests it doesn't exist and some suggests it leads to worse remembering.
            
            McDaniel and Einstein argues that bizarreness intrinsically does not enhance memory in their paper
            from 1986. They claim that bizarre information becomes distinctive. It is the distinctiveness that
            according to them makes encoding easier. Which makes common sense from an instinctual perspective
            as the human brain will disregard ingesting information it already is familiar with and will be
            particularly attuned to taking in new information as an adaptation technique.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Bizarreness_effect",
        ],
    };

    private static readonly Bias HUMOUR_EFFECT = new()
    {
        Id = new Guid("4c5ef2d4-5ebb-48ea-b9ee-9b2751ae6914"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Humour Effect",
        Description =
            """
            # Humour Effect
            The tendency to better remember humorous items than non-humorous ones.
            """,

        Related = [],
        Links = [],
    };

    private static readonly Bias VON_RESTORFF_EFFECT = new()
    {
        Id = new Guid("b922da6f-765e-42e9-b675-f8109c010f2f"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Von Restorff Effect",
        Description =
            """
            # Von Restorff Effect
            The Von Restorff effect, also known as the "isolation effect", predicts that when multiple
            homogeneous stimuli are presented, the stimulus that differs from the rest is more likely
            to be remembered. The theory was coined by German psychiatrist and pediatrician Hedwig
            von Restorff (1906–1962), who, in her 1933 study, found that when participants were presented
            with a list of categorically similar items with one distinctive, isolated item on the list,
            memory for the item was improved.
            
            For example, if a person examines a shopping list with one item highlighted in bright green,
            he or she will be more likely to remember the highlighted item than any of the others.
            Additionally, in the following list of words – desk, chair, bed, table, chipmunk, dresser,
            stool, couch – "chipmunk" will be remembered the most as it stands out against the other
            words in its meaning.
            
            There have been many studies that demonstrate and confirm the von Restorff effect in children
            and young adults. Another study found that college-aged students performed better when trying
            to remember an outstanding item in a list during an immediate memory-task whereas elderly
            individuals did not remember it well, suggesting a difference in processing strategies
            between the age groups.
            
            In yet another study, although a significant von Restorff effect was produced amongst both
            age groups when manipulating font color, it was found to be smaller in older adults than
            younger adults. This too indicates that older people display lesser benefits for distinctive
            information compared to younger people.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Von_Restorff_effect",
        ],
    };

    private static readonly Bias PICTURE_SUPERIORITY_EFFECT = new()
    {
        Id = new Guid("2b8f679b-480c-4588-96b5-951767f870e3"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Picture Superiority Effect",
        Description =
            """
            # Picture Superiority Effect
            The picture superiority effect refers to the phenomenon in which pictures and images are more
            likely to be remembered than are words. This effect has been demonstrated in numerous experiments
            using different methods. It is based on the notion that "human memory is extremely sensitive to
            the symbolic modality of presentation of event information". Explanations for the picture
            superiority effect are not concrete and are still being debated, however an evolutionary
            explanation is that sight has a long history stretching back millions of years and was
            crucial to survival in the past, whereas reading is a relatively recent invention, and
            specific cognitive processes, such as decoding symbols and linking them to meaning.
            """,

        Related = [
            new Guid("eeca14c3-8710-4522-8991-81db170d7f8b"), // MODALITY_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Picture_superiority_effect",
        ],
    };

    private static readonly Bias SELF_REFERENCE_EFFECT = new()
    {
        Id = new Guid("302b0004-1f18-4ed0-8fc1-6396fc7e6dbe"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Self-Reference Effect",
        Description =
            """
            # Self-Reference Effect
            The self-reference effect is a tendency for people to encode information differently depending on
            whether they are implicated in the information. When people are asked to remember information when
            it is related in some way to themselves, the recall rate can be improved.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Self-reference_effect",
        ],
    };

    private static readonly Bias ANCHORING_EFFECT = new()
    {
        Id = new Guid("fe94ab26-70bb-4682-b7ee-e2828e4b02bd"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Anchoring Effect",
        Description =
            """
            # Anchoring Effect
            The anchoring effect is a psychological phenomenon in which an individual's judgments or decisions
            are influenced by a reference point or "anchor" which can be completely irrelevant. Both numeric
            and non-numeric anchoring have been reported in research. In numeric anchoring, once the value
            of the anchor is set, subsequent arguments, estimates, etc. made by an individual may change
            from what they would have otherwise been without the anchor. For example, an individual may
            be more likely to purchase a car if it is placed alongside a more expensive model (the anchor).
            Prices discussed in negotiations that are lower than the anchor may seem reasonable, perhaps
            even cheap to the buyer, even if said prices are still relatively higher than the actual market
            value of the car. Another example may be when estimating the orbit of Mars, one might start
            with the Earth's orbit (365 days) and then adjust upward until they reach a value that seems
            reasonable (usually less than 687 days, the correct answer).
            
            The original description of the anchoring effect came from psychophysics. When judging stimuli
            along a continuum, it was noticed that the first and last stimuli were used to compare the other
            stimuli (this is also referred to as "end anchoring"). This was applied to attitudes by Sherif
            et al. in their 1958 article "Assimilation and effects of anchoring stimuli on judgments".
            
            Anchoring in negotiation
            In the negotiation process anchoring serves to determine an accepted starting point for the
            subsequent negotiations. As soon as one side states their first price offer, the (subjective)
            anchor is set. The counterbid (counter-anchor) is the second-anchor.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Anchoring_effect",
        ],
    };

    private static readonly Bias CONSERVATISM_BIAS = new()
    {
        Id = new Guid("8533edf9-3117-48c5-8f78-efbd996911f0"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Conservatism Bias",
        Description =
            """
            # Conservatism Bias
            In cognitive psychology and decision science, conservatism or conservatism bias is a bias which refers
            to the tendency to revise one's belief insufficiently when presented with new evidence. This bias
            describes human belief revision in which people over-weigh the prior distribution (base rate) and
            under-weigh new sample evidence when compared to Bayesian belief-revision. In other words, people
            update their prior beliefs as new evidence becomes available, but they do so more slowly than
            they would if they used Bayes' theorem.
            
            In finance, evidence has been found that investors under-react to corporate events, consistent
            with conservatism. This includes announcements of earnings, changes in dividends, and stock splits.
            """,

        Related = [
            new Guid("1de0de03-a2a7-4248-b004-4152d84a3c86"), // BASE_RATE_FALLACY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Conservatism_(belief_revision)",
        ],
    };

    private static readonly Bias CONTRAST_EFFECT = new()
    {
        Id = new Guid("bc69c14d-0f2d-47ce-b20f-836fae36beb6"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Contrast Effect",
        Description =
            """
            # Contrast Effect
            A contrast effect is the enhancement or diminishment, relative to normal, of perception, cognition
            or related performance as a result of successive (immediately previous) or simultaneous exposure
            to a stimulus of lesser or greater value in the same dimension. (Here, normal perception, cognition
            or performance is that which would be obtained in the absence of the comparison stimulus—i.e.,
            one based on all previous experience.)
            
            Perception example: A neutral gray target will appear lighter or darker than it does in isolation
            when immediately preceded by, or simultaneously compared to, respectively, a dark gray or light
            gray target.
            
            Cognition example: A person will appear more or less attractive than that person does in isolation
            when immediately preceded by, or simultaneously compared to, respectively, a less or more attractive
            person.
            
            Performance example: A laboratory rat will work faster, or slower, during a stimulus predicting a
            given amount of reward when that stimulus and reward are immediately preceded by, or alternated with,
            respectively, different stimuli associated with either a lesser or greater amount of reward.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Contrast_effect",
        ],
    };

    private static readonly Bias DISTINCTION_BIAS = new()
    {
        Id = new Guid("0c65fbf8-c74a-49a1-8a16-0e789bce9524"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Distinction Bias",
        Description =
            """
            # Distinction Bias
            The tendency to view two options as more distinctive when evaluating them simultaneously than when
            evaluating them separately.
            
            For example, when televisions are displayed next to each other on the sales floor, the difference
            in quality between two very similar, high-quality televisions may appear great. A consumer may pay
            a much higher price for the higher-quality television, even though the difference in quality is
            imperceptible when the televisions are viewed in isolation. Because the consumer will likely be
            watching only one television at a time, the lower-cost television would have provided a similar
            experience at a lower cost.
            
            To avoid this bias, avoid comparing two jobs, or houses, directly. Instead, consider each job, or
            house, individually and make an overall assessment of each one on its own, and then compare
            assessments, which allows them to make a choice that accurately predicts future experience.
            """,

        Related = [
            new Guid("593f2a10-46a6-471e-9ab3-86df740df6f2"), // LESS_IS_BETTER_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Distinction_bias",
        ],
    };

    private static readonly Bias FOCUSING_EFFECT = new()
    {
        Id = new Guid("490f26a1-3b9b-4048-9488-8ba93b8bd8af"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Focusing Effect",
        Description =
            """
            # Focusing Effect
            A cognitive bias that occurs when people place too much importance on only one aspect of an
            evaluation, causing an error in accurately predicting the utility of a future outcome.
            
            Example: It is sunnier in California therefore people must be more happy there. Or a job
            that pays more money must be better.
            """,

        Related = [],
        Links = [],
    };

    private static readonly Bias FRAMING_EFFECT = new()
    {
        Id = new Guid("a1950fc4-20e0-4d36-8e68-540b491b2d23"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Framing Effect",
        Description =
            """
            # Framing Effect
            The framing effect is a cognitive bias in which people decide between options based on whether the options
            are presented with positive or negative connotations. Individuals have a tendency to make risk-avoidant
            or choices when options are positively framed, while selecting more loss-avoidant options when presented with
            a negative frame. In studies of the bias, options are presented in terms of the probability of either
            losses or gains. While differently expressed, the options described are in effect identical. Gain and
            loss are defined in the scenario as descriptions of outcomes, for example, lives lost or saved, patients
            treated or not treated, monetary gains or losses.
            
            Prospect theory posits that a loss is more significant than the equivalent gain, that a sure gain (certainty
            effect and pseudocertainty effect) is favored over a probabilistic gain, and that a probabilistic loss is
            preferred to a definite loss. One of the dangers of framing effects is that people are often provided with
            options within the context of only one of the two frames.
            
            The concept helps to develop an understanding of frame analysis within social movements, and also in the
            formation of political opinion where spin plays a large role in political opinion polls that are framed to
            encourage a response beneficial to the organization that has commissioned the poll. It has been suggested
            that the use of the technique is discrediting political polls themselves. The effect is reduced, or even
            eliminated, if ample credible information is provided to people.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Framing_effect_(psychology)",
        ],
    };

    private static readonly Bias MONEY_ILLUSION = new()
    {
        Id = new Guid("33136203-8d52-42e5-ad32-561b3c288676"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Money Illusion",
        Description =
            """
            # Money Illusion
            In economics, money illusion, or price illusion, is a cognitive bias where money is thought of in nominal,
            rather than real terms. In other words, the face value (nominal value) of money is mistaken for its
            purchasing power (real value) at a previous point in time. Viewing purchasing power as measured by the
            nominal value is false, as modern fiat currencies have no intrinsic value and their real value depends
            purely on the price level. The term was coined by Irving Fisher in *Stabilizing the Dollar*. It was
            popularized by John Maynard Keynes in the early twentieth century, and Irving Fisher wrote an important
            book on the subject, *The Money Illusion*, in 1928.
            
            The existence of money illusion is disputed by monetary economists who contend that people act rationally
            (i.e. think in real prices) with regard to their wealth. Eldar Shafir, Peter A. Diamond, and Amos
            Tversky (1997) have provided empirical evidence for the existence of the effect and it has been shown to
            affect behaviour in a variety of experimental and real-world situations.
            
            Shafir et al. also state that money illusion influences economic behaviour in three main ways:
            
            - Price stickiness. Money illusion has been proposed as one reason why nominal prices are slow to change
              even where inflation has caused real prices to fall or costs to rise.
              
            - Contracts and laws are not indexed to inflation as frequently as one would rationally expect.
            
            - Social discourse, in formal media and more generally, reflects some confusion about real and nominal value.
            
            Money illusion can also influence people's perceptions of outcomes. Experiments have shown that people
            generally perceive an approximate 2% cut in nominal income with no change in monetary value as unfair,
            but see a 2% rise in nominal income where there is 4% inflation as fair, despite them being almost rational
            equivalents. This result is consistent with the 'Myopic Loss Aversion theory'. Furthermore, the money illusion
            means nominal changes in price can influence demand even if real prices have remained constant.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Money_illusion",
        ],
    };

    private static readonly Bias WEBER_FECHNER_LAW = new()
    {
        Id = new Guid("528077d5-fdad-47df-89d4-6a32287c321b"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Weber–Fechner Law",
        Description =
            """
            # Weber–Fechner Law
            The Weber–Fechner laws are two related scientific laws in the field of psychophysics, known as Weber's law and
            Fechner's law. Both relate to human perception, more specifically the relation between the actual change in a
            physical stimulus and the perceived change. This includes stimuli to all senses: vision, hearing, taste, touch,
            and smell.
            
            Ernst Heinrich Weber states that "the minimum increase of stimulus which will produce a perceptible increase
            of sensation is proportional to the pre-existent stimulus," while Gustav Fechner's law is an inference from
            Weber's law (with additional assumptions) which states that the intensity of our sensation increases as the
            logarithm of an increase in energy rather than as rapidly as the increase.
            
            Psychological studies show that it becomes increasingly difficult to discriminate between two numbers as the
            difference between them decreases. This is called the distance effect. This is important in areas of magnitude
            estimation, such as dealing with large scales and estimating distances. It may also play a role in explaining
            why consumers neglect to shop around to save a small percentage on a large purchase, but will shop around to
            save a large percentage on a small purchase which represents a much smaller absolute dollar amount.
            
            Preliminary research has found that pleasant emotions adhere to Weber’s Law, with accuracy in judging their
            intensity decreasing as pleasantness increases. However, this pattern wasn't observed for unpleasant emotions,
            suggesting a survival-related need for accurately discerning high-intensity negative emotions.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Weber%E2%80%93Fechner_law",
        ],
    };

    private static readonly Bias CONFIRMATION_BIAS = new()
    {
        Id = new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Confirmation Bias",
        Description =
            """
            # Confirmation Bias
            Confirmation bias (also confirmatory bias, myside bias, or congeniality bias) is the tendency to search for,
            interpret, favor, and recall information in a way that confirms or supports one's prior beliefs or values.
            People display this bias when they select information that supports their views, ignoring contrary information,
            or when they interpret ambiguous evidence as supporting their existing attitudes. The effect is strongest for
            desired outcomes, for emotionally charged issues, and for deeply entrenched beliefs.
            
            In social media, confirmation bias is amplified by the use of filter bubbles, or "algorithmic editing", which
            displays to individuals only information they are likely to agree with, while excluding opposing views. Some
            have argued that confirmation bias is the reason why society can never escape from filter bubbles, because
            individuals are psychologically hardwired to seek information that agrees with their preexisting values and
            beliefs. Others have further argued that the mixture of the two is degrading democracy—claiming that this
            "algorithmic editing" removes diverse viewpoints and information—and that unless filter bubble algorithms
            are removed, voters will be unable to make fully informed political decisions.
            
            Many times in the history of science, scientists have resisted new discoveries by selectively interpreting or
            ignoring unfavorable data. Several studies have shown that scientists rate studies that report findings
            consistent with their prior beliefs more favorably than studies reporting findings inconsistent with
            their previous beliefs. Further, confirmation biases can sustain scientific theories or research programs
            in the face of inadequate or even contradictory evidence. The discipline of parapsychology is often cited
            as an example.
            """,

        Related = [
            new Guid("4e571eaf-7c2b-44c8-b8cb-0c8da658b82d"), // FREQUENCY_ILLUSION
            new Guid("d749ce96-32f3-4c3d-86f7-26ff4edabe4a"), // AVAILABILITY_HEURISTIC
            new Guid("0378a05c-b55b-4451-a7f4-b5e1d6287d83"), // FADING_AFFECT_BIAS
            new Guid("fee14af4-34af-4cd0-a72c-9ad489516b60"), // CONGRUENCE_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Confirmation_bias",
        ],
    };

    private static readonly Bias CONGRUENCE_BIAS = new()
    {
        Id = new Guid("fee14af4-34af-4cd0-a72c-9ad489516b60"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Congruence Bias",
        Description =
            """
            # Congruence Bias
            Congruence bias is the tendency of people to over-rely on testing their initial hypothesis (the most congruent one)
            while neglecting to test alternative hypotheses. That is, people rarely try experiments that could disprove their
            initial belief, but rather try to repeat their initial results. It is a special case of the confirmation bias.
            """,

        Related = [
            new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"), // CONFIRMATION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Congruence_bias",
        ],
    };

    private static readonly Bias CHOICE_SUPPORTIVE_BIAS = new()
    {
        Id = new Guid("51337702-9dc4-442a-8584-78f56e9ec186"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Choice-supportive Bias",
        Description =
            """
            # Choice-supportive Bias
            Choice-supportive bias or post-purchase rationalization is the tendency to retroactively ascribe positive attributes
            to an option one has selected and/or to demote the forgone options. It is part of cognitive science, and is a
            distinct cognitive bias that occurs once a decision is made. For example, if a person chooses option A instead of
            option B, they are likely to ignore or downplay the faults of option A while amplifying or ascribing new negative
            faults to option B. Conversely, they are also likely to notice and amplify the advantages of option A and not notice
            or de-emphasize those of option B.
            """,

        Related = [
            new Guid("dd45c762-0599-4c6d-82e0-d10f7ee85bb1"), // MISATTRIBUTION_OF_MEMORY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Choice-supportive_bias",
        ],
    };

    private static readonly Bias SELECTIVE_PERCEPTION = new()
    {
        Id = new Guid("91fded4f-de89-405e-8627-dba49cf5deaa"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Selective Perception",
        Description =
            """
            # Selective Perception
            Selective perception is the tendency not to notice and more quickly forget stimuli that cause emotional discomfort
            and contradict prior beliefs. For example, a teacher may have a favorite student because they are biased by in-group
            favoritism. The teacher ignores the student's poor attainment. Conversely, they might not notice the progress of
            their least favorite student. It can also occur when consuming mass media, allowing people to see facts and
            opinions they like while ignoring those that do not fit with particular opinions, values, beliefs, or frame of
            reference. Psychologists believe this process occurs automatically.
            """,

        Related = [
            new Guid("75e51ef5-f992-41c2-8778-0002c617db9a"), // OSTRICH_EFFECT
            new Guid("1dfd3e9e-e44e-44cf-b8a0-95dea7a0e780"), // NORMALCY_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Selective_perception",
        ],
    };

    private static readonly Bias OBSERVER_EXPECTANCY_EFFECT = new()
    {
        Id = new Guid("c06c9a63-15aa-4601-aff4-ddfe6dd9727a"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Observer-Expectancy Effect",
        Description =
            """
            # Observer-Expectancy Effect
            The observer-expectancy effect[a] is a form of reactivity in which a researcher's cognitive bias causes them to
            subconsciously influence the participants of an experiment. Confirmation bias can lead to the experimenter
            interpreting results incorrectly because of the tendency to look for information that conforms to their hypothesis,
            and overlook information that argues against it. It is a significant threat to a study's internal validity, and
            is therefore typically controlled using a double-blind experimental design.
            
            The classic example of experimenter bias is that of "Clever Hans", an Orlov Trotter horse claimed by his owner
            von Osten to be able to do arithmetic and other tasks. As a result of the large public interest in Clever Hans,
            philosopher and psychologist Carl Stumpf, along with his assistant Oskar Pfungst, investigated these claims.
            Ruling out simple fraud, Pfungst determined that the horse could answer correctly even when von Osten did not
            ask the questions. However, the horse was unable to answer correctly when either it could not see the questioner,
            or if the questioner themselves was unaware of the correct answer: When von Osten knew the answers to the questions,
            Hans answered correctly 89% of the time. However, when von Osten did not know the answers, Hans guessed only 6% of
            questions correctly. Pfungst then proceeded to examine the behaviour of the questioner in detail, and showed that
            as the horse's taps approached the right answer, the questioner's posture and facial expression changed in ways
            that were consistent with an increase in tension, which was released when the horse made the final, correct tap.
            This provided a cue that the horse had learned to use as a reinforced cue to stop tapping.
            """,

        Related = [
            new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"), // CONFIRMATION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Blinded_experiment",
            "https://en.wikipedia.org/wiki/Observer-expectancy_effect",
        ],
    };

    private static readonly Bias OSTRICH_EFFECT = new()
    {
        Id = new Guid("75e51ef5-f992-41c2-8778-0002c617db9a"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Ostrich Effect",
        Description =
            """
            # Ostrich Effect
            The ostrich effect, also known as the ostrich problem, was originally coined by Galai & Sade (2003). The name
            comes from the common (but false) legend that ostriches bury their heads in the sand to avoid danger. This
            effect is a cognitive bias where people tend to “bury their head in the sand” and avoid potentially negative
            but useful information, such as feedback on progress, to avoid psychological discomfort.
            
            There is neuroscientific evidence of the ostrich effect. Sharot et al. (2012) investigated the differences in
            positive and negative information when updating existing beliefs. Consistent with the ostrich effect,
            participants presented with negative information were more likely to avoid updating their beliefs.
            
            An everyday example of the ostrich effect in a financial context is people avoiding checking their bank account
            balance after spending a lot of money. There are known negative implications of the ostrich effect in healthcare.
            For example, people with diabetes avoid monitoring their blood sugar levels.
            """,

        Related = [
            new Guid("91fded4f-de89-405e-8627-dba49cf5deaa"), // SELECTIVE_PERCEPTION
            new Guid("1dfd3e9e-e44e-44cf-b8a0-95dea7a0e780"), // NORMALCY_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Ostrich_effect",
        ],
    };

    private static readonly Bias SUBJECTIVE_VALIDATION = new()
    {
        Id = new Guid("85612b34-0a78-454e-a204-7840bc11521c"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Subjective Validation",
        Description =
            """
            # Subjective Validation
            Subjective validation, sometimes called personal validation effect, is a cognitive bias by which people will
            consider a statement or another piece of information to be correct if it has any personal meaning or significance
            to them. People whose opinion is affected by subjective validation will perceive two unrelated events (i.e., a
            coincidence) to be related because their personal beliefs demand that they be related. Closely related to the
            Forer effect, subjective validation is an important element in cold reading. It is considered to be the main
            reason behind most reports of paranormal phenomena.
            
            Example: Belief in a cold reading. Cold reading is a set of techniques used by mentalists, psychics, fortune-tellers,
            and mediums. Without prior knowledge, a practiced cold-reader can quickly obtain a great deal of information by
            analyzing the person's body language, age, clothing or fashion, hairstyle, gender, sexual orientation, religion,
            ethnicity, level of education, manner of speech, place of origin, etc. during a line of questioning.
            """,

        Related = [
            new Guid("05e4a15d-5c3e-42e9-88aa-bb40350d17e2"), // BARNUM_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Subjective_validation",
        ],
    };

    private static readonly Bias BARNUM_EFFECT = new()
    {
        Id = new Guid("2cb8514a-c4a2-4cf6-aed7-72d7870ace84"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Barnum Effect",
        Description =
            """
            # Barnum Effect
            The Barnum effect, also called the Forer effect or, less commonly, the Barnum–Forer effect, is a common psychological
            phenomenon whereby individuals give high accuracy ratings to descriptions of their personality that supposedly are
            tailored specifically to them, yet which are in fact vague and general enough to apply to a wide range of people.
            This effect can provide a partial explanation for the widespread acceptance of some paranormal beliefs and practices,
            such as astrology, fortune telling, aura reading, and some types of personality tests.
            
            Example: Belief in a cold reading. Cold reading is a set of techniques used by mentalists, psychics, fortune-tellers,
            and mediums. Without prior knowledge, a practiced cold-reader can quickly obtain a great deal of information by
            analyzing the person's body language, age, clothing or fashion, hairstyle, gender, sexual orientation, religion,
            ethnicity, level of education, manner of speech, place of origin, etc. during a line of questioning.
            """,

        Related = [
            new Guid("85612b34-0a78-454e-a204-7840bc11521c"), // SUBJECTIVE_VALIDATION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Barnum_effect",
        ],
    };

    private static readonly Bias CONTINUED_INFLUENCE_EFFECT = new()
    {
        Id = new Guid("7169c5e4-ca95-4568-b816-a36e2049993b"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Continued Influence Effect",
        Description =
            """
            # Continued Influence Effect
            The tendency to believe previously learned misinformation even after it has been corrected. Misinformation can still
            influence inferences one generates after a correction has occurred.
            """,

        Related = [
            new Guid("2b69b071-6587-4ea1-a4f5-aee4e2fef43c"), // MISINFORMATION_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/List_of_cognitive_biases#Other_memory_biases",
        ],
    };

    private static readonly Bias SEMMELWEIS_REFLEX = new()
    {
        Id = new Guid("48e2374a-9919-43eb-baa6-fc8c4f837d31"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Semmelweis Reflex",
        Description =
            """
            # Semmelweis Reflex
            The Semmelweis reflex or "Semmelweis effect" is a metaphor for the reflex-like tendency to reject new evidence
            or new knowledge because it contradicts established norms, beliefs, or paradigms.
            """,

        Related = [
            new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"), // CONFIRMATION_BIAS
            new Guid("7256f3f1-6650-4c45-bb85-36d81c9edd1a"), // AUTHORITY_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Semmelweis_reflex",
        ],
    };

    private static readonly Bias BIAS_BLIND_SPOT = new()
    {
        Id = new Guid("d8f01e8b-23c3-47da-979e-f18a3d4e104d"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Bias Blind Spot",
        Description =
            """
            # Bias Blind Spot
            The bias blind spot is the cognitive bias of recognizing the impact of biases on the judgment of others, while failing
            to see the impact of biases on one's own judgment. The term was created by Emily Pronin, a social psychologist from
            Princeton University's Department of Psychology, with colleagues Daniel Lin and Lee Ross. The bias blind spot is named
            after the visual blind spot. Most people appear to exhibit the bias blind spot. In a sample of more than 600 residents
            of the United States, more than 85% believed they were less biased than the average American. Only one participant
            believed that they were more biased than the average American. People do vary with regard to the extent to which
            they exhibit the bias blind spot. This phenomenon has been successfully replicated and it appears that in general,
            stronger personal free will beliefs are associated with bias blind spot. It appears to be a stable individual
            difference that is measurable.
            
            The bias blind spot appears to be a true blind spot in that it is unrelated to actual decision making ability.
            Performance on indices of decision making competence are not related to individual differences in bias blind spot.
            In other words, most people appear to believe that they are less biased than others, regardless of their actual
            decision making ability.
            """,

        Related = [
            new Guid("80f9b496-798a-4a1e-a426-815f23b8698e"), // INTROSPECTION_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Bias_blind_spot",
        ],
    };

    private static readonly Bias INTROSPECTION_ILLUSION = new()
    {
        Id = new Guid("80f9b496-798a-4a1e-a426-815f23b8698e"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Introspection Illusion",
        Description =
            """
            # Introspection Illusion
            The introspection illusion is a cognitive bias in which people wrongly think they have direct insight into the origins
            of their mental states, while treating others' introspections as unreliable. The illusion has been examined in
            psychological experiments, and suggested as a basis for biases in how people compare themselves to others. These
            experiments have been interpreted as suggesting that, rather than offering direct access to the processes underlying
            mental states, introspection is a process of construction and inference, much as people indirectly infer others' mental
            states from their behaviour.
            
            When people mistake unreliable introspection for genuine self-knowledge, the result can be an illusion of superiority
            over other people, for example when each person thinks they are less biased and less conformist than the rest of the
            group. Even when experimental subjects are provided with reports of other subjects' introspections, in as detailed a
            form as possible, they still rate those other introspections as unreliable while treating their own as reliable.
            Although the hypothesis of an introspection illusion informs some psychological research, the existing evidence is
            arguably inadequate to decide how reliable introspection is in normal circumstances.
            
            The phrase "introspection illusion" was coined by Emily Pronin. Pronin describes the illusion as having four components:
            
            - People give a strong weighting to introspective evidence when assessing themselves.
            
            - They do not give such a strong weight when assessing others.
            
            - People disregard their own behaviour when assessing themselves (but not others).
            
            - Own introspections are more highly weighted than others. It is not just that people lack access to each other's
              introspections: they regard only their own as reliable.
            """,

        Related = [
            new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"), // ILLUSORY_SUPERIORITY 
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Introspection_illusion",
        ],
    };

    private static readonly Bias ILLUSORY_SUPERIORITY = new()
    {
        Id = new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Illusory Superiority",
        Description =
            """
            # Illusory Superiority
            In social psychology, illusory superiority is a cognitive bias wherein people overestimate their own qualities and
            abilities compared to others. Illusory superiority is one of many positive illusions, relating to the self, that
            are evident in the study of intelligence, the effective performance of tasks and tests, and the possession of
            desirable personal characteristics and personality traits. Overestimation of abilities compared to an objective
            measure is known as the overconfidence effect.
            
            The term "illusory superiority" was first used by the researchers Van Yperen and Buunk, in 1991. The phenomenon is
            also known as the above-average effect, the superiority bias, the leniency error, the sense of relative superiority,
            the primus inter pares effect, and the Lake Wobegon effect, named after the fictional town where all the children are
            above average. The Dunning-Kruger effect is a form of illusory superiority shown by people on a task where their
            level of skill is low.
            """,

        Related = [
            new Guid("b821d449-64e5-4c0a-9d5a-3fda609a9b86"), // OVERCONFIDENCE_EFFECT
            new Guid("b9c06da1-d2eb-4871-8159-a2a6d25e9eff"), // DUNNING_KRUGER_EFFECT
            new Guid("80f9b496-798a-4a1e-a426-815f23b8698e"), // INTROSPECTION_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusory_superiority",
        ],
    };

    private static readonly Bias OVERCONFIDENCE_EFFECT = new()
    {
        Id = new Guid("b821d449-64e5-4c0a-9d5a-3fda609a9b86"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Overconfidence Effect",
        Description =
            """
            # Overconfidence Effect
            The overconfidence effect is a well-established bias in which a person's subjective confidence in their
            judgments is reliably greater than the objective accuracy of those judgments, especially when confidence
            is relatively high.
            
            The most common way in which overconfidence has been studied is by asking people how confident they are of
            specific beliefs they hold or answers they provide. The data show that confidence systematically exceeds
            accuracy, implying people are more sure that they are correct than they deserve to be.
            
            The following is an incomplete list of events related or triggered by bias/overconfidence and a failing
            (safety) culture:
            
            - Chernobyl disaster
            - Sinking of the Titanic
            - Space Shuttle Challenger disaster
            - Space Shuttle Columbia disaster
            - Deepwater Horizon oil spill
            - Titan submersible implosion
            """,

        Related = [
            new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"), // ILLUSORY_SUPERIORITY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Overconfidence_effect",
        ],
    };

    private static readonly Bias NAÏVE_CYNICISM = new()
    {
        Id = new Guid("5ae6f7ec-3be2-47ad-ad75-0ed114f97fe0"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Naïve Cynicism",
        Description =
            """
            # Naïve Cynicism
            Naïve cynicism is a philosophy of mind, cognitive bias and form of psychological egoism that occurs when
            people naïvely expect more egocentric bias in others than actually is the case.
            
            The term was formally proposed by Justin Kruger and Thomas Gilovich and has been studied across a wide range
            of contexts including: negotiations, group-membership, marriage, economics, government policy and more.
            
            The theory of naïve cynicism can be described as:
            
            - I am not biased.
            - You are biased if you disagree with me.
            - Your intentions/actions reflect your underlying egocentric biases.
            
            As with naïve cynicism, the theory of naïve realism hinges on the acceptance of the following three beliefs:
            
            - I am not biased.
            - Reasonable people are not biased.
            - All others are biased.
            
            Naïve cynicism can be thought of as the counter to naïve realism, which is the belief that an individual
            perceives the social world objectively while others perceive it subjectively.
            
            It is important to discern that naïve cynicism is related to the notion that others have an egocentric bias
            that motivates them to do things for their own self-interest rather than for altruistic reasons.
            
            Both of these theories, however, relate to the extent that adults credit or discredit the beliefs or statements
            of others.
            
            Example: Cold War
            The American reaction to a Russian SALT treaty during the Cold War is one well-known example of naïve cynicism
            in history. Political leaders negotiating on behalf of the United States discredited the offer simply because
            it was proposed by the Russian side.
            
            Former U.S. congressman Floyd Spence indicates the use of naïve cynicism in this quote:
            "I have had a philosophy for some time in regard to SALT, and it goes like this: the Russians will not accept
            a SALT treaty that is not in their best interest, and it seems to me that if it is their best interests, it
            can‘t be in our best interest."
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Na%C3%AFve_cynicism",
        ],
    };

    private static readonly Bias NAÏVE_REALISM = new()
    {
        Id = new Guid("f0ad095e-8e9c-4bfb-855e-11fb5dd58cea"),
        Category = BiasCategory.TOO_MUCH_INFORMATION,
        Name = "Naïve Realism",
        Description =
            """
            # Naïve Realism
            In social psychology, naïve realism is the human tendency to believe that we see the world around us objectively,
            and that people who disagree with us must be uninformed, irrational, or biased.
            
            Naïve realism provides a theoretical basis for several other cognitive biases, which are systematic errors when it
            comes to thinking and making decisions. These include the false consensus effect, actor–observer bias, bias blind
            spot, and fundamental attribution error, among others.
            
            Lee Ross and fellow psychologist Andrew Ward have outlined three interrelated assumptions, or "tenets", that make up
            naïve realism. They argue that these assumptions are supported by a long line of thinking in social psychology,
            along with several empirical studies. According to their model, people:
            
            - Believe that they see the world objectively and without bias.
            
            - Expect that others will come to the same conclusions, so long as they are exposed to the same information and
            interpret it in a rational manner.
            
            - Assume that others who do not share the same views must be ignorant, irrational, or biased.
            """,

        Related = [
            new Guid("bc0dc6d3-5115-4def-91ae-a38aebed185e"), // FALSE_CONSENSUS_EFFECT
            new Guid("d8f01e8b-23c3-47da-979e-f18a3d4e104d"), // BIAS_BLIND_SPOT
            new Guid("5da6dcf4-ed01-4e14-99b0-7a624b16cf17"), // ACTOR_OBSERVER_BIAS
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Na%C3%AFve_realism_(psychology)",
        ],
    };

    #endregion

    #region NOT_ENOUGH_MEANING

    private static readonly Bias CONFABULATION = new()
    {
        Id = new Guid("2bbea096-a2a6-413f-85ce-32b5ae18669f"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Confabulation",
        Description =
            """
            # Confabulation
            In psychology, confabulation is a memory error consisting of the production of fabricated, distorted, or misinterpreted
            memories about oneself or the world. People who confabulate present with incorrect memories ranging from subtle inaccuracies
            to surreal fabrications, and may include confusion or distortion in the temporal framing (timing, sequence or duration) of
            memories. In general, they are very confident about their recollections, even when challenged with contradictory evidence.
            Confabulation occurs when individuals mistakenly recall false information, without intending to deceive.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Confabulation",
        ],
    };

    private static readonly Bias CLUSTERING_ILLUSION = new()
    {
        Id = new Guid("2c2ed1a8-aa4d-486d-a9b4-5a16ae9230c9"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Clustering Illusion",
        Description =
            """
            # Clustering Illusion
            The clustering illusion is the tendency to erroneously consider the inevitable "streaks" or "clusters" arising in small
            samples from random distributions to be non-random. The illusion is caused by a human tendency to underpredict the amount
            of variability likely to appear in a small sample of random or pseudorandom data.
            """,

        Related = [
            new Guid("7fce783e-2120-4aad-9805-2c2a2b937b7d"), // ILLUSION_OF_CONTROL
            new Guid("465418ae-54b8-42ef-a29e-6ee9e9ffa769"), // INSENSITIVITY_TO_SAMPLE_SIZE
            new Guid("61cd7e34-23be-43ef-ab97-8118cef7d23f"), // MONTE_CARLO_FALLACY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Clustering_illusion",
        ],
    };

    private static readonly Bias INSENSITIVITY_TO_SAMPLE_SIZE = new()
    {
        Id = new Guid("465418ae-54b8-42ef-a29e-6ee9e9ffa769"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Insensitivity to Sample Size",
        Description =
            """
            # Insensitivity to Sample Size
            Insensitivity to sample size is a cognitive bias that occurs when people judge the probability of obtaining a sample statistic
            without respect to the sample size. For example, in one study, subjects assigned the same probability to the likelihood of
            obtaining a mean height of above six feet (183 cm) in samples of 10, 100, and 1,000 men. In other words, variation is more
            likely in smaller samples, but people may not expect this.
            """,

        Related = [
            new Guid("2c2ed1a8-aa4d-486d-a9b4-5a16ae9230c9"), // CLUSTERING_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Insensitivity_to_sample_size",
        ],
    };

    private static readonly Bias NEGLECT_OF_PROBABILITY = new()
    {
        Id = new Guid("44c6efd7-53f1-4d22-82fe-25e941390089"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Neglect of Probability",
        Description =
            """
            # Neglect of Probability
            The neglect of probability, a type of cognitive bias, is the tendency to disregard probability when making a decision under
            uncertainty and is one simple way in which people regularly violate the normative rules for decision making. Small risks are
            typically either neglected entirely or hugely overrated. The continuum between the extremes is ignored. The term probability
            neglect was coined by Cass Sunstein.
            
            There are many related ways in which people violate the normative rules of decision making with regard to probability including
            the hindsight bias, the neglect of prior base rates effect, and the gambler's fallacy. However, this bias is different, in that,
            rather than incorrectly using probability, the actor disregards it.
            """,

        Related = [
            new Guid("b9edd2f0-8503-4eb5-a4c3-369fcb318894"), // HINDSIGHT_BIAS
            new Guid("1de0de03-a2a7-4248-b004-4152d84a3c86"), // BASE_RATE_FALLACY
            new Guid("61cd7e34-23be-43ef-ab97-8118cef7d23f"), // MONTE_CARLO_FALLACY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Neglect_of_probability",
        ],
    };

    private static readonly Bias ANECDOTAL_FALLACY = new()
    {
        Id = new Guid("a448fe93-b176-4b5f-9498-f57f3f970a67"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Anecdotal Fallacy",
        Description =
            """
            # Anecdotal Fallacy
            Misuse of anecdotal evidence is an informal fallacy and is sometimes referred to as the "person who" fallacy
            ("I know a person who..."; "I know of a case where..." etc.) which places undue weight on experiences of close
            peers which may not be typical.
            
            A common way anecdotal evidence becomes unscientific is through fallacious reasoning such as the "Post hoc ergo
            propter hoc" fallacy, the human tendency to assume that if one event happens after another, then the first must
            be the cause of the second. Another fallacy involves inductive reasoning. For instance, if an anecdote illustrates
            a desired conclusion rather than a logical conclusion, it is considered a faulty or hasty generalization.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Anecdotal_evidence",
            "https://en.wikipedia.org/wiki/Post_hoc_ergo_propter_hoc",
        ],
    };

    private static readonly Bias ILLUSION_OF_VALIDITY = new()
    {
        Id = new Guid("8f68af8b-7b27-4697-bcf6-8bd4a5392a22"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Illusion of Validity",
        Description =
            """
            # Illusion of Validity
            A cognitive bias in which a person overestimates his or her ability to interpret and predict accurately the outcome
            when analyzing a set of data, in particular when the data analyzed show a very consistent pattern — that is, when the
            data "tell" a coherent story. This effect persists even when the person is aware of all the factors that limit the
            accuracy of his or her predictions, that is when the data and/or methods used to judge them lead to highly fallible
            predictions. 
            
            Example: Subjects reported higher confidence in a prediction of the final grade point average of a student after
            seeing a first-year record of consistent B’s than a first-year record of an even number of A’s and C’s. Consistent
            patterns may be observed when input variables are highly redundant or correlated, which may increase subjective
            confidence. However, a number of highly correlated inputs should not increase confidence much more than only one
            of the inputs; instead higher confidence should be merited when a number of highly independent inputs show a
            consistent pattern.
            """,

        Related = [
            new Guid("ecfa5b28-3900-45ba-89c7-f8d995dfe406"), // WYSIATI
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusion_of_validity",
        ],
    };

    private static readonly Bias WYSIATI = new()
    {
        Id = new Guid("ecfa5b28-3900-45ba-89c7-f8d995dfe406"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "WYSIATI (What You See Is All There Is)",
        Description =
            """
            # WYSIATI (What You See Is All There Is)
            It solves a difficult problem by replacing it with a simpler problem that you know about. One problem does not solve
            the other. The acronym WYSIATI stands for "What you see is all there is." It was coined by Nobel laureate Daniel
            Kahneman in his book "Thinking, Fast and Slow." WYSIATI refers to the fact that we make decisions based on the
            information we currently have. For example, when we meet an unknown person, we decide within seconds whether we
            like the person or not.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Thinking,_Fast_and_Slow",
        ],
    };

    private static readonly Bias MASKED_MAN_FALLACY = new()
    {
        Id = new Guid("5ddf8011-0ba2-4341-9e18-46178f8d4fbe"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Masked-Man Fallacy",
        Description =
            """
            # Masked-Man Fallacy
            In philosophical logic, the masked-man fallacy (also known as the intensional fallacy or epistemic fallacy) is
            committed when one makes an illicit use of Leibniz's law in an argument. Leibniz's law states that if A and B
            are the same object, then A and B are indiscernible (that is, they have all the same properties). By modus tollens,
            this means that if one object has a certain property, while another object does not have the same property, the two
            objects cannot be identical. The fallacy is "epistemic" because it posits an immediate identity between a subject's
            knowledge of an object with the object itself, failing to recognize that Leibniz's Law is not capable of accounting
            for intensional contexts.
            
            The name of the fallacy comes from the example:
            
            - Premise 1: I know who Claus is.
            - Premise 2: I do not know who the masked man is.
            - Conclusion: Therefore, Claus is not the masked man.
            
            The premises may be true and the conclusion false if Claus is the masked man and the speaker does not know that.
            Thus the argument is a fallacious one.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Masked-man_fallacy",
        ],
    };

    private static readonly Bias RECENCY_ILLUSION = new()
    {
        Id = new Guid("d0a79f6e-7786-4dd7-8a3f-62f167252171"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Recency Illusion",
        Description =
            """
            # Recency Illusion
            The recency illusion is the belief or impression, on the part of someone who has only recently become aware of a
            long-established phenomenon, that the phenomenon itself must be of recent origin. The term was coined by Arnold
            Zwicky, a linguist at Stanford University who is primarily interested in examples involving words, meanings,
            phrases, and grammatical constructions. However, use of the term is not restricted to linguistic phenomena:
            Zwicky has defined it simply as, "the belief that things you have noticed only recently are in fact recent".
            According to Zwicky, the illusion is caused by selective attention.
            """,

        Related = [
            new Guid("91fded4f-de89-405e-8627-dba49cf5deaa"), // SELECTIVE_PERCEPTION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Recency_illusion",
        ],
    };

    private static readonly Bias GAMBLERS_FALLACY = new()
    {
        Id = new Guid("61cd7e34-23be-43ef-ab97-8118cef7d23f"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Gambler's Fallacy",
        Description =
            """
            # Gambler's Fallacy
            he gambler's fallacy, also known as the Monte Carlo fallacy or the fallacy of the maturity of chances, is the belief
            that, if an event (whose occurrences are independent and identically distributed) has occurred less frequently than
            expected, it is more likely to happen again in the future (or vice versa). The fallacy is commonly associated with
            gambling, where it may be believed, for example, that the next dice roll is more than usually likely to be six
            because there have recently been fewer than the expected number of sixes.
            
            In a study aimed at discovering if the negative autocorrelation that exists with the gambler's fallacy existed in
            the decision made by U.S. asylum judges, results showed that after two successive asylum grants, a judge would be
            5.5% less likely to approve a third grant.
            
            In the decision making of loan officers, it can be argued that monetary incentives are a key factor in biased
            decision making, rendering it harder to examine the gambler's fallacy effect. However, research shows that loan
            officers who are not incentivised by monetary gain are 8% less likely to approve a loan if they approved one
            for the previous client.
            
            Several video games feature the use of loot boxes, a collection of in-game items awarded on opening with random
            contents set by rarity metrics, as a monetization scheme. Since around 2018, loot boxes have come under scrutiny
            from governments and advocates on the basis they are akin to gambling, particularly for games aimed at youth.
            Some games use a special "pity-timer" mechanism, that if the player has opened several loot boxes in a row
            without obtaining a high-rarity item, subsequent loot boxes will improve the odds of a higher-rate item drop.
            This is considered to feed into the gambler's fallacy since it reinforces the idea that a player will eventually
            obtain a high-rarity item (a win) after only receiving common items from a string of previous loot boxes.
            """,

        Related = [
            new Guid("44c6efd7-53f1-4d22-82fe-25e941390089"), // NEGLECT_OF_PROBABILITY
            new Guid("2c2ed1a8-aa4d-486d-a9b4-5a16ae9230c9"), // CLUSTERING_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Gambler%27s_fallacy",
        ],
    };

    private static readonly Bias HOT_HAND_FALLACY = new()
    {
        Id = new Guid("5fd14849-7041-42ee-976e-9a2b10522d29"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Hot Hand Fallacy",
        Description =
            """
            # Hot Hand Fallacy
            The "hot hand" (also known as the "hot hand phenomenon" or "hot hand fallacy") is a phenomenon, previously
            considered a cognitive social bias, that a person who experiences a successful outcome has a greater
            chance of success in further attempts. The concept is often applied to sports and skill-based tasks
            in general and originates from basketball, where a shooter is more likely to score if their previous
            attempts were successful; i.e., while having the "hot hand.” While previous success at a task can indeed
            change the psychological attitude and subsequent success rate of a player, researchers for many years
            did not find evidence for a "hot hand" in practice, dismissing it as fallacious. However, later research
            questioned whether the belief is indeed a fallacy. Some recent studies using modern statistical
            analysis have observed evidence for the "hot hand" in some sporting activities; however, other recent
            studies have not observed evidence of the "hot hand". Moreover, evidence suggests that only a small
            subset of players may show a "hot hand" and, among those who do, the magnitude (i.e., effect size) of the
            "hot hand" tends to be small.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Hot_hand",
        ],
    };

    private static readonly Bias ILLUSORY_CORRELATION = new()
    {
        Id = new Guid("829d3178-8ebc-417c-b587-2ead31525327"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Illusory Correlation",
        Description =
            """
            # Illusory Correlation
            In psychology, illusory correlation is the phenomenon of perceiving a relationship between variables
            (typically people, events, or behaviors) even when no such relationship exists. A false association
            may be formed because rare or novel occurrences are more salient and therefore tend to capture one's
            attention. This phenomenon is one way stereotypes form and endure. Hamilton & Rose (1980) found that
            stereotypes can lead people to expect certain groups and traits to fit together, and then to overestimate
            the frequency with which these correlations actually occur. These stereotypes can be learned and perpetuated
            without any actual contact occurring between the holder of the stereotype and the group it is about.
            
            Example: A woman has her purse stolen by a person of a specific demographic. Henceforth, she keeps her
            close purse each time she sees a similar person.
            
            Example: A man holds the belief that people in urban environments tend to be rude. Therefore, when he
            meets someone who is rude he assumes that the person lives in a city, rather than a rural area.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusory_correlation",
        ],
    };

    private static readonly Bias PAREIDOLIA = new()
    {
        Id = new Guid("274cc868-df03-4fae-9dca-ccb07a66aeaf"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Pareidolia",
        Description =
            """
            # Pareidolia
            Pareidolia is the tendency for perception to impose a meaningful interpretation on a nebulous stimulus,
            usually visual, so that one detects an object, pattern, or meaning where there is none. Pareidolia is
            a type of apophenia.
            
            Common examples include perceived images of animals, faces, or objects in cloud formations; seeing faces
            in inanimate objects; or lunar pareidolia like the Man in the Moon or the Moon rabbit. The concept of
            pareidolia may extend to include hidden messages in recorded music played in reverse or at higher- or
            lower-than-normal speeds, and hearing voices (mainly indistinct) or music in random noise, such as that
            produced by air conditioners or by fans.
            """,

        Related = [
            new Guid("6ab69dc8-6fcc-42c3-b190-90125a15b49f"), // APHOPHENIA
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Pareidolia",
        ],
    };

    private static readonly Bias APOPHENIA = new()
    {
        Id = new Guid("6ab69dc8-6fcc-42c3-b190-90125a15b49f"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Apophenia",
        Description =
            """
            # Apophenia
            Apophenia is the tendency to perceive meaningful connections between unrelated things. The term
            (German: Apophänie from the Greek verb ἀποφαίνειν (apophaínein)) was coined by psychiatrist Klaus
            Conrad in his 1958 publication on the beginning stages of schizophrenia. He defined it as "unmotivated
            seeing of connections [accompanied by] a specific feeling of abnormal meaningfulness". He described the
            early stages of delusional thought as self-referential over-interpretations of actual sensory
            perceptions, as opposed to hallucinations. Apophenia has also come to describe a human propensity to
            unreasonably seek definite patterns in random information, such as can occur in gambling.
            
            Pareidolia is a type of apophenia involving the perception of images or sounds in random stimuli.
            Gamblers may imagine that they see patterns in the numbers that appear in lotteries, card games, or
            roulette wheels, where no such patterns exist. A common example of this is the gambler's fallacy.
            """,

        Related = [
            new Guid("274cc868-df03-4fae-9dca-ccb07a66aeaf"), // PAREIDOLIA
            new Guid("61cd7e34-23be-43ef-ab97-8118cef7d23f"), // MONTE_CARLO_FALLACY
            new Guid("2c2ed1a8-aa4d-486d-a9b4-5a16ae9230c9"), // CLUSTERING_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Apophenia",
        ],
    };

    private static readonly Bias ANTHROPOMORPHISM = new()
    {
        Id = new Guid("70470097-52a8-4ea7-a85c-ed88ad1ed972"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Anthropomorphism",
        Description =
            """
            # Anthropomorphism
            Anthropomorphism is the attribution of human traits, emotions, or intentions to non-human entities.
            It is considered to be an innate tendency of human psychology. Personification is the related
            attribution of human form and characteristics to abstract concepts such as nations, emotions, and
            natural forces, such as seasons and weather. Both have ancient roots as storytelling and artistic
            devices, and most cultures have traditional fables with anthropomorphized animals as characters.
            People have also routinely attributed human emotions and behavioral traits to wild as well as
            domesticated animals.
            
            Anthropomorphism can be used to assist learning. Specifically, anthropomorphized words and
            describing scientific concepts with intentionality can improve later recall of these concepts.
            
            In people with depression, social anxiety, or other mental illnesses, emotional support animals
            are a useful component of treatment partially because anthropomorphism of these animals can satisfy
            the patients' need for social connection.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Anthropomorphism",
        ],
    };

    private static readonly Bias GROUP_ATTRIBUTION_ERROR = new()
    {
        Id = new Guid("577e79e5-0a53-4c4c-a2ea-d039870bfbb9"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Group Attribution Error",
        Description =
            """
            # Group Attribution Error
            The group attribution error refers to people's tendency to believe either
            
            (a) the characteristics of an individual group member are reflective of
                the group as a whole, or
                
            (b) a group's decision outcome must reflect the preferences of individual
                group members, even when external information is available suggesting otherwise.
                
            The group attribution error shares an attribution bias analogous to the fundamental
            attribution error. Rather than focusing on individual's behavior, it relies on group
            outcomes and attitudes as its main basis for conclusions.
            """,

        Related = [
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("efceb4b1-e19f-4997-9f96-1657bb269b2d"), // ATTRIBUTION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Group_attribution_error",
        ],
    };

    private static readonly Bias ATTRIBUTION_BIAS = new()
    {
        Id = new Guid("efceb4b1-e19f-4997-9f96-1657bb269b2d"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Attribution Bias",
        Description =
            """
            # Attribution Bias
            In psychology, an attribution bias or attributional errors is a cognitive bias that refers to the
            systematic errors made when people evaluate or try to find reasons for their own and others' behaviors.
            It refers to the systematic patterns of deviation from norm or rationality in judgment, often leading to
            perceptual distortions, inaccurate assessments, or illogical interpretations of events and behaviors.
            
            Attributions are the judgments and assumptions people make about why others behave a certain way. However,
            these judgments may not always reflect the true situation. Instead of being completely objective, people
            often make errors in perception that lead to skewed interpretations of social situations. Attribution
            biases are present in everyday life. For example, when a driver cuts someone off, the person who has been
            cut off is often more likely to attribute blame to the reckless driver's inherent personality traits (e.g.,
            "That driver is rude and incompetent") rather than situational circumstances (e.g., "That driver may have
            been late to work and was not paying attention").
            
            Additionally, there are many different types of attribution biases, such as the ultimate attribution error,
            fundamental attribution error, actor-observer bias, and hostile attribution bias. Each of these biases
            describes a specific tendency that people exhibit when reasoning about the cause of different behaviors.
            """,

        Related = [
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("5da6dcf4-ed01-4e14-99b0-7a624b16cf17"), // ACTOR_OBSERVER_BIAS
            new Guid("e85d8b16-5a36-4b63-af07-72c5188f089f"), // HOSTILE_ATTRIBUTION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Attribution_bias",
        ],
    };

    private static readonly Bias HOSTILE_ATTRIBUTION_BIAS = new()
    {
        Id = new Guid("e85d8b16-5a36-4b63-af07-72c5188f089f"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Hostile Attribution Bias",
        Description =
            """
            # Hostile Attribution Bias
            Hostile attribution bias, or hostile attribution of intent, is the tendency to interpret others' behaviors as
            having hostile intent, even when the behavior is ambiguous or benign. For example, a person with high levels
            of hostile attribution bias might see two people laughing and immediately interpret this behavior as two people
            laughing about them, even though the behavior was ambiguous and may have been benign.
            """,

        Related = [
            new Guid("efceb4b1-e19f-4997-9f96-1657bb269b2d"), // ATTRIBUTION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Hostile_attribution_bias",
        ],
    };

    private static readonly Bias ULTIMATE_ATTRIBUTION_ERROR = new()
    {
        Id = new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Ultimate Attribution Error",
        Description =
            """
            # Ultimate Attribution Error
            The ultimate attribution error is a type of attribution error which describes how attributions of outgroup
            behavior are more negative than ingroup behavior. As a cognitive bias, the error results in negative outgroup
            behavior being more likely to be attributed to factors internal and specific to the actor, such as personality,
            and the attribution of negative ingroup behavior to external factors such as luck or circumstance. The bias
            reinforces negative stereotypes and prejudice about the outgroup and favouritism of the ingroup through positive
            stereotypes. The theory also extends to the bias that positive acts performed by ingroup members are more likely
            a result of their personality.
            
            Four categories have been identified that describe the negative attribution of positive outgroup behaviour.
            First, that the outgroup member is an exception to a general rule; second, that the member was lucky or had specific
            advantages; third, that the member was highly motivated; and lastly that the behaviour as attributable to situational causes.
            """,

        Related = [
            new Guid("efceb4b1-e19f-4997-9f96-1657bb269b2d"), // ATTRIBUTION_BIAS
            new Guid("efb6606f-4629-4e5e-973f-94d5ac496638"), // PREJUDICE
            new Guid("46c2a0b2-6b1b-4e02-86ea-3cff2bf292d0"), // STEREOTYPE
            new Guid("b1cc861b-f445-450b-9bdf-e9d222abdb4e"), // IN_GROUP_FAVORITISM
            new Guid("b57a862b-b490-4d61-96b8-29d548c2eee4"), // POSITIVITY_EFFECT
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("5da6dcf4-ed01-4e14-99b0-7a624b16cf17"), // ACTOR_OBSERVER_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Ultimate_attribution_error",
        ],
    };

    private static readonly Bias IN_GROUP_FAVORITISM = new()
    {
        Id = new Guid("b1cc861b-f445-450b-9bdf-e9d222abdb4e"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "In-Group Favoritism",
        Description =
            """
            # In-Group Favoritism
            In-group favoritism, sometimes known as in-group–out-group bias, in-group bias, intergroup bias, or
            in-group preference, is a pattern of favoring members of one's in-group over out-group members.
            This can be expressed in evaluation of others, in allocation of resources, and in many other ways.
            
            This effect has been researched by many psychologists and linked to many theories related to group 
            conflict and prejudice. The phenomenon is primarily viewed from a social psychology standpoint.
            Studies have shown that in-group favoritism arises as a result of the formation of cultural groups.
            These cultural groups can be divided based on seemingly trivial observable traits, but with time,
            populations grow to associate certain traits with certain behavior, increasing covariation. This
            then incentivizes in-group bias.
            
            Two prominent theoretical approaches to the phenomenon of in-group favoritism are realistic conflict
            theory and social identity theory. Realistic conflict theory proposes that intergroup competition,
            and sometimes intergroup conflict, arises when two groups have opposing claims to scarce resources.
            In contrast, social identity theory posits a psychological drive for positively distinct social
            identities as the general root cause of in-group favoring behavior.
            """,

        Related = [
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
            new Guid("6f5f4cbf-e6f3-439b-ad78-81b2dd266315"), // OUT_GROUP_HOMOGENEITY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/In-group_favoritism",
        ],
    };

    private static readonly Bias STEREOTYPING = new()
    {
        Id = new Guid("46c2a0b2-6b1b-4e02-86ea-3cff2bf292d0"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Stereotyping",
        Description =
            """
            # Stereotyping
            In social psychology, a stereotype is a generalized belief about a particular category of people.
            It is an expectation that people might have about every person of a particular group. The type of
            expectation can vary; it can be, for example, an expectation about the group's personality, preferences,
            appearance or ability. Stereotypes are often overgeneralized, inaccurate, and resistant to new information.
            A stereotype does not necessarily need to be a negative assumption. They may be positive, neutral, or negative.
            """,

        Related = [
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Stereotype",
        ],
    };

    private static readonly Bias ESSENTIALISM = new()
    {
        Id = new Guid("179535d0-5da5-4c0f-b9b3-fb6644496254"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Essentialism",
        Description =
            """
            # Essentialism
            The view that all objects have an essential substance that make the thing what it is, and without which
            it would be not that kind of thing.
            
            Essentialism has emerged as an important concept in psychology, particularly developmental psychology.
            In 1991, Kathryn Kremer and Susan Gelman studied the extent to which children from four–seven years old
            demonstrate essentialism. Children believed that underlying essences predicted observable behaviours.
            Children were able to describe living objects' behaviour as self-perpetuated and non-living objects'
            behavior as a result of an adult influencing the object. Understanding the underlying causal mechanism
            for behaviour suggests essentialist thinking. Younger children were unable to identify causal
            mechanisms of behaviour whereas older children were able to. This suggests that essentialism is rooted
            in cognitive development. It can be argued that there is a shift in the way that children represent
            entities, from not understanding the causal mechanism of the underlying essence to showing sufficient
            understanding.
            
            *Controversial. This is a philosophical viewpoint not a cognitive bias.*
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Essentialism",
        ],
    };

    private static readonly Bias FUNCTIONAL_FIXEDNESS = new()
    {
        Id = new Guid("4346bdf9-4448-413f-92cd-4d146bf4789d"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Functional Fixedness",
        Description =
            """
            # Functional Fixedness
            Functional fixedness is a cognitive bias that limits a person to use an object only in the way it is traditionally
            used. The concept of functional fixedness originated in Gestalt psychology, a movement in psychology that emphasizes
            holistic processing. Karl Duncker defined functional fixedness as being a mental block against using an object in a
            new way that is required to solve a problem. This "block" limits the ability of an individual to use components given
            to them to complete a task, as they cannot move past the original purpose of those components. For example, if someone
            needs a paperweight, but they only have a hammer, they may not see how the hammer can be used as a paperweight.
            Functional fixedness is this inability to see a hammer's use as anything other than for pounding nails; the person
            couldn't think to use the hammer in a way other than in its conventional function.
            
            When tested, 5-year-old children show no signs of functional fixedness. It has been argued that this is because at
            age 5, any goal to be achieved with an object is equivalent to any other goal. However, by age 7, children have
            acquired the tendency to treat the originally intended purpose of an object as special.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Functional_fixedness",
        ],
    };

    private static readonly Bias MORAL_CREDENTIAL_EFFECT = new()
    {
        Id = new Guid("e36f82b7-43dd-4073-99d9-c33073007185"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Moral Credential Effect",
        Description =
            """
            # Moral Credential Effect
            Self-licensing (also moral self-licensing, moral licensing, or licensing effect) is a term used in social
            psychology and marketing to describe the subconscious phenomenon whereby increased confidence and security
            in one's self-image or self-concept tends to make that individual worry less about the consequences of
            subsequent immoral behavior and, therefore, more likely to make immoral choices and act immorally.
            In simple terms, self-licensing occurs when people allow themselves to indulge after doing something positive
            first; for example, drinking a diet soda with a greasy hamburger and fries can lead one to subconsciously
            discount the negative attributes of the meal's high caloric and cholesterol content.
            
            A large subset of this effect, the moral credential effect, is a bias that occurs when a person's track
            record as a good egalitarian establishes in them an unconscious ethical certification, endorsement, or
            license that increases the likelihood of less egalitarian decisions later. This effect occurs even when
            the audience or moral peer group is unaware of the affected person's previously established moral credential.
            For example, individuals who had the opportunity to recruit a woman or Black person in one setting were more
            likely to say later, in a different setting, that a job would be better suited for a man or a white person.
            Similar effects also appear to occur when a person observes another person from a group they identify with
            making an egalitarian decision.
            
            Self-licensing can have negative societal consequences since it has a permissive effect on behaviors such
            as racial prejudice and discrimination, selfishness, poor dietary and health habits, and excessive
            energy consumption.
            
            But recent scholarship has failed to replicate seminal studies of the licensing effect, and meta-analysis
            found it to be exaggerated by publication bias. Furthermore, where licensing typically assumes that a
            good deed is the cause that makes subsequent transgressions more likely, an alternative (or additional)
            account is that people are faced with a temptation to do something morally dubious, and use a prior good
            deed as an excuse or reason why it is allowed for them to indulge.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Self-licensing",
        ],
    };

    private static readonly Bias JUST_WORLD_FALLACY = new()
    {
        Id = new Guid("50c5f877-e656-494d-bc15-57c45a190cf9"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Just-World Fallacy",
        Description =
            """
            # Just-World Fallacy
            The just-world fallacy, or just-world hypothesis, is the cognitive bias that assumes that "people get what
            they deserve" – that actions will necessarily have morally fair and fitting consequences for the actor.
            For example, the assumptions that noble actions will eventually be rewarded and evil actions will eventually
            be punished fall under this fallacy. In other words, the just-world fallacy is the tendency to attribute
            consequences to—or expect consequences as the result of— either a universal force that restores moral balance
            or a universal connection between the nature of actions and their results. This belief generally implies the
            existence of cosmic justice, destiny, divine providence, desert, stability, order, or the anglophone colloquial
            use of "karma". It is often associated with a variety of fundamental fallacies, especially in regard to
            rationalizing suffering on the grounds that the sufferers "deserve" it. This is called victim blaming.
            
            This fallacy popularly appears in the English language in various figures of speech that imply guaranteed
            punishment for wrongdoing, such as: "you got what was coming to you", "what goes around comes around",
            "chickens come home to roost", "everything happens for a reason", and "you reap what you sow". This
            hypothesis has been widely studied by social psychologists since Melvin J. Lerner conducted seminal work
            on the belief in a just world in the early 1960s. Research has continued since then, examining the
            predictive capacity of the fallacy in various situations and across cultures, and clarifying and expanding
            the theoretical understandings of just-world beliefs.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Just-world_fallacy",
        ],
    };

    private static readonly Bias ARGUMENT_FROM_FALLACY = new()
    {
        Id = new Guid("704695f1-9753-478b-9e9f-878e3a01e041"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Argument from Fallacy",
        Description =
            """
            # Argument from Fallacy
            Argument from fallacy is the formal fallacy of analyzing an argument and inferring that, since it contains
            a fallacy, its conclusion must be false. It is also called argument to logic (argumentum ad logicam), the
            fallacy fallacy, the fallacist's fallacy, and the bad reasons fallacy.
            
            Example 1:
            - Alice: All cats are animals. Ginger is an animal. Therefore, Ginger is a cat.
            - Bob: You have just fallaciously affirmed the consequent. You are incorrect. Therefore, Ginger is not a cat.
            
            Example 2:
            - Alice: I speak English. Therefore, I am English.
            - Bob: Americans and Canadians, among others, speak English too. By assuming that speaking English and being
              English always go together, you have just committed the package-deal fallacy. You are incorrect. Therefore,
              you are not English.
              
            Both of Bob's rebuttals are arguments from fallacy. Ginger may or may not be a cat, and Alice may or may not
            be English. The fact that Alice's argument was fallacious is not, in itself, proof that her conclusion is false.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Argument_from_fallacy",
        ],
    };

    private static readonly Bias AUTHORITY_BIAS = new()
    {
        Id = new Guid("7256f3f1-6650-4c45-bb85-36d81c9edd1a"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Authority Bias",
        Description =
            """
            # Authority Bias
            Authority bias is the tendency to attribute greater accuracy to the opinion of an authority figure (unrelated to
            its content) and be more influenced by that opinion. An individual is more influenced by the opinion of this
            authority figure, believing their views to be more credible, and hence place greater emphasis on the authority
            figure's viewpoint and are more likely to obey them. This concept is considered one of the social cognitive
            biases or collective cognitive biases.
            
            Cultural differences in the strength of authority bias have been identified, in which the differences in edits
            made to Wikipedia articles by administrators and regular users were compared for accuracy. In Western Europe,
            the bias has a negligible effect. In Eastern Europe, the bias is larger and the administrator's edits are
            perceived as more likely to be true (despite the edits being inaccurate), indicating a cultural difference
            in the extent to which authority bias is experienced.
            
            Business: The authority bias is demonstrated in the case of the highest-paid persons' opinion (HIPPO) impact,
            which describes how employees and other stakeholders in the solution environment tend to go with the opinions
            and impressions of the highly paid people in an organization.
            """,

        Related = [
            new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"), // CONFIRMATION_BIAS
            new Guid("b1d46b0f-fa51-4e82-b0aa-71ba2c6ad1f1"), // BANDWAGON_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Authority_bias",
        ],
    };

    private static readonly Bias AUTOMATION_BIAS = new()
    {
        Id = new Guid("c9e10d5b-6a32-4766-b937-aa03e276f018"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Automation Bias",
        Description =
            """
            # Automation Bias
            Automation bias is the propensity for humans to favor suggestions from automated decision-making systems and to
            ignore contradictory information made without automation, even if it is correct. Automation bias stems from
            the social psychology literature that found a bias in human-human interaction that showed that people assign more
            positive evaluations to decisions made by humans than to a neutral object. The same type of positivity bias
            has been found for human-automation interaction, where the automated decisions are rated more positively
            than neutral. This has become a growing problem for decision making as intensive care units, nuclear power
            plants, and aircraft cockpits have increasingly integrated computerized system monitors and decision aids
            to mostly factor out possible human error. Errors of automation bias tend to occur when decision-making
            is dependent on computers or other automated aids and the human is in an observatory role but able to
            make decisions. Examples of automation bias range from urgent matters like flying a plane on automatic
            pilot to such mundane matters as the use of spell-checking programs.
            
            An operator's trust in the system can also lead to different interactions with the system, including system
            use, misuse, disuse, and abuse. Automation use and disuse can also influence stages of information processing:
            information acquisition, information analysis, decision making and action selection, and action implementation.
            
            For example, information acquisition, the first step in information processing, is the process by which a user
            registers input via the senses. An automated engine gauge might assist the user with information acquisition
            through simple interface features—such as highlighting changes in the engine's performance—thereby directing
            the user's selective attention. When faced with issues originating from an aircraft, pilots may tend to
            overtrust an aircraft's engine gauges, losing sight of other possible malfunctions not related to the engine.
            This attitude is a form of automation complacency and misuse. If, however, the pilot devotes time to interpret
            the engine gauge, and manipulate the aircraft accordingly, only to discover that the flight turbulence has not
            changed, the pilot may be inclined to ignore future error recommendations conveyed by an engine gauge—a form
            of automation complacency leading to disuse.
            """,

        Related = [
            new Guid("b821d449-64e5-4c0a-9d5a-3fda609a9b86"), // OVERCONFIDENCE_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Automation_bias",
        ],
    };

    private static readonly Bias BANDWAGON_EFFECT = new()
    {
        Id = new Guid("b1d46b0f-fa51-4e82-b0aa-71ba2c6ad1f1"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Bandwagon Effect",
        Description =
            """
            # Bandwagon Effect
            The bandwagon effect is a psychological phenomenon where people adopt certain behaviors, styles, or attitudes
            simply because others are doing so. More specifically, it is a cognitive bias by which public opinion or
            behaviours can alter due to particular actions and beliefs rallying amongst the public. It is a psychological
            phenomenon whereby the rate of uptake of beliefs, ideas, fads and trends increases with respect to the proportion
            of others who have already done so. As more people come to believe in something, others also "hop on the bandwagon"
            regardless of the underlying evidence.
            
            Following others' actions or beliefs can occur because of conformism or deriving information from others. Much of
            the influence of the bandwagon effect comes from the desire to 'fit in' with peers; by making similar selections
            as other people, this is seen as a way to gain access to a particular social group. An example of this is fashion
            trends wherein the increasing popularity of a certain garment or style encourages more acceptance. When individuals
            make rational choices based on the information they receive from others, economists have proposed that information
            cascades can quickly form in which people ignore their personal information signals and follow the behaviour of
            others. Cascades explain why behaviour is fragile as people understand that their behaviour is based on a very
            limited amount of information. As a result, fads form easily but are also easily dislodged. The phenomenon is
            observed in various fields, such as economics, political science, medicine, and psychology. In social psychology,
            people's tendency to align their beliefs and behaviors with a group is known as 'herd mentality' or 'groupthink'.
            The reverse bandwagon effect (also known as the snob effect in certain contexts) is a cognitive bias that causes
            people to avoid doing something, because they believe that other people are doing it.
            """,

        Related = [
            new Guid("7256f3f1-6650-4c45-bb85-36d81c9edd1a"), // AUTHORITY_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Bandwagon_effect",
        ],
    };

    private static readonly Bias PLACEBO_EFFECT = new()
    {
        Id = new Guid("8d76fae9-cd8e-46b5-9cbc-c8fffa6613a8"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Placebo Effect",
        Description =
            """
            # Placebo Effect
            The psychological phenomenon in which the recipient perceives an improvement in condition due to personal
            expectations rather than treatment itself.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Placebo",
        ],
    };

    private static readonly Bias OUT_GROUP_HOMOGENEITY = new()
    {
        Id = new Guid("6f5f4cbf-e6f3-439b-ad78-81b2dd266315"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Out-Group Homogeneity",
        Description =
            """
            # Out-Group Homogeneity
            The out-group homogeneity effect is the perception of out-group members as more similar to one another than are in-group members,
            e.g. "they are alike; we are diverse". Perceivers tend to have impressions about the diversity or variability of group members around
            those central tendencies or typical attributes of those group members. Thus, outgroup stereotypicality judgments are overestimated,
            supporting the view that out-group stereotypes are overgeneralizations. The term "outgroup homogeneity effect", "outgroup homogeneity
            bias" or "relative outgroup homogeneity" have been explicitly contrasted with "outgroup homogeneity" in general, the latter referring
            to perceived outgroup variability unrelated to perceptions of the ingroup.
            
            The outgroup homogeneity effect is sometimes referred to as "outgroup homogeneity bias". Such nomenclature hints at a broader
            meta-theoretical debate that is present in the field of social psychology. This debate centres on the validity of heightened perceptions
            of ingroup and outgroup homogeneity, where some researchers view the homogeneity effect as an example of cognitive bias and error, while
            other researchers view the effect as an example of normal and often adaptive social perception. The out-group homogeneity effect has
            been found using a wide variety of different social groups, from political and racial groups to age and gender groups.
            """,

        Related = [
            new Guid("b1cc861b-f445-450b-9bdf-e9d222abdb4e"), // IN_GROUP_FAVORITISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Out-group_homogeneity",
        ],
    };

    private static readonly Bias CROSS_RACE_EFFECT = new()
    {
        Id = new Guid("d36f046d-fe5c-4f4a-8d7f-14427b834581"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Cross-Race Effect",
        Description =
            """
            # Cross-Race Effect
            The cross-race effect (sometimes called cross-race bias, other-race bias, own-race bias or other-race effect) is the tendency to more easily
            recognize faces that belong to one's own racial group, or racial groups that one has been in contact with. In social psychology, the
            cross-race effect is described as the "ingroup advantage," whereas in other fields, the effect can be seen as a specific form of the
            "ingroup advantage" since it is only applied in interracial or inter-ethnic situations. The cross-race effect is thought to contribute
            to difficulties in cross-race identification, as well as implicit racial bias.
            
            A number of theories as to why the cross-race effect exists have been conceived, including social cognition and perceptual expertise.
            However, no model has been able to fully account for the full body of evidence.
            
            Cross-race identification bias
            This effect refers to the decreased ability of people of one race to recognize faces and facial expressions of people of another race. This
            differs from the cross-race bias because this effect is found mostly during eyewitness identification as well as identification of a suspect
            in a line-up. In these situations, many people feel as if races other than their own look alike, and they have difficulty distinguishing
            between members of different ethnic groups. Cross-race identification bias is also known as the misinformation effect since people are
            considered to be misinformed about other races and have difficulty identifying them. A study was made which examined 271 real court
            cases. In photographic line-ups, 231 witnesses participated in cross-race versus same-race identification. In cross-race lineups,
            only 45% were correctly identified versus 60% for same-race identifications. In a study dealing with eyewitness testimony,
            investigators examined forty participants in a racially diverse area of the US. Participants watched a video of a property crime
            being committed, then in the next 24 hours came to pick the suspect out of a photo line-up. Most of the participants in the study
            either misidentified the suspect or stated the suspect was not in the line-up at all. Correct identification of the suspect
            occurred more often when the eyewitness and the suspect were of the same race. In another study, 86 convenience store
            clerks were asked to identify three customers: one white, one black, and one Mexican, all of whom had purchased in the store
            earlier that day. The clerks tended to identify customers belonging to their own race accurately, but were more likely to make
            errors when attempting to identify other races members. Meanwhile, another study found that "alcohol intoxication reduces
            the own-race bias in face recognition," albeit by impairing accurate perception and leaving in place or increasing random error
            rather than by improving facial recognition of members of other groups.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Cross-race_effect",
        ],
    };

    private static readonly Bias CHEERLEADER_EFFECT = new()
    {
        Id = new Guid("79f705e9-c461-4ad7-8b5e-83358aa345f7"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Cheerleader Effect",
        Description =
            """
            # Cheerleader Effect
            The cognitive bias which causes people to think individuals are more attractive when they are in a group. This effect occurs
            with male-only, female-only and mixed gender groups; and both small and large groups. The effect occurs to the same extent
            with groups of four and 16 people. Participants in studies looked more at the attractive people than the unattractive people
            in the group. The effect does not occur because group photos give the impression that individuals have more social or emotional
            intelligence. This was shown to be the case by a study which used individual photos grouped together in a single image, rather
            than photos taken of people in a group. The study generated the same effect.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Cheerleader_effect",
        ],
    };

    private static readonly Bias POSITIVITY_EFFECT = new()
    {
        Id = new Guid("b57a862b-b490-4d61-96b8-29d548c2eee4"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Positivity Effect",
        Description =
            """
            # Positivity Effect
            The positivity effect is the ability to constructively analyze a situation where the desired results are not achieved, but still
            obtain positive feedback that assists one's future progression. Empirical research findings suggest that the positivity effect
            can be influenced by internal positive speech, where engaging in constructive self-dialogue can significantly improve one’s
            ability to perceive and react to challenging situations more optimistically.
            
            The findings of a study show that the optimism bias in future-oriented thinking fulfils a self-improvement purpose while also
            suggesting this bias probably reflects a common underpinning motivational process across various future-thinking domains,
            either episodic or semantic.
            
            ## In attribution
            The positivity effect as an attribution phenomenon relates to the habits and characteristics of people when evaluating
            the causes of their behaviors. To positively attribute is to be open to attributing a person’s inherent disposition as
            the cause of their positive behaviors, and the situations surrounding them as the potential cause of their negative
            behaviors.
            
            ## In perception
            Two studies by Emilio Ferrara have shown that, on online social networks like Twitter and Instagram, users prefer to share
            positive news, and are emotionally affected by positive news more than twice as much as they are by negative news.
            """,

        Related = [
            new Guid("ef521fbb-c20b-47c9-87f8-a571a06a03eb"), // NEGATIVITY_BIAS
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("5da6dcf4-ed01-4e14-99b0-7a624b16cf17"), // ACTOR_OBSERVER_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Positivity_effect",
        ],
    };

    private static readonly Bias NOT_INVENTED_HERE = new()
    {
        Id = new Guid("72fd9f08-b3c2-40b7-8d56-a2e84d776041"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Not Invented Here",
        Description =
            """
            # Not Invented Here
            Not invented here (NIH) is the tendency to avoid using or buying products, research, standards, or knowledge from external origins.
            It is usually adopted by social, corporate, or institutional cultures. Research illustrates a strong bias against ideas from the
            outside.
            
            The reasons for not wanting to use the work of others are varied, but can include a desire to support a local economy instead of
            paying royalties to a foreign license-holder, fear of patent infringement, lack of understanding of the foreign work, an
            unwillingness to acknowledge or value the work of others, jealousy, belief perseverance, or forming part of a wider turf war.
            As a social phenomenon, this tendency can manifest itself as an unwillingness to adopt an idea or product because it originates
            from another culture, a form of tribalism and/or an inadequate effort in choosing the right approach for the business.
            
            The term is typically used in a pejorative sense. The opposite predisposition is sometimes called "proudly found elsewhere" (PFE)
            or "invented elsewhere".
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Not_invented_here",
        ],
    };

    private static readonly Bias REACTIVE_DEVALUATION = new()
    {
        Id = new Guid("46493445-4a8b-4488-901c-85da417c80a3"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Reactive Devaluation",
        Description =
            """
            # Reactive Devaluation
            Reactive devaluation is a cognitive bias that occurs when a proposal is devalued if it appears to originate from an antagonist. The
            bias was proposed by Lee Ross and Constance Stillinger (1988). Reactive devaluation could be caused by loss aversion or attitude
            polarization, or naïve realism.
            
            In an initial experiment, Stillinger and co-authors asked pedestrians in the US whether they would support a drastic bilateral
            nuclear arms reduction program. If they were told the proposal came from President Ronald Reagan, 90 percent said it would be
            favorable or even-handed to the United States; if they were told the proposal came from a group of unspecified policy analysts,
            80 percent thought it was favorable or even; but, if respondents were told it came from Mikhail Gorbachev only 44 percent thought
            it was favorable or neutral to the United States.
            
            In another experiment, a contemporaneous controversy at Stanford University led to the university divesting of South African
            assets because of the apartheid regime. Students at Stanford were asked to evaluate the University's divestment plan before
            it was announced publicly and after such. Proposals including the actual eventual proposal were valued more highly when they
            were hypothetical.
            
            In another study, experimenters showed Israeli participants a peace proposal which had been actually proposed by Israel. If
            participants were told the proposal came from a Palestinian source, they rated it lower than if they were told (correctly)
            the identical proposal came from the Israeli government. If participants identified as "hawkish" were told it came from a
            "dovish" Israeli government, they believed it was relatively bad for their people and good for the other side, but not if
            participants identified as "doves".
            """,

        Related = [
            new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"), // LOSS_AVERSION
            new Guid("f0ad095e-8e9c-4bfb-855e-11fb5dd58cea"), // NAÏVE_REALISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Reactive_devaluation",
        ],
    };

    private static readonly Bias WELL_TRAVELLED_ROAD_EFFECT = new()
    {
        Id = new Guid("9ee2b5b5-463c-4bca-af85-087683f89ab3"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Well-Travelled Road Effect",
        Description =
            """
            # Well-Travelled Road Effect
            The well travelled road effect is a cognitive bias in which travellers will estimate the time taken to traverse routes differently
            depending on their familiarity with the route. Frequently travelled routes are assessed as taking a shorter time than unfamiliar
            routes. This effect creates errors when estimating the most efficient route to an unfamiliar destination, when one candidate
            route includes a familiar route, whilst the other candidate route includes no familiar routes. The effect is most salient when
            subjects are driving, but is still detectable for pedestrians and users of public transport. The effect has been observed for
            centuries but was first studied scientifically in the 1980s and 1990s following from earlier "heuristics and biases" work
            undertaken by Daniel Kahneman and Amos Tversky.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Well_travelled_road_effect",
        ],
    };

    private static readonly Bias MENTAL_ACCOUNTING = new()
    {
        Id = new Guid("9444923f-90c9-4269-a4dc-291513fa6d12"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Mental Accounting",
        Description =
            """
            # Mental Accounting
            Mental accounting (or psychological accounting) is a model of consumer behaviour developed by Richard Thaler that attempts to describe the
            process whereby people code, categorize and evaluate economic outcomes. Mental accounting incorporates the economic concepts of prospect
            theory and transactional utility theory to evaluate how people create distinctions between their financial resources in the form of mental
            accounts, which in turn impacts the buyer decision process and reaction to economic outcomes. People are presumed to make mental accounts
            as a self control strategy to manage and keep track of their spending and resources. People budget money into mental accounts for savings
            (e.g., saving for a home) or expense categories (e.g., gas money, clothing, utilities). People also are assumed to make mental accounts to
            facilitate savings for larger purposes (e.g., a home or college tuition). Mental accounting can result in people demonstrating greater
            loss aversion for certain mental accounts, resulting in cognitive bias that incentivizes systematic departures from consumer rationality.
            Through an increased understanding of mental accounting differences in decision making based on different resources, and different
            reactions based on similar outcomes can be greater understood.
            
            As Thaler puts it, “All organizations, from General Motors down to single person households, have explicit and/or implicit accounting
            systems. The accounting system often influences decisions in unexpected ways”.
            
            A more proximal psychological mechanism through which mental accounting influences spending is through its influence on the pain of
            paying that is associated with spending money from a mental account. Pain of paying is a negative affective response associated
            with a financial loss. Prototypical examples are the unpleasant feeling that one experiences when watching the fare increase on a
            taximeter or at the gas pump. When considering an expense, consumers appear to compare the cost of the expense to the size of an
            account that it would deplete (e.g., numerator vs. denominator). A $30 t-shirt, for example, would be a subjectively larger
            expense when drawn from $50 in one's wallet than $500 in one's checking account. The larger the fraction, the more pain of
            paying the purchase appears to generate and the less likely consumers are to then exchange money for the good. Other evidence
            of the relation between pain of paying and spending include the lower debt held by consumers who report experiencing a higher
            pain of paying for the same goods and services than consumers who report experiencing less pain of paying.
            """,

        Related = [
            new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"), // LOSS_AVERSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Mental_accounting",
        ],
    };

    private static readonly Bias APPEAL_TO_POSSIBILITY = new()
    {
        Id = new Guid("73ca0caa-25e5-4edb-91d4-f375a773f82c"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Appeal to Probability",
        Description =
            """
            # Appeal to Probability
            An appeal to probability (or appeal to possibility, also known as possibiliter ergo probabiliter, "possibly, therefore
            probably") is the logical fallacy of taking something for granted because it is possibly the case. The fact that an
            event is possible does not imply that the event is probable, nor that the event was realized.
            
            A fallacious appeal to possibility:
            
            - If it can happen (premise).
            - It will happen. (invalid conclusion)
            
            - Something can go wrong (premise).
            - Therefore, something will go wrong (invalid conclusion).
            
            - If I do not bring my umbrella (premise)
            - It will rain. (invalid conclusion).
            
            Murphy's law is a (typically deliberate, tongue-in-cheek) invocation of the fallacy.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Appeal_to_probability",
        ],
    };

    private static readonly Bias NORMALCY_BIAS = new()
    {
        Id = new Guid("1dfd3e9e-e44e-44cf-b8a0-95dea7a0e780"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Normalcy Bias",
        Description =
            """
            # Normalcy Bias
            Normalcy bias, or normality bias, is a cognitive bias which leads people to disbelieve or minimize threat warnings. Consequently,
            individuals underestimate the likelihood of a disaster, when it might affect them, and its potential adverse effects. The normalcy
            bias causes many people to prepare inadequately for natural disasters, market crashes, and calamities caused by human error. About
            80% of people reportedly display normalcy bias during a disaster.
            
            The normalcy bias can manifest in response to warnings about disasters and actual catastrophes. Such events can range in scale
            from incidents such as traffic collisions to global catastrophic risk. The event may involve social constructionism phenomena
            such as loss of money in market crashes, or direct threats to continuity of life: as in natural disasters like a tsunami or
            violence in war.
            
            Normalcy bias has also been called analysis paralysis, the ostrich effect, and by first responders, the negative panic. The
            opposite of normalcy bias is overreaction, or worst-case scenario bias, in which small deviations from normality are dealt
            with as signals of an impending catastrophe.
            
            ## Prevention
            The negative effects of normalcy bias can be combated through the four stages of disaster response:
            
            - preparation, including publicly acknowledging the possibility of disaster and forming contingency plans.
            
            - warning, including issuing clear, unambiguous, and frequent warnings and helping the public to understand and believe them.
            
            - impact, the stage at which the contingency plans take effect and emergency services, rescue teams, and disaster relief
              teams work in tandem.
              
            - aftermath, reestablishing equilibrium after the fact, by providing both supplies and aid to those in need.
            """,

        Related = [
            new Guid("75e51ef5-f992-41c2-8778-0002c617db9a"), // OSTRICH_EFFECT
            new Guid("91fded4f-de89-405e-8627-dba49cf5deaa"), // SELECTIVE_PERCEPTION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Normalcy_bias",
        ],
    };

    private static readonly Bias ZERO_SUM_BIAS = new()
    {
        Id = new Guid("35c21723-8dd7-4fea-9404-b26660fa6db1"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Zero-Sum Bias",
        Description =
            """
            # Zero-Sum Thinking & Zero-Sum Bias
            Zero-sum thinking perceives situations as zero-sum games, where one person's gain would be another's loss. The term is
            derived from game theory. However, unlike the game theory concept, zero-sum thinking refers to a psychological
            construct — a person's subjective interpretation of a situation. Zero-sum thinking is captured by the saying
            "your gain is my loss" (or conversely, "your loss is my gain").
            
            Rozycka-Tran et al. (2015) defined zero-sum thinking as:
            "A general belief system about the antagonistic nature of social relations, shared by people in a society or culture
            and based on the implicit assumption that a finite amount of goods exists in the world, in which one person's winning
            makes others the losers, and vice versa ... a relatively permanent and general conviction that social relations are
            like a zero-sum game. People who share this conviction believe that success, especially economic success, is possible
            only at the expense of other people's failures."
            
            Zero-sum bias is a cognitive bias towards zero-sum thinking; it is people's tendency to intuitively judge that a
            situation is zero-sum, even when this is not the case. This bias promotes zero-sum fallacies, false beliefs that
            situations are zero-sum. Such fallacies can cause other false judgements and poor decisions. In economics,
            "zero-sum fallacy" generally refers to the fixed-pie fallacy.
            
            ## Examples
            There are many examples of zero-sum thinking, some of them fallacious.
            
            - When jurors assume that any evidence compatible with more than one theory offers no support for any theory, even
              if the evidence is incompatible with some possibilities or the theories are not mutually exclusive.
            
            - When students in a classroom think they are being graded on a curve when in fact they are being graded based
              on predetermined standards.
            
            - In a negotiation when one negotiator thinks that they can only gain at the expense of the other party (i.e.,
              that mutual gain is not possible).
            
            - In the context of social group competition, the belief that more resources for one group (e.g., immigrants)
              means less for others (e.g., non-immigrants).
            
            - Jack of all trades, master of none: the idea that having more skills means having less aptitude (also known
              as compensatory reasoning).
            
            - In copyright infringement debate, the idea that every unauthorized duplication is a lost sale.
            
            - When politicians argue that international trade must mean that one party is "winning" and another is "losing"
              when transfer of goods and services at mutually-agreeable prices is in general mutually beneficial, or that a
              trade deficit represents "losing" money to another country.
            
            - Group membership is sometimes treated as zero-sum, such that stronger membership in one group is seen as
              weaker membership in another.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Zero-sum_thinking",
        ],
    };

    private static readonly Bias SURVIVORSHIP_BIAS = new()
    {
        Id = new Guid("87ef31b2-6b2a-4fbb-9974-fefec5480c28"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Survivorship Bias",
        Description =
            """
            # Survivorship Bias
            Survivorship bias or survival bias is the logical error of concentrating on entities that passed a selection
            process while overlooking those that did not. This can lead to incorrect conclusions because of incomplete data.
            
            Survivorship bias is a form of selection bias that can lead to overly optimistic beliefs because multiple
            failures are overlooked, such as when companies that no longer exist are excluded from analyses of financial
            performance. It can also lead to the false belief that the successes in a group have some special property,
            rather than just coincidence as in correlation "proves" causality.
            
            Another kind of survivorship bias would involve thinking that an incident happened in a particular way when
            the only people who were involved in the incident who can speak about it are those who survived it. Even if
            one knew that some people are dead, they would not have their voice to add to the conversation, making it
            biased.
            
            ## Examples
            ### Finance and Economics
            In finance, survivorship bias is the tendency for failed companies to be excluded from performance studies
            because they no longer exist. It often causes the results of studies to skew higher because only companies
            that were successful enough to survive until the end of the period are included. For example, a mutual fund
            company's selection of funds today will include only those that are successful now. Many losing funds are
            closed and merged into other funds to hide poor performance. In theory, 70% of extant funds could truthfully
            claim to have performance in the first quartile of their peers, if the peer group includes funds that have
            closed.
            
            ### Business
            Michael Shermer in Scientific American and Larry Smith of the University of Waterloo have described
            how advice about commercial success distorts perceptions of it by ignoring all of the businesses and college
            dropouts that failed. Journalist and author David McRaney observes that the "advice business is a monopoly
            run by survivors. When something becomes a non-survivor, it is either completely eliminated, or whatever
            voice it has is muted to zero". Alec Liu wrote in Vice that "for every Mark Zuckerberg, there's
            thousands of also-rans, who had parties no one ever attended, obsolete before we ever knew they
            existed."
            
            In his book The Black Swan, financial writer Nassim Taleb called the data obscured by survivorship bias
            "silent evidence".
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Survivorship_bias",
        ],
    };

    private static readonly Bias SUBADDITIVITY_EFFECT = new()
    {
        Id = new Guid("73e39503-4a2e-4090-88c2-5ce20565a722"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Subadditivity Effect",
        Description =
            """
            # Subadditivity Effect
            The subadditivity effect is the tendency to judge probability of the whole to be less than the
            probabilities of the parts.
            
            Example:
            For instance, subjects in one experiment judged the probability of death from cancer in the United
            States was 18%, the probability from heart attack was 22%, and the probability of death from
            "other natural causes" was 33%. Other participants judged the probability of death from a natural
            cause was 58%. Natural causes are made up of precisely cancer, heart attack, and "other natural
            causes," however, the sum of the latter three probabilities was 73%, and not 58%. According to
            Tversky and Koehler (1994) this kind of result is observed consistently.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Subadditivity_effect",
        ],
    };

    private static readonly Bias DENOMINATION_EFFECT = new()
    {
        Id = new Guid("a913b2cf-dc2f-4dd9-87dc-3e11efb9457b"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Denomination Effect",
        Description =
            """
            # Denomination Effect
            The denomination effect is a form of cognitive bias relating to currency, suggesting people may be
            less likely to spend larger currency denominations than their equivalent value in smaller denominations.
            It was proposed by Priya Raghubir, professor at the New York University Stern School of Business, and
            Joydeep Srivastava, professor at University of Maryland, in their 2009 paper "Denomination Effect".
            
            Raghubir and Srivastava conducted three studies in their research on the denomination effect; their
            findings suggested people may be more likely to spend money represented by smaller denominations and
            that consumers may prefer to receive money in a large denomination when there is a need to control
            spending. The denomination effect can occur when large denominations are perceived as less exchangeable
            than smaller denominations.
            
            The effect's influence on spending decisions has implications throughout various sectors in society,
            including consumer welfare, monetary policy and the finance industry. For example, during the Great
            Recession, one businessman observed employees using more coins rather than banknotes in an office
            vending machine, perceiving the customers used coins to feel thriftier. Raghubir and Srivastava
            also suggested the effect may involve incentives to alter future behavior and that a large
            denomination can serve as a mechanism to prevent the urge to spend.
            """,

        Related = [
            new Guid("9444923f-90c9-4269-a4dc-291513fa6d12"), // MENTAL_ACCOUNTING
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Denomination_effect",
        ],
    };

    private static readonly Bias MILLERS_LAW = new()
    {
        Id = new Guid("81ca1f50-aaf9-4416-a94a-3676b26e510a"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Miller's Law",
        Description =
            """
            # Miller's Law
            The observation, also by George A. Miller, that the number of objects the average person can hold in
            working memory is about seven. It was put forward in a 1956 edition of Psychological Review in a
            paper titled "The Magical Number Seven, Plus or Minus Two".
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Miller%27s_law",
        ],
    };

    private static readonly Bias ILLUSION_OF_TRANSPARENCY = new()
    {
        Id = new Guid("c727e47c-da6f-4804-a1d0-9027af645218"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Illusion of Transparency",
        Description =
            """
            # Illusion of Transparency
            The illusion of transparency is a tendency for people to overestimate the degree to which their personal
            mental state is known by others. Another manifestation of the illusion of transparency (sometimes called
            the observer's illusion of transparency) is a tendency for people to overestimate how well they understand
            others' personal mental states. This cognitive bias is similar to the illusion of asymmetric insight.
            """,

        Related = [
            new Guid("a44a6bcf-b2b8-47f1-84e0-d740af56aa1e"), // ILLUSION_OF_ASYMMETRIC_INSIGHT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusion_of_transparency",
        ],
    };

    private static readonly Bias CURSE_OF_KNOWLEDGE = new()
    {
        Id = new Guid("697f58a7-45d7-4268-8951-81681fb005de"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Curse of Knowledge",
        Description =
            """
            # Curse of Knowledge
            The curse of knowledge, also called the curse of expertise or expert's curse, is a cognitive bias
            that occurs when a person who has specialized knowledge assumes that others share in that knowledge.
            
            For example, in a classroom setting, teachers may have difficulty if they cannot put themselves
            in the position of the student. A knowledgeable professor might no longer remember the difficulties
            that a young student encounters when learning a new subject for the first time. This curse of
            knowledge also explains the danger behind thinking about student learning based on what appears
            best to faculty members, as opposed to what has been verified with students.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Curse_of_knowledge",
        ],
    };

    private static readonly Bias SPOTLIGHT_EFFECT = new()
    {
        Id = new Guid("1a6f6356-6d61-4892-8494-0257a7fa718b"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Spotlight Effect",
        Description =
            """
            # Spotlight Effect
            The spotlight effect is the psychological phenomenon by which people tend to believe they are being
            noticed more than they really are. Being that one is constantly in the center of one's own world,
            an accurate evaluation of how much one is noticed by others is uncommon. The reason for the spotlight
            effect is the innate tendency to forget that although one is the center of one's own world, one is
            not the center of everyone else's. This tendency is especially prominent when one does something
            atypical.
            
            Research has empirically shown that such drastic over-estimation of one's effect on others is widely
            common. Many professionals in social psychology encourage people to be conscious of the spotlight
            effect and to allow this phenomenon to moderate the extent to which one believes one is in a
            social spotlight.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Spotlight_effect",
        ],
    };

    private static readonly Bias EXTRINSIC_INCENTIVE_BIAS = new()
    {
        Id = new Guid("07237744-843d-4c0c-81b5-0c9c8664daea"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Extrinsic Incentives Bias",
        Description =
            """
            # Extrinsic Incentives Bias
            The extrinsic incentives bias is an attributional bias according to which people attribute relatively
            more to "extrinsic incentives" (such as monetary reward) than to "intrinsic incentives" (such as
            learning a new skill) when weighing the motives of others rather than themselves.
            
            It is a counter-example to the fundamental attribution error as according to the extrinsic bias
            others are presumed to have situational motivations while oneself is seen as having dispositional
            motivations. This is the opposite of what the fundamental attribution error would predict. It also
            might help to explain some of the backfiring effects that can occur when extrinsic incentives are
            attached to activities that people are intrinsically motivated to do. The term was first proposed
            by Chip Heath, citing earlier research by others in management science.
            
            Example:
            In the simplest experiment Heath reported, MBA students were asked to rank the expected job motivations
            of Citibank customer service representatives. Their average ratings were as follows:
            
            1. Amount of pay
            2. Having job security
            3. Quality of fringe benefits
            4. Amount of praise from your supervisor
            5. Doing something that makes you feel good about yourself
            6. Developing skills and abilities
            7. Accomplishing something worthwhile
            8. Learning new things
            
            Actual customer service representatives rank ordered their own motivations as follows:
            
            1. Developing skills and abilities
            2. Accomplishing something worthwhile
            3. Learning new things
            4. Quality of fringe benefits
            5. Having job security
            6. Doing something that makes you feel good about yourself
            7. Amount of pay
            8. Amount of praise from your supervisor
            
            The order of the predicted and actual reported motivations was nearly reversed; in particular, pay was
            rated first by others but near last for respondents of themselves. Similar effects were observed when
            MBA students rated managers and their classmates.
            
            Debiasing:
            Heath suggests trying to infer others' motivations as one would by inferring one's own motivations.
            """,

        Related = [
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("bf8f304d-2e8e-4a90-a9c5-7bd56f6058a6"), // BACKFIRE_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Extrinsic_incentives_bias",
        ],
    };

    private static readonly Bias ILLUSION_OF_EXTERNAL_AGENCY = new()
    {
        Id = new Guid("184c9dc0-6885-4dee-b777-bc1725cc7e2c"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Illusion of External Agency",
        Description =
            """
            # Illusion of External Agency
            People typically underestimate their capacity to generate satisfaction with future outcomes. When people
            experience such self-generated satisfaction, they may mistakenly conclude that it was caused by an
            influential, insightful, and benevolent external agent.
            
            When outcomes are unchangeable, people are more likely to turn ‘truly mediocre’ into ‘falsely great’.
            This subjective transformation is often termed a psychological immune response, in that it is our brain
            kicking in to protect us from the emotional consequences of undesirable outcomes. The illusion of external
            agency is thought to arise from this undetected transformation of ‘truly mediocre’ outcomes to ‘falsely
            great’ ones.
            """,

        Related = [],
        Links = [],
    };

    private static readonly Bias ILLUSION_OF_ASYMMETRIC_INSIGHT = new()
    {
        Id = new Guid("a44a6bcf-b2b8-47f1-84e0-d740af56aa1e"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Illusion of Asymmetric Insight",
        Description =
            """
            # Illusion of Asymmetric Insight
            The illusion of asymmetric insight is a cognitive bias whereby people perceive their knowledge of others to
            surpass other people's knowledge of them. This bias "has been traced to people's tendency to view their own
            spontaneous or off-the-cuff responses to others' questions as relatively unrevealing even though they view
            others' similar responses as meaningful".
            
            A study finds that people seem to believe that they know themselves better than their peers know themselves
            and that their social group knows and understands other social groups better than other social groups know
            them. For example: Person A knows Person A better than Person B knows Person B or Person A. This bias may be
            sustained by a few cognitive beliefs, including:
            
            - The personal conviction that observed behaviors are more revealing of other people than of the self, while
              private thoughts and feelings are more revealing of the self.
              
            - The more an individual perceives negative traits ascribed to someone else, the more doubt individuals express
              about this person's self-knowledge. But, this doubt does not exist for our own self-knowledge. (For example:
              if Person A believes Person B has some great character flaw, Person A will distrust Person B's self-knowledge,
              while sustaining that they do not hold that same flaw in self-knowledge.)
            """,

        Related = [
            new Guid("c727e47c-da6f-4804-a1d0-9027af645218"), // ILLUSION_OF_TRANSPARENCY
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("f0ad095e-8e9c-4bfb-855e-11fb5dd58cea"), // NAÏVE_REALISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusion_of_asymmetric_insight",
        ],
    };

    private static readonly Bias TELESCOPING_EFFECT = new()
    {
        Id = new Guid("88b90cfb-93f5-429b-b00f-fabe7ada485c"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Telescoping Effect",
        Description =
            """
            # Telescoping Effect
            In cognitive psychology, the telescoping effect (or telescoping bias) refers to the temporal displacement of an
            event whereby people perceive recent events as being more remote than they are and distant events as being more
            recent than they are. The former is known as backward telescoping or time expansion, and the latter as is known
            as forward telescoping.
            
            The approximate time frame in which events switch from being displaced backward in time to forward in time is three
            years, with events occurring three years in the past being equally likely to be reported with forward telescoping
            bias as with backward telescoping bias. Although telescoping occurs in both the forward and backward directions,
            in general the effect is to increase the number of events reported too recently. This net effect in the forward
            direction is because forces that impair memory, such as lack of salience, also impair time perception.
            
            Telescoping leads to an over-reporting of the frequency of events. This over-reporting is because participants
            include events beyond the period, either events that are too recent for the target time period (backward
            telescoping) or events that are too old for the target time period (forward telescoping).
            
            ## Real-world example
            A real-world example of the telescoping effect is the case of Ferdi Elsas, an infamous kidnapper and murderer
            in the Netherlands. When he was let out of prison, most of the general population did not believe he had been
            in prison long enough. Due to forward telescoping, people thought Ferdi Elsas' sentence started more recently
            than it actually did. Telescoping has important real world applications, especially in survey research. Marketing
            firms often use surveys to ask when consumers last bought a product, and government agencies often use surveys
            to discover information about drug abuse or about victimology. Telescoping may bias answers to these questions.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Telescoping_effect",
        ],
    };

    private static readonly Bias ROSY_RETROSPECTION = new()
    {
        Id = new Guid("5e08ec28-0814-499f-82bd-eb7afb2080aa"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Rosy Retrospection",
        Description =
            """
            # Rosy Retrospection
            Rosy retrospection is a proposed psychological phenomenon of recalling the past more positively than it
            was actually experienced. The highly unreliable nature of human memory is well documented and accepted
            amongst psychologists. Some research suggests a 'blue retrospective' which also exaggerates negative
            emotions.
            
            Though it is a cognitive bias which distorts one's view of reality, it is suggested that rosy retrospection
            serves a useful purpose in increasing self-esteem and sense of well-being. Simplifications and exaggerations
            of memories such as occur in rosy retrospection may make it easier for the brain to store long-term memories,
            as removing details may reduce the burden of those memories by requiring the generation and maintenance of
            fewer neural connections.
            
            Declinism, the predisposition to view the past more favourably and the future more negatively, may be related
            to cognitive biases like rosy retrospection. Rosy retrospection is very closely related to the concept of
            nostalgia, though the broader phenomenon of nostalgia is not usually seen as based on a biased perspective.
            
            The English idiom "rose-colored glasses" or "rose-tinted glasses" refers to perceiving something more
            positively than it is in reality. The Romans occasionally referred to this phenomenon with the Latin phrase
            "memoria praeteritorum bonorum", which translates into English roughly as "memory of good past", or more
            idiomatically as "good old days".
            """,

        Related = [
            new Guid("b9edd2f0-8503-4eb5-a4c3-369fcb318894"), // HINDSIGHT_BIAS
            new Guid("7bf44f8f-a4b0-404c-8f15-8ca6e3322d32"), // OPTIMISM_BIAS
            new Guid("b57a862b-b490-4d61-96b8-29d548c2eee4"), // POSITIVITY_EFFECT
            new Guid("23e4b2ad-c915-4d47-ab2d-79a3dce2a7e5"), // DECLINISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Rosy_retrospection",
        ],
    };

    private static readonly Bias PROJECTION_BIAS = new()
    {
        Id = new Guid("61ca5b76-66d0-4ce2-b260-7fd42696000a"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Projection Bias",
        Description =
            """
            # Projection Bias
            
            ## Affective forecasting
            Affective forecasting, also known as hedonic forecasting or the hedonic forecasting mechanism, is the
            prediction of one's affect (emotional state) in the future. As a process that influences preferences,
            decisions, and behavior, affective forecasting is studied by both psychologists and economists, with
            broad applications.
            
            ## Bias
            Projection bias is the tendency to falsely project current preferences onto a future event. When people
            are trying to estimate their emotional state in the future they attempt to give an unbiased estimate.
            However, people's assessments are contaminated by their current emotional state. Thus, it may be difficult
            for them to predict their emotional state in the future, an occurrence known as mental contamination. For
            example, if a college student was currently in a negative mood because he just found out he failed a test,
            and if the college student forecasted how much he would enjoy a party two weeks later, his current negative
            mood may influence his forecast. In order to make an accurate forecast the student would need to be aware
            that his forecast is biased due to mental contamination, be motivated to correct the bias, and be able to
            correct the bias in the right direction and magnitude.
            
            Projection bias can arise from empathy gaps (or hot/cold empathy gaps), which occur when the present and
            future phases of affective forecasting are characterized by different states of physiological arousal,
            which the forecaster fails to take into account. For example, forecasters in a state of hunger are likely
            to overestimate how much they will want to eat later, overlooking the effect of their hunger on future
            preferences. As with projection bias, economists use the visceral motivations that produce empathy gaps
            to help explain impulsive or self-destructive behaviors, such as smoking.
            """,

        Related = [
            new Guid("8e0f2242-6ad8-4e1e-a9e5-b55a4166781a"), // IMPACT_BIAS
            new Guid("e4e091cf-fed3-4c09-9c21-509db0b2729b"), // HOT_COLD_EMPATHY_GAP
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Affective_forecasting#Projection_bias",
        ],
    };

    private static readonly Bias IMPACT_BIAS = new()
    {
        Id = new Guid("8e0f2242-6ad8-4e1e-a9e5-b55a4166781a"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Impact Bias",
        Description =
            """
            # Impact Bias
            
            ## Affective forecasting
            Affective forecasting, also known as hedonic forecasting or the hedonic forecasting mechanism, is the
            prediction of one's affect (emotional state) in the future. As a process that influences preferences,
            decisions, and behavior, affective forecasting is studied by both psychologists and economists, with
            broad applications.
            
            ## Bias
            One of the most common sources of error in affective forecasting across various populations and situations
            is impact bias, the tendency to overestimate the emotional impact of a future event, whether in terms of
            intensity or duration. The tendencies to overestimate intensity and duration are both robust and reliable
            errors found in affective forecasting.
            
            One study documenting impact bias examined college students participating in a housing lottery. These students
            predicted how happy or unhappy they would be one year after being assigned to either a desirable or an undesirable
            dormitory. These college students predicted that the lottery outcomes would lead to meaningful differences in
            their own level of happiness, but follow-up questionnaires revealed that students assigned to desirable or
            undesirable dormitories reported nearly the same levels of happiness. Thus, differences in forecasts
            overestimated the impact of the housing assignment on future happiness.
            
            Some studies specifically address "durability bias," the tendency to overestimate the length of time future
            emotional responses will last. Even if people accurately estimate the intensity of their future emotions, they
            may not be able to estimate their duration. Durability bias is generally stronger in reaction to negative events.
            This is important because people tend to work toward events they believe will cause lasting happiness, and according
            to durability bias, people might be working toward the wrong things. Similar to impact bias, durability bias causes
            a person to overemphasize where the root cause of their happiness lies.
            """,

        Related = [
            new Guid("61ca5b76-66d0-4ce2-b260-7fd42696000a"), // PROJECTION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Affective_forecasting#Impact_bias",
        ],
    };

    private static readonly Bias PRO_INNOVATION_BIAS = new()
    {
        Id = new Guid("fa033e14-41f3-45a9-887f-17e30f24c4e5"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Pro-Innovation Bias",
        Description =
            """
            # Pro-Innovation Bias
            In diffusion of innovation theory, a pro-innovation bias is a belief that innovation should be adopted by the whole
            society without the need for its alteration. The innovation's "champion" has a such strong bias in favor of the
            innovation, that they may not see its limitations or weaknesses and continue to promote it nonetheless.
            
            Example:
            A feeling of nuclear optimism emerged in the 1950s in which it was believed that all power generators in the future
            would be atomic in nature. The atomic bomb would render all conventional explosives obsolete and nuclear power plants
            would do the same for power sources such as coal and oil. There was a general feeling that everything would use a
            nuclear power source of some sort, in a positive and productive way, from irradiating food to preserve it, to the
            development of nuclear medicine. There would be an age of peace and plenty in which atomic energy would "provide the
            power needed to desalinate water for the thirsty, irrigate the deserts for the hungry, and fuel interstellar travel
            deep into outer space". This use would render the Atomic Age as significant a step in technological progress as the
            first smelting of Bronze, of Iron, or the commencement of the Industrial Revolution.
            
            Roger Smith, then chairman of General Motors, said in 1986: "By the turn of the century, we will live in a paperless
            society." In the late 20th century, there were many predictions of this kind. This transformation has so far not taken
            place.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Pro-innovation_bias",
        ],
    };

    private static readonly Bias TIME_SAVING_BIAS = new()
    {
        Id = new Guid("f262db5e-b668-4bf9-9591-e38e153717da"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Time-Saving Bias",
        Description =
            """
            # Time-Saving Bias
            Time-saving bias is a concept that describes people's tendency to misestimate the time that could be saved (or
            lost) when increasing (or decreasing) speed. In general, people underestimate the time that could be saved when
            increasing from a relatively low speed—e.g., 25 mph (40 km/h) or 40 mph (64 km/h)—and overestimate the time that
            could be saved when increasing from a relatively high speed—e.g., 55 mph (89 km/h) or 90 mph (140 km/h). People
            also underestimate the time that could be lost when decreasing from a low speed and overestimate the time that
            could be lost when decreasing from a high speed.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Time-saving_bias",
        ],
    };

    private static readonly Bias PLANNING_FALLACY = new()
    {
        Id = new Guid("144a4177-96fa-428f-8f42-bd7c3671c8a6"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Planning Fallacy",
        Description =
            """
            # Planning Fallacy
            The planning fallacy is a phenomenon in which predictions about how much time will be needed to complete a future task
            display an optimism bias and underestimate the time needed. This phenomenon sometimes occurs regardless of the individual's
            knowledge that past tasks of a similar nature have taken longer to complete than generally planned. The bias affects
            predictions only about one's own tasks. On the other hand, when outside observers predict task completion times, they
            tend to exhibit a pessimistic bias, overestimating the time needed. The planning fallacy involves estimates of task
            completion times more optimistic than those encountered in similar projects in the past.
            
            Real-world examples:
            
            - The Sydney Opera House was expected to be completed in 1963. A scaled-down version opened in 1973, a decade later.
              The original cost was estimated at $7 million, but its delayed completion led to a cost of $102 million.
            
            - The Eurofighter Typhoon defense project took six years longer than expected, with an overrun cost of 8 billion euros.
            
            - The Big Dig which undergrounded the Boston Central Artery was completed seven years later than planned, for $8.08
              billion on a budget of $2.8 billion (in 1988 dollars).
            
            - The Denver International Airport opened sixteen months later than scheduled, with a total cost of $4.8 billion,
              over $2 billion more than expected.
            
            - The Berlin Brandenburg Airport is another case. After 15 years of planning, construction began in 2006, with the
              opening planned for October 2011. There were numerous delays. It was finally opened on October 31, 2020. The
              original budget was €2.83 billion; current projections are close to €10.0 billion.
            
            - Olkiluoto Nuclear Power Plant Unit 3 faced severe delay and a cost overrun. The construction started in 2005 and
              was expected to be completed by 2009, but completed only in 2023. Initially, the estimated cost of the project was
              around 3 billion euros, but the cost has escalated to approximately 10 billion euros.
            
            - California High-Speed Rail is still under construction, with tens of billions of dollars in overruns expected,
              and connections to major cities postponed until after completion of the rural segment.
            
            - The James Webb Space Telescope went over budget by approximately 9 billion dollars, and was sent into orbit 14
              years later than its originally planned launch date.
            """,

        Related = [
            new Guid("7bf44f8f-a4b0-404c-8f15-8ca6e3322d32"), // OPTIMISM_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Planning_fallacy",
        ],
    };

    private static readonly Bias PESSIMISM_BIAS = new()
    {
        Id = new Guid("67041978-ac8e-4254-ae2c-509e7301619f"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Pessimism Bias",
        Description =
            """
            # Pessimism Bias
            The opposite of optimism bias is pessimism bias (or pessimistic bias), because the principles of the optimistic
            bias continue to be in effect in situations where individuals regard themselves as worse off than others. Optimism
            may occur from either a distortion of personal estimates, representing personal optimism, or a distortion for others,
            representing personal pessimism.
            
            Pessimism bias is an effect in which people exaggerate the likelihood that negative things will happen to them. It
            contrasts with optimism bias. People with depression are particularly likely to exhibit pessimism bias. Surveys of
            smokers have found that their ratings of their risk of heart disease showed a small but significant pessimism bias;
            however, the literature as a whole is inconclusive.
            """,

        Related = [
            new Guid("7bf44f8f-a4b0-404c-8f15-8ca6e3322d32"), // OPTIMISM_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Optimism_bias#Pessimism_bias",
        ],
    };

    private static readonly Bias DECLINISM = new()
    {
        Id = new Guid("23e4b2ad-c915-4d47-ab2d-79a3dce2a7e5"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Declinism",
        Description =
            """
            # Declinism
            Declinism is the belief that a society or institution is tending towards decline. Particularly, it is the
            predisposition, caused by cognitive biases such as rosy retrospection, to view the past more favourably and
            the future more negatively. "The great summit of declinism" according to Adam Gopnick, "was established in
            1918, in the book that gave decline its good name in publishing: the German historian Oswald Spengler's
            best-selling, thousand-page work *The Decline of the West*."
            """,

        Related = [
            new Guid("5e08ec28-0814-499f-82bd-eb7afb2080aa"), // ROSY_RETROSPECTION
            new Guid("8533edf9-3117-48c5-8f78-efbd996911f0"), // CONSERVATISM_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Declinism",
        ],
    };

    private static readonly Bias MORAL_LUCK = new()
    {
        Id = new Guid("7534480a-1abf-40d5-acec-ace1bfc5be3a"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Moral Luck",
        Description =
            """
            # Moral Luck
            Moral luck describes circumstances whereby a moral agent is assigned moral blame or praise for an action or
            its consequences even if it is clear that said agent did not have full control over either the action or its
            consequences.
            
            Example: There are two people driving cars, Driver A and Driver B. They are alike in every way. Driver A is
            driving down a road and in a moment of inattention runs a red light as a child is crossing the street. Driver
            A slams the brakes, swerves, and does everything to try to avoid hitting the child. Alas, the car hits and
            kills the child. Driver B in the meantime also runs a red light, but since no one is crossing, gets a traffic
            ticket but nothing more.
            
            If it is given that moral responsibility should only be relevant when the agent voluntarily performed or
            failed to perform some action, Drivers A and B should be blamed equally, or praised equally, as may be
            the case. However, due to the effect of Moral Luck, if a bystander were asked to morally evaluate Drivers
            A and B, there is very good reason to expect them to say that Driver A is due more moral blame than Driver B.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Moral_luck",
        ],
    };

    private static readonly Bias OUTCOME_BIAS = new()
    {
        Id = new Guid("a3f4415d-b7fa-4668-bcc2-20c79f714bdd"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Outcome Bias",
        Description =
            """
            # Outcome Bias
            The outcome bias is an error made in evaluating the quality of a decision when the outcome of that decision is
            already known. Specifically, the outcome effect occurs when the same "behavior produce[s] more ethical condemnation
            when it happen[s] to produce bad rather than good outcome, even if the outcome is determined by chance."
            
            While similar to the hindsight bias, the two phenomena are markedly different. Hindsight bias focuses on memory
            distortion to favor the actor, while the outcome bias focuses exclusively on weighting the outcome heavier than
            other pieces of information in deciding if a past decision was correct.
            
            The outcome bias is closely related to the philosophical concept of moral luck as in both concepts, the evaluation
            of actions is influenced by factors that are not logically justifiable.
            """,

        Related = [
            new Guid("7534480a-1abf-40d5-acec-ace1bfc5be3a"), // MORAL_LUCK
            new Guid("b9edd2f0-8503-4eb5-a4c3-369fcb318894"), // HINDSIGHT_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Outcome_bias",
        ],
    };

    private static readonly Bias HINDSIGHT_BIAS = new()
    {
        Id = new Guid("b9edd2f0-8503-4eb5-a4c3-369fcb318894"),
        Category = BiasCategory.NOT_ENOUGH_MEANING,
        Name = "Hindsight Bias",
        Description =
            """
            # Hindsight Bias
            Hindsight bias, also known as the knew-it-all-along phenomenon or creeping determinism, is the common tendency
            for people to perceive past events as having been more predictable than they were. After an event has occurred,
            people often believe that they could have predicted or perhaps even known with a high degree of certainty what
            the outcome of the event would be before it occurred. Hindsight bias may cause distortions of memories of what
            was known or believed before an event occurred and is a significant source of overconfidence in one’s ability
            to predict the outcomes of future events. Examples of hindsight bias can be seen in the writings of historians
            describing the outcomes of battles, in physicians’ recall of clinical trials, and in criminal or civil trials
            as people tend to assign responsibility on the basis of the supposed predictability of accidents.
            
            Hindsight bias has both positive and negative consequences. The bias also plays a role in the process of
            decision-making within the medical field.
            
            Positive consequences of hindsight bias is an increase in one's confidence and performance, as long as the bias
            distortion is reasonable and does not create overconfidence. Another positive consequence is that one's
            self-assurance of their knowledge and decision-making, even if it ends up being a poor decision, can be
            beneficial to others; allowing others to experience new things or to learn from those who made the poor
            decisions.
            
            Negative: Hindsight bias causes overconfidence in one's performance relative to others. Hindsight bias
            decreases one's rational thinking because of when a person experiences strong emotions, which in turn
            decreases rational thinking. Another negative consequence of hindsight bias is the interference of one's
            ability to learn from experience, as a person is unable to look back on past decisions and learn from
            mistakes. A third consequence is a decrease in sensitivity toward a victim by the person who caused the
            wrongdoing. The person demoralizes the victim and does not allow for a correction of behaviors and actions.
            
            Medical decision-making: Hindsight bias may lead to overconfidence and malpractice in regards to physicians.
            Hindsight bias and overconfidence is often attributed to the number of years of experience the physician has.
            After a procedure, physicians may have a "knew it the whole time" attitude, when in reality they may not have
            known it. Medical decision support systems are designed to assist physicians in diagnosis and treatment, and
            have been suggested as a way to counteract hindsight bias. However, these decision support systems come with
            drawbacks, as going against a recommended decision resulted in more punitive jury outcomes when physicians
            were found liable for causing harm.
            """,

        Related = [
            new Guid("af63ce77-f6c6-4e0f-8a9e-3daedc497f9a"), // CONFIRMATION_BIAS
            new Guid("697f58a7-45d7-4268-8951-81681fb005de"), // CURSE_OF_KNOWLEDGE
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Hindsight_bias",
        ],
    };
    
    #endregion

    #region NEED_TO_ACT_FAST

    private static readonly Bias LESS_IS_BETTER_EFFECT = new()
    {
        Id = new Guid("593f2a10-46a6-471e-9ab3-86df740df6f2"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Less-Is-Better Effect",
        Description =
            """
            # Less-Is-Better Effect
            The less-is-better effect is a type of preference reversal that occurs when the lesser
            or smaller alternative of a proposition is preferred when evaluated separately, but not
            evaluated together.
            
            In a 1998 study, Hsee, a professor at the Graduate School of Business of The University
            of Chicago, discovered a less-is-better effect in three contexts:
            
            - (1) a person giving a $45 scarf (from scarves ranging from $5-$50) as a gift was
               perceived to be more generous than one giving a $55 coat (from coats ranging from $50-$500);
               
            - (2) an overfilled ice cream serving with 7 oz of ice cream was valued more than an underfilled
              serving with 8 oz of ice cream;
              
            - (3) a dinnerware set with 24 intact pieces was judged more favourably than one with 31 intact
              pieces (including the same 24) plus a few broken ones.
              
            Hsee noted that the less-is-better effect was observed "only when the options were evaluated 
            separately, and reversed itself when the options were juxtaposed.” Hsee explained these seemingly
            counterintuitive results “in terms of the evaluability hypothesis, which states that separate
            evaluations of objects are often influenced by attributes that are easy to evaluate rather than
            by those that are important."
            
            The less-is-better effect occurs only under specific circumstances. Evidence has shown that it
            manifests itself only when the options are evaluated individually; it disappears when they are
            assessed jointly. "If the options are put right next to each other, the effect disappears, as
            people see the true value of both," states one source. "It's just the gifts in isolation that
            give people a flipped sense of happiness and gratitude."
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Less-is-better_effect",
        ],
    };

    private static readonly Bias OCCAMS_RAZOR = new()
    {
        Id = new Guid("3d5e3115-a98e-4d11-9760-4a3ddbbe6c69"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Occam's Razor",
        Description =
            """
            # Occam’s Razor
            Among competing hypotheses, the one with the fewest assumptions should be selected. Alternatively,
            other things being equal, simpler explanations are generally better than more complex ones.
            Controversial. This is not a cognitive bias. It is a heuristic, but not one that deviates from
            rationality in judgment.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Occam%27s_razor",
        ],
    };

    private static readonly Bias CONJUNCTION_FALLACY = new()
    {
        Id = new Guid("b0c60f50-cc40-4bde-996c-1833741622a0"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Conjunction Fallacy",
        Description =
            """
            # Conjunction Fallacy
            The conjunction fallacy (also known as the Linda problem) is an inference that a conjoint set of two or more
            specific conclusions is likelier than any single member of that same set, in violation of the laws of
            probability. It is a type of formal fallacy.
            
            The most often-cited example of this fallacy originated with Amos Tversky and Daniel Kahneman:
            
            "Linda is 31 years old, single, outspoken, and very bright. She majored in philosophy. As a student, she
            was deeply concerned with issues of discrimination and social justice, and also participated in anti-nuclear
            demonstrations."
            
            Which is more probable?
            
            - Linda is a bank teller.
            - Linda is a bank teller and is active in the feminist movement.
            
            The majority of those asked chose option 2. However, the probability of two events occurring together
            (that is, in conjunction) is always less than or equal to the probability of either one occurring itself.
            
            Tversky and Kahneman argue that most people get this problem wrong because they use a heuristic (an easily
            calculated) procedure called representativeness to make this kind of judgment: Option 2 seems more
            "representative" of Linda from the description of her, even though it is clearly mathematically less likely.
            
            ## Debiasing
            Drawing attention to set relationships, using frequencies instead of probabilities, and/or thinking
            diagrammatically (e.g. use a Venn diagram) sharply reduce the error in some forms of the conjunction
            fallacy.
            
            In one experiment the question of the Linda problem was reformulated as follows:
            
            "There are 100 persons who fit the description above (that is, Linda's). How many of them are:
            
            - Bank tellers? __ of 100
            - Bank tellers and active in the feminist movement? __ of 100"
            
            Whereas previously 85% of participants gave the wrong answer (bank teller and active in the feminist
            movement), in experiments done with this questioning the proportion of incorrect answers is dramatically
            reduced (to ~20%). Participants were forced to use a mathematical approach and thus recognized the
            difference more easily.
            
            However, in some tasks only based on frequencies, not on stories, that used clear logical formulations,
            conjunction fallacies continued to occur dominantly, with only few exceptions, when the observed pattern
            of frequencies resembled a conjunction.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Conjunction_fallacy",
        ],
    };

    private static readonly Bias DELMORE_EFFECT = new()
    {
        Id = new Guid("93a3d088-183f-47e7-a010-721f1cd6bac8"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Delmore Effect",
        Description =
            """
            # Delmore Effect
            The Delmore effect is about how we tend to set clearer and more detailed goals for less important areas
            of our lives. In other words, we distract ourselves from the most important tasks by focusing on the
            easy stuff instead.
            """,

        Related = [],
        Links =
        [
            "https://www.42courses.com/blog/home/2022/9/2/42-effects-you-should-know-part-2",
            "https://bias.transhumanity.net/delmore-effect/",
        ],
    };

    private static readonly Bias PARKINSONS_LAW_OF_TRIVIALITY = new()
    {
        Id = new Guid("d3ec6a5d-91cf-4aec-8541-bd87e1ad834b"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Law of Triviality",
        Description =
            """
            # Law of Triviality
            The law of triviality is C. Northcote Parkinson's 1957 argument that people within an organization commonly
            give disproportionate weight to trivial issues. Parkinson provides the example of a fictional committee
            whose job was to approve the plans for a nuclear power plant spending the majority of its time on discussions
            about relatively minor but easy-to-grasp issues, such as what materials to use for the staff bicycle shed,
            while neglecting the proposed design of the plant itself, which is far more important and a far more difficult
            and complex task.
            
            The law has been applied to software development and other activities. The terms bicycle-shed effect,
            bike-shed effect, and bike-shedding were coined based on Parkinson's example; it was popularized in the
            Berkeley Software Distribution community by the Danish software developer Poul-Henning Kamp in 1999
            and, due to that, has since become popular within the field of software development generally.
            """,

        Related = [
            new Guid("b9c06da1-d2eb-4871-8159-a2a6d25e9eff"), // DUNNING_KRUGER_EFFECT
            new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"), // OMISSION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Law_of_triviality",
        ],
    };

    private static readonly Bias RHYME_AS_REASON_EFFECT = new()
    {
        Id = new Guid("0d290221-81a0-4e44-bdec-30709117d90d"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Rhyme as Reason Effect",
        Description =
            """
            # Rhyme as Reason Effect
            The rhyme-as-reason effect, also known as the Eaton–Rosen phenomenon, is a cognitive bias where sayings
            or aphorisms are perceived as more accurate or truthful when they rhyme. In experiments, participants
            evaluated variations of sayings that either rhymed or did not rhyme. Those that rhymed were consistently
            judged as more truthful, even when the meaning was controlled for. For instance, the rhyming saying "What
            sobriety conceals, alcohol reveals" was rated as more accurate on average than its non-rhyming counterpart,
            "What sobriety conceals, alcohol unmasks," across different groups of subjects (each group assessed the
            accuracy of only one version of the statement).
            
            This effect may be explained by the Keats heuristic, which suggests that people assess a statement's truth
            based on its aesthetic qualities. Another explanation is the fluency heuristic, which posits that statements
            are preferred due to their ease of cognitive processing.
            """,

        Related = [
            new Guid("cadafb8f-d1ed-4c92-9c29-2f1cb0797a66"), // ILLUSORY_TRUTH_EFFECT
            new Guid("1c5aa90a-e732-4f45-bf26-1b86c49a82f9"), // BELIEF_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Rhyme-as-reason_effect",
        ],
    };

    private static readonly Bias BELIEF_BIAS = new()
    {
        Id = new Guid("1c5aa90a-e732-4f45-bf26-1b86c49a82f9"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Belief Bias",
        Description =
            """
            # Belief Bias
            Belief bias is the tendency to judge the strength of arguments based on the plausibility of their conclusion
            rather than how strongly they justify that conclusion. A person is more likely to accept an argument that
            supports a conclusion that aligns with their values, beliefs and prior knowledge, while rejecting counter
            arguments to the conclusion. Belief bias is an extremely common and therefore significant form of error;
            we can easily be blinded by our beliefs and reach the wrong conclusion. Belief bias has been found to
            influence various reasoning tasks, including conditional reasoning, relation reasoning and transitive reasoning.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Belief_bias",
        ],
    };

    private static readonly Bias INFORMATION_BIAS = new()
    {
        Id = new Guid("d0e251bb-3e09-43f5-8c5e-bc933e743509"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Information Bias",
        Description =
            """
            # Information Bias
            The tendency to seek information when it does not affect action. An example of information bias is believing
            that the more information that can be acquired to make a decision, the better, even if that extra information
            is irrelevant for the decision.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Information_bias_(psychology)",
        ],
    };

    private static readonly Bias AMBIGUITY_EFFECT = new()
    {
        Id = new Guid("e9b00144-0cb3-46de-8a68-09daa00de1e4"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Ambiguity Effect",
        Description =
            """
            # Ambiguity Effect
            The ambiguity effect is a cognitive tendency where decision making is affected by a lack of information, or
            "ambiguity". The effect implies that people tend to select options for which the probability of a favorable
            outcome is known, over an option for which the probability of a favorable outcome is unknown. The effect was
            first described by Daniel Ellsberg in 1961.
            
            One possible explanation of the effect is that people have a rule of thumb (heuristic) to avoid options where
            information is missing. This will often lead them to seek out the missing information. In many cases, though,
            the information cannot be obtained. The effect is often the result of calling some particular missing piece of
            information to the person's attention.
            """,

        Related = [
            new Guid("44c6efd7-53f1-4d22-82fe-25e941390089"), // NEGLECT_OF_PROBABILITY
            new Guid("73ca0caa-25e5-4edb-91d4-f375a773f82c"), // APPEAL_TO_POSSIBILITY
            new Guid("b0c60f50-cc40-4bde-996c-1833741622a0"), // CONJUNCTION_FALLACY
            new Guid("656c78c9-d75a-4c07-a80d-f3a5026f859c"), // PSEUDOCERTAINTY_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Ambiguity_effect",
        ],
    };

    private static readonly Bias STATUS_QUO_BIAS = new()
    {
        Id = new Guid("b9e05a25-ac09-407d-8aee-f54a04decf0b"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Status Quo Bias",
        Description =
            """
            # Status Quo Bias
            A status quo bias or default bias is a cognitive bias which results from a preference for the maintenance
            of one's existing state of affairs. The current baseline (or status quo) is taken as a reference point,
            and any change from that baseline is perceived as a loss or gain. Corresponding to different alternatives,
            this current baseline or default option is perceived and evaluated by individuals as a positive.
            
            Status quo bias should be distinguished from a rational preference for the status quo ante, as when the
            current state of affairs is objectively superior to the available alternatives, or when imperfect information
            is a significant problem. A large body of evidence, however, shows that status quo bias frequently affects
            human decision-making. Status quo bias should also be distinguished from psychological inertia, which refers
            to a lack of intervention in the current course of affairs.
            
            The bias intersects with other non-rational cognitive processes such as loss aversion, in which losses
            comparative to gains are weighed to a greater extent. Further non-rational cognitive processes include
            existence bias, endowment effect, longevity, mere exposure, and regret avoidance. Experimental evidence
            for the detection of status quo bias is seen through the use of the reversal test. A vast amount of
            experimental and field examples exist. Behaviour in regard to economics, retirement plans, health, and
            ethical choices show evidence of the status quo bias.
            """,

        Related = [
            new Guid("b81482f8-b2cf-4b86-a5a4-fcd29aee4e69"), // ENDOWMENT_EFFECT
            new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"), // OMISSION_BIAS
            new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"), // LOSS_AVERSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Status_quo_bias",
        ],
    };

    private static readonly Bias SOCIAL_COMPARISON_BIAS = new()
    {
        Id = new Guid("09527928-6417-4eea-9719-d8ed4748691f"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Social Comparison Bias",
        Description =
            """
            # Social Comparison Bias
            Social comparison bias is the tendency to have feelings of dislike and competitiveness with someone seen as
            physically, socially, or mentally better than oneself. Social comparison bias or social comparison theory is
            the idea that individuals determine their own worth based on how they compare to others. The theory was
            developed in 1954 by psychologist Leon Festinger. This can be compared to social comparison, which is
            believed to be central to achievement motivation, feelings of injustice, depression, jealousy, and people's
            willingness to remain in relationships or jobs. The basis of the theory is that people are believed to
            compete for the best outcome in relation to their peers. For example, one might make a comparison between the
            low-end department stores they go to frequently and the designer stores of their peers. Such comparisons may
            evoke feelings of resentment, anger, and envy with their peers. This bias revolves mostly around wealth and
            social status; it is unconscious and people who make these are largely unaware of them. In most cases, people
            try to compare themselves to those in their peer group or with whom they are similar.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Social_comparison_bias",
        ],
    };

    private static readonly Bias DECOY_EFFECT = new()
    {
        Id = new Guid("c8a532e9-5958-4894-aa0d-29ed6412780f"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Decoy Effect",
        Description =
            """
            # Decoy Effect
            In marketing, the decoy effect (or attraction effect or asymmetric dominance effect) is the phenomenon
            whereby consumers will tend to have a specific change in preference between two options when also presented
            with a third option that is asymmetrically dominated. An option is asymmetrically dominated when it is
            inferior in all respects to one option; but, in comparison to the other option, it is inferior in some
            respects and superior in others. In other words, in terms of specific attributes determining preferences,
            it is completely dominated by (i.e., inferior to) one option and only partially dominated by the other.
            When the asymmetrically dominated option is present, a higher percentage of consumers will prefer the
            dominating option than when the asymmetrically dominated option is absent. The asymmetrically dominated
            option is therefore a decoy serving to increase preference for the dominating option. The decoy effect
            is also an example of the violation of the independence of irrelevant alternatives axiom of decision
            theory. More simply, when deciding between two options, an unattractive third option can change the
            perceived preference between the other two.
            
            The decoy effect is considered particularly important in choice theory because it is a violation of the
            assumption of "regularity" present in all axiomatic choice models, for example in a Luce model of choice.
            Regularity means that it should not be possible for the market share of any alternative to increase when
            another alternative is added to the choice set. The new alternative should reduce, or at best leave unchanged,
            the choice share of existing alternatives. Regularity is violated in the example shown below where a new
            alternative C not only changes the relative shares of A and B but actually increases the share of A in
            absolute terms. Similarly, the introduction of a new alternative D increases the share of B in absolute
            terms.
            
            ## Example
            Suppose there is a consideration set (options to choose from in a menu) that involves smartphones. Consumers
            will generally see higher storage capacity (number of GB) and lower price as positive attributes; while some
            consumers may want a device that can store more photos, music, etc., other consumers will want a device that
            costs less. In Consideration Set 1, two devices are available:
            
            Consideration Set 1:
            - A: $400, 300GB
            - B: $300, 200GB
            
            In this case, some consumers will prefer A for its greater storage capacity, while others will prefer B for
            its lower price.
            
            Now suppose that a new player, C, the "decoy", is added to the market; it is more expensive than both A, the
            "target", and B, the "competitor", and has more storage than B but less than A:
            
            Consideration Set 2:
            - A (target): $400, 300GB
            - B (competitor): $300, 200GB
            - C (decoy): $450, 250GB
            
            The addition of decoy C — which consumers would presumably avoid, given that a lower price can be paid for a
            model with more storage—causes A, the dominating option, to be chosen more often than if only the two choices
            in Consideration Set 1 existed; C affects consumer preferences by acting as a basis of comparison for A and B.
            Because A is better than C in both respects, while B is only partially better than C, more consumers will
            prefer A now than did before. C is therefore a decoy whose sole purpose is to increase sales of A.
            
            Conversely, suppose that instead of C, a player D is introduced that has less storage than both A and B, and
            that is more expensive than B but not as expensive as A:
            
            Consideration Set 3:
            - A (competitor): $400, 300GB
            - B (target): $300, 200GB
            - D (decoy): $350, 150GB
            
            The result here is similar: consumers will not prefer D, because it is not as good as B in any respect. However,
            whereas C increased preference for A, D has the opposite effect, increasing preference for B.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Decoy_effect",
        ],
    };

    private static readonly Bias REACTANCE = new()
    {
        Id = new Guid("d3c2cb4b-ec29-4cf3-a485-9a98e9f1f223"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Reactance",
        Description =
            """
            # Reactance
            In psychology, reactance is an unpleasant motivational reaction to offers, persons, rules, regulations, advice, or
            recommendations that are perceived to threaten or eliminate specific behavioral freedoms. Reactance occurs when an
            individual feels that an agent is attempting to limit one's choice of response and/or range of alternatives.
            
            Reactance can occur when someone is heavily pressured into accepting a certain view or attitude. Reactance can
            encourage an individual to adopt or strengthen a view or attitude which is indeed contrary to that which was
            intended — which is to say, to a response of noncompliance — and can also increase resistance to persuasion.
            Some individuals might employ reverse psychology in a bid to exploit reactance for their benefit, in an attempt
            to influence someone to choose the opposite of what is being requested. Reactance can occur when an individual
            senses that someone is trying to compel them to do something; often the individual will offer resistance and
            attempt to extricate themselves from the situation.
            
            Some individuals are naturally high in reactance, a personality characteristic called trait reactance.
            """,

        Related = [
            new Guid("a9c7faa7-2368-4be5-9eda-a37ffd8f7ab1"), // REVERSE_PSYCHOLOGY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Reactance_(psychology)",
        ],
    };

    private static readonly Bias REVERSE_PSYCHOLOGY = new()
    {
        Id = new Guid("a9c7faa7-2368-4be5-9eda-a37ffd8f7ab1"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Reverse Psychology",
        Description =
            """
            # Reverse Psychology
            Reverse psychology is a technique involving the assertion of a belief or behavior that is opposite to the one desired, with the expectation
            that this approach will encourage the subject of the persuasion to do what is actually desired. This technique relies on the psychological
            phenomenon of reactance, in which a person has a negative emotional reaction to being persuaded, and thus chooses the option which is being
            advocated against. This may work especially well on a person who is resistant by nature, while direct requests work best for people who are
            compliant. The one being manipulated is usually unaware of what is really going on.
            """,

        Related = [
            new Guid("d3c2cb4b-ec29-4cf3-a485-9a98e9f1f223"), // REACTANCE
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Reverse_psychology",
        ],
    };

    private static readonly Bias SYSTEM_JUSTIFICATION = new()
    {
        Id = new Guid("755c8f9e-b172-4ff7-9797-9cc130bf4939"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "System Justification",
        Description =
            """
            # System Justification
            System justification theory is a theory within social psychology that system-justifying beliefs serve a psychologically
            palliative function. It proposes that people have several underlying needs, which vary from individual to individual,
            that can be satisfied by the defense and justification of the status quo, even when the system may be disadvantageous
            to certain people. People have epistemic, existential, and relational needs that are met by and manifest as ideological
            support for the prevailing structure of social, economic, and political norms. Need for order and stability, and thus
            resistance to change or alternatives, for example, can be a motivator for individuals to see the status quo as good,
            legitimate, and even desirable.
            
            According to system justification theory, people desire not only to hold favorable attitudes about themselves
            (ego-justification) and the groups to which they belong (group-justification), but also to hold positive attitudes
            about the overarching social structure in which they are entwined and find themselves obligated to (system-justification).
            This system-justifying motive sometimes produces the phenomenon known as out-group favoritism, an acceptance of inferiority
            among low-status groups and a positive image of relatively higher status groups. Thus, the notion that individuals are
            simultaneously supporters and victims of the system-instilled norms is a central idea in system justification theory.
            Additionally, the passive ease of supporting the current structure, when compared to the potential price (material,
            social, psychological) of acting out against the status quo, leads to a shared environment in which the existing social,
            economic, and political arrangements tend to be preferred. Alternatives to the status quo tend to be disparaged, and
            inequality tends to perpetuate.
            """,

        Related = [
            new Guid("b9e05a25-ac09-407d-8aee-f54a04decf0b"), // STATUS_QUO_BIAS
            new Guid("b1cc861b-f445-450b-9bdf-e9d222abdb4e"), // IN_GROUP_FAVORITISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/System_justification",
        ],
    };

    private static readonly Bias BELIEF_PERSEVERANCE = new()
    {
        Id = new Guid("bf8f304d-2e8e-4a90-a9c5-7bd56f6058a6"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Belief Perseverance",
        Description =
            """
            # Belief Perseverance
            Belief perseverance (also known as conceptual conservatism) is maintaining a belief despite new information that
            firmly contradicts it. Since rationality involves conceptual flexibility, belief perseverance is consistent with
            the view that human beings act at times in an irrational manner. Philosopher F.C.S. Schiller holds that belief
            perseverance "deserves to rank among the fundamental 'laws' of nature".
            
            If beliefs are strengthened after others attempt to present evidence debunking them, this is known as a backfire
            effect. There are psychological mechanisms by which backfire effects could potentially occur, but the evidence on
            this topic is mixed, and backfire effects are very rare in practice. A 2020 review of the scientific literature on
            backfire effects found that there have been widespread failures to replicate their existence, even under conditions
            that would be theoretically favorable to observing them. Due to the lack of reproducibility, as of 2020 most
            researchers believe that backfire effects are either unlikely to occur on the broader population level, or they
            only occur in very specific circumstances, or they do not exist. For most people, corrections and fact-checking
            are very unlikely to have a negative impact, and there is no specific group of people in which backfire effects
            have been consistently observed.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Belief_perseverance",
        ],
    };

    private static readonly Bias ENDOWMENT_EFFECT = new()
    {
        Id = new Guid("b81482f8-b2cf-4b86-a5a4-fcd29aee4e69"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Endowment Effect",
        Description =
            """
            # Endowment Effect
            In psychology and behavioral economics, the endowment effect, also known as divestiture aversion, is the finding
            that people are more likely to retain an object they own than acquire that same object when they do not own it.
            The endowment theory can be defined as "an application of prospect theory positing that loss aversion associated
            with ownership explains observed exchange asymmetries."
            
            This is typically illustrated in two ways. In a valuation paradigm, people's maximum willingness to pay (WTP) to
            acquire an object is typically lower than the least amount they are willing to accept (WTA) to give up that same
            object when they own it—even when there is no cause for attachment, or even if the item was only obtained minutes
            ago. In an exchange paradigm, people given a good are reluctant to trade it for another good of similar value.
            For example, participants first given a pen of equal expected value to that of a coffee mug were generally unwilling
            to trade, whilst participants first given the coffee mug were also unwilling to trade it for the pen.
            """,

        Related = [
            new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"), // LOSS_AVERSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Endowment_effect",
        ],
    };

    private static readonly Bias PROCESSING_DIFFICULTY_EFFECT = new()
    {
        Id = new Guid("4f61b9fa-146a-4b6e-b075-f0ba2ee0d9d0"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Processing Difficulty Effect",
        Description =
            """
            # Processing Difficulty Effect
            That information that takes longer to read and is thought about more (processed with more difficulty) is more easily remembered.
            """,

        Related = [
            new Guid("a4027640-1f52-4ff1-ae13-bd14a30d5b8d"), // LEVELS_OF_PROCESSING_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/List_of_cognitive_biases#Other_memory_biases",
        ],
    };

    private static readonly Bias PSEUDOCERTAINTY_EFFECT = new()
    {
        Id = new Guid("656c78c9-d75a-4c07-a80d-f3a5026f859c"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Pseudocertainty Effect",
        Description =
            """
            # Pseudocertainty Effect
            In prospect theory, the pseudocertainty effect is the tendency for people to perceive an outcome as certain while it is
            actually uncertain in multi-stage decision making. The evaluation of the certainty of the outcome in a previous stage of
            decisions is disregarded when selecting an option in subsequent stages. Not to be confused with certainty effect, the
            pseudocertainty effect was discovered from an attempt at providing a normative use of decision theory for the certainty
            effect by relaxing the cancellation rule.
            """,

        Related = [
            new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"), // LOSS_AVERSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Pseudocertainty_effect",
        ],
    };

    private static readonly Bias CERTAINTY_EFFECT = new()
    {
        Id = new Guid("ac7d745c-d66e-4886-87d7-ddaba349d4e8"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Certainty Effect",
        Description =
            """
            # Certainty Effect
            The certainty effect is the psychological effect resulting from the reduction of probability from certain to probable
            (Tversky & Kahneman 1986). It is an idea introduced in prospect theory. Normally a reduction in the probability of
            winning a reward (e.g., a reduction from 80% to 20% in the chance of winning a reward) creates a psychological effect
            such as displeasure to individuals, which leads to the perception of loss from the original probability thus favoring
            a risk-averse decision. However, the same reduction results in a larger psychological effect when it is done from
            certainty than from uncertainty.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Certainty_effect",
        ],
    };

    private static readonly Bias DISPOSITION_EFFECT = new()
    {
        Id = new Guid("4ecb0187-b2e2-446f-87e2-1e32f269e497"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Disposition Effect",
        Description =
            """
            # Disposition Effect
            The disposition effect is an anomaly discovered in behavioral finance. It relates to the tendency of investors to sell
            assets that have increased in value, while keeping assets that have dropped in value. Hersh Shefrin and Meir Statman
            identified and named the effect in their 1985 paper, which found that people dislike losing significantly more than they
            enjoy winning. The disposition effect has been described as one of the foremost vigorous actualities around individual
            investors because investors will hold stocks that have lost value yet sell stocks that have gained value.
            
            In 1979, Daniel Kahneman and Amos Tversky traced the cause of the disposition effect to the so-called "prospect theory".
            The prospect theory proposes that when an individual is presented with two equal choices, one having possible gains and
            the other with possible losses, the individual is more likely to opt for the former choice even though both would yield
            the same economic result.
            
            The disposition effect can be minimized by means of a mental approach called "hedonic framing". For example, individuals
            can try to force themselves to think of a single large gain as a number of smaller gains, to think of a number of smaller
            losses as a single large loss, to think of the combination of a major gain and a minor loss as a net minor gain, and, in
            the case of a combined major loss and minor gain, to think of the two separately. In a similar manner, investors show a
            reversed disposition effect when they are framed to think of their investment as progress towards a specific investment
            goal rather than a generic investment.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Disposition_effect",
        ],
    };

    private static readonly Bias ZERO_RISK_BIAS = new()
    {
        Id = new Guid("77553998-bfa7-450e-acd9-586a55064302"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Zero-Risk Bias",
        Description =
            """
            # Zero-Risk Bias
            Zero-risk bias is a tendency to prefer the complete elimination of risk in a sub-part over alternatives with greater
            overall risk reduction. It often manifests in cases where decision makers address problems concerning health, safety,
            and the environment. Its effect on decision making has been observed in surveys presenting hypothetical scenarios.
            
            Zero-risk bias is based on the way people feel better if a risk is eliminated instead of being merely mitigated.
            Scientists identified a zero-risk bias in responses to a questionnaire about a hypothetical cleanup scenario involving
            two hazardous sites X and Y, with X causing 8 cases of cancer annually and Y causing 4 cases annually. The respondents
            ranked three cleanup approaches: two options each reduced the total number of cancer cases by 6, while the third reduced
            the number by 5 and eliminated the cases at site Y. While the latter option featured the worst reduction overall, 42% of
            the respondents ranked it better than at least one of the other options. This conclusion resembled one from an earlier
            economics study that found people were willing to pay high costs to eliminate a risk. It has a normative justification
            since once risk is eliminated, people would have less to worry about and such removal of worry also has utility. It is
            also driven by our preference for winning much more than losing as well as the old instead of the new way, all of which
            cloud the way the world is viewed.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Zero-risk_bias",
        ],
    };

    private static readonly Bias UNIT_BIAS = new()
    {
        Id = new Guid("ff43a9e2-7dde-47ca-a3ef-5a9c2d3117c9"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Unit Bias",
        Description =
            """
            # Unit Bias
            The standard suggested amount of consumption (e.g., food serving size) is perceived to be appropriate, and a person would
            consume it all even if it is too much for this particular person.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/List_of_cognitive_biases#Other",
        ],
    };

    private static readonly Bias IKEA_EFFECT = new()
    {
        Id = new Guid("565616dc-ed84-42af-b9cc-6fa666cc5d66"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "IKEA Effect",
        Description =
            """
            # IKEA Effect
            The IKEA effect is a cognitive bias in which consumers place a disproportionately high value on products they
            partially created. The name refers to Swedish manufacturer and furniture retailer IKEA, which sells many items
            of furniture that require assembly. A 2011 study found that subjects were willing to pay 63% more for furniture
            they had assembled themselves than for equivalent pre-assembled items.
            """,

        Related = [
            new Guid("b9c06da1-d2eb-4871-8159-a2a6d25e9eff"), // DUNNING_KRUGER_EFFECT
            new Guid("30deb7d6-4019-4fef-9823-8d8126e54f0a"), // ESCALATION_OF_COMMITMENT
            new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"), // OMISSION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/IKEA_effect",
        ],
    };

    private static readonly Bias LOSS_AVERSION = new()
    {
        Id = new Guid("ad3ed908-c56e-411b-a130-8af8574ff67b"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Loss Aversion",
        Description =
            """
            # Loss Aversion
            In cognitive science and behavioral economics, loss aversion refers to a cognitive bias in which the same situation
            is perceived as worse if it is framed as a loss, rather than a gain. It should not be confused with risk aversion,
            which describes the rational behavior of valuing an uncertain outcome at less than its expected value.
            
            ## Application
            In marketing, the use of trial periods and rebates tries to take advantage of the buyer's tendency to value the good
            more after the buyer incorporates it in the status quo. In past behavioral economics studies, users participate up
            until the threat of loss equals any incurred gains. Methods established by Botond Kőszegi and Matthew Rabin in
            experimental economics illustrates the role of expectation, wherein an individual's belief about an outcome can
            create an instance of loss aversion, whether or not a tangible change of state has occurred.
            
            Whether a transaction is framed as a loss or as a gain is important to this calculation. The same change in price
            framed differently, for example as a $5 discount or as a $5 surcharge avoided, has a significant effect on
            consumer behavior. Although traditional economists consider this "endowment effect", and all other effects of
            loss aversion, to be completely irrational, it is important to the fields of marketing and behavioral finance.
            Users in behavioral and experimental economics studies decided to cease participation in iterative money-making
            games when the threat of loss was close to the expenditure of effort, even when the user stood to further their
            gains. Loss aversion coupled with myopia has been shown to explain macroeconomic phenomena, such as the equity
            premium puzzle. Loss aversion to kinship is an explanation for aversion to inheritance tax.
            """,

        Related = [
            new Guid("b81482f8-b2cf-4b86-a5a4-fcd29aee4e69"), // ENDOWMENT_EFFECT
            new Guid("ef521fbb-c20b-47c9-87f8-a571a06a03eb"), // NEGATIVITY_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Loss_aversion",
        ],
    };

    private static readonly Bias GENERATION_EFFECT = new()
    {
        Id = new Guid("af442ab1-ffc5-404c-9ee8-3497fe6992ec"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Generation Effect",
        Description =
            """
            # Generation Effect
            The generation effect is a phenomenon whereby information is better remembered if it is generated from one's own
            mind rather than simply read. Researchers have struggled to fully explain why generated information is better
            recalled than read information, as no single explanation has been comprehensive.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Generation_effect",
        ],
    };

    private static readonly Bias ESCALATION_OF_COMMITMENT = new()
    {
        Id = new Guid("30deb7d6-4019-4fef-9823-8d8126e54f0a"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Escalation of Commitment",
        Description =
            """
            # Escalation of Commitment
            Escalation of commitment is a human behavior pattern in which an individual or group facing increasingly negative
            outcomes from a decision, action, or investment nevertheless continue the behavior instead of altering course.
            The actor maintains behaviors that are irrational, but align with previous decisions and actions.
            
            Economists and behavioral scientists use a related term, sunk-cost fallacy, to describe the justification of
            increased investment of money or effort in a decision, based on the cumulative prior investment ("sunk cost")
            despite new evidence suggesting that the future cost of continuing the behavior outweighs the expected benefit.
            
            In sociology, irrational escalation of commitment or commitment bias describe similar behaviors. The phenomenon
            and the sentiment underlying them are reflected in such proverbial images as "throwing good money after bad",
            or "In for a penny, in for a pound", or "It's never the wrong time to make the right decision", or "If you find
            yourself in a hole, stop digging."
            """,

        Related = [
            new Guid("9a2d58f5-bbf1-4b34-8e1b-f9bcd8814f05"), // SUNK_COST_FALLACY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Escalation_of_commitment",
        ],
    };

    private static readonly Bias SUNK_COST_FALLACY = new()
    {
        Id = new Guid("9a2d58f5-bbf1-4b34-8e1b-f9bcd8814f05"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Sunk Cost Fallacy",
        Description =
            """
            # Sunk Cost Fallacy
            The Misconception: You make rational decisions based on the future value of objects, investments and experiences.
            The Truth: Your decisions are tainted by the emotional investments you accumulate, and the more you invest in
            something the harder it becomes to abandon it.
            
            Example: R&D costs. Once spent, such costs are sunk and should have no effect on future pricing decisions. So a
            pharmaceutical company's attempt to justify high prices because of the need to recoup R&D expenses is fallacious.
            The company will charge market prices whether R&D had cost one dollar or one million dollars. However, R&D costs,
            and the ability to recoup those costs, are a factor in deciding whether to spend the money on R&D. It’s important
            to distinguish that while justifying high prices on past R&D is a fallacy, raising prices in order to finance
            future R&D is not.
            
            Counterpoint: It is sometimes not that simple. In a broad range of situations, it is rational for people to condition
            behavior on sunk costs, because of informational content, reputational concerns, or financial and time constraints.
            """,

        Related = [
            new Guid("30deb7d6-4019-4fef-9823-8d8126e54f0a"), // ESCALATION_OF_COMMITMENT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Sunk_cost#Fallacy_effect",
        ],
    };

    private static readonly Bias IDENTIFIABLE_VICTIM_EFFECT = new()
    {
        Id = new Guid("0c18a8bd-5e5f-4cf0-a90e-47dd7a421035"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Identifiable Victim Effect",
        Description =
            """
            # Identifiable Victim Effect
            The identifiable victim effect is the tendency of individuals to offer greater aid when a specific, identifiable
            person ("victim") is observed under hardship, as compared to a large, vaguely defined group with the same need.
            
            The identifiable victim effect has two components. People are more inclined to help an identified victim than an
            unidentified one, and people are more inclined to help a single identified victim than a group of identified victims.
            Although helping an identified victim may be commendable, the identifiable victim effect is considered a cognitive
            bias. From a consequentialist point of view, the cognitive error is the failure to offer N times as much help to N
            unidentified victims.
            
            The identifiable victim effect has a mirror image that is sometimes called the identifiable perpetrator effect.
            Research has shown that individuals are more inclined to mete out punishment, even at their own expense, when they
            are punishing a specific, identified perpetrator.
            
            The conceptualization of the identifiable victim effect as it is known today is commonly attributed to American
            economist Thomas Schelling. He wrote that harm to a particular person invokes "anxiety and sentiment, guilt and awe,
            responsibility and religion, [but]…most of this awesomeness disappears when we deal with statistical death".
            
            Historical figures from Joseph Stalin to Mother Teresa are credited with statements that epitomize the identifiable
            victim effect. The remark "One death is a tragedy; a million deaths is a statistic" is widely, although probably
            incorrectly, attributed to Stalin. The remark "If I look at the mass I will never act. If I look at the one, I
            will," is attributed to Mother Teresa.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Identifiable_victim_effect",
        ],
    };

    private static readonly Bias APPEAL_TO_NOVELTY = new()
    {
        Id = new Guid("2d57f4d6-e599-4738-812a-c12cef877779"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Appeal to Novelty",
        Description =
            """
            # Appeal to Novelty
            The appeal to novelty (also called appeal to modernity or argumentum ad novitatem) is a fallacy in which one
            prematurely claims that an idea or proposal is correct or superior, exclusively because it is new and modern.
            In a controversy between status quo and new inventions, an appeal to novelty argument is not in itself a valid
            argument. The fallacy may take two forms: overestimating the new and modern, prematurely and without investigation
            assuming it to be best-case, or underestimating status quo, prematurely and without investigation assuming it to
            be worst-case.
            
            Investigation may prove these claims to be true, but it is a fallacy to prematurely conclude this only from the
            general claim that all novelty is good.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Appeal_to_novelty",
        ],
    };

    private static readonly Bias HYPERBOLIC_DISCOUNTING = new()
    {
        Id = new Guid("19a483d0-2c8f-486f-bf9e-619d0df4c916"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Hyperbolic Discounting",
        Description =
            """
            # Hyperbolic Discounting
            Given two similar rewards, humans show a preference for one that arrives in a more prompt timeframe. Humans are said
            to discount the value of the later reward, by a factor that increases with the length of the delay. In the financial
            world, this process is normally modeled in the form of exponential discounting, a time-consistent model of discounting.
            Many psychological studies have since demonstrated deviations in instinctive preference from the constant discount rate
            assumed in exponential discounting. Hyperbolic discounting is an alternative mathematical model that agrees more closely
            with these findings.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Hyperbolic_discounting",
        ],
    };

    private static readonly Bias RISK_COMPENSATION = new()
    {
        Id = new Guid("10fcc295-02b6-4dbf-b655-f5bcff3c1ca7"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Risk Compensation",
        Description =
            """
            # Risk Compensation
            Risk compensation is a theory which suggests that people typically adjust their behavior in response to perceived
            levels of risk, becoming more careful where they sense greater risk and less careful if they feel more protected.
            Although usually small in comparison to the fundamental benefits of safety interventions, it may result in a lower
            net benefit than expected or even higher risks.
            
            By way of example, it has been observed that motorists drove closer to the vehicle in front when the vehicles were
            fitted with anti-lock brakes. There is also evidence that the risk compensation phenomenon could explain the failure
            of condom distribution programs to reverse HIV prevalence and that condoms may foster disinhibition, with people
            engaging in risky sex both with and without condoms.
            
            By contrast, shared space is an urban street design method which consciously aims to increase the level of perceived
            risk and uncertainty, thereby slowing traffic and reducing the number and seriousness of injuries.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Risk_compensation",
        ],
    };

    private static readonly Bias EFFORT_JUSTIFICATION = new()
    {
        Id = new Guid("cff2c74d-a160-4a90-b0b2-10f145b804cb"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Effort Justification",
        Description =
            """
            # Effort Justification
            Effort justification is an idea and paradigm in social psychology stemming from Leon Festinger's theory of cognitive
            dissonance. Effort justification is a person's tendency to attribute the value of an outcome they put effort into
            achieving as greater than the objective value of the outcome.
            
            Cognitive dissonance theory explains changes in people's attitudes or beliefs as the result of an attempt to reduce a
            dissonance (discrepancy) between contradicting ideas or cognitions. In the case of effort justification, there is a
            dissonance between the amount of effort exerted into achieving a goal or completing a task (high effort equalling high
            "cost") and the subjective reward for that effort (lower than was expected for such an effort). By adjusting and increasing
            one's attitude or subjective value of the goal, this dissonance is resolved.
            
            One of the first and most classic examples of effort justification is Aronson and Mills's study. A group of young women
            who volunteered to join a discussion group on the topic of the psychology of sex were asked to do a small reading test
            to make sure they were not too embarrassed to talk about sexual-related topics with others. The mild-embarrassment
            condition subjects were asked to read aloud a list of sex-related words such as prostitute or virgin. The
            severe-embarrassment condition subjects were asked to read aloud a list of highly sexual words (e.g. fuck, cock) and
            to read two vivid descriptions of sexual activity taken from contemporary novels. All subjects then listened to a
            recording of a discussion about sexual behavior in animals which was dull and unappealing. When asked to rate the
            group and its members, control and mild-embarrassment groups did not differ, but the severe-embarrassment group's
            ratings were significantly higher. This group, whose initiation process was more difficult (embarrassment equalling
            effort), had to increase their subjective value of the discussion group to resolve the dissonance.
            """,

        Related = [
            new Guid("565616dc-ed84-42af-b9cc-6fa666cc5d66"), // IKEA_EFFECT
            new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"), // OMISSION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Effort_justification",
        ],
    };

    private static readonly Bias TRAIT_ASCRIPTION_BIAS = new()
    {
        Id = new Guid("4727839d-64c5-4ba4-b044-6b09f14d5a34"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Trait Ascription Bias",
        Description =
            """
            # Trait Ascription Bias
            Trait ascription bias is the tendency for people to view themselves as relatively variable in terms of personality,
            behavior and mood while viewing others as much more predictable in their personal traits across different situations.
            More specifically, it is a tendency to describe one's own behaviour in terms of situational factors while preferring
            to describe another's behaviour by ascribing fixed dispositions to their personality. This may occur because peoples'
            own internal states are more readily observable and available to them than those of others.
            
            This attributional bias intuitively plays a role in the formation and maintenance of stereotypes and prejudice,
            combined with the negativity effect. However, trait ascription and trait-based models of personality remain
            contentious in modern psychology and social science research. Trait ascription bias refers to the situational
            and dispositional evaluation and description of personality traits on a personal level. A similar bias on the
            group level is called the outgroup homogeneity bias. 
            """,

        Related = [
            new Guid("ef521fbb-c20b-47c9-87f8-a571a06a03eb"), // NEGATIVITY_BIAS
            new Guid("2cb8514a-c4a2-4cf6-aed7-72d7870ace84"), // BARNUM_EFFECT
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("a44a6bcf-b2b8-47f1-84e0-d740af56aa1e"), // ILLUSION_OF_ASYMMETRIC_INSIGHT
            new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"), // ILLUSORY_SUPERIORITY
            new Guid("80f9b496-798a-4a1e-a426-815f23b8698e"), // INTROSPECTION_ILLUSION
            new Guid("5ae6f7ec-3be2-47ad-ad75-0ed114f97fe0"), // NAÏVE_CYNICISM
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
            
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Trait_ascription_bias",
        ],
    };

    private static readonly Bias DEFENSIVE_ATTRIBUTION_HYPOTHESIS = new()
    {
        Id = new Guid("5a973490-c19a-43c7-8a01-a26e0d05f275"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Defensive Attribution Hypothesis",
        Description =
            """
            # Defensive Attribution Hypothesis
            The defensive attribution hypothesis (or bias, theory, or simply defensive attribution) is a social
            psychological term where an observer attributes the causes for a mishap to minimize their fear of
            being a victim or a cause in a similar situation. The attributions of blame are negatively correlated
            to similarities between the observer and the people involved in the mishap, i.e. more responsibility
            is attributed to the people involved who are dissimilar to the observer. Assigning responsibility
            allows the observer to believe that the mishap was controllable and thus preventable.
            
            A defensive attribution may also be used to protect the person's self-esteem if, despite everything,
            the mishap does occur, because blame can be assigned to the "other" (person or situation). The use of
            defensive attributions is considered a cognitive bias because an individual will change their beliefs
            about a situation based upon their motivations or desires rather than the factual characteristics of
            the situation.
            
            ## Sexual assault
            Researchers examining sexual assault have consistently found that male participants blamed rapists less
            than female participants did, and that male participants blamed the rape victims more than female
            participants did. These findings support Shaver's similarity-responsibility hypothesis: male participants,
            who are personally similar to (male) rapists, blame rapists less than female participants who are dissimilar
            to rapists. On the other hand, female participants, who are personally similar to (female) rape victims,
            blame the victims less than male participants.
            """,

        Related = [
            new Guid("efceb4b1-e19f-4997-9f96-1657bb269b2d"), // ATTRIBUTION_BIAS
            new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"), // OMISSION_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Defensive_attribution_hypothesis",
        ],
    };

    private static readonly Bias FUNDAMENTAL_ATTRIBUTION_ERROR = new()
    {
        Id = new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Fundamental Attribution Error",
        Description =
            """
            # Fundamental Attribution Error
            In social psychology, the fundamental attribution error (FAE) [a] is a cognitive attribution bias in which
            observers underemphasize situational and environmental factors for the behavior of an actor while overemphasizing
            dispositional or personality factors. In other words, observers tend to overattribute the behaviors of others to
            their personality (e.g., he is late because he's selfish) and underattribute them to the situation or context
            (e.g., he is late because he got stuck in traffic). Although personality traits and predispositions are considered
            to be observable facts in psychology, the fundamental attribution error is an error because it misinterprets their
            effects.
            
            The group attribution error (GAE) is identical to the fundamental attribution error, where the bias is shown between
            members of different groups rather than different individuals. The ultimate attribution error is a derivative of the
            FAE and GAE relating to the actions of groups, with an additional layer of self-justification relating to whether
            the action of an individual is representative of the wider group.
            """,

        Related = [
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
            new Guid("577e79e5-0a53-4c4c-a2ea-d039870bfbb9"), // GROUP_ATTRIBUTION_ERROR
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Fundamental_attribution_error",
        ],
    };

    private static readonly Bias ILLUSION_OF_CONTROL = new()
    {
        Id = new Guid("7fce783e-2120-4aad-9805-2c2a2b937b7d"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Illusion of Control",
        Description =
            """
            # Illusion of Control
            The illusion of control is the tendency for people to overestimate their ability to control events. It was named
            by U.S. psychologist Ellen Langer and is thought to influence gambling behavior and belief in the paranormal.
            
            It is the tendency for people to overestimate their ability to control events, for example, when someone feels a
            sense of control over outcomes that they demonstrably do not influence. The illusion might arise because a person
            lacks direct introspective insight into whether they are in control of events. This has been called the introspection
            illusion. Instead, they may judge their degree of control by a process which is often unreliable. As a result, they see
            themselves as responsible for events to which there is little or no causal link. For example, in one study, college
            students were in a virtual reality setting to treat a fear of heights using an elevator. Those who were told that they
            had control, yet had none, felt as though they had as much control as those who actually did have control over the
            elevator. Those who were led to believe they did not have control said they felt as though they had little control.
            
            The illusion is more common in familiar situations, and in situations where the person knows the desired outcome.
            Feedback that emphasizes success rather than failure can increase the effect, while feedback that emphasizes failure
            can decrease or reverse the effect. The illusion is weaker for depressed individuals and is stronger when individuals
            have an emotional need to control the outcome. The illusion is strengthened by stressful and competitive situations,
            including financial trading. Although people are likely to overestimate their control when the situations are heavily
            chance-determined, they also tend to underestimate their control when they actually have it, which runs contrary to
            some theories of the illusion and its adaptiveness. People also showed a higher illusion of control when they were
            allowed to become familiar with a task through practice trials, make their choice before the event happens like
            with throwing dice, and when they can make their choice rather than have it made for them with the same odds.
            People are more likely to show control when they have more answers right at the beginning than at the end,
            even when the people had the same number of correct answers.
            
            Being in a position of power enhances the illusion of control, which may lead to overreach in risk taking.
            """,

        Related = [
            new Guid("80f9b496-798a-4a1e-a426-815f23b8698e"), // INTROSPECTION_ILLUSION
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Illusion_of_control",
        ],
    };

    private static readonly Bias ACTOR_OBSERVER_BIAS = new()
    {
        Id = new Guid("5da6dcf4-ed01-4e14-99b0-7a624b16cf17"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Actor-Observer Bias",
        Description =
            """
            # Actor-Observer Bias
            Actor–observer asymmetry (also actor–observer bias) is a bias one makes when forming attributions about the behavior
            of others or themselves. When people judge their own behavior, they are more likely to attribute their actions to the
            particular situation than to their personality. However, when an observer is explaining the behavior of another person,
            they are more likely to attribute this behavior to the actors' personality rather than to situational factors.
            
            Sometimes the actor–observer asymmetry is defined as the fundamental attribution error, which is when people tend to
            explain behavior on the internal, personal characteristics rather than the external factors or situational influences.
            
            The specific hypothesis of an actor–observer asymmetry in attribution was originally proposed by Edward Jones and
            Richard Nisbett, where they said that "actors tend to attribute the causes of their behavior to stimuli inherent
            in the situation, while observers tend to attribute behavior to stable dispositions of the actor". Supported by
            initial evidence, the hypothesis was long held as firmly established. However, a meta-analysis of all the published
            tests of the hypothesis between 1971 and 2004 found that there was no actor–observer asymmetry of the sort that had
            been previously proposed. The author of the study interpreted this result not so much as proof that actors and observers
            explained behavior exactly the same way but as evidence that the original hypothesis was fundamentally flawed in the way
            it framed people's explanations of behavior as attributions to either stable dispositions or the situation.
            
            Considerations of actor–observer differences can be found in other disciplines as well, such as philosophy (e.g.
            privileged access, incorrigibility), management studies, artificial intelligence, semiotics, anthropology, and
            political science.
            """,

        Related = [
            new Guid("ca2d4f1f-924f-44ae-886b-19240cf2c8c0"), // ULTIMATE_ATTRIBUTION_ERROR
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("b57a862b-b490-4d61-96b8-29d548c2eee4"), // POSITIVITY_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Actor%E2%80%93observer_asymmetry",
        ],
    };

    private static readonly Bias SELF_SERVING_BIAS = new()
    {
        Id = new Guid("923ee6c0-2f9c-47fc-a570-339190c1a250"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Self-Serving Bias",
        Description =
            """
            # Self-Serving Bias
            A self-serving bias is any cognitive or perceptual process that is distorted by the need to maintain and enhance
            self-esteem, or the tendency to perceive oneself in an overly favorable manner. It is the belief that individuals
            tend to ascribe success to their own abilities and efforts, but ascribe failure to external factors. When individuals
            reject the validity of negative feedback, focus on their strengths and achievements but overlook their faults and
            failures, or take more credit for their group's work than they give to other members, they are protecting their
            self-esteem from threat and injury. These cognitive and perceptual tendencies perpetuate illusions and error, but
            they also serve the self's need for esteem. For example, a student who attributes earning a good grade on an exam
            to their own intelligence and preparation but attributes earning a poor grade to the teacher's poor teaching ability
            or unfair test questions might be exhibiting a self-serving bias. Studies have shown that similar attributions are
            made in various situations, such as the workplace, interpersonal relationships, sports, and consumer decisions.
            
            Both motivational processes (i.e. self-enhancement, self-presentation) and cognitive processes (i.e. locus of control,
            self-esteem) influence the self-serving bias. There are both cross-cultural (i.e. individualistic and collectivistic
            culture differences) and special clinical population (i.e. depression) considerations within the bias. Much of the
            research on the self-serving bias has used participant self-reports of attribution based on experimental manipulation
            of task outcomes or in naturalistic situations. Some more modern research, however, has shifted focus to physiological
            manipulations, such as emotional inducement and neural activation, in an attempt to better understand the biological
            mechanisms that contribute to the self-serving bias.
            """,

        Related = [
            new Guid("b9c06da1-d2eb-4871-8159-a2a6d25e9eff"), // DUNNING_KRUGER_EFFECT
            new Guid("f1570784-f8ec-46fd-8bb8-763aef31a04a"), // FUNDAMENTAL_ATTRIBUTION_ERROR
            new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"), // ILLUSORY_SUPERIORITY
            new Guid("ad32d669-fc79-44c9-a570-609e1ccdc799"), // OMISSION_BIAS
            new Guid("7bf44f8f-a4b0-404c-8f15-8ca6e3322d32"), // OPTIMISM_BIAS
            new Guid("b821d449-64e5-4c0a-9d5a-3fda609a9b86"), // OVERCONFIDENCE_EFFECT
            new Guid("e36f82b7-43dd-4073-99d9-c33073007185"), // MORAL_CREDENTIAL_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Self-serving_bias",
        ],
    };

    private static readonly Bias OPTIMISM_BIAS = new()
    {
        Id = new Guid("7bf44f8f-a4b0-404c-8f15-8ca6e3322d32"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Optimism Bias",
        Description =
            """
            # Optimism Bias
            Optimism bias or optimistic bias is a cognitive bias that causes someone to believe that they themselves
            are less likely to experience a negative event. It is also known as unrealistic optimism or comparative optimism.
            
            Optimism bias is common and transcends gender, ethnicity, nationality, and age. However, autistic people are less
            susceptible to this kind of biases. Optimistic biases have also reported in other animals, such as rats and birds.
            
            Four factors can cause a person to be optimistically biased: their desired end state, their cognitive mechanisms,
            the information they have about themselves versus others, and overall mood. The optimistic bias is seen in a number
            of situations. For example: people believing that they are less at risk of being a crime victim, smokers believing
            that they are less likely to contract lung cancer or disease than other smokers, first-time bungee jumpers believing
            that they are less at risk of an injury than other jumpers, or traders who think they are less exposed to potential
            losses in the markets.
            
            Although the optimism bias occurs for both positive events (such as believing oneself to be more financially successful
            than others) and negative events (such as being less likely to have a drinking problem), there is more research and
            evidence suggesting that the bias is stronger for negative events (the valence effect). Different consequences result
            from these two types of events: positive events often lead to feelings of well being and self-esteem, while negative
            events lead to consequences involving more risk, such as engaging in risky behaviors and not taking precautionary
            measures for safety.
            """,

        Related = [
            new Guid("67041978-ac8e-4254-ae2c-509e7301619f"), // PESSIMISM_BIAS
            new Guid("7fce783e-2120-4aad-9805-2c2a2b937b7d"), // ILLUSION_OF_CONTROL
            new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"), // ILLUSORY_SUPERIORITY
            new Guid("ef521fbb-c20b-47c9-87f8-a571a06a03eb"), // NEGATIVITY_BIAS
            new Guid("b57a862b-b490-4d61-96b8-29d548c2eee4"), // POSITIVITY_EFFECT
            new Guid("923ee6c0-2f9c-47fc-a570-339190c1a250"), // SELF_SERVING_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Optimism_bias",
        ],
    };

    private static readonly Bias EGOCENTRIC_BIAS = new()
    {
        Id = new Guid("953746dc-ce10-4e3b-8f9e-9246de63f531"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Egocentric Bias",
        Description =
            """
            # Egocentric Bias
            Egocentric bias is the tendency to rely too heavily on one's own perspective and/or have a higher opinion of
            oneself than reality. It appears to be the result of the psychological need to satisfy one's ego and to be
            advantageous for memory consolidation. Research has shown that experiences, ideas, and beliefs are more easily
            recalled when they match one's own, causing an egocentric outlook. Michael Ross and Fiore Sicoly first identified
            this cognitive bias in their 1979 paper, "Egocentric Biases in Availability and Attribution". Egocentric bias is
            referred to by most psychologists as a general umbrella term under which other related phenomena fall.
            
            The effects of egocentric bias can differ based on personal characteristics, such as age and the number of
            languages one speaks. Thus far, there have been many studies focusing on specific implications of egocentric
            bias in different contexts. Research on collaborative group tasks have emphasized that people view their own
            contributions differently than they view that of others. Other areas of research have been aimed at studying
            how mental health patients display egocentric bias, and at the relationship between egocentric bias and voter
            distribution. These types of studies surrounding egocentric bias usually involve written or verbal questionnaires,
            based on the subject's personal life or their decision in various hypothetical scenarios. 
            """,

        Related = [
            new Guid("923ee6c0-2f9c-47fc-a570-339190c1a250"), // SELF_SERVING_BIAS
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Egocentric_bias",
        ],
    };

    private static readonly Bias DUNNING_KRUGER_EFFECT = new()
    {
        Id = new Guid("b9c06da1-d2eb-4871-8159-a2a6d25e9eff"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Dunning-Kruger Effect", 
        Description =
            """
            # Dunning-Kruger Effect
            The Dunning–Kruger effect is a cognitive bias in which people with limited competence in a particular domain overestimate
            their abilities. It was first described by Justin Kruger and David Dunning in 1999. Some researchers also include the
            opposite effect for high performers: their tendency to underestimate their skills. In popular culture, the Dunning–Kruger
            effect is often misunderstood as a claim about general overconfidence of people with low intelligence instead of specific
            overconfidence of people unskilled at a particular task.
            """,

        Related = [
            new Guid("f8fd4635-69b3-47be-8243-8c7c6749cae2"), // ILLUSORY_SUPERIORITY
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Dunning%E2%80%93Kruger_effect",
        ],
    };

    private static readonly Bias HARD_EASY_EFFECT = new()
    {
        Id = new Guid("07f0c252-1d97-4207-8000-8e4d8800fb42"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Hard-Easy Effect",
        Description =
            """
            # Hard-Easy Effect
            The hard–easy effect is a cognitive bias that manifests itself as a tendency to overestimate the probability of
            one's success at a task perceived as hard, and to underestimate the likelihood of one's success at a task perceived
            as easy. The hard-easy effect takes place, for example, when individuals exhibit a degree of underconfidence in
            answering relatively easy questions and a degree of overconfidence in answering relatively difficult questions.
            "Hard tasks tend to produce overconfidence but worse-than-average perceptions," reported Katherine A. Burson,
            Richard P. Larrick, and Jack B. Soll in a 2005 study, "whereas easy tasks tend to produce underconfidence and
            better-than-average effects."
            
            The hard-easy effect falls under the umbrella of "social comparison theory", which was originally formulated by
            Leon Festinger in 1954. Festinger argued that individuals are driven to evaluate their own opinions and abilities
            accurately, and social comparison theory explains how individuals carry out those evaluations by comparing themselves
            to others.
            """,

        Related = [
            new Guid("b821d449-64e5-4c0a-9d5a-3fda609a9b86"), // OVERCONFIDENCE_EFFECT
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/Hard%E2%80%93easy_effect",
        ],
    };

    private static readonly Bias FALSE_CONSENSUS_EFFECT = new()
    {
        Id = new Guid("bc0dc6d3-5115-4def-91ae-a38aebed185e"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "False Consensus Effect",
        Description =
            """
            # False Consensus Effect
            In psychology, the false consensus effect, also known as consensus bias, is a pervasive cognitive bias that causes
            people to "see their own behavioral choices and judgments as relatively common and appropriate to existing
            circumstances". In other words, they assume that their personal qualities, characteristics, beliefs, and actions
            are relatively widespread through the general population.
            
            This false consensus is significant because it increases self-esteem (overconfidence effect). It can be derived from
            a desire to conform and be liked by others in a social environment. This bias is especially prevalent in group
            settings where one thinks the collective opinion of their own group matches that of the larger population. Since
            the members of a group reach a consensus and rarely encounter those who dispute it, they tend to believe that
            everybody thinks the same way. The false-consensus effect is not restricted to cases where people believe that
            their values are shared by the majority, but it still manifests as an overestimate of the extent of their belief.
            
            Additionally, when confronted with evidence that a consensus does not exist, people often assume that those who
            do not agree with them are defective in some way. There is no single cause for this cognitive bias; the
            availability heuristic, self-serving bias, and naïve realism have been suggested as at least partial underlying
            factors. The bias may also result, at least in part, from non-social stimulus-reward associations. Maintenance
            of this cognitive bias may be related to the tendency to make decisions with relatively little information.
            When faced with uncertainty and a limited sample from which to make decisions, people often "project"
            themselves onto the situation. When this personal knowledge is used as input to make generalizations,
            it often results in the false sense of being part of the majority.
            """,

        Related = [
            new Guid("b821d449-64e5-4c0a-9d5a-3fda609a9b86"), // OVERCONFIDENCE_EFFECT
            new Guid("d749ce96-32f3-4c3d-86f7-26ff4edabe4a"), // AVAILABILITY_HEURISTIC
            new Guid("923ee6c0-2f9c-47fc-a570-339190c1a250"), // SELF_SERVING_BIAS
            new Guid("f0ad095e-8e9c-4bfb-855e-11fb5dd58cea"), // NAÏVE_REALISM
        ],
        Links =
        [
            "https://en.wikipedia.org/wiki/False_consensus_effect",
        ],
    };

    private static readonly Bias THIRD_PERSON_EFFECT = new()
    {
        Id = new Guid("b9186d75-3362-4dd4-a3ec-4345a04161c9"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Third-Person Effect",
        Description =
            """
            # Third-Person Effect
            The third-person effect hypothesis predicts that people tend to perceive that mass media messages have a greater
            effect on others than on themselves, based on personal biases. The third-person effect manifests itself through
            an individual's overestimation of the effect of a mass communicated message on the generalized other, or an
            underestimation of the effect of a mass communicated message on themselves.
            
            These types of perceptions stem from a self-motivated social desirability (not feeling influenced by mass messages
            promotes self-esteem), a social-distance corollary (choosing to dissociate oneself from the others who may be
            influenced), and a perceived exposure to a message (others choose to be influenced by persuasive communication).
            Other names for the effect are "Third-person perception" and "Web Third-person effect". From 2015, the effect is
            named "Web Third-person effect" when it is verified in social media, media websites, blogs and in websites in general.
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Third-person_effect",
        ],
    };

    private static readonly Bias SOCIAL_DESIRABILITY_BIAS = new()
    {
        Id = new Guid("a378b725-8bf9-4213-a875-326426d5c759"),
        Category = BiasCategory.NEED_TO_ACT_FAST,
        Name = "Social-Desirability Bias",
        Description =
            """
            # Social-Desirability Bias
            In social science research, social-desirability bias is a type of response bias that is the tendency of survey
            respondents to answer questions in a manner that will be viewed favorably by others. It can take the form of
            over-reporting "good behavior" or under-reporting "bad", or undesirable behavior. The tendency poses a serious
            problem with conducting research with self-reports. This bias interferes with the interpretation of average
            tendencies as well as individual differences.
            
            Topics where socially desirable responding (SDR) is of special concern are self-reports of abilities, personality,
            sexual behavior, and drug use. When confronted with the question "How often do you masturbate?," for example,
            respondents may be pressured by a social taboo against masturbation, and either under-report the frequency or
            avoid answering the question. Therefore, the mean rates of masturbation derived from self-report surveys are
            likely to be severely underestimated.
            
            When confronted with the question, "Do you use drugs/illicit substances?" the respondent may be influenced by
            the fact that controlled substances, including the more commonly used marijuana, are generally illegal.
            Respondents may feel pressured to deny any drug use or rationalize it, e.g. "I only smoke marijuana when my
            friends are around." The bias can also influence reports of number of sexual partners. In fact, the bias
            may operate in opposite directions for different subgroups: Whereas men tend to inflate the numbers, women
            tend to underestimate theirs. In either case, the mean reports from both groups are likely to be distorted
            by social desirability bias.
            
            Other topics that are sensitive to social-desirability bias include:
            
            - Self-reported personality traits will correlate strongly with social desirability bias
            - Personal income and earnings, often inflated when low and deflated when high
            - Feelings of low self-worth and/or powerlessness, often denied
            - Excretory functions, often approached uncomfortably, if discussed at all
            - Compliance with medicinal-dosing schedules, often inflated
            - Family planning, including use of contraceptives and abortion
            - Religion, often either avoided or uncomfortably approached
            - Patriotism, either inflated or, if denied, done so with a fear of other party's judgment
            - Bigotry and intolerance, often denied, even if it exists within the responder
            - Intellectual achievements, often inflated
            - Physical appearance, either inflated or deflated
            - Acts of real or imagined physical violence, often denied
            - Indicators of charity or "benevolence," often inflated
            - Illegal acts, often denied
            - Voter turnout
            """,

        Related = [],
        Links =
        [
            "https://en.wikipedia.org/wiki/Social-desirability_bias",
        ],
    };
    
    #endregion
    
    public static readonly IReadOnlyDictionary<Guid, Bias> ALL_BIAS = new Dictionary<Guid, Bias>
    {
        { SOCIAL_DESIRABILITY_BIAS.Id, SOCIAL_DESIRABILITY_BIAS },
        { THIRD_PERSON_EFFECT.Id, THIRD_PERSON_EFFECT },
        { FALSE_CONSENSUS_EFFECT.Id, FALSE_CONSENSUS_EFFECT },
        { HARD_EASY_EFFECT.Id, HARD_EASY_EFFECT },
        { DUNNING_KRUGER_EFFECT.Id, DUNNING_KRUGER_EFFECT },
        { EGOCENTRIC_BIAS.Id, EGOCENTRIC_BIAS },
        { OPTIMISM_BIAS.Id, OPTIMISM_BIAS },
        { SELF_SERVING_BIAS.Id, SELF_SERVING_BIAS },
        { ACTOR_OBSERVER_BIAS.Id, ACTOR_OBSERVER_BIAS }, 
        { ILLUSION_OF_CONTROL.Id, ILLUSION_OF_CONTROL },
        { FUNDAMENTAL_ATTRIBUTION_ERROR.Id, FUNDAMENTAL_ATTRIBUTION_ERROR }, 
        { DEFENSIVE_ATTRIBUTION_HYPOTHESIS.Id, DEFENSIVE_ATTRIBUTION_HYPOTHESIS },
        { TRAIT_ASCRIPTION_BIAS.Id, TRAIT_ASCRIPTION_BIAS }, 
        { EFFORT_JUSTIFICATION.Id, EFFORT_JUSTIFICATION },
        { RISK_COMPENSATION.Id, RISK_COMPENSATION },
        { HYPERBOLIC_DISCOUNTING.Id, HYPERBOLIC_DISCOUNTING },
        { APPEAL_TO_NOVELTY.Id, APPEAL_TO_NOVELTY },
        { IDENTIFIABLE_VICTIM_EFFECT.Id, IDENTIFIABLE_VICTIM_EFFECT },
        { SUNK_COST_FALLACY.Id, SUNK_COST_FALLACY },
        { ESCALATION_OF_COMMITMENT.Id, ESCALATION_OF_COMMITMENT },
        { GENERATION_EFFECT.Id, GENERATION_EFFECT },
        { LOSS_AVERSION.Id, LOSS_AVERSION },
        { IKEA_EFFECT.Id, IKEA_EFFECT },
        { UNIT_BIAS.Id, UNIT_BIAS },
        { ZERO_RISK_BIAS.Id, ZERO_RISK_BIAS },
        { DISPOSITION_EFFECT.Id, DISPOSITION_EFFECT },
        { CERTAINTY_EFFECT.Id, CERTAINTY_EFFECT },
        { PSEUDOCERTAINTY_EFFECT.Id, PSEUDOCERTAINTY_EFFECT },
        { PROCESSING_DIFFICULTY_EFFECT.Id, PROCESSING_DIFFICULTY_EFFECT },
        { ENDOWMENT_EFFECT.Id, ENDOWMENT_EFFECT },
        { BELIEF_PERSEVERANCE.Id, BELIEF_PERSEVERANCE },
        { SYSTEM_JUSTIFICATION.Id, SYSTEM_JUSTIFICATION },
        { REVERSE_PSYCHOLOGY.Id, REVERSE_PSYCHOLOGY },
        { REACTANCE.Id, REACTANCE },
        { DECOY_EFFECT.Id, DECOY_EFFECT },
        { SOCIAL_COMPARISON_BIAS.Id, SOCIAL_COMPARISON_BIAS },
        { STATUS_QUO_BIAS.Id, STATUS_QUO_BIAS },
        { AMBIGUITY_EFFECT.Id, AMBIGUITY_EFFECT },
        { INFORMATION_BIAS.Id, INFORMATION_BIAS },
        { BELIEF_BIAS.Id, BELIEF_BIAS },
        { RHYME_AS_REASON_EFFECT.Id, RHYME_AS_REASON_EFFECT },
        { PARKINSONS_LAW_OF_TRIVIALITY.Id, PARKINSONS_LAW_OF_TRIVIALITY },
        { DELMORE_EFFECT.Id, DELMORE_EFFECT },
        { CONJUNCTION_FALLACY.Id, CONJUNCTION_FALLACY },
        { OCCAMS_RAZOR.Id, OCCAMS_RAZOR },
        { LESS_IS_BETTER_EFFECT.Id, LESS_IS_BETTER_EFFECT},
        { HINDSIGHT_BIAS.Id, HINDSIGHT_BIAS },
        { OUTCOME_BIAS.Id, OUTCOME_BIAS },
        { MORAL_LUCK.Id, MORAL_LUCK },
        { DECLINISM.Id, DECLINISM },
        { PESSIMISM_BIAS.Id, PESSIMISM_BIAS },
        { PLANNING_FALLACY.Id, PLANNING_FALLACY },
        { TIME_SAVING_BIAS.Id, TIME_SAVING_BIAS },
        { PRO_INNOVATION_BIAS.Id, PRO_INNOVATION_BIAS },
        { IMPACT_BIAS.Id, IMPACT_BIAS },
        { PROJECTION_BIAS.Id, PROJECTION_BIAS },
        { ROSY_RETROSPECTION.Id, ROSY_RETROSPECTION },
        { TELESCOPING_EFFECT.Id, TELESCOPING_EFFECT },
        { ILLUSION_OF_ASYMMETRIC_INSIGHT.Id, ILLUSION_OF_ASYMMETRIC_INSIGHT },
        { ILLUSION_OF_EXTERNAL_AGENCY.Id, ILLUSION_OF_EXTERNAL_AGENCY },
        { EXTRINSIC_INCENTIVE_BIAS.Id, EXTRINSIC_INCENTIVE_BIAS },
        { SPOTLIGHT_EFFECT.Id, SPOTLIGHT_EFFECT },
        { CURSE_OF_KNOWLEDGE.Id, CURSE_OF_KNOWLEDGE },
        { ILLUSION_OF_TRANSPARENCY.Id, ILLUSION_OF_TRANSPARENCY },
        { MILLERS_LAW.Id, MILLERS_LAW },
        { DENOMINATION_EFFECT.Id, DENOMINATION_EFFECT },
        { SUBADDITIVITY_EFFECT.Id, SUBADDITIVITY_EFFECT },
        { SURVIVORSHIP_BIAS.Id, SURVIVORSHIP_BIAS },
        { ZERO_SUM_BIAS.Id, ZERO_SUM_BIAS },
        { NORMALCY_BIAS.Id, NORMALCY_BIAS },
        { APPEAL_TO_POSSIBILITY.Id, APPEAL_TO_POSSIBILITY },
        { MENTAL_ACCOUNTING.Id, MENTAL_ACCOUNTING },
        { WELL_TRAVELLED_ROAD_EFFECT.Id, WELL_TRAVELLED_ROAD_EFFECT },
        { REACTIVE_DEVALUATION.Id, REACTIVE_DEVALUATION },
        { NOT_INVENTED_HERE.Id, NOT_INVENTED_HERE },
        { POSITIVITY_EFFECT.Id, POSITIVITY_EFFECT },
        { CHEERLEADER_EFFECT.Id, CHEERLEADER_EFFECT },
        { CROSS_RACE_EFFECT.Id, CROSS_RACE_EFFECT },
        { OUT_GROUP_HOMOGENEITY.Id, OUT_GROUP_HOMOGENEITY },
        { PLACEBO_EFFECT.Id, PLACEBO_EFFECT },
        { BANDWAGON_EFFECT.Id, BANDWAGON_EFFECT },
        { AUTOMATION_BIAS.Id, AUTOMATION_BIAS },
        { AUTHORITY_BIAS.Id, AUTHORITY_BIAS },
        { ARGUMENT_FROM_FALLACY.Id, ARGUMENT_FROM_FALLACY },
        { JUST_WORLD_FALLACY.Id, JUST_WORLD_FALLACY },
        { MORAL_CREDENTIAL_EFFECT.Id, MORAL_CREDENTIAL_EFFECT },
        { FUNCTIONAL_FIXEDNESS.Id, FUNCTIONAL_FIXEDNESS },
        { ESSENTIALISM.Id, ESSENTIALISM },
        { STEREOTYPING.Id, STEREOTYPING },
        { IN_GROUP_FAVORITISM.Id, IN_GROUP_FAVORITISM },
        { ULTIMATE_ATTRIBUTION_ERROR.Id, ULTIMATE_ATTRIBUTION_ERROR },
        { HOSTILE_ATTRIBUTION_BIAS.Id, HOSTILE_ATTRIBUTION_BIAS },
        { ATTRIBUTION_BIAS.Id, ATTRIBUTION_BIAS },
        { GROUP_ATTRIBUTION_ERROR.Id, GROUP_ATTRIBUTION_ERROR },
        { ANTHROPOMORPHISM.Id, ANTHROPOMORPHISM },
        { APOPHENIA.Id, APOPHENIA },
        { PAREIDOLIA.Id, PAREIDOLIA },
        { ILLUSORY_CORRELATION.Id, ILLUSORY_CORRELATION },
        { HOT_HAND_FALLACY.Id, HOT_HAND_FALLACY },
        { GAMBLERS_FALLACY.Id, GAMBLERS_FALLACY },
        { RECENCY_ILLUSION.Id, RECENCY_ILLUSION },
        { MASKED_MAN_FALLACY.Id, MASKED_MAN_FALLACY },
        { WYSIATI.Id, WYSIATI },
        { ILLUSION_OF_VALIDITY.Id, ILLUSION_OF_VALIDITY },
        { ANECDOTAL_FALLACY.Id, ANECDOTAL_FALLACY },
        { NEGLECT_OF_PROBABILITY.Id, NEGLECT_OF_PROBABILITY },
        { INSENSITIVITY_TO_SAMPLE_SIZE.Id, INSENSITIVITY_TO_SAMPLE_SIZE },
        { CLUSTERING_ILLUSION.Id, CLUSTERING_ILLUSION },
        { CONFABULATION.Id, CONFABULATION },
        { NAÏVE_REALISM.Id, NAÏVE_REALISM },
        { NAÏVE_CYNICISM.Id, NAÏVE_CYNICISM },
        { OVERCONFIDENCE_EFFECT.Id, OVERCONFIDENCE_EFFECT },
        { ILLUSORY_SUPERIORITY.Id, ILLUSORY_SUPERIORITY },
        { INTROSPECTION_ILLUSION.Id, INTROSPECTION_ILLUSION },
        { BIAS_BLIND_SPOT.Id, BIAS_BLIND_SPOT },
        { SEMMELWEIS_REFLEX.Id, SEMMELWEIS_REFLEX },
        { CONTINUED_INFLUENCE_EFFECT.Id, CONTINUED_INFLUENCE_EFFECT },
        { BARNUM_EFFECT.Id, BARNUM_EFFECT },
        { SUBJECTIVE_VALIDATION.Id, SUBJECTIVE_VALIDATION },
        { OSTRICH_EFFECT.Id, OSTRICH_EFFECT },
        { OBSERVER_EXPECTANCY_EFFECT.Id, OBSERVER_EXPECTANCY_EFFECT },
        { SELECTIVE_PERCEPTION.Id, SELECTIVE_PERCEPTION },
        { CHOICE_SUPPORTIVE_BIAS.Id, CHOICE_SUPPORTIVE_BIAS },
        { CONGRUENCE_BIAS.Id, CONGRUENCE_BIAS },
        { CONFIRMATION_BIAS.Id, CONFIRMATION_BIAS },
        { WEBER_FECHNER_LAW.Id, WEBER_FECHNER_LAW },
        { MONEY_ILLUSION.Id, MONEY_ILLUSION },
        { FRAMING_EFFECT.Id, FRAMING_EFFECT },
        { FOCUSING_EFFECT.Id, FOCUSING_EFFECT },
        { DISTINCTION_BIAS.Id, DISTINCTION_BIAS },
        { CONTRAST_EFFECT.Id, CONTRAST_EFFECT },
        { CONSERVATISM_BIAS.Id, CONSERVATISM_BIAS },
        { ANCHORING_EFFECT.Id, ANCHORING_EFFECT },
        { SELF_REFERENCE_EFFECT.Id, SELF_REFERENCE_EFFECT },
        { PICTURE_SUPERIORITY_EFFECT.Id, PICTURE_SUPERIORITY_EFFECT },
        { VON_RESTORFF_EFFECT.Id, VON_RESTORFF_EFFECT },
        { HUMOUR_EFFECT.Id, HUMOUR_EFFECT },
        { BIZARRENESS_EFFECT.Id, BIZARRENESS_EFFECT },
        { BASE_RATE_FALLACY.Id, BASE_RATE_FALLACY },
        { OMISSION_BIAS.Id, OMISSION_BIAS},
        { HOT_COLD_EMPATHY_GAP.Id, HOT_COLD_EMPATHY_GAP },
        { FREQUENCY_ILLUSION.Id, FREQUENCY_ILLUSION },
        { CONTEXT_DEPENDENT_MEMORY.Id, CONTEXT_DEPENDENT_MEMORY },
        { STATE_DEPENDENT_MEMORY.Id, STATE_DEPENDENT_MEMORY },
        { CUE_DEPENDENT_FORGETTING.Id, CUE_DEPENDENT_FORGETTING },
        { CONTEXT_EFFECT.Id, CONTEXT_EFFECT },
        { MERE_EXPOSURE_EFFECT.Id, MERE_EXPOSURE_EFFECT },
        { ILLUSORY_TRUTH_EFFECT.Id, ILLUSORY_TRUTH_EFFECT },
        { ATTENTIONAL_BIAS.Id, ATTENTIONAL_BIAS },
        { AVAILABILITY_HEURISTIC.Id, AVAILABILITY_HEURISTIC },
        { MODALITY_EFFECT.Id, MODALITY_EFFECT },
        { MEMORY_INHIBITION.Id, MEMORY_INHIBITION },
        { PRIMACY_EFFECT.Id, PRIMACY_EFFECT },
        { RECENCY_EFFECT.Id, RECENCY_EFFECT },
        { PART_LIST_CUING.Id, PART_LIST_CUING },
        { SERIAL_POSITION_EFFECT.Id, SERIAL_POSITION_EFFECT },
        { SUFFIX_EFFECT.Id, SUFFIX_EFFECT },
        { LEVELS_OF_PROCESSING_EFFECT.Id, LEVELS_OF_PROCESSING_EFFECT },
        { ABSENT_MINDEDNESS.Id, ABSENT_MINDEDNESS },
        { TESTING_EFFECT.Id, TESTING_EFFECT },
        { NEXT_IN_LINE_EFFECT.Id, NEXT_IN_LINE_EFFECT },
        { GOOGLE_EFFECT.Id, GOOGLE_EFFECT },
        { TIP_OF_THE_TONGUE_PHENOMENON.Id, TIP_OF_THE_TONGUE_PHENOMENON },
        { SUGGESTIBILITY.Id, SUGGESTIBILITY },
        { SPACING_EFFECT.Id, SPACING_EFFECT },
        { MISATTRIBUTION_OF_MEMORY.Id, MISATTRIBUTION_OF_MEMORY },
        { LIST_LENGTH_EFFECT.Id, LIST_LENGTH_EFFECT },
        { MISINFORMATION_EFFECT.Id, MISINFORMATION_EFFECT },
        { LEVELING_AND_SHARPENING.Id, LEVELING_AND_SHARPENING },
        { PEAK_END_RULE.Id, PEAK_END_RULE },
        { FADING_AFFECT_BIAS.Id, FADING_AFFECT_BIAS },
        { NEGATIVITY_BIAS.Id, NEGATIVITY_BIAS },
        { PREJUDICE.Id, PREJUDICE },
        { IMPLICIT_STEREOTYPES.Id, IMPLICIT_STEREOTYPES },
        { IMPLICIT_ASSOCIATIONS.Id, IMPLICIT_ASSOCIATIONS },
    };
    
    public static Bias GetRandomBias(IList<int> usedBias)
    {
        if(usedBias.Count >= ALL_BIAS.Count)
            usedBias.Clear();

        var randomBiasIndex = Random.Shared.Next(0, ALL_BIAS.Count);
        while(usedBias.Contains(randomBiasIndex))
            randomBiasIndex = Random.Shared.Next(0, ALL_BIAS.Count);
        
        usedBias.Add(randomBiasIndex);
        return ALL_BIAS.Values.ElementAt(randomBiasIndex);
    }
}