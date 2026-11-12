using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks which personal details the BeyondFacts Lab removes from a session before the person shares it.
/// </summary>
/// <remarks>
/// The anonymizer follows rules. These tests describe what the rules promise: the forms in which names appear,
/// addresses, paths, and phone numbers, and that a second run changes nothing.
/// </remarks>
[TestFixture]
public sealed class BflAnonymizerTests
{
    [TestCase("Jörg Müller hat geantwortet.")]
    [TestCase("Joerg Mueller hat geantwortet.")]
    [TestCase("Jorg Muller hat geantwortet.")]
    [TestCase("JÖRG MÜLLER hat geantwortet.")]
    public void AFullNameIsReplacedWithAndWithoutItsUmlauts(string text) => Assert.That(Anonymize(text, ["Jörg Müller"]), Is.EqualTo("[Person 1] hat geantwortet."));

    [Test]
    public void ANameWrittenOutIsAlsoReplacedWithItsUmlauts() => Assert.That(Anonymize("Herr Müller und Jörg", ["Joerg Mueller"]), Is.EqualTo("[Person 1] und [Person 1]"));

    [TestCase("Prof. Dr. Anna Schmidt spricht.")]
    [TestCase("Dr. Schmidt spricht.")]
    [TestCase("Frau Schmidt spricht.")]
    [TestCase("Prof.Dr. Anna Schmidt spricht.")]
    [TestCase("Anna spricht.")]
    public void TheTitlesGoWithTheName(string text) => Assert.That(Anonymize(text, ["Anna Schmidt"]), Is.EqualTo("[Person 1] spricht."));

    [TestCase("Schmidts Studie", "[Person 1]s Studie")]
    [TestCase("Anna Schmidts Studie", "[Person 1]s Studie")]
    [TestCase("Anna's talk", "[Person 1]'s talk")]
    [TestCase("Schmidt’s talk", "[Person 1]’s talk")]
    [TestCase("„Schmidt“ sagte", "„[Person 1]“ sagte")]
    public void TheGenitiveStaysAfterThePlaceholder(string text, string expected) => Assert.That(Anonymize(text, ["Anna Schmidt"]), Is.EqualTo(expected));

    [Test]
    public void TheGenitiveOfANameEndingWithSIsReplaced() => Assert.That(Anonymize("Schmitz' Buch", ["Lena Schmitz"]), Is.EqualTo("[Person 1]' Buch"));

    [Test]
    public void ALongerNameWinsOverTheShorterOneItContains()
    {
        var text = Anonymize("Anna Schmidt-Weber sprach mit Anna Schmidt. Schmidt-Weber widersprach.", ["Anna Schmidt", "Anna Schmidt-Weber"]);
        Assert.That(text, Is.EqualTo("[Person 2] sprach mit [Person 1]. [Person 2] widersprach."));
    }

    [Test]
    public void APartWhichSeveralPeopleShareGetsNoNumber()
    {
        var text = Anonymize("Schmidt sagte zu Anna Schmidt und Bernd Schmidt: Bernd!", ["Anna Schmidt", "Bernd Schmidt"]);
        Assert.That(text, Is.EqualTo("[Person] sagte zu [Person 1] und [Person 2]: [Person 2]!"));
    }

    [Test]
    public void APartOfANameOnItsOwnStaysInADoubleNameOfSomebodyElse() => Assert.That(Anonymize("Frau Müller-Schmidt kam.", ["Anna Schmidt"]), Is.EqualTo("Frau Müller-Schmidt kam."));

    [Test]
    public void APartOfANameOnItsOwnCountsOnlyWithACapitalLetter() => Assert.That(Anonymize("Der Topf ist klein, Klein kam.", ["Eva Klein"]), Is.EqualTo("Der Topf ist klein, [Person 1] kam."));

    [TestCase("anna-schmidt")]
    [TestCase("anna.schmidt")]
    [TestCase("annaschmidt")]
    [TestCase("schmidt_anna")]
    [TestCase("AnnaSchmidt")]
    public void TheFormsOfANameInAddressesAreReplaced(string form) => Assert.That(Anonymize($"Profil: @{form} und {form}-lab.de", ["Anna Schmidt"]), Is.EqualTo("Profil: @[Person 1] und [Person 1]-lab.de"));

    [Test]
    public void ANameWithTheLastNameFirstIsReplaced() => Assert.That(Anonymize("Schmidt, Anna (TUM)", ["Anna Schmidt"]), Is.EqualTo("[Person 1] (TUM)"));

    [Test]
    public void ANameListedWithTheLastNameFirstIsUnderstood() => Assert.That(Anonymize("Anna Schmidt kam.", ["Schmidt, Anna"]), Is.EqualTo("[Person 1] kam."));

    [Test]
    public void AParticleBelongsToTheLastName() => Assert.That(Anonymize("Frau von der Leyen und Leyen", ["Ursula von der Leyen"]), Is.EqualTo("[Person 1] und [Person 1]"));

    [TestCase("Siehe https://example.org/team/anna?x=1.", "Siehe [Link].")]
    [TestCase("(https://example.org/a)", "([Link])")]
    [TestCase("Auf www.example.org steht es.", "Auf [Link] steht es.")]
    [TestCase("Das [Programm](https://example.org/programm \"Titel\") lesen", "Das Programm [Link] lesen")]
    [TestCase("<https://example.org/x>", "[Link]")]
    [TestCase("Schreib an [Anna](mailto:anna@example.org)", "Schreib an Anna [Link]")]
    public void LinksAreReplaced(string text, string expected) => Assert.That(Anonymize(text), Is.EqualTo(expected));

    [TestCase("Mail: anna.schmidt+lab@tum.de.", "Mail: [E-mail address].")]
    [TestCase("jörg@müller.example", "[E-mail address]")]
    public void EMailAddressesAreReplaced(string text, string expected) => Assert.That(Anonymize(text), Is.EqualTo(expected));

    [TestCase("Datei /Users/anna/Documents/talk.pdf liegt da.", "Datei [Path] liegt da.")]
    [TestCase(@"Datei C:\Users\anna\talk.docx.", "Datei [Path].")]
    [TestCase("Datei ~/Notes/talk.md", "Datei [Path]")]
    [TestCase(@"Freigabe \\server\share\talk.pptx", "Freigabe [Path]")]
    [TestCase("und/oder, 120 km/h, 1/2", "und/oder, 120 km/h, 1/2")]
    public void PathsAreReplaced(string text, string expected) => Assert.That(Anonymize(text), Is.EqualTo(expected));

    [TestCase("Tel. +49 89 123456-78.", "Tel. [Phone number].")]
    [TestCase("Mobil 0176 12345678", "Mobil [Phone number]")]
    [TestCase("Büro (089) 1234567", "Büro [Phone number]")]
    [TestCase("Am 05.11.2026 von 08.00-09.30 Uhr", "Am 05.11.2026 von 08.00-09.30 Uhr")]
    [TestCase("p = 0,05 bei 1234567 Fällen", "p = 0,05 bei 1234567 Fällen")]
    public void PhoneNumbersAreReplaced(string text, string expected) => Assert.That(Anonymize(text), Is.EqualTo(expected));

    [Test]
    public void TermsAreReplacedInAnyCase()
    {
        var text = BflAnonymizer.Anonymize("Die TUM und die Technische Universität München, kurz tum.", [], ["TUM", "Technische Universität München"]).Text;
        Assert.That(text, Is.EqualTo("Die [Term 1] und die [Term 2], kurz [Term 1]."));
    }

    [Test]
    public void PlaceholdersStayAlsoWhenATermLooksLikeThem()
    {
        var text = BflAnonymizer.Anonymize("[Person 1] nannte [Link] und den Link.", [], ["Link", "Person"]).Text;
        Assert.That(text, Is.EqualTo("[Person 1] nannte [Link] und den [Term 1]."));
    }

    [Test]
    public void ASecondRunChangesNothing()
    {
        const string TEXT = """
            Prof. Dr. Jörg Müller (joerg.mueller@tum.de, +49 89 123456-78) schrieb in /Users/joerg/Talk/notes.md:
            Siehe https://example.org/joerg-mueller und [das Programm](https://example.org/p). Müllers These, Eva Klein und die TUM.
            """;

        var first = BflAnonymizer.Anonymize(TEXT, ["Jörg Müller", "Eva Klein"], ["TUM"]);
        var second = BflAnonymizer.Anonymize(first.Text, ["Jörg Müller", "Eva Klein"], ["TUM"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Text, Is.EqualTo(first.Text));
            Assert.That(second.Replacements, Is.Empty);
        }
    }

    [Test]
    public void NoNameRemains()
    {
        const string TEXT = """
            Prof. Dr. Jörg Müller-Lüdenscheidt, Joerg Mueller-Luedenscheidt und Jörg sprachen.
            Müller-Lüdenscheidts Studie, Dr. Müller-Lüdenscheidt, joerg.mueller-luedenscheidt und @JoergMuellerLuedenscheidt.
            Ursula von der Leyen, Frau von der Leyen, Leyens Rede, ursula-von-der-leyen.
            """;

        var text = Anonymize(TEXT, ["Jörg Müller-Lüdenscheidt", "Ursula von der Leyen"]);

        using (Assert.EnterMultipleScope())
        {
            foreach (var part in new[] { "Jörg", "Joerg", "Müller", "Mueller", "Lüdenscheidt", "Luedenscheidt", "Ursula", "Leyen" })
                Assert.That(text, Does.Not.Contain(part).IgnoreCase, part);
        }
    }

    [Test]
    public void TheReportCountsWhatWasReplaced()
    {
        var anonymization = BflAnonymizer.Anonymize("Anna Schmidt, Anna Schmidt und Dr. Schmidt: anna@example.org", ["Anna Schmidt"], []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(anonymization.Replacements, Does.Contain(new BflReplacement("[Person 1]", "Anna Schmidt", 2)));
            Assert.That(anonymization.Replacements, Does.Contain(new BflReplacement("[Person 1]", "Dr. Schmidt", 1)));
            Assert.That(anonymization.Replacements, Does.Contain(new BflReplacement("[E-mail address]", "anna@example.org", 1)));
        }
    }

    [Test]
    public void AnEditedTextKeepsTheReportOfTheFirstRun()
    {
        var first = BflAnonymizer.Anonymize("Anna Schmidt", ["Anna Schmidt"], []);
        var later = BflAnonymizer.Anonymize($"{first.Text} und Anna Schmidt", ["Anna Schmidt"], []);
        var combined = first.CombinedWith(later);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(combined.Text, Is.EqualTo("[Person 1] und [Person 1]"));
            Assert.That(combined.Replacements, Is.EqualTo(new[] { new BflReplacement("[Person 1]", "Anna Schmidt", 2) }));
        }
    }

    [Test]
    public void EmptyAndRepeatedEntriesGetNoNumber() => Assert.That(Anonymize("Eva Klein und Anna Schmidt", ["", "  ", "Eva Klein", "eva klein", "Anna Schmidt"]), Is.EqualTo("[Person 1] und [Person 2]"));

    private static string Anonymize(string text, IReadOnlyList<string>? people = null) => BflAnonymizer.Anonymize(text, people ?? [], []).Text;
}