require("contentHome")
require("icon")

-- The ID for this plugin:
ID = "43065dbc-78d0-45b7-92be-f14c2926e2dc"

-- The icon for the plugin:
ICON_SVG = SVG

-- The name of the plugin:
NAME = "MindWork AI Studio - German / Deutsch"

-- The description of the plugin:
DESCRIPTION = "Dieses Plugin bietet deutsche Sprachunterstützung für MindWork AI Studio."

-- The version of the plugin:
VERSION = "1.0.0"

-- The type of the plugin:
TYPE = "LANGUAGE"

-- The authors of the plugin:
AUTHORS = {"MindWork AI Community"}

-- The support contact for the plugin:
SUPPORT_CONTACT = "MindWork AI Community"

-- The source URL for the plugin:
SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"

-- The categories for the plugin:
CATEGORIES = { "CORE" }

-- The target groups for the plugin:
TARGET_GROUPS = { "EVERYONE" }

-- The flag for whether the plugin is maintained:
IS_MAINTAINED = true

-- When the plugin is deprecated, this message will be shown to users:
DEPRECATION_MESSAGE = ""

-- The IETF BCP 47 tag for the language. It's the ISO 639 language
-- code followed by the ISO 3166-1 country code:
IETF_TAG = "de-DE"

-- The language name in the user's language:
LANG_NAME = "Deutsch (Deutschland)"

UI_TEXT_CONTENT = {}

-- Stop generation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ASSISTANTBASE::T1317408357"] = "Generierung stoppen"

-- Reset
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ASSISTANTBASE::T180921696"] = "Zurücksetzen"

-- Assistant - {0}
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ASSISTANTBASE::T3043922"] = "Assistent – {0}"

-- Send to ...
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ASSISTANTBASE::T4242312602"] = "Senden an ..."

-- Copy result
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ASSISTANTBASE::T83711157"] = "Ergebnis kopieren"

-- Provide a list of bullet points and some basic information for an e-mail. The assistant will generate an e-mail based on that input.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T1143222914"] = "Geben Sie eine Liste von Stichpunkten sowie einige Basisinformationen für eine E-Mail ein. Der Assistent erstellt anschließend eine E-Mail auf Grundlage Ihrer Angaben."

-- Your name for the closing salutation of your e-mail.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T134060413"] = "Ihr Name für die Grußformel am Ende Ihrer E-Mail."

-- Please start each line of your content list with a dash (-) to create a bullet point list.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T1384718254"] = "Bitte beginne jede Zeile deiner Inhaltsliste mit einem Bindestrich (-), um eine Aufzählung zu erstellen."

-- Create email
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T1686330485"] = "E-Mail erstellen"

-- Previous conversation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2074063439"] = "Frühere Unterhaltung"

-- Select the writing style
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2241531659"] = "Schreibstil auswählen"

-- Target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T237828418"] = "Zielsprache"

-- Please provide some content for the e-mail.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2381517938"] = "Bitte geben Sie einen Text für die E-Mail ein."

-- Please provide some history for the e-mail.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2471325767"] = "Bitte geben Sie einen Verlauf für die E-Mail an."

-- Your bullet points
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2582330385"] = "Ihre Stichpunkte"

-- Yes, I provide the previous conversation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2652980489"] = "Ja, ich stelle das vorherige Gespräch zur Verfügung."

-- Please select a writing style for the e-mail.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T2969942806"] = "Bitte wählen Sie einen Schreibstil für die E-Mail aus."

-- E-Mail
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3026443472"] = "E-Mail"

-- (Optional) The greeting phrase to use
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T306513209"] = "(Optional) Die zu verwendende Grußformel"

-- Bullet list the content of the e-mail roughly. Use dashes (-) to separate the items.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3259604530"] = "Fasse den Inhalt der E-Mail stichpunktartig zusammen. Verwende dafür Striche (-), um die Punkte zu trennen."

-- Is there a history, a previous conversation?
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3438127996"] = "Gibt es einen Verlauf, ein vorheriges Gespräch?"

-- Provide the previous conversation, e.g., the last e-mail, the last chat, etc.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3706154604"] = "Stellen Sie die vorherige Unterhaltung bereit, z. B. die letzte E-Mail, den letzten Chat usw."

-- No, I don't provide a previous conversation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3823693145"] = "Nein, ich stelle kein vorheriges Gespräch bereit."

-- (Optional) Are any of your points particularly important?
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3843104162"] = "(Optional) Sind einige Ihrer Punkte besonders wichtig?"

-- Custom target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T3848935911"] = "Benutzerdefinierte Zielsprache"

-- (Optional) Your name for the closing salutation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T453962275"] = "(Optional) Ihr Name für die abschließende Grußformel"

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T656744944"] = "Bitte wählen Sie eine benutzerdefinierte Sprache aus."

-- Dear Colleagues
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T759263763"] = "Liebe Kolleginnen und Kollegen"

-- Please select a target language for the e-mail.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::EMAIL::ASSISTANTEMAIL::T891073054"] = "Bitte wählen Sie eine Zielsprache für die E-Mail aus."

-- Please provide a text as input. You might copy the desired text from a document or a website.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T137304886"] = "Bitte geben Sie einen Text ein. Sie können den gewünschten Text aus einem Dokument oder einer Website kopieren."

-- Proofread
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T2325568297"] = "Korrektur lesen"

-- Language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T2591284123"] = "Sprache"

-- Your input to check
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T2861221443"] = "Ihre Eingabe zur Überprüfung"

-- Custom language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T3032662264"] = "Benutzerdefinierte Sprache"

-- Grammar & Spelling Checker
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T3169549433"] = "Grammatik- und Rechtschreibprüfung"

-- Check the grammar and spelling of a text.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T3184716499"] = "Rechtschreibung und Grammatik eines Textes überprüfen."

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::GRAMMARSPELLING::ASSISTANTGRAMMARSPELLING::T656744944"] = "Bitte geben Sie eine benutzerdefinierte Sprache an."

-- Your icon source
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T1302165948"] = "Ihre Icons-Quelle"

-- Find Icon
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T1975161003"] = "Symbol suchen"

-- Finding the right icon for a context, such as for a piece of text, is not easy. The first challenge: You need to extract a concept from your context, such as from a text. Let's take an example where your text contains statements about multiple departments. The sought-after concept could be "departments." The next challenge is that we need to anticipate the bias of the icon designers: under the search term "departments," there may be no relevant icons or only unsuitable ones. Depending on the icon source, it might be more effective to search for "buildings," for instance. LLMs assist you with both steps.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T347756684"] = "Das richtige Symbol für einen bestimmten Kontext zu finden, zum Beispiel für einen Text, ist nicht einfach. Die erste Herausforderung besteht darin, ein Konzept aus dem Kontext, wie etwa aus einem Text, herauszufiltern. Nehmen wir ein Beispiel: Ihr Text enthält Aussagen über verschiedene Abteilungen. Das gesuchte Konzept könnte also „Abteilungen“ sein. Die nächste Herausforderung ist, die Denkweise der Icon-Designer vorherzusehen: Unter dem Suchbegriff „Abteilungen“ gibt es möglicherweise keine passenden oder sogar völlig ungeeignete Symbole. Je nach Icon-Quelle kann es daher effektiver sein, zum Beispiel nach „Gebäude“ zu suchen. LLMs unterstützen Sie bei beiden Schritten."

-- Icon Finder
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T3693102312"] = "Symbolfinder"

-- Open website
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T4239378936"] = "Website öffnen"

-- Your context
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T596802185"] = "Ihr Kontext"

-- Please provide a context. This will help the AI to find the right icon. You might type just a keyword or copy a sentence from your text, e.g., from a slide where you want to use the icon.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::ICONFINDER::ASSISTANTICONFINDER::T653229070"] = "Bitte geben Sie einen Kontext an. Das hilft der KI, das passende Symbol zu finden. Sie können einfach ein Stichwort eingeben oder einen Satz aus Ihrem Text kopieren, zum Beispiel von einer Folie, auf der Sie das Symbol verwenden möchten."

-- Please provide a legal document as input. You might copy the desired text from a document or a website.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T1160217683"] = "Bitte geben Sie ein rechtliches Dokument ein. Sie können den gewünschten Text aus einem Dokument oder von einer Website kopieren."

-- Legal Check
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T1348190638"] = "Rechtliche Prüfung"

-- Legal document
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T1887742531"] = "Rechtsdokument"

-- Your questions
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T1947954583"] = "Ihre Fragen"

-- Provide a legal document and ask a question about it. This assistant does not replace legal advice. Consult a lawyer to get professional advice. Remember that LLMs can invent answers and facts. Please do not rely on this answers.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T4016275181"] = "Stellen Sie ein juristisches Dokument bereit und stellen Sie eine Frage dazu. Dieser Assistent ersetzt keine Rechtsberatung. Wenden Sie sich an einen Anwalt, um professionelle Beratung zu erhalten. Bitte beachten Sie, dass Sprachmodelle Antworten und Fakten erfinden können. Verlassen Sie sich daher nicht auf diese Antworten."

-- Please provide your questions as input.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T4154383818"] = "Bitte geben Sie Ihre Fragen ein."

-- Ask your questions
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::LEGALCHECK::ASSISTANTLEGALCHECK::T467099852"] = "Stellen Sie Ihre Fragen"

-- Please provide some text as input. For example, an email.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T1962809521"] = "Bitte geben Sie einen Text ein. Zum Beispiel eine E-Mail."

-- Analyze text
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T2268303626"] = "Text analysieren"

-- Target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T237828418"] = "Zielsprache"

-- My Tasks
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T3011450657"] = "Meine Aufgaben"

-- You received a cryptic email that was sent to many recipients and you are now wondering if you need to do something? Copy the email into the input field. You also need to select a personal profile. In this profile, you should describe your role in the organization. The AI will then try to give you hints on what your tasks might be.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T3646084045"] = "Sie haben eine rätselhafte E-Mail erhalten, die an viele Empfänger verschickt wurde, und fragen sich nun, ob Sie etwas unternehmen müssen? Kopieren Sie die E-Mail in das Eingabefeld. Außerdem müssen Sie ein persönliches Profil auswählen. In diesem Profil sollten Sie Ihre Rolle in der Organisation beschreiben. Die KI wird Ihnen dann Hinweise geben, welche Aufgaben für Sie daraus entstehen könnten."

-- Custom target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T3848935911"] = "Benutzerdefinierte Zielsprache"

-- Please select one of your profiles.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T465395981"] = "Bitte wählen Sie eines Ihrer Profile aus."

-- Text or email
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T534887559"] = "Text oder E-Mail"

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::MYTASKS::ASSISTANTMYTASKS::T656744944"] = "Bitte wählen Sie eine eigene Sprache aus."

-- Please provide a text as input. You might copy the desired text from a document or a website.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T137304886"] = "Bitte geben Sie einen Text ein. Sie können den gewünschten Text aus einem Dokument oder einer Website kopieren."

-- Sentence structure
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T1714063121"] = "Satzstruktur"

-- Rewrite & Improve Text
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T1994150308"] = "Text umschreiben & verbessern"

-- Improve your text
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T2163831433"] = "Verbessern Sie Ihren Text"

-- Language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T2591284123"] = "Sprache"

-- Custom language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T3032662264"] = "Benutzerdefinierte Sprache"

-- Your input to improve
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T3037449423"] = "Ihr Vorschlag zur Verbesserung"

-- Writing style
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T3754048862"] = "Schreibstil"

-- Rewrite and improve your text. Please note, that the capabilities of the different LLM providers will vary.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T480915300"] = "Überarbeite und verbessere deinen Text. Bitte beachte, dass die Fähigkeiten der verschiedenen LLM-Anbieter unterschiedlich sein können."

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::REWRITEIMPROVE::ASSISTANTREWRITEIMPROVE::T656744944"] = "Bitte geben Sie eine benutzerdefinierte Sprache an."

-- Your word or phrase
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T1847246020"] = "Ihr Wort oder Ausdruck"

-- (Optional) The context for the given word or phrase
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T2250963999"] = "(Optional) Der Kontext für das angegebene Wort oder die Phrase"

-- Synonyms
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T2547582747"] = "Synonyme"

-- Language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T2591284123"] = "Sprache"

-- Find synonyms for words or phrases.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T2733641217"] = "Finde Synonyme für Wörter oder Ausdrücke."

-- Find synonyms
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T3106607224"] = "Synonyme finden"

-- Please provide a word or phrase as input.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T3501110371"] = "Bitte geben Sie ein Wort oder eine Phrase ein."

-- Custom target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T3848935911"] = "Benutzerdefinierte Zielsprache"

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::SYNONYM::ASSISTANTSYNONYMS::T656744944"] = "Bitte geben Sie eine benutzerdefinierte Sprache an."

-- Your input
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T1249704194"] = "Ihre Eingabe"

-- Target complexity
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T1318882688"] = "Ziel-Komplexität"

-- Please provide a text as input. You might copy the desired text from a document or a website.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T137304886"] = "Bitte geben Sie einen Text ein. Sie können den gewünschten Text aus einem Dokument oder einer Webseite kopieren."

-- Text Summarizer
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T1907192403"] = "Textzusammenfasser"

-- Target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T237828418"] = "Zielsprache"

-- Summarize long text into a shorter version while retaining the main points. You might want to change the language of the summary to make it more readable. It is also possible to change the complexity of the summary to make it easy to understand.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T359929871"] = "Fasse einen langen Text zu einer kürzeren Version zusammen und behalte dabei die wichtigsten Punkte bei. Du kannst die Sprache des Textes anpassen, um die Zusammenfassung verständlicher zu machen. Außerdem ist es möglich, die Zusammenfassung einfacher zu formulieren, damit sie leichter zu verstehen ist."

-- Please provide your field of expertise.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T3610378685"] = "Bitte geben Sie Ihr Fachgebiet an."

-- Custom target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T3848935911"] = "Benutzerdefinierte Zielsprache"

-- Summarize
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T502888730"] = "Zusammenfassen"

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T656744944"] = "Bitte wähle eine benutzerdefinierte Sprache aus."

-- Your expertise
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TEXTSUMMARIZER::ASSISTANTTEXTSUMMARIZER::T970222193"] = "Ihre Fachkenntnisse"

-- Please select a target language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T1173859091"] = "Bitte wählen Sie eine Zielsprache aus."

-- Your input
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T1249704194"] = "Ihre Eingabe"

-- Please provide a text as input. You might copy the desired text from a document or a website.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T137304886"] = "Bitte geben Sie einen Text ein. Sie können den gewünschten Text aus einem Dokument oder einer Webseite kopieren."

-- Translate
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T2028202101"] = "Übersetzen"

-- Target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T237828418"] = "Zielsprache"

-- Translate text from one language to another.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T3230457846"] = "Text aus einer Sprache in eine andere übersetzen."

-- No live translation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T3556243327"] = "Keine Live-Übersetzung"

-- Custom target language
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T3848935911"] = "Benutzerdefinierte Zielsprache"

-- Live translation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T4279308324"] = "Live-Übersetzung"

-- Translation
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T613888204"] = "Übersetzung"

-- Please provide a custom language.
UI_TEXT_CONTENT["AISTUDIO::ASSISTANTS::TRANSLATION::ASSISTANTTRANSLATION::T656744944"] = "Bitte geben Sie eine eigene Sprache an."

-- Edit Message
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T1183581066"] = "Nachricht bearbeiten"

-- Copies the content to the clipboard
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T12948066"] = "Kopiert den Inhalt in die Zwischenablage"

-- Do you really want to remove this message?
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T1347427447"] = "Möchten Sie diese Nachricht wirklich löschen?"

-- Yes, remove the AI response and edit it
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T1350385882"] = "Ja, entferne die KI-Antwort und bearbeite sie."

-- Yes, regenerate it
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T1603883875"] = "Ja, neu generieren"

-- Yes, remove it
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T1820166585"] = "Ja, entferne es"

-- Do you really want to edit this message? In order to edit this message, the AI response will be deleted.
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T2018431076"] = "Möchten Sie diese Nachricht wirklich bearbeiten? Um die Nachricht zu bearbeiten, wird die Antwort der KI gelöscht."

-- Removes this block
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T2093355991"] = "Entfernt diesen Block"

-- Regenerate Message
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T2308444540"] = "Nachricht neu erstellen"

-- Cannot render content of type {0} yet.
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T3175548294"] = "Der Inhaltstyp {0} kann noch nicht angezeigt werden."

-- Edit
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T3267849393"] = "Bearbeiten"

-- Regenerate
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T3587744975"] = "Neu generieren"

-- Do you really want to regenerate this message?
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T3878878761"] = "Möchten Sie diese Nachricht wirklich neu generieren?"

-- Cannot copy this content type to clipboard!
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T4021525742"] = "Dieser Inhaltstyp kann nicht in die Zwischenablage kopiert werden!"

-- Remove Message
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T4070211974"] = "Nachricht entfernen"

-- No, keep it
UI_TEXT_CONTENT["AISTUDIO::CHAT::CONTENTBLOCKCOMPONENT::T4188329028"] = "Nein, behalten"

-- Open Settings
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::ASSISTANTBLOCK::T1172211894"] = "Einstellungen öffnen"

-- Changelog
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHANGELOG::T3017574265"] = "Änderungsprotokoll"

-- Move chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T1133040906"] = "Chat verschieben"

-- Are you sure you want to move this chat? All unsaved changes will be lost.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T1142475422"] = "Sind Sie sicher, dass Sie diesen Chat verschieben möchten? Alle ungespeicherten Änderungen gehen verloren."

-- Save chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T1516264254"] = "Chat speichern"

-- Type your input here...
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T1849313532"] = "Geben Sie hier Ihre Eingabe ein..."

-- Your Prompt (use selected instance '{0}', provider '{1}')
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T1967611328"] = "Ihr Prompt (verwendete Instanz: '{0}', Anbieter: '{1}')"

-- Delete this chat & start a new one.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T2991985411"] = "Diesen Chat löschen & neuen beginnen."

-- Move Chat to Workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T3045856778"] = "Chat in den Arbeitsbereich verschieben"

-- The selected provider is not allowed in this chat due to data security reasons.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T3403290862"] = "Der ausgewählte Anbieter ist aus Gründen der Datensicherheit in diesem Chat nicht erlaubt."

-- Select a provider first
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T3654197869"] = "Wähle zuerst einen Anbieter aus"

-- Start temporary chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T4113970938"] = "Temporären Chat starten"

-- Please select the workspace where you want to move the chat to.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T474393241"] = "Bitte wählen Sie den Arbeitsbereich aus, in den Sie den Chat verschieben möchten."

-- Move the chat to a workspace, or to another if it is already in one.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T636393754"] = "Verschieben Sie den Chat in einen Arbeitsbereich oder in einen anderen, falls er sich bereits in einem befindet."

-- Show your workspaces
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CHATCOMPONENT::T733672375"] = "Zeige deine Arbeitsbereiche"

-- Region
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T1227782301"] = "Region"

-- Description
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T1725856265"] = "Beschreibung"

-- Confidence Level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T2492230131"] = "Vertrauensniveau"

-- Sources
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T2730980305"] = "Quellen"

-- Confidence Card
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T2960002005"] = "Vertrauenskarte"

-- Confidence
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T3243388657"] = "Vertrauenskarte"

-- Shows and hides the confidence card with information about the selected LLM provider.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIDENCEINFO::T847071819"] = "Zeigt oder verbirgt die Vertrauenskarte mit Informationen über den ausgewählten LLM-Anbieter."

-- Choose the minimum confidence level that all LLM providers must meet. This way, you can ensure that only trustworthy providers are used. You cannot use any provider that falls below this level.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONMINCONFIDENCESELECTION::T2526727283"] = "Wählen Sie das minimale Vertrauensniveau, das alle LLM-Anbieter erfüllen müssen. So stellen Sie sicher, dass nur vertrauenswürdige Anbieter verwendet werden. Anbieter, die dieses Niveau unterschreiten, können nicht verwendet werden."

-- Select a minimum confidence level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONMINCONFIDENCESELECTION::T2579793544"] = "Wählen Sie ein minimales Vertrauensniveau aus"

-- You have selected 1 preview feature.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONMULTISELECT::T1384241824"] = "Sie haben 1 Vorschaufunktion ausgewählt."

-- No preview features selected.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONMULTISELECT::T2809641588"] = "Keine Vorschaufunktionen ausgewählt."

-- You have selected {0} preview features.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONMULTISELECT::T3513450626"] = "Sie haben {0} Vorschaufunktionen ausgewählt."

-- Preselected provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONPROVIDERSELECTION::T1469984996"] = "Vorausgewählter Anbieter"

-- Use app default
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::CONFIGURATIONPROVIDERSELECTION::T3672477670"] = "App-Standard verwenden"

-- Yes, let the AI decide which data sources are needed.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T1031370894"] = "Ja, lassen Sie die KI entscheiden, welche Datenquellen benötigt werden."

-- Yes, let the AI validate & filter the retrieved data.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T1309929755"] = "Ja, die KI soll die abgerufenen Daten überprüfen und filtern."

-- Data Source Selection
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T15302104"] = "Datenauswahl"

-- AI-Selected Data Sources
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T168406579"] = "KI-ausgewählte Datenquellen"

-- AI-based data validation
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T1744745490"] = "KI-gestützte Datenvalidierung"

-- Yes, I want to use data sources.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T1975014927"] = "Ja, ich möchte Datenquellen verwenden."

-- You haven't configured any data sources. To grant the AI access to your data, you need to add such a source. However, if you wish to use data from your device, you first have to set up a so-called embedding. This embedding is necessary so the AI can effectively search your data, find and retrieve the correct information required for each task. In addition to local data, you can also incorporate your company's data. To do so, your company must provide the data through an ERI (External Retrieval Interface).
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T2113594442"] = "Sie haben noch keine Datenquellen konfiguriert. Um der KI Zugriff auf Ihre Daten zu ermöglichen, müssen Sie zunächst eine solche Quelle hinzufügen. Wenn Sie jedoch Daten von Ihrem Gerät verwenden möchten, müssen Sie zuerst ein sogenanntes Embedding einrichten. Dieses Embedding ist notwendig, damit die KI Ihre Daten effektiv durchsuchen, die passenden Informationen finden und für jede Aufgabe bereitstellen kann. Neben lokalen Daten können Sie auch die Daten Ihres Unternehmens einbinden. Dafür muss Ihr Unternehmen die Daten über eine ERI (External Retrieval Interface) bereitstellen."

-- Select the data you want to use here.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T21181525"] = "Wählen Sie hier die Daten aus, die Sie verwenden möchten."

-- Manage your data sources
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T2149927097"] = "Verwalten Sie Ihre Datenquellen"

-- Select data
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T274155039"] = "Daten auswählen"

-- Read more about ERI
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3095532189"] = "Mehr über ERI erfahren"

-- AI-based data source selection
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3100256862"] = "KI-basierte Datenauswahl"

-- No, I don't want to use data sources.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3135725655"] = "Nein, ich möchte keine Datenquellen verwenden."

-- Your data sources cannot be used with the LLM provider you selected due to data privacy, or they are currently unavailable.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3215374102"] = "Ihre Datenquellen können mit dem von Ihnen ausgewählten LLM-Anbieter aufgrund von Datenschutzbestimmungen nicht verwendet werden oder sind derzeit nicht verfügbar."

-- No, I manually decide which data source to use.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3440789294"] = "Nein, ich wähle die Datenquelle manuell aus."

-- Close
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3448155331"] = "Schließen"

-- The AI evaluates each of your inputs to determine whether and which data sources are necessary. Currently, the AI has not selected any source.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3574254516"] = "Die KI bewertet jede Ihrer Eingaben, um zu bestimmen, ob und welche Datenquellen notwendig sind. Derzeit hat die KI keine Quelle ausgewählt."

-- No, use all data retrieved from the data sources.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T3751463241"] = "Nein, verwende alle Daten, die aus den Datenquellen abgerufen wurden."

-- Are data sources enabled?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T396683085"] = "Sind Datenquellen aktiviert?"

-- Manage Data Sources
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T700666808"] = "Datenquellen verwalten"

-- Available Data Sources
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::DATASOURCESELECTION::T86053874"] = "Verfügbare Datenquellen"

-- Issues
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::ISSUES::T3229841001"] = "Probleme"

-- Given that my employer's workplace uses both Windows and Linux, I wanted a cross-platform solution that would work seamlessly across all major operating systems, including macOS. Additionally, I wanted to demonstrate that it is possible to create modern, efficient, cross-platform applications without resorting to Electron bloatware. The combination of .NET and Rust with Tauri proved to be an excellent technology stack for building such robust applications.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T1057189794"] = "Da mein Arbeitgeber sowohl Windows als auch Linux am Arbeitsplatz nutzt, wollte ich eine plattformübergreifende Lösung, die nahtlos auf allen wichtigen Betriebssystemen, einschließlich macOS, funktioniert. Außerdem wollte ich zeigen, dass es möglich ist, moderne, effiziente und plattformübergreifende Anwendungen zu erstellen, ohne auf Software-Balast, wie z.B. das Electron-Framework, zurückzugreifen. Die Kombination aus .NET und Rust mit Tauri hat sich dabei als hervorragender Technologiestapel für den Bau solch robuster Anwendungen erwiesen."

-- Limitations of Existing Solutions
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T1086130692"] = "Einschränkungen bestehender Lösungen"

-- Personal Needs and Limitations of Web Services
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T1839655973"] = "Persönliche Bedürfnisse und Einschränkungen von Webdiensten"

-- While exploring available solutions, I found a desktop application called Anything LLM. Unfortunately, it fell short of meeting my specific requirements and lacked the user interface design I envisioned. For macOS, there were several apps similar to what I had in mind, but they were all commercial solutions shrouded in uncertainty. The developers' identities and the origins of these apps were unclear, raising significant security concerns. Reports from users about stolen API keys and unwanted charges only amplified my reservations.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T3552777197"] = "Während ich nach passenden Lösungen suchte, stieß ich auf eine Desktop-Anwendung namens Anything LLM. Leider konnte sie meine spezifischen Anforderungen nicht erfüllen und entsprach auch nicht dem Benutzeroberflächendesign, das ich mir vorgestellt hatte. Für macOS gab es zwar mehrere Apps, die meiner Vorstellung ähnelten, aber sie waren allesamt kostenpflichtige Lösungen mit unklarer Herkunft. Die Identität der Entwickler und die Ursprünge dieser Apps waren nicht ersichtlich, was erhebliche Sicherheitsbedenken hervorrief. Berichte von Nutzern über gestohlene API-Schlüssel und unerwünschte Abbuchungen verstärkten meine Bedenken zusätzlich."

-- Hello, my name is Thorsten Sommer, and I am the initial creator of MindWork AI Studio. The motivation behind developing this app stems from several crucial needs and observations I made over time.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T3569462457"] = "Hallo, mein Name ist Thorsten Sommer und ich bin der ursprüngliche Entwickler von MindWork AI Studio. Die Motivation zur Entwicklung dieser App entstand aus mehreren wichtigen Bedürfnissen und Beobachtungen, die ich im Laufe der Zeit gemacht habe."

-- Through MindWork AI Studio, I aim to provide a secure, flexible, and user-friendly tool that caters to a wider audience without compromising on functionality or design. This app is the culmination of my desire to meet personal requirements, address existing gaps in the market, and showcase innovative development practices.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T3622193740"] = "Mit MindWork AI Studio möchte ich ein sicheres, flexibles und benutzerfreundliches Werkzeug bereitstellen, das für ein breites Publikum geeignet ist, ohne Kompromisse bei Funktionalität oder Design einzugehen. Diese App ist das Ergebnis meines Wunsches, persönliche Anforderungen zu erfüllen, bestehende Lücken auf dem Markt zu schließen und innovative Entwicklungsmethoden zu präsentieren."

-- Relying on web services like ChatGPT was not a sustainable solution for me. I needed an AI that could also access files directly on my device, a functionality web services inherently lack due to security and privacy constraints. Although I could have scripted something in Python to meet my needs, this approach was too cumbersome for daily use. More importantly, I wanted to develop a solution that anyone could use without needing any programming knowledge.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T372007989"] = "Sich auf Webdienste wie ChatGPT zu verlassen, war für mich keine nachhaltige Lösung. Ich brauchte eine KI, die auch direkt auf Dateien auf meinem Gerät zugreifen kann – eine Funktion, die Webdienste aus Sicherheits- und Datenschutzgründen grundsätzlich nicht bieten. Zwar hätte ich mir eine eigene Lösung in Python programmieren können, aber das wäre für den Alltag zu umständlich gewesen. Noch wichtiger war mir, eine Lösung zu entwickeln, die jeder nutzen kann, ganz ohne Programmierkenntnisse."

-- Cross-Platform and Modern Development
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::MOTIVATION::T843057510"] = "Plattformübergreifende und moderne Entwicklung"

-- Alpha phase means that we are working on the last details before the beta phase.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWALPHA::T166807685"] = "Alpha-Phase bedeutet, dass wir an den letzten Details arbeiten, bevor die Beta-Phase beginnt."

-- This feature is currently in the alpha phase. Expect bugs and unfinished work.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWALPHA::T2635524607"] = "Diese Funktion befindet sich derzeit in der Alpha-Phase. Es können Fehler und unfertige Elemente auftreten."

-- Alpha
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWALPHA::T55079499"] = "Alpha"

-- This feature is currently in the beta phase. It is still be possible that there are some bugs.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWBETA::T1045026949"] = "Diese Funktion befindet sich derzeit in der Beta-Phase. Es kann noch zu Fehlern kommen."

-- Beta phase means that we are testing the feature.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWBETA::T3605158616"] = "Beta-Phase bedeutet, dass wir diese Funktion testen."

-- Beta
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWBETA::T375487463"] = "Beta"

-- This feature is currently in the experimental phase. Expect bugs, unfinished work, changes in future versions, and more.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWEXPERIMENTAL::T1735169242"] = "Dieses Feature befindet sich derzeit in der experimentellen Phase. Es kann zu Fehlern, unfertigen Funktionen, Änderungen in zukünftigen Versionen und Ähnlichem kommen."

-- Experimental phase means that we have a vision for a feature but not a clear plan yet. We are still exploring the possibilities.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWEXPERIMENTAL::T3709099979"] = "Experimentelle Phase bedeutet, dass wir eine Vorstellung von einer Funktion haben, aber noch keinen klaren Plan. Wir erkunden derzeit noch die Möglichkeiten."

-- Experimental
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWEXPERIMENTAL::T3729365343"] = "Experimentell"

-- Prototype
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWPROTOTYPE::T1043365177"] = "Prototyp"

-- Prototype phase means that we have a plan but we are still working on it.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWPROTOTYPE::T2995187557"] = "Die Prototypenphase bedeutet, dass wir einen Plan haben, aber noch daran arbeiten."

-- This feature is currently in the prototype phase. Expect bugs, unfinished work, changes in future versions, and more.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWPROTOTYPE::T4145334644"] = "Diese Funktion befindet sich derzeit in der Prototypphase. Es können Fehler, unfertige Arbeiten, Änderungen in zukünftigen Versionen und anderes auftreten."

-- This feature is about to be released. We think it's ready for production. There should be no more bugs.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWRELEASECANDIDATE::T2003588956"] = "Diese Funktion wird in Kürze veröffentlicht. Wir halten sie für bereit zur Nutzung. Es sollten keine Fehler mehr auftreten."

-- Release Candidate
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWRELEASECANDIDATE::T3451939995"] = "Release-Kandidat"

-- Release candidates are the final step before a feature is proven to be stable.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PREVIEWRELEASECANDIDATE::T696585888"] = "Release-Kandidaten sind der letzte Schritt, bevor eine Funktion als stabil gilt."

-- Select one of your profiles
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PROFILEFORMSELECTION::T2003449133"] = "Wählen Sie eines Ihrer Profile aus"

-- You can switch between your profiles here
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PROFILESELECTION::T918741365"] = "Hier kannst du zwischen deinen Profilen wechseln."

-- Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::PROVIDERSELECTION::T900237532"] = "Anbieter"

-- The content is cleaned using an LLM agent: the main content is extracted, advertisements and other irrelevant things are attempted to be removed; relative links are attempted to be converted into absolute links so that they can be used.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T1164201762"] = "Der Inhalt wird mithilfe eines LLM-Agents bereinigt: Der Hauptinhalt wird extrahiert, Werbung und andere irrelevante Elemente werden nach Möglichkeit entfernt. Relative Links werden nach Möglichkeit in absolute Links umgewandelt, damit sie verwendet werden können."

-- Fetch
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T1396322691"] = "Abrufen"

-- Please select a provider to use the cleanup agent.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T2035652317"] = "Bitte wählen Sie einen Anbieter aus, um den Bereinigungsassistenten zu verwenden."

-- Please provide a URL to load the content from.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T2235427807"] = "Bitte geben Sie eine URL an, von der der Inhalt geladen werden soll."

-- Loads the content from your URL. Does not work when the content is hidden behind a paywall.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T2672192696"] = "Lädt den Inhalt von Ihrer URL. Funktioniert nicht, wenn der Inhalt hinter einer Bezahlschranke verborgen ist."

-- URL from which to load the content
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T2883163022"] = "URL, von der der Inhalt geladen werden soll"

-- Read content from web?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T2927391091"] = "Inhalte aus dem Internet lesen?"

-- Cleanup content by using an LLM agent?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T2939928117"] = "Inhalte mit einem LLM-Agenten bereinigen?"

-- Hide web content options
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T3031774728"] = "Web-Inhaltoptionen ausblenden"

-- Please provide a valid HTTP or HTTPS URL.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T307442288"] = "Bitte geben Sie eine gültige HTTP- oder HTTPS-URL ein."

-- No content cleaning
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T3588401674"] = "Keine Inhaltsbereinigung"

-- Please provide a valid URL.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T3825586228"] = "Bitte geben Sie eine gültige URL ein."

-- Show web content options
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::READWEBCONTENT::T4249712357"] = "Web-Inhalte anzeigen"

-- Spellchecking is disabled
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1059411425"] = "Rechtschreibprüfung ist deaktiviert"

-- Do you want to show preview features in the app?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1118505044"] = "Möchtest du Vorschaufunktionen in der App anzeigen?"

-- How often should we check for app updates?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1364944735"] = "Wie oft sollen wir nach App-Updates suchen?"

-- Select preview features
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1439783084"] = "Vorschaufunktionen auswählen"

-- Select the desired behavior for the navigation bar.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1555038969"] = "Wählen Sie das gewünschte Verhalten für die Navigationsleiste aus."

-- Color theme
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1599198973"] = "Farbthema"

-- Would you like to set one of your profiles as the default for the entire app? When you configure a different profile for an assistant, it will always take precedence.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1666052109"] = "Möchten Sie eines Ihrer Profile als Standard für die gesamte App festlegen? Wenn Sie einem Assistenten ein anderes Profil zuweisen, hat dieses immer Vorrang."

-- Select the language behavior for the app. The default is to use the system language. You might want to choose a language manually?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T186780842"] = "Wählen Sie das Sprachverhalten für die App aus. Standardmäßig wird die Systemsprache verwendet. Möchten Sie die Sprache manuell einstellen?"

-- Check for updates
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1890416390"] = "Nach Updates suchen"

-- Which preview features would you like to enable?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1898060643"] = "Welche Vorschaufunktionen möchten Sie aktivieren?"

-- Select the language for the app.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T1907446663"] = "Wählen Sie die Sprache für die App aus."

-- Language behavior
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T2341504363"] = "Sprachverhalten"

-- Language
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T2591284123"] = "Sprache"

-- Save energy?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T3100928009"] = "Energie sparen?"

-- Spellchecking is enabled
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T3165555978"] = "Rechtschreibprüfung ist aktiviert"

-- App Options
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T3577148634"] = "App-Einstellungen"

-- When enabled, streamed content from the AI is updated once every third second. When disabled, streamed content will be updated as soon as it is available.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T3652888444"] = "Wenn aktiviert, wird gestreamter Inhalt von der KI alle drei Sekunden aktualisiert. Wenn deaktiviert, wird gestreamter Inhalt sofort aktualisiert, sobald er verfügbar ist."

-- Enable spellchecking?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T3914529369"] = "Rechtschreibprüfung aktivieren?"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T4004501229"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- When enabled, spellchecking will be active in all input fields. Depending on your operating system, errors may not be visually highlighted, but right-clicking may still offer possible corrections.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T4067492921"] = "Wenn aktiviert, ist die Rechtschreibprüfung in allen Eingabefeldern aktiv. Je nach Betriebssystem werden Fehler möglicherweise nicht visuell hervorgehoben, aber ein Rechtsklick kann dennoch Korrekturvorschläge anzeigen."

-- Navigation bar behavior
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T602293588"] = "Verhalten der Navigationsleiste"

-- Choose the color theme that best suits for you.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T654667432"] = "Wählen Sie das Farbschema, das am besten zu Ihnen passt."

-- Energy saving is enabled
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T71162186"] = "Energiesparmodus ist aktiviert"

-- Energy saving is disabled
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T716338721"] = "Energiesparmodus ist deaktiviert"

-- Preview feature visibility
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T817101267"] = "Sichtbarkeit der Vorschaufunktion"

-- Would you like to set one provider as the default for the entire app? When you configure a different provider for an assistant, it will always take precedence.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELAPP::T844514734"] = "Möchten Sie einen Anbieter als Standard für die gesamte App festlegen? Wenn Sie einen anderen Anbieter für einen Assistenten konfigurieren, hat dieser immer Vorrang."

-- Control how the LLM provider for loaded chats is selected and when assistant results are sent to chat.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T172255919"] = "Legen Sie fest, wie der LLM-Anbieter für geladene Chats ausgewählt wird und wann Assistenten-Ergebnisse an den Chat gesendet werden."

-- Chat Options
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T1757092713"] = "Chat-Optionen"

-- Shortcut to send input
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T1773585398"] = "Tastenkombination zum Senden der Eingabe"

-- Provider selection when creating new chats
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T189306836"] = "Anbieterauswahl beim Erstellen neuer Chats"

-- Would you like to set one of your profiles as the default for chats?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T1933521846"] = "Möchten Sie eines Ihrer Profile als Standardprofil für Chats festlegen?"

-- Apply default data source option when sending assistant results to chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T2510376349"] = "Standarddatenquelle verwenden, wenn Assistentenergebnisse in den Chat gesendet werden"

-- Control how the LLM provider for added chats is selected.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T263621180"] = "Steuern Sie, wie der LLM-Anbieter für hinzugefügte Chats ausgewählt wird."

-- Provider selection when loading a chat and sending assistant results to chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T2868379953"] = "Anbieterauswahl beim Laden eines Chats und beim Senden von Assistentenergebnissen in den Chat"

-- Show the latest message after loading?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T2913693228"] = "Die neueste Nachricht nach dem Laden anzeigen?"

-- Do you want to use any shortcut to send your input?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T2936560092"] = "Möchten Sie eine Tastenkombination verwenden, um Ihre Eingabe zu senden?"

-- No chat options are preselected
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T3383186996"] = "Keine Chat-Optionen sind vorausgewählt"

-- First (oldest) message is shown, after loading a chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T3507181366"] = "Die erste (älteste) Nachricht wird nach dem Laden eines Chats angezeigt."

-- Preselect chat options?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T3728624759"] = "Chat-Optionen vorauswählen?"

-- Chat options are preselected
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T3730599555"] = "Chat-Optionen sind vorausgewählt"

-- Latest message is shown, after loading a chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T3755993611"] = "Die neueste Nachricht wird nach dem Laden eines Chats angezeigt."

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T4004501229"] = "Eines Ihrer Profile vorauswählen?"

-- Do you want to apply the default data source options when sending assistant results to chat?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T4033153439"] = "Möchten Sie die Standardoptionen für Datenquellen verwenden, wenn die Ergebnisse des Assistenten an den Chat gesendet werden?"

-- When enabled, you can preselect chat options. This is might be useful when you prefer a specific provider.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T477675197"] = "Wenn aktiviert, können Sie Chat-Optionen im Voraus auswählen. Das kann nützlich sein, wenn Sie einen bestimmten Anbieter bevorzugen."

-- You can set default data sources and options for new chats. You can change these settings later for each individual chat.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T492357592"] = "Sie können Standarddatenquellen und -optionen für neue Chats festlegen. Diese Einstellungen lassen sich später für jeden einzelnen Chat anpassen."

-- When enabled, the latest message is shown after loading a chat. When disabled, the first (oldest) message is shown.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELCHAT::T582516016"] = "Wenn diese Option aktiviert ist, wird nach dem Laden eines Chats die neueste Nachricht angezeigt. Wenn sie deaktiviert ist, wird die erste (älteste) Nachricht angezeigt."

-- Edit Profile
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T1143111468"] = "Profil bearbeiten"

-- Configure Profiles
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T1352823555"] = "Profile konfigurieren"

-- No profiles configured yet.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T1433534732"] = "Noch keine Profile eingerichtet."

-- Delete
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T1469573738"] = "Löschen"

-- Your Profiles
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T2378610256"] = "Ihre Profile"

-- Edit
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T3267849393"] = "Bearbeiten"

-- Profile Name
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T3392578705"] = "Profilname"

-- Delete Profile
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T3804515427"] = "Profil löschen"

-- Actions
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T3865031940"] = "Aktionen"

-- Store personal data about yourself in various profiles so that the AIs know your personal context. This saves you from having to explain your context each time, for example, in every chat. When you have different roles, you can create a profile for each role.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T4125557797"] = "Speichere persönliche Daten über dich in verschiedenen Profilen, damit die KIs deinen persönlichen Kontext kennen. So musst du deinen Kontext nicht jedes Mal erneut erklären, zum Beispiel in jedem Chat. Wenn du verschiedene Rollen hast, kannst du für jede Rolle ein eigenes Profil anlegen."

-- Add Profile
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T4248067241"] = "Profil hinzufügen"

-- Are you sure you want to delete the profile '{0}'?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T55364659"] = "Möchten Sie das Profil „{0}“ wirklich löschen?"

-- Are you a project manager in a research facility? You might want to create a profile for your project management activities, one for your scientific work, and a profile for when you need to write program code. In these profiles, you can record how much experience you have or which methods you like or dislike using. Later, you can choose when and where you want to use each profile.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROFILES::T56359901"] = "Sind Sie Projektleiter in einer Forschungseinrichtung? Dann möchten Sie vielleicht ein Profil für Ihre Projektmanagement-Aktivitäten anlegen, eines für Ihre wissenschaftliche Arbeit und ein weiteres Profil, wenn Sie Programmcode schreiben müssen. In diesen Profilen können Sie festhalten, wie viel Erfahrung Sie haben oder welche Methoden Sie bevorzugen oder nicht gerne verwenden. Später können Sie dann auswählen, wann und wo Sie jedes Profil nutzen möchten."

-- Show provider's confidence level?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1052533048"] = "Anzeigen, wie sicher sich der Anbieter ist?"

-- Delete
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1469573738"] = "Löschen"

-- When enabled, we show you the confidence level for the selected provider in the app. This helps you assess where you are sending your data at any time. Example: are you currently working with sensitive data? Then choose a particularly trustworthy provider, etc.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1505516304"] = "Wenn diese Option aktiviert ist, zeigen wir Ihnen das Vertrauensniveau des ausgewählten Anbieters in der App an. So können Sie jederzeit einschätzen, wohin Ihre Daten gesendet werden. Beispiel: Arbeiten Sie gerade mit sensiblen Daten? Dann wählen Sie einen besonders vertrauenswürdigen Anbieter usw."

-- No, please hide the confidence level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1628475119"] = "Nein, bitte verbergen Sie das Vertrauensniveau."

-- Description
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1725856265"] = "Beschreibung"

-- Add Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1806589097"] = "Anbieter hinzufügen"

-- Edit LLM Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T1868766523"] = "LLM-Anbieter bearbeiten"

-- Are you sure you want to delete the provider '{0}'?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2031310917"] = "Möchten Sie den Anbieter „{0}“ wirklich löschen?"

-- Do you want to always be able to recognize how trustworthy your LLM providers are? This way, you keep control over which provider you send your data to. You have two options for this: Either you choose a common schema, or you configure the trust levels for each LLM provider yourself.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2082904277"] = "Möchten Sie immer erkennen können, wie vertrauenswürdig Ihre LLM-Anbieter sind? So behalten Sie die Kontrolle darüber, an welchen Anbieter Sie Ihre Daten senden. Dafür haben Sie zwei Möglichkeiten: Entweder wählen Sie ein gängiges Schema, oder Sie konfigurieren die Vertrauensstufen für jeden LLM-Anbieter selbst."

-- Model
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2189814010"] = "Modell"

-- Choose the scheme that best suits you and your life. Do you trust any western provider? Or only providers from the USA or exclusively European providers? Then choose the appropriate scheme. Alternatively, you can assign the confidence levels to each provider yourself.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2283885378"] = "Wähle das Schema, das am besten zu dir und deinem Leben passt. Vertraust du einem westlichen Anbieter? Oder nur Anbietern aus den USA oder ausschließlich europäischen Anbietern? Dann wähle das passende Schema aus. Alternativ kannst du auch die Vertrauensstufen für jeden Anbieter selbst festlegen."

-- LLM Provider Confidence
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2349972795"] = "LLM-Anbieter-Vertrauen"

-- What we call a provider is the combination of an LLM provider such as OpenAI and a model like GPT-4o. You can configure as many providers as you want. This way, you can use the appropriate model for each task. As an LLM provider, you can also choose local providers. However, to use this app, you must configure at least one provider.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2460361126"] = "Was wir als „Anbieter“ bezeichnen, ist die Kombination aus einem LLM-Anbieter wie OpenAI und einem Modell wie GPT-4o. Sie können beliebig viele Anbieter einrichten. So können Sie für jede Aufgabe das passende Modell nutzen. Als LLM-Anbieter können Sie auch lokale Anbieter auswählen. Um diese App zu verwenden, müssen Sie jedoch mindestens einen Anbieter konfigurieren."

-- Confidence Level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2492230131"] = "Vertrauensniveau"

-- When enabled, you can enforce a minimum confidence level for all LLM providers. This way, you can ensure that only trustworthy providers are used.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T281063702"] = "Wenn aktiviert, kannst du einen minimalen Vertrauensgrad für alle LLM-Anbieter festlegen. So stellst du sicher, dass nur vertrauenswürdige Anbieter verwendet werden."

-- Instance Name
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2842060373"] = "Instanzname"

-- No providers configured yet.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T2911731076"] = "Noch keine Anbieter konfiguriert."

-- Configure Providers
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3027859089"] = "Anbieter konfigurieren"

-- as selected by provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3082210376"] = "wie vom Anbieter ausgewählt"

-- Edit
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3267849393"] = "Bearbeiten"

-- Couldn't delete the provider '{0}'. The issue: {1}. We can ignore this issue and delete the provider anyway. Do you want to ignore it and delete this provider?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3313715309"] = "Der Anbieter „{0}“ konnte nicht gelöscht werden. Das Problem: {1}. Sie können dieses Problem ignorieren und den Anbieter trotzdem löschen. Möchten Sie das Problem ignorieren und diesen Anbieter löschen?"

-- Add LLM Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3346433704"] = "LLM-Anbieter hinzufügen"

-- LLM Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3612415205"] = "LLM-Anbieter"

-- No, do not enforce a minimum confidence level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3642102079"] = "Nein, kein Mindestvertrauensniveau erzwingen"

-- Configured Providers
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3850871263"] = "Konfigurierte Anbieter"

-- Actions
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T3865031940"] = "Aktionen"

-- Select a confidence scheme
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T4144206465"] = "Wählen Sie ein Vertrauensschema aus"

-- Do you want to enforce an app-wide minimum confidence level?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T4258968041"] = "Möchten Sie einen appweiten Mindestvertrauenswert festlegen?"

-- Delete LLM Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T4269256234"] = "LLM-Anbieter löschen"

-- Yes, enforce a minimum confidence level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T458854917"] = "Ja, ein Mindestvertrauensniveau erzwingen"

-- Not yet configured
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T48051324"] = "Noch nicht konfiguriert"

-- Open Dashboard
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T78223861"] = "Dashboard öffnen"

-- Yes, show me the confidence level
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T853225204"] = "Ja, zeige mir das Vertrauensniveau"

-- Provider
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELPROVIDERS::T900237532"] = "Anbieter"

-- If and when should we delete your temporary chats?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T1014418451"] = "Sollten wir Ihre temporären Chats löschen, und wenn ja, wann?"

-- Workspace display behavior
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T2151409362"] = "Verhalten der Arbeitsbereichsanzeige"

-- Workspace behavior
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T2562846516"] = "Arbeitsbereich-Verhalten"

-- How should we display your workspaces?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T3566924898"] = "Wie sollen wir Ihre Arbeitsbereiche anzeigen?"

-- Should we store your chats?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T3942969162"] = "Sollen wir Ihre Chats speichern?"

-- Workspace Options
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T476474348"] = "Arbeitsbereich-Optionen"

-- Workspace maintenance
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::SETTINGS::SETTINGSPANELWORKSPACES::T49653413"] = "Arbeitsbereich-Wartung"

-- Copy {0} to the clipboard
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::TEXTINFOLINE::T2206391442"] = "Kopiere {0} in die Zwischenablage"

-- Copy {0} to the clipboard
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::TEXTINFOLINES::T2206391442"] = "{0} in die Zwischenablage kopieren"

-- Open the repository or website
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::THIRDPARTYCOMPONENT::T1392042694"] = "Repository oder Website öffnen"

-- License:
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::THIRDPARTYCOMPONENT::T1908172666"] = "Lizenz:"

-- You'll interact with the AI systems using your voice. To achieve this, we want to integrate voice input (speech-to-text) and output (text-to-speech). However, later on, it should also have a natural conversation flow, i.e., seamless conversation.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1015366320"] = "Du wirst mit den KI-Systemen über deine Stimme interagieren. Dafür möchten wir Sprach­eingabe (Sprache-zu-Text) und Sprach­ausgabe (Text-zu-Sprache) integrieren. Später soll außerdem ein natürlicher Gesprächsfluss möglich sein, also eine nahtlose Unterhaltung."

-- We hope this vision excites you as much as it excites us. Together, let's build a powerful and flexible AI toolkit to support all your creative, professional, and everyday needs with MindWork AI Studio.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1061000046"] = "Wir hoffen, dass diese Vision Sie genauso begeistert wie uns. Lassen Sie uns gemeinsam mit MindWork AI Studio ein leistungsstarkes und flexibles KI-Werkzeug schaffen, das Sie bei all Ihren kreativen, beruflichen und alltäglichen Aufgaben unterstützt."

-- Integration of enterprise data
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1127694951"] = "Integration von Unternehmensdaten"

-- Meet your needs
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T127032776"] = "Entspricht Ihren Bedürfnissen"

-- We're integrating a writing mode to help you create extensive works, like comprehensive project proposals, tenders, or your next fantasy novel.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1457213518"] = "Wir integrieren einen Schreibmodus, der Ihnen dabei hilft, umfangreiche Werke zu erstellen – zum Beispiel ausführliche Projektvorschläge, Ausschreibungen oder Ihren nächsten Fantasyroman."

-- Email monitoring
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1520989255"] = "E-Mail-Überwachung"

-- You'll be able to integrate your data into AI Studio, like your PDF or Office files, or your Markdown notes.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1648606751"] = "Sie können Ihre Daten in AI Studio integrieren, zum Beispiel Ihre PDF- oder Office-Dateien oder Ihre Markdown-Notizen."

-- It will soon be possible to integrate data from the corporate network using a specified interface (External Retrieval Interface, ERI for short). This will likely require development work by the organization in question.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T1926587044"] = "Bald wird es möglich sein, Daten aus dem Firmennetzwerk über eine festgelegte Schnittstelle (External Retrieval Interface, kurz ERI) zu integrieren. Dafür wird voraussichtlich Entwicklungsaufwand seitens der jeweiligen Organisation nötig sein."

-- Whatever your job or task is, MindWork AI Studio aims to meet your needs: whether you're a project manager, scientist, artist, author, software developer, or game developer.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2144737937"] = "Was auch immer Ihr Beruf oder Ihre Aufgabe ist, MindWork AI Studio möchte Ihre Bedürfnisse erfüllen: Egal, ob Sie Projektmanager, Wissenschaftler, Künstler, Autor, Softwareentwickler oder Spieleentwickler sind."

-- You can connect your email inboxes with AI Studio. The AI will read your emails and notify you of important events. You'll also be able to access knowledge from your emails in your chats.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2289234741"] = "Sie können Ihre E-Mail-Postfächer mit AI Studio verbinden. Die KI liest Ihre E-Mails und benachrichtigt Sie über wichtige Ereignisse. Außerdem haben Sie in Ihren Chats Zugriff auf das Wissen aus Ihren E-Mails."

-- Browser usage
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2345974992"] = "Browser-Nutzung"

-- Integrating your data
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2416595938"] = "Integration Ihrer Daten"

-- Curious about the vision for MindWork AI Studio and what the future holds? We're here to address just that. Remember, this is a free, open-source project, meaning we can't guarantee when or if this vision will be fully realized. Our aim is to share our vision with you to help you decide whether this app is right for you.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2491403346"] = "Neugierig auf die Vision von MindWork AI Studio und darauf, was die Zukunft bringt? Genau darauf möchten wir eingehen. Bitte denken Sie daran, dass dies ein kostenloses Open-Source-Projekt ist. Das bedeutet, wir können nicht garantieren, wann oder ob diese Vision vollständig umgesetzt wird. Unser Ziel ist es, Ihnen unsere Vision zu vermitteln, damit Sie entscheiden können, ob diese App das Richtige für Sie ist."

-- Voice control
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2827242540"] = "Sprachsteuerung"

-- Specific requirements
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2868740431"] = "Spezifische Anforderungen"

-- We'll develop more assistants for everyday tasks.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T2899555955"] = "Wir werden weitere Assistenten für alltägliche Aufgaben entwickeln."

-- We're working on offering AI Studio features in your browser via a plugin, allowing, e.g., for spell-checking or text rewriting directly in the browser.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T308543246"] = "Wir arbeiten daran, die Funktionen von AI Studio über ein Plugin auch in deinem Browser anzubieten. So kannst du zum Beispiel direkt im Browser Rechtschreibprüfungen durchführen oder Texte umschreiben lassen."

-- There will be an interface for AI Studio to create content in other apps. You could, for example, create blog posts directly on the target platform or add entries to an internal knowledge management tool. This requires development work by the tool developers.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T3290746961"] = "Es wird eine Schnittstelle für AI Studio geben, um Inhalte in anderen Apps zu erstellen. So könnten Sie zum Beispiel Blogbeiträge direkt auf der Zielplattform verfassen oder Einträge zu einem internen Wissensmanagement-Tool hinzufügen. Dafür ist Entwicklungsarbeit durch die jeweiligen Tool-Entwickler erforderlich."

-- Want an assistant that suits your specific needs? We aim to offer a plugin architecture so organizations and enthusiasts can implement such ideas.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T3440464089"] = "Sie möchten einen Assistenten, der genau auf Ihre Bedürfnisse zugeschnitten ist? Wir planen, eine Plugin-Architektur anzubieten, damit Organisationen und Interessierte solche Ideen umsetzen können."

-- Writing mode
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T3640675146"] = "Schreibmodus"

-- So, where are we headed, and how could the app evolve in the coming months and years? The following list outlines our ideas, though not in order of priority:
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T4106960135"] = "Wohin geht die Reise, und wie könnte sich die App in den kommenden Monaten und Jahren weiterentwickeln? Die folgende Liste stellt unsere Ideen vor, allerdings ohne Priorisierung:"

-- Content creation
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T428040679"] = "Erstellung von Inhalten"

-- Useful assistants
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::VISION::T586430036"] = "Nützliche Assistenten"

-- Are you sure you want to delete the chat '{0}' in the workspace '{1}'?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1016188706"] = "Möchten Sie den Chat „{0}“ im Arbeitsbereich „{1}“ wirklich löschen?"

-- Move chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1133040906"] = "Chat verschieben"

-- Delete
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1469573738"] = "Löschen"

-- Rename Workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1474303418"] = "Arbeitsbereich umbenennen"

-- Rename Chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T156144855"] = "Chat umbenennen"

-- Add workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1586005241"] = "Arbeitsbereich hinzufügen"

-- Add chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1874060138"] = "Chat hinzufügen"

-- Create Chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T1939006681"] = "Chat erstellen"

-- Please name your workspace:
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T201482774"] = "Bitte benennen Sie Ihren Arbeitsbereich:"

-- Are you sure you want to load another chat? All unsaved changes will be lost.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T2133593288"] = "Möchten Sie wirklich einen anderen Chat laden? Alle ungespeicherten Änderungen gehen dabei verloren."

-- Are you sure you want to delete the workspace '{0}'? This will also delete {1} chat(s) in this workspace.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T2151341762"] = "Möchten Sie den Arbeitsbereich „{0}“ wirklich löschen? Dadurch werden auch {1} Chat(s) in diesem Arbeitsbereich gelöscht."

-- Are you sure you want to create a another chat? All unsaved changes will be lost.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T2237618267"] = "Möchten Sie wirklich einen neuen Chat erstellen? Alle nicht gespeicherten Änderungen gehen verloren."

-- Delete Chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T2244038752"] = "Chat löschen"

-- Move to workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T2509305748"] = "In den Arbeitsbereich verschieben"

-- Are you sure you want to delete the temporary chat '{0}'?
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T3043761007"] = "Sind Sie sicher, dass Sie den temporären Chat „{0}“ löschen möchten?"

-- Move Chat to Workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T3045856778"] = "Chat in den Arbeitsbereich verschieben"

-- Please enter a new or edit the name for your workspace '{0}':
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T323280982"] = "Bitte geben Sie einen neuen Namen für Ihren Arbeitsbereich „{0}“ ein oder bearbeiten Sie ihn:"

-- Rename
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T3355849203"] = "Umbenennen"

-- Please enter a new or edit the name for your chat '{0}':
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T3419791373"] = "Bitte geben Sie einen neuen Namen für Ihren Chat „{0}“ ein oder bearbeiten Sie ihn:"

-- Load Chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T3555709365"] = "Chat laden"

-- Add Workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T3672981145"] = "Arbeitsbereich hinzufügen"

-- Empty chat
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T4019509364"] = "Leerer Chat"

-- Workspaces
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T4048389951"] = "Arbeitsbereiche"

-- Disappearing Chats
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T4201703117"] = "Verschwindende Chats"

-- Please select the workspace where you want to move the chat to.
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T474393241"] = "Bitte wählen Sie den Arbeitsbereich aus, in den Sie den Chat verschieben möchten."

-- Delete Workspace
UI_TEXT_CONTENT["AISTUDIO::COMPONENTS::WORKSPACES::T701874671"] = "Arbeitsbereich löschen"

-- No
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::CONFIRMDIALOG::T1642511898"] = "Nein"

-- Yes
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::CONFIRMDIALOG::T3013883440"] = "Ja"

-- Tell the AI what you want it to do for you. What are your goals or are you trying to achieve? Like having the AI address you informally.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T1458195391"] = "Teile der KI mit, was sie für dich tun soll. Was sind deine Ziele oder was möchtest du erreichen? Zum Beispiel, dass die KI dich duzt."

-- Update
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T1847791252"] = "Aktualisieren"

-- Tell the AI something about yourself. What is your profession? How experienced are you in this profession? Which technologies do you like?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T2119274961"] = "Erzähle der KI etwas über dich. Was ist dein Beruf? Wie erfahren bist du in diesem Beruf? Welche Technologien magst du?"

-- What should the AI do for you?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T2261456575"] = "Was soll die KI für Sie tun?"

-- Please enter a profile name.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T2386844536"] = "Bitte geben Sie einen Profilnamen ein."

-- The text must not exceed 256 characters.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T2560188276"] = "Der Text darf 256 Zeichen nicht überschreiten."

-- Add
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T2646845972"] = "Hinzufügen"

-- The profile name must not exceed 40 characters.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T3243902394"] = "Der Profilname darf nicht länger als 40 Zeichen sein."

-- The text must not exceed 444 characters.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T3253349421"] = "Der Text darf 444 Zeichen nicht überschreiten."

-- Profile Name
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T3392578705"] = "Profilname"

-- Please enter what the LLM should know about you and/or what actions it should take.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T3708405102"] = "Bitte geben Sie ein, was das LLM über Sie wissen sollte und/oder welche Aktionen es ausführen soll."

-- The name of the profile is mandatory. Each profile must have a unique name. Whether you provide information about yourself or only fill out the actions is up to you. Only one of these pieces is required.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T4061896123"] = "Der Name des Profils ist erforderlich. Jedes Profil muss einen eindeutigen Namen haben. Ob Sie zusätzliche Angaben zu Ihrer Person machen oder nur die Aktionen ausfüllen, bleibt Ihnen überlassen. Es reicht aus, eines von beidem anzugeben."

-- Store personal data about yourself in various profiles so that the AIs know your personal context. This saves you from having to explain your context each time, for example, in every chat. When you have different roles, you can create a profile for each role.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T4125557797"] = "Speichere persönliche Daten über dich in verschiedenen Profilen, damit die KIs deinen persönlichen Kontext kennen. So musst du deinen Kontext nicht jedes Mal, zum Beispiel in jedem Chat, neu erklären. Wenn du unterschiedliche Rollen hast, kannst du für jede Rolle ein eigenes Profil anlegen."

-- What should the AI know about you?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T4227846635"] = "Was sollte die KI über Sie wissen?"

-- Are you a project manager in a research facility? You might want to create a profile for your project management activities, one for your scientific work, and a profile for when you need to write program code. In these profiles, you can record how much experience you have or which methods you like or dislike using. Later, you can choose when and where you want to use each profile.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T56359901"] = "Sind Sie Projektleiter in einer Forschungseinrichtung? Dann möchten Sie vielleicht ein Profil für Ihre Projektmanagement-Aufgaben erstellen, eines für Ihre wissenschaftliche Arbeit und ein Profil für das Schreiben von Programmcode. In diesen Profilen können Sie festhalten, wie viel Erfahrung Sie haben oder welche Methoden Sie gerne oder weniger gerne nutzen. Später können Sie auswählen, wann und wo Sie jedes Profil verwenden möchten."

-- Cancel
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T900713019"] = "Abbrechen"

-- The profile name must be unique; the chosen name is already in use.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROFILEDIALOG::T911748898"] = "Der Profilname muss eindeutig sein; der ausgewählte Name wird bereits verwendet."

-- Hugging Face Inference Provider
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1085481431"] = "Hugging Face Inferenz-Anbieter"

-- Failed to store the API key in the operating system. The message was: {0}. Please try again.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1122745046"] = "Der API-Schlüssel konnte nicht im Betriebssystem gespeichert werden. Die Meldung war: {0}. Bitte versuchen Sie es erneut."

-- API Key
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1324664716"] = "API-Schlüssel"

-- Create account
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1356621346"] = "Konto erstellen"

-- Load models
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T15352225"] = "Modelle laden"

-- Hostname
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1727440780"] = "Hostname"

-- Update
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1847791252"] = "Aktualisieren"

-- Failed to load the API key from the operating system. The message was: {0}. You might ignore this message and provide the API key again.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1870831108"] = "Der API-Schlüssel konnte nicht vom Betriebssystem geladen werden. Die Meldung war: {0}. Sie können diese Meldung ignorieren und den API-Schlüssel erneut eingeben."

-- Please enter a model name.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T1936099896"] = "Bitte geben Sie einen Modellnamen ein."

-- Model
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T2189814010"] = "Modell"

-- (Optional) API Key
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T2331453405"] = "(Optional) API-Schlüssel"

-- Add
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T2646845972"] = "Hinzufügen"

-- No models loaded or available.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T2810182573"] = "Keine Modelle geladen oder verfügbar."

-- Instance Name
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T2842060373"] = "Instanzname"

-- Show available models
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T3763891899"] = "Verfügbare Modelle anzeigen"

-- Host
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T808120719"] = "Host"

-- Provider
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T900237532"] = "Anbieter"

-- Cancel
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::PROVIDERDIALOG::T900713019"] = "Abbrechen"

-- There is no social event
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1222800281"] = "Es gibt keine gesellschaftliche Veranstaltung."

-- Agenda options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1249372829"] = "Agendaoptionen sind vorausgewählt"

-- Preselect a duration?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1404615656"] = "Eine Dauer vorauswählen?"

-- Preselect the number of participants
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1444356399"] = "Anzahl der Teilnehmer vorauswählen"

-- Meeting is virtual
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1446638309"] = "Das Meeting findet online statt"

-- Preselect a name?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1471770981"] = "Einen Namen vorauswählen?"

-- Preselect whether participants needs to arrive and depart
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1648427207"] = "Legen Sie im Voraus fest, ob Teilnehmer anreisen und abreisen müssen"

-- Preselect a start time?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1901151023"] = "Startzeit vorauswählen?"

-- Preselect a location?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1908318849"] = "Standort vorauswählen?"

-- How many participants should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T1998244307"] = "Wie viele Teilnehmer sollen vorausgewählt werden?"

-- Preselect whether the meeting is virtual
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2084951012"] = "Wählen Sie aus, ob das Meeting virtuell ist"

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- When enabled, you can preselect most agenda options. This is might be useful when you need to create similar agendas often.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2373110543"] = "Wenn diese Option aktiviert ist, kannst du die meisten Agendapunkte vorauswählen. Das kann hilfreich sein, wenn du häufig ähnliche Agenden erstellst."

-- Preselect whether the participants should get to know each other
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2519703500"] = "Vorab festlegen, ob sich die Teilnehmenden kennenlernen sollen"

-- Which agenda language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2801220321"] = "Welche Sprache soll für die Tagesordnung vorausgewählt sein?"

-- Preselect another agenda language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2915422331"] = "Wählen Sie eine andere Agendasprache aus"

-- Participants do not need to get to know each other
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T2949002251"] = "Teilnehmer müssen sich nicht kennenlernen"

-- There is a social event
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T296183299"] = "Es gibt eine gesellschaftliche Veranstaltung"

-- Participants should be actively involved
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T298324727"] = "Teilnehmer sollten aktiv mitwirken"

-- Meeting is in person
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3008159782"] = "Das Treffen findet persönlich statt."

-- Participants do not need to arrive and depart
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3087504452"] = "Teilnehmer müssen nicht ankommen oder abreisen."

-- Preselect whether there is a joint dinner
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3175009548"] = "Vorab auswählen, ob ein gemeinsames Abendessen stattfindet"

-- Preselect an objective?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3439476935"] = "Ein Ziel vorauswählen?"

-- Preselect a moderator?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3482798491"] = "Einen Moderator vorauswählen?"

-- Participants need to arrive and depart
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3591032034"] = "Die Teilnehmer müssen ankommen und abreisen"

-- Participants do not need to be actively involved
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3679899885"] = "Teilnehmende müssen nicht aktiv mitwirken"

-- Preselect the approx. lunch time
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3709527588"] = "Wählen Sie die ungefähre Mittagszeit vorab aus"

-- Preselect a topic?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T3835166371"] = "Ein Thema vorauswählen?"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T4004501229"] = "Eines Ihrer Profile vorauswählen?"

-- Preselect the agenda language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T4055846391"] = "Wählen Sie die Sprache der Agenda vorab aus"

-- No agenda options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T4094211586"] = "Keine Tagesordnungspunkte sind vorausgewählt"

-- Participants should get to know each other
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T464127805"] = "Die Teilnehmer sollten sich kennenlernen"

-- Assistant: Agenda Planner Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T677962779"] = "Assistent: Optionen für den Terminplaner"

-- There is a joint dinner
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T707310400"] = "Es gibt ein gemeinsames Abendessen."

-- Preselect the approx. break time
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T722113273"] = "Wählen Sie die ungefähre Pausenzeit vorab aus"

-- There is no joint dinner
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T768936730"] = "Es gibt kein gemeinsames Abendessen."

-- Preselect agenda options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T800921421"] = "Agendaoptionen vorauswählen?"

-- Preselect whether there is a social event
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T816053055"] = "Wählen Sie im Voraus aus, ob eine gesellschaftliche Veranstaltung stattfindet"

-- Preselect whether the participants should actively involved
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGAGENDA::T817726429"] = "Wählen Sie aus, ob die Teilnehmenden aktiv beteiligt sein sollen."

-- Restrict to one bias a day?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T1608129203"] = "Auf einen Bias pro Tag beschränken?"

-- Yes, you can only retrieve one bias per day
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T1765683725"] = "Ja, Sie können nur einmal pro Tag eine Voreingenommenheit abrufen."

-- Reset
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T180921696"] = "Zurücksetzen"

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- No restriction. You can retrieve as many biases as you want per day.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T2305356277"] = "Keine Einschränkung. Sie können beliebig viele Vorurteile pro Tag abrufen."

-- Which language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T2345162613"] = "Welche Sprache soll vorausgewählt werden?"

-- Reset your bias-of-the-day statistics
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T2350981714"] = "Setze deine Statistik zum „Bias des Tages“ zurück"

-- Preselect another language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T2382415529"] = "Eine andere Sprache vorauswählen"

-- Preselect the language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T2571465005"] = "Sprache vorauswählen"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T3448155331"] = "Schließen"

-- No options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T354528094"] = "Es sind keine Optionen vorausgewählt"

-- Assistant: Bias of the Day
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T384887684"] = "Assistent: Vorurteil des Tages"

-- Options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T3875604319"] = "Optionen sind vorausgewählt"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T4004501229"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- Are you sure you want to reset your bias-of-the-day statistics? The system will no longer remember which biases you already know. As a result, biases you are already familiar with may be addressed again.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T405627382"] = "Sind Sie sicher, dass Sie Ihre „Bias des Tages“-Statistiken zurücksetzen möchten? Das System merkt sich dann nicht mehr, welche Verzerrungen Sie bereits kennen. Dadurch kann es sein, dass Ihnen bereits bekannte Verzerrungen erneut angezeigt werden."

-- Assistant: Bias of the Day Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T4235808594"] = "Assistent: Optionen für „Bias des Tages“"

-- Preselect options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T42672465"] = "Optionen vorauswählen?"

-- You have learned about {0} out of {1} biases.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T679061561"] = "Du hast {0} von {1} Vorurteilen kennengelernt."

-- When enabled, you can preselect options. This is might be useful when you prefer a specific language or LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGASSISTANTBIAS::T711745239"] = "Wenn diese Option aktiviert ist, kannst du Voreinstellungen treffen. Das kann nützlich sein, wenn du eine bestimmte Sprache oder ein bestimmtes LLM-Modell bevorzugst."

-- Which programming language should be preselected for added contexts?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T1073540083"] = "Welche Programmiersprache soll für hinzugefügte Kontexte vorausgewählt werden?"

-- Compiler messages are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T1110902070"] = "Compiler-Nachrichten sind vorausgewählt"

-- Preselect a programming language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T2181567002"] = "Programmiersprache vorauswählen"

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- When enabled, you can preselect the coding options. This is might be useful when you prefer a specific programming language or LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T2619641701"] = "Wenn aktiviert, können Sie die Code-Optionen im Voraus auswählen. Das kann nützlich sein, wenn Sie eine bestimmte Programmiersprache oder ein bestimmtes LLM-Modell bevorzugen."

-- Preselect coding options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T2790579667"] = "Codierungsoptionen vorauswählen?"

-- Preselect compiler messages?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T2970689954"] = "Kompilermeldungen vorauswählen?"

-- No coding options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T3015105896"] = "Es sind keine Programmieroptionen vorausgewählt"

-- Coding options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T3567850751"] = "Codierungsoptionen sind vorausgewählt"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T4004501229"] = "Eines Ihrer Profile vorauswählen?"

-- Preselect another programming language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T4230412334"] = "Eine andere Programmiersprache vorauswählen"

-- Compiler messages are not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T516498299"] = "Compiler-Meldungen sind nicht vorausgewählt"

-- Assistant: Coding Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGCODING::T585868261"] = "Assistent: Programmieroptionen"

-- You might configure different data sources. A data source can include one file, all files in a directory, or data from your company. Later, you can incorporate these data sources as needed when the AI requires this data to complete a certain task.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1084943026"] = "Sie können verschiedene Datenquellen konfigurieren. Eine Datenquelle kann eine einzelne Datei, alle Dateien in einem Verzeichnis oder Daten aus Ihrem Unternehmen enthalten. Später können Sie diese Datenquellen bei Bedarf einbinden, wenn die KI diese Daten zur Erledigung einer bestimmten Aufgabe benötigt."

-- Are you sure you want to delete the data source '{0}' of type {1}?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1096979935"] = "Möchten Sie die Datenquelle „{0}“ vom Typ {1} wirklich löschen?"

-- Edit Local Directory Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1215599168"] = "Lokalen Verzeichnis-Datenspeicher bearbeiten"

-- Add Local Directory as Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1454193397"] = "Lokales Verzeichnis als Datenquelle hinzufügen"

-- Delete
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1469573738"] = "Löschen"

-- External (ERI)
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1652430727"] = "Extern (ERI)"

-- Local File
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1687345358"] = "Lokale Datei"

-- Delete Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T1849107431"] = "Datenquelle löschen"

-- Local Directory Data Source Information
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T2146756020"] = "Informationen zur lokalen Verzeichnisdatenquelle"

-- Edit ERI v1 Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T221059217"] = "ERI v1 Datenquelle bearbeiten"

-- Edit Local File Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T2453292893"] = "Lokale Datei-Datenquelle bearbeiten"

-- ERI v1 Data Source Information
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T26243729"] = "ERI v1 Datenquellen-Informationen"

-- Name
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T266367750"] = "Name"

-- No valid embedding
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T2698203405"] = "Keine gültige Einbettung"

-- Embedding
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T2838542994"] = "Einbettung"

-- Edit
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3267849393"] = "Bearbeiten"

-- Add Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3387511033"] = "Datenquelle hinzufügen"

-- Unknown
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3424652889"] = "Unbekannt"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3448155331"] = "Schließen"

-- Add Local File as Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3500365052"] = "Lokale Datei als Datenquelle hinzufügen"

-- Type
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3512062061"] = "Typ"

-- Local File Data Source Information
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3525663993"] = "Informationen zur lokalen Dateiquelle"

-- No data sources configured yet.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3549650120"] = "Noch keine Datenquellen konfiguriert."

-- Actions
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T3865031940"] = "Aktionen"

-- Configured Data Sources
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T543942217"] = "Konfigurierte Datenquellen"

-- Add ERI v1 Data Source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T590005498"] = "ERI v1 Datenquelle hinzufügen"

-- External Data (ERI-Server v1)
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T774473996"] = "Externe Daten (ERI-Server v1)"

-- Local Directory
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGDATASOURCES::T926703547"] = "Lokales Verzeichnis"

-- When enabled, you can preselect some ERI server options.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T1280666275"] = "Wenn aktiviert, können Sie einige ERI-Serveroptionen vorauswählen."

-- Preselect ERI server options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T1664055662"] = "ERI-Serveroptionen vorauswählen?"

-- No ERI server options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T1793785587"] = "Keine ERI-Serveroptionen sind vorausgewählt"

-- Most ERI server options can be customized and saved directly in the ERI server assistant. For this, the ERI server assistant has an auto-save function.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T2093534613"] = "Die meisten ERI-Serveroptionen können direkt im ERI-Server-Assistenten angepasst und gespeichert werden. Dazu verfügt der ERI-Server-Assistent über eine automatische Speicherfunktion."

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T3448155331"] = "Schließen"

-- Assistant: ERI Server Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T3629372826"] = "Assistent: ERI-Server-Optionen"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T4004501229"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- ERI server options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGERISERVER::T488190224"] = "ERI-Serveroptionen sind vorausgewählt"

-- Preselect the target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGGRAMMARSPELLING::T1417990312"] = "Zielsprache vorauswählen"

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGGRAMMARSPELLING::T1462295644"] = "Eine andere Zielsprache vorauswählen"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGGRAMMARSPELLING::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGGRAMMARSPELLING::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- Assistant: Grammar & Spelling Checker Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGGRAMMARSPELLING::T886675455"] = "Assistent: Optionen für Grammatik- und Rechtschreibprüfung"

-- Preselect the target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGI18N::T1417990312"] = "Zielsprache vorauswählen"

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGI18N::T1462295644"] = "Wähle eine andere Zielsprache aus"

-- Assistant: Localization
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGI18N::T2573041664"] = "Assistent: Lokalisierung"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGI18N::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGI18N::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- Preselect the icon source
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGICONFINDER::T1116652851"] = "Wählen Sie die Symbolquelle aus"

-- Assistant: Icon Finder Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGICONFINDER::T1570765862"] = "Assistent: Symbolfinder-Optionen"

-- No icon options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGICONFINDER::T1694910115"] = "Keine Symboloptionen sind vorausgewählt"

-- Icon options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGICONFINDER::T1792507476"] = "Symboloptionen sind vorausgewählt"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGICONFINDER::T3448155331"] = "Schließen"

-- Preselect icon options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGICONFINDER::T725252382"] = "Symboloptionen vorauswählen?"

-- No job posting options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T1257718691"] = "Keine Joboptionen sind vorausgewählt"

-- Preselect some mandatory information about the job posting?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T1332068481"] = "Wählen Sie einige Pflichtangaben zur Stellenanzeige im Voraus aus."

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T1462295644"] = "Wähle eine andere Zielsprache aus"

-- Job posting options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T1827578822"] = "Jobanzeigen-Optionen sind vorausgewählt"

-- Preselect the work location?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T1867962106"] = "Arbeitsort vorauswählen?"

-- Preselect the language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T2571465005"] = "Sprache vorauswählen"

-- Preselect job posting options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T2624983038"] = "Vorauswahl von Stellenanzeigenoptionen?"

-- Preselect the company name?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T2679442990"] = "Firmennamen vorauswählen?"

-- When enabled, you can preselect some job posting options. This is might be useful when you prefer a specific LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T2907036553"] = "Wenn diese Option aktiviert ist, können Sie einige Optionen für die Stellenausschreibung im Voraus auswählen. Das kann nützlich sein, wenn Sie ein bestimmtes LLM-Modell bevorzugen."

-- Preselect the job qualifications?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T3223375709"] = "Qualifikationen für die Stelle vorauswählen?"

-- Assistant: Job Posting Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T3307661496"] = "Assistent: Optionen für Stellenanzeigen"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- Preselect the job responsibilities?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T3788397013"] = "Die Aufgabenbereiche vorab auswählen?"

-- Preselect the job description?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGJOBPOSTINGS::T3825475093"] = "Die Stellenbeschreibung vorauswählen?"

-- Content cleaner agent is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1013787967"] = "Der Content Cleaner-Assistent ist vorausgewählt"

-- Web content reader is shown
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1030372436"] = "Webinhaltsleser wird angezeigt"

-- When enabled, the web content reader is preselected. This is might be useful when you prefer to load legal content from the web very often.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1507288278"] = "Wenn diese Option aktiviert ist, wird der Webinhaltsleser standardmäßig ausgewählt. Das kann nützlich sein, wenn Sie häufig juristische Inhalte aus dem Internet laden möchten."

-- Preselect legal check options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1563865738"] = "Rechtliche Überprüfungsoptionen vorauswählen?"

-- No legal check options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1591931823"] = "Keine rechtlichen Prüfoptionen sind vorausgewählt"

-- When activated, the web content reader is hidden and cannot be used. As a result, the user interface becomes a bit easier to use.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1633101895"] = "Wenn diese Option aktiviert ist, wird der Webinhaltsleser ausgeblendet und kann nicht verwendet werden. Dadurch wird die Benutzeroberfläche etwas einfacher zu bedienen."

-- Web content reader is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1701127912"] = "Web-Inhaltsleser ist nicht vorausgewählt"

-- Content cleaner agent is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T1969816694"] = "Inhaltsbereiniger-Agent ist nicht vorausgewählt"

-- Hide the web content reader?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T2090693677"] = "Webinhaltsleser ausblenden?"

-- When enabled, you can preselect some legal check options. This is might be useful when you prefer a specific LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T2164667361"] = "Wenn aktiviert, können Sie einige rechtliche Prüfoptionen vorauswählen. Dies kann nützlich sein, wenn Sie ein bestimmtes LLM-Modell bevorzugen."

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- Legal check options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T252916114"] = "Rechtsprüfungsoptionen sind vorausgewählt"

-- When enabled, the content cleaner agent is preselected. This is might be useful when you prefer to clean up the legal content before translating it.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T2746583995"] = "Wenn aktiviert, ist der Content Cleaner Agent vorausgewählt. Das kann nützlich sein, wenn Sie den rechtlichen Inhalt bereinigen möchten, bevor Sie ihn übersetzen."

-- Web content reader is hidden
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T2799795311"] = "Web-Inhaltsleser ist ausgeblendet"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T3448155331"] = "Schließen"

-- Web content reader is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T3641773985"] = "Web-Inhaltleser ist vorausgewählt"

-- Preselect the content cleaner agent?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T3649428096"] = "Inhaltsbereinigungs-Assistent vorauswählen?"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T4004501229"] = "Eines deiner Profile vorauswählen?"

-- Assistant: Legal Check Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T4033382756"] = "Assistent: Optionen für rechtliche Prüfung"

-- Preselect the web content reader?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGLEGALCHECK::T629158142"] = "Web-Inhaltsleser vorauswählen?"

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- Which language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T2345162613"] = "Welche Sprache soll vorausgewählt werden?"

-- Preselect another language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T2382415529"] = "Eine andere Sprache vorauswählen"

-- Preselect the language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T2571465005"] = "Sprache vorauswählen"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T3448155331"] = "Schließen"

-- No options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T354528094"] = "Keine Optionen sind vorausgewählt"

-- Assistant: My Tasks Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T3710380967"] = "Assistent: Meine Aufgabenoptionen"

-- Options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T3875604319"] = "Optionen sind vorausgewählt"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T4004501229"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- Preselect options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T42672465"] = "Optionen vorauswählen?"

-- When enabled, you can preselect options. This is might be useful when you prefer a specific language or LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGMYTASKS::T711745239"] = "Wenn aktiviert, können Sie Optionen im Voraus auswählen. Das kann nützlich sein, wenn Sie eine bestimmte Sprache oder ein bestimmtes LLM-Modell bevorzugen."

-- Which writing style should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T1173034744"] = "Welcher Schreibstil soll standardmäßig ausgewählt werden?"

-- Preselect the target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T1417990312"] = "Ziellsprache vorauswählen"

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T1462295644"] = "Eine andere Zielsprache vorauswählen"

-- Preselect a sentence structure
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T1621537655"] = "Satzstruktur vorauswählen"

-- Assistant: Rewrite & Improve Text Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T1995708818"] = "Assistent: Text umschreiben & verbessern"

-- Which voice should be preselected for the sentence structure?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T2661599097"] = "Welche Stimme soll für die Satzstruktur vorausgewählt werden?"

-- Preselect a writing style
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T28456020"] = "Wählen Sie einen Schreibstil aus"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGREWRITE::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- When enabled, you can preselect synonym options. This is might be useful when you prefer a specific language or LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T183953912"] = "Wenn diese Option aktiviert ist, können Sie Synonym-Vorschläge im Voraus auswählen. Dies kann nützlich sein, wenn Sie eine bestimmte Sprache oder ein bestimmtes LLM-Modell bevorzugen."

-- No synonym options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T2183758387"] = "Es sind keine Synonymoptionen vorausgewählt"

-- Which language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T2345162613"] = "Welche Sprache soll vorausgewählt werden?"

-- Preselect another language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T2382415529"] = "Wähle eine andere Sprache aus"

-- Synonym options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T2390458990"] = "Synonym-Optionen sind vorausgewählt"

-- Preselect the language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T2571465005"] = "Sprache vorauswählen"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T3448155331"] = "Schließen"

-- Assistant: Synonyms Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T3889117881"] = "Assistent: Synonyme-Optionen"

-- Preselect synonym options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGSYNONYMS::T4170921846"] = "Synonymoptionen vorauswählen?"

-- Content cleaner agent is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1013787967"] = "Der Inhaltsbereiniger-Assistent ist vorausgewählt"

-- Web content reader is shown
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1030372436"] = "Web-Inhaltsleser wird angezeigt"

-- Preselect the summarizer complexity
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T104409170"] = "Wähle die Zusammenfassungs-Komplexität vor"

-- Preselect summarizer options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T108151178"] = "Vorab Zusammenfasser-Optionen auswählen?"

-- Preselect the target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1417990312"] = "Zielsprache vorauswählen"

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1462295644"] = "Wähle eine andere Zielsprache aus"

-- When activated, the web content reader is hidden and cannot be used. As a result, the user interface becomes a bit easier to use.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1633101895"] = "Wenn aktiviert, wird der Webinhaltsleser ausgeblendet und kann nicht verwendet werden. Dadurch wird die Benutzeroberfläche etwas einfacher zu bedienen."

-- Web content reader is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1701127912"] = "Web-Inhaltsleser ist nicht vorausgewählt"

-- Assistant: Text Summarizer Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1767527569"] = "Assistent: Optionen zur Textzusammenfassung"

-- Content cleaner agent is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T1969816694"] = "Inhaltsbereinigungs-Agent ist nicht vorausgewählt"

-- Hide the web content reader?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T2090693677"] = "Webinhaltsleser ausblenden?"

-- Summarizer options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T2355441996"] = "Optionen für den Zusammenfasser sind vorausgewählt"

-- Web content reader is hidden
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T2799795311"] = "Webinhaltsleser ist ausgeblendet"

-- No summarizer options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3215334223"] = "Keine Zusammenfasser-Optionen sind vorausgewählt"

-- When enabled, the web content reader is preselected. This is might be useful when you prefer to load content from the web very often.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3216157681"] = "Wenn aktiviert, ist der Webinhaltsleser vorausgewählt. Das kann hilfreich sein, wenn Sie häufig Inhalte aus dem Internet laden möchten."

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- Web content reader is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3641773985"] = "Web-Inhaltsleser ist vorausgewählt"

-- Preselect the content cleaner agent?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3649428096"] = "Den Inhaltsbereinigungs-Agenten vorauswählen?"

-- When enabled, the content cleaner agent is preselected. This is might be useful when you prefer to clean up the content before summarize it.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3660434400"] = "Wenn diese Option aktiviert ist, wird der Content Cleaner-Agent automatisch vorausgewählt. Das kann nützlich sein, wenn Sie den Inhalt bereinigen möchten, bevor Sie ihn zusammenfassen."

-- When enabled, you can preselect the text summarizer options. This is might be useful when you prefer a specific language, complexity, or LLM.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T3820844575"] = "Wenn aktiviert, können Sie die Optionen für die Textzusammenfassung im Voraus auswählen. Das kann hilfreich sein, wenn Sie eine bestimmte Sprache, einen bestimmten Schwierigkeitsgrad oder ein bestimmtes LLM bevorzugen."

-- Which summarizer complexity should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T408530182"] = "Welche Zusammenfassungs-Komplexität soll vorausgewählt werden?"

-- Preselect your expertise
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T51139714"] = "Wählen Sie Ihr Fachgebiet aus"

-- Preselect the web content reader?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTEXTSUMMARIZER::T629158142"] = "Web-Inhaltsleser vorauswählen?"

-- Content cleaner agent is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1013787967"] = "Inhaltsbereinigungs-Assistent ist vorausgewählt"

-- Assistant: Translator Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1016384269"] = "Assistent: Übersetzer-Optionen"

-- Web content reader is shown
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1030372436"] = "Web-Inhaltsleser wird angezeigt"

-- When enabled, you can preselect the translator options. This is might be useful when you prefer a specific target language or LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1111006275"] = "Wenn diese Option aktiviert ist, können Sie die Übersetzungsoptionen im Voraus auswählen. Das ist nützlich, wenn Sie eine bestimmte Zielsprache oder ein bestimmtes LLM-Modell bevorzugen."

-- milliseconds
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1275514075"] = "Millisekunden"

-- Preselect the target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1417990312"] = "Zielsprache vorauswählen"

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1462295644"] = "Wähle eine andere Zielsprache aus"

-- When activated, the web content reader is hidden and cannot be used. As a result, the user interface becomes a bit easier to use.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1633101895"] = "Wenn aktiviert, wird der Webinhaltsleser ausgeblendet und kann nicht verwendet werden. Dadurch wird die Benutzeroberfläche etwas einfacher zu bedienen."

-- Web content reader is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1701127912"] = "Web-Inhaltsleser ist nicht vorausgewählt"

-- Live translation is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1825690873"] = "Live-Übersetzung ist nicht vorausgewählt"

-- Content cleaner agent is not preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1969816694"] = "Inhaltsbereinigungs-Assistent ist nicht vorausgewählt"

-- Preselect translator options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T1989346399"] = "Übersetzeroptionen vorauswählen?"

-- Hide the web content reader?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T2090693677"] = "Webinhaltsleser ausblenden?"

-- Translator options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T2234531191"] = "Übersetzeroptionen sind vorausgewählt"

-- Live translation is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T2435743076"] = "Live-Übersetzung ist vorausgewählt"

-- Web content reader is hidden
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T2799795311"] = "Web-Inhaltsleser ist ausgeblendet"

-- No translator options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T2866358796"] = "Keine Übersetzungseinstellungen sind vorausgewählt"

-- When enabled, the web content reader is preselected. This is might be useful when you prefer to load content from the web very often.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T3216157681"] = "Wenn aktiviert, ist der Webinhaltsleser standardmäßig ausgewählt. Das kann nützlich sein, wenn Sie häufig Inhalte aus dem Internet laden möchten."

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- Web content reader is preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T3641773985"] = "Web-Inhaltsleser ist vorausgewählt"

-- Preselect the content cleaner agent?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T3649428096"] = "Inhaltsbereinigungs-Agent vorauswählen?"

-- Preselect the web content reader?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T629158142"] = "Web-Inhaltsleser vorauswählen?"

-- How fast should the live translation react?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T884246296"] = "Wie schnell soll die Live-Übersetzung reagieren?"

-- When enabled, the content cleaner agent is preselected. This is might be useful when you prefer to clean up the content before translating it.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T894123480"] = "Wenn aktiviert, ist der Inhaltsbereiniger-Assistent vorausgewählt. Das kann hilfreich sein, wenn Sie den Inhalt vor der Übersetzung bereinigen möchten."

-- Preselect live translation?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGTRANSLATION::T918172772"] = "Live-Übersetzung vorauswählen?"

-- Which writing style should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T1173034744"] = "Welcher Schreibstil soll standardmäßig ausgewählt sein?"

-- Preselect a greeting?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T1254399201"] = "Begrüßung vorauswählen?"

-- Preselect the target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T1417990312"] = "Zielsprache vorauswählen"

-- Preselect another target language
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T1462295644"] = "Eine andere Zielsprache auswählen"

-- Assistant: Writing E-Mails Options
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T2021226503"] = "Assistent: Optionen zum Schreiben von E-Mails"

-- When enabled, you can preselect the e-mail options. This is might be useful when you prefer a specific language or LLM model.
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T2116404483"] = "Wenn diese Option aktiviert ist, können Sie die E-Mail-Optionen im Voraus auswählen. Dies kann nützlich sein, wenn Sie eine bestimmte Sprache oder ein bestimmtes LLM-Modell bevorzugen."

-- Preselect your name for the closing salutation?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T221974240"] = "Ihren Namen für die Grußformel vorauswählen?"

-- Would you like to preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T2221665527"] = "Möchten Sie eines Ihrer Profile vorauswählen?"

-- Preselect a writing style
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T28456020"] = "Wähle einen Schreibstil vor"

-- E-Mail options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T2985974420"] = "E-Mail-Optionen sind vorausgewählt"

-- No e-mail options are preselected
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T3047605763"] = "Keine E-Mail-Optionen sind vorausgewählt"

-- Close
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T3448155331"] = "Schließen"

-- Which target language should be preselected?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T3547337928"] = "Welche Zielsprache soll vorausgewählt werden?"

-- Preselect e-mail options?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T3832719342"] = "E-Mail-Optionen vorauswählen?"

-- Preselect one of your profiles?
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SETTINGS::SETTINGSDIALOGWRITINGEMAILS::T4004501229"] = "Eines Ihrer Profile vorauswählen?"

-- Chat name
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SINGLEINPUTDIALOG::T1746586282"] = "Chat-Name"

-- Cancel
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::SINGLEINPUTDIALOG::T900713019"] = "Abbrechen"

-- Install now
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::UPDATEDIALOG::T2366359512"] = "Jetzt installieren"

-- Update from v{0} to v{1}
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::UPDATEDIALOG::T25417398"] = "Aktualisieren von v{0} auf v{1}"

-- Install later
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::UPDATEDIALOG::T2936430090"] = "Später installieren"

-- Cancel
UI_TEXT_CONTENT["AISTUDIO::DIALOGS::WORKSPACESELECTIONDIALOG::T900713019"] = "Abbrechen"

-- Settings
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T1258653480"] = "Einstellungen"

-- Home
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T1391791790"] = "Startseite"

-- About
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T1491113694"] = "Über"

-- Are you sure you want to leave the chat page? All unsaved changes will be lost.
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T1563130494"] = "Sind Sie sicher, dass Sie die Chat-Seite verlassen möchten? Alle nicht gespeicherten Änderungen gehen verloren."

-- Assistants
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T1614176092"] = "Assistenten"

-- Update
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T1847791252"] = "Aktualisieren"

-- Leave Chat Page
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T2124749705"] = "Chat-Seite verlassen"

-- Plugins
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T2222816203"] = "Plugins"

-- An update to version {0} is available.
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T2800137365"] = "Ein Update auf Version {0} ist verfügbar."

-- Please wait for the update to complete...
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T2864211629"] = "Bitte warten Sie, bis das Update abgeschlossen ist ..."

-- Supporters
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T2929332068"] = "Unterstützer"

-- Writer
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T2979224202"] = "Schreiber"

-- Show details
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T3692372066"] = "Details anzeigen"

-- Chat
UI_TEXT_CONTENT["AISTUDIO::LAYOUT::MAINLAYOUT::T578410699"] = "Chat"

-- Startup log file
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1019424746"] = "Startprotokolldatei"

-- About MindWork AI Studio
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1020427799"] = "Über MindWork AI Studio"

-- Browse AI Studio's source code on GitHub — we welcome your contributions.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1107156991"] = "Sieh dir den Quellcode von AI Studio auf GitHub an – wir freuen uns über deine Beiträge."

-- This library is used to read PDF files. This is necessary, e.g., for using PDFs as a data source for a chat.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1388816916"] = "Diese Bibliothek wird verwendet, um PDF-Dateien zu lesen. Das ist zum Beispiel notwendig, um PDFs als Datenquelle für einen Chat zu nutzen."

-- This library is used to extend the MudBlazor library. It provides additional components that are not part of the MudBlazor library.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1421513382"] = "Diese Bibliothek wird verwendet, um die MudBlazor-Bibliothek zu erweitern. Sie stellt zusätzliche Komponenten bereit, die nicht Teil der MudBlazor-Bibliothek sind."

-- We use Lua as the language for plugins. Lua-CSharp lets Lua scripts communicate with AI Studio and vice versa. Thank you, Yusuke Nakada, for this great library.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T162898512"] = "Wir verwenden Lua als Sprache für Plugins. Lua-CSharp ermöglicht die Kommunikation zwischen Lua-Skripten und AI Studio in beide Richtungen. Vielen Dank an Yusuke Nakada für diese großartige Bibliothek."

-- Building on .NET, ASP.NET Core, and Blazor, MudBlazor is used as a library for designing and developing the user interface. It is a great project that significantly accelerates the development of advanced user interfaces with Blazor.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1629800076"] = "Basierend auf .NET, ASP.NET Core und Blazor wird MudBlazor als Bibliothek für das Design und die Entwicklung der Benutzeroberfläche verwendet. Es ist ein großartiges Projekt, das die Entwicklung fortschrittlicher Benutzeroberflächen mit Blazor erheblich beschleunigt."

-- AI Studio creates a log file at startup, in which events during startup are recorded. After startup, another log file is created that records all events that occur during the use of the app. This includes any errors that may occur. Depending on when an error occurs (at startup or during use), the contents of these log files can be helpful for troubleshooting. Sensitive information such as passwords is not included in the log files.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1630237140"] = "AI Studio erstellt beim Start eine Protokolldatei, in der Ereignisse während des Starts aufgezeichnet werden. Nach dem Start wird eine weitere Protokolldatei erstellt, die alle Ereignisse während der Nutzung der App dokumentiert. Dazu gehören auch eventuell auftretende Fehler. Je nachdem, wann ein Fehler auftritt (beim Start oder während der Nutzung), können die Inhalte dieser Protokolldateien bei der Fehlerbehebung hilfreich sein. Sensible Informationen wie Passwörter werden nicht in den Protokolldateien gespeichert."

-- This library is used to display the differences between two texts. This is necessary, e.g., for the grammar and spelling assistant.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1772678682"] = "Diese Bibliothek wird verwendet, um die Unterschiede zwischen zwei Texten anzuzeigen. Das ist zum Beispiel für den Grammatik- und Rechtschreibassistenten notwendig."

-- By clicking on the respective path, the path is copied to the clipboard. You might open these files with a text editor to view their contents.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1806897624"] = "Wenn Sie auf den jeweiligen Pfad klicken, wird dieser in die Zwischenablage kopiert. Sie können diese Dateien mit einem Texteditor öffnen, um ihren Inhalt anzusehen."

-- Check for updates
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1890416390"] = "Nach Updates suchen"

-- Vision
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1892426825"] = "Vision"

-- This library is used to convert HTML to Markdown. This is necessary, e.g., when you provide a URL as input for an assistant.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1924365263"] = "Diese Bibliothek wird verwendet, um HTML in Markdown umzuwandeln. Das ist zum Beispiel notwendig, wenn Sie eine URL als Eingabe für einen Assistenten angeben."

-- We use Rocket to implement the runtime API. This is necessary because the runtime must be able to communicate with the user interface (IPC). Rocket is a great framework for implementing web APIs in Rust.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T1943216839"] = "Wir verwenden Rocket zur Implementierung der Runtime-API. Dies ist notwendig, da die Runtime mit der Benutzeroberfläche (IPC) kommunizieren muss. Rocket ist ein ausgezeichnetes Framework zur Umsetzung von Web-APIs in Rust."

-- This library is used to determine the file type of a file. This is necessary, e.g., when we want to stream a file.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2173617769"] = "Diese Bibliothek wird verwendet, um den Dateityp einer Datei zu bestimmen. Das ist zum Beispiel notwendig, wenn wir eine Datei streamen möchten."

-- For the secure communication between the user interface and the runtime, we need to create certificates. This Rust library is great for this purpose.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2174764529"] = "Für die sichere Kommunikation zwischen der Benutzeroberfläche und der Laufzeit müssen wir Zertifikate erstellen. Diese Rust-Bibliothek eignet sich hervorragend dafür."

-- We must generate random numbers, e.g., for securing the interprocess communication between the user interface and the runtime. The rand library is great for this purpose.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2273492381"] = "Wir müssen Zufallszahlen erzeugen, z. B. um die Kommunikation zwischen der Benutzeroberfläche und der Laufzeitumgebung abzusichern. Die rand-Bibliothek eignet sich dafür hervorragend."

-- In order to use any LLM, each user must store their so-called token for each LLM provider. This token must be kept secure, similar to a password. The safest way to do this is offered by operating systems like macOS, Windows, and Linux: They have mechanisms to store such data, if available, on special security hardware. Since this is currently not possible in .NET, we use this Rust library.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T228561878"] = "Um ein beliebiges LLM nutzen zu können, muss jeder Benutzer seinen sogenannten Token für jeden LLM-Anbieter speichern. Dieser Token muss sicher aufbewahrt werden, ähnlich wie ein Passwort. Am sichersten gelingt dies mit den Betriebssystemen wie macOS, Windows und Linux: Sie verfügen über Mechanismen, solche Daten – sofern vorhanden – auf spezieller Sicherheits-Hardware zu speichern. Da dies in .NET derzeit nicht möglich ist, verwenden wir diese Rust-Bibliothek."

-- The C# language is used for the implementation of the user interface and the backend. To implement the user interface with C#, the Blazor technology from ASP.NET Core is used. All these technologies are integrated into the .NET SDK.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2329884315"] = "Die Programmiersprache C# wird für die Umsetzung der Benutzeroberfläche und des Backends verwendet. Für die Entwicklung der Benutzeroberfläche mit C# kommt die Blazor-Technologie aus ASP.NET Core zum Einsatz. Alle diese Technologien sind im .NET SDK integriert."

-- This library is used to determine the language of the operating system. This is necessary to set the language of the user interface.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2557014401"] = "Diese Bibliothek wird verwendet, um die Sprache des Betriebssystems zu erkennen. Dies ist notwendig, um die Sprache der Benutzeroberfläche einzustellen."

-- Used Open Source Projects
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2557066213"] = "Verwendete Open-Source-Projekte"

-- Build time
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T260228112"] = "Build-Zeit"

-- To be able to use the responses of the LLM in other apps, we often use the clipboard of the respective operating system. Unfortunately, in .NET there is no solution that works with all operating systems. Therefore, I have opted for this library in Rust. This way, data transfer to other apps works on every system.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2644379659"] = "Um die Antworten des LLM in anderen Apps nutzen zu können, verwenden wir häufig die Zwischenablage des jeweiligen Betriebssystems. Leider gibt es in .NET keine Lösung, die auf allen Betriebssystemen funktioniert. Deshalb habe ich mich für diese Bibliothek in Rust entschieden. So funktioniert die Datenübertragung zu anderen Apps auf jedem System."

-- Usage log file
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2689995864"] = "Nutzungsprotokolldatei"

-- Logbook
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2706940196"] = "Logbuch"

-- This component is used to render Markdown text. This is important because the LLM often responds with Markdown-formatted text, allowing us to present it in a way that is easier to read.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2726131107"] = "Diese Komponente wird verwendet, um Markdown-Text darzustellen. Das ist wichtig, weil das LLM häufig mit im Markdown-Format formatiertem Text antwortet. Dadurch können wir die Antworten besser lesbar anzeigen."

-- Code in the Rust language can be specified as synchronous or asynchronous. Unlike .NET and the C# language, Rust cannot execute asynchronous code by itself. Rust requires support in the form of an executor for this. Tokio is one such executor.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2777988282"] = "Code in der Programmiersprache Rust kann als synchron oder asynchron spezifiziert werden. Im Gegensatz zu .NET und der Sprache C# kann Rust asynchronen Code jedoch nicht von selbst ausführen. Dafür benötigt Rust Unterstützung in Form eines Executors. Tokio ist ein solcher Executor."

-- View our project roadmap and help shape AI Studio's future development.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2829971158"] = "Sehen Sie sich unsere Roadmap an und helfen Sie mit, die zukünftige Entwicklung von AI Studio mitzugestalten."

-- Used .NET runtime
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2840227993"] = "Verwendete .NET-Laufzeit"

-- Explanation
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2840582448"] = "Erklärung"

-- The .NET backend cannot be started as a desktop app. Therefore, I use a second backend in Rust, which I call runtime. With Rust as the runtime, Tauri can be used to realize a typical desktop app. Thanks to Rust, this app can be offered for Windows, macOS, and Linux desktops. Rust is a great language for developing safe and high-performance software.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T2868174483"] = "Das .NET-Backend kann nicht als Desktop-App gestartet werden. Deshalb verwende ich ein zweites Backend in Rust, das ich „Runtime“ nenne. Mit Rust als Runtime kann Tauri genutzt werden, um eine typische Desktop-App zu realisieren. Dank Rust kann diese App für Windows-, macOS- und Linux-Desktops angeboten werden. Rust ist eine großartige Sprache für die Entwicklung sicherer und leistungsstarker Software."

-- Changelog
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3017574265"] = "Änderungsprotokoll"

-- Connect AI Studio to your organization's data with our External Retrieval Interface (ERI).
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T313276297"] = "Verbinden Sie AI Studio mit den Daten Ihrer Organisation über unsere Schnittstelle für externe Datenabfrage (ERI)."

-- Have feature ideas? Submit suggestions for future AI Studio enhancements.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3178730036"] = "Haben Sie Ideen für neue Funktionen? Senden Sie uns Vorschläge für zukünftige Verbesserungen von AI Studio."

-- Discover MindWork AI's mission and vision on our official homepage.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3294830584"] = "Entdecken Sie die Mission und Vision von MindWork AI auf unserer offiziellen Homepage."

-- User-language provided by the OS
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3334355246"] = "Vom Betriebssystem bereitgestellte Sprache"

-- The following list shows the versions of the MindWork AI Studio, the used compilers, build time, etc.:
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3405978777"] = "Die folgende Liste zeigt die Versionen von MindWork AI Studio, die verwendeten Compiler, den Build-Zeitpunkt und weitere Informationen:"

-- Used Rust compiler
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3440211747"] = "Verwendeter Rust-Compiler"

-- Tauri is used to host the Blazor user interface. It is a great project that allows the creation of desktop applications using web technologies. I love Tauri!
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3494984593"] = "Tauri wird verwendet, um die Blazor-Benutzeroberfläche bereitzustellen. Es ist ein großartiges Projekt, das die Erstellung von Desktop-Anwendungen mit Webtechnologien ermöglicht. Ich liebe Tauri!"

-- Motivation
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3563271893"] = "Motivation"

-- This library is used to read Excel and OpenDocument spreadsheet files. This is necessary, e.g., for using spreadsheets as a data source for a chat.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3722989559"] = "Diese Bibliothek wird verwendet, um Excel- und OpenDocument-Tabellendateien zu lesen. Dies ist zum Beispiel notwendig, wenn Tabellen als Datenquelle für einen Chat verwendet werden sollen."

-- Now we have multiple systems, some developed in .NET and others in Rust. The data format JSON is responsible for translating data between both worlds (called data serialization and deserialization). Serde takes on this task in the Rust world. The counterpart in the .NET world is an integral part of .NET and is located in System.Text.Json.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T3908558992"] = "Jetzt haben wir mehrere Systeme, einige entwickelt in .NET und andere in Rust. Das Datenformat JSON ist dafür zuständig, Daten zwischen beiden Welten zu übersetzen (dies nennt man Serialisierung und Deserialisierung von Daten). In der Rust-Welt übernimmt Serde diese Aufgabe. Das Pendant in der .NET-Welt ist ein fester Bestandteil von .NET und findet sich in System.Text.Json."

-- Versions
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T4010195468"] = "Versionen"

-- This library is used to create asynchronous streams in Rust. It allows us to work with streams of data that can be produced asynchronously, making it easier to handle events or data that arrive over time. We use this, e.g., to stream arbitrary data from the file system to the embedding system.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T4079152443"] = "Diese Bibliothek wird verwendet, um asynchrone Datenströme in Rust zu erstellen. Sie ermöglicht es uns, mit Datenströmen zu arbeiten, die asynchron bereitgestellt werden, wodurch sich Ereignisse oder Daten, die nach und nach eintreffen, leichter verarbeiten lassen. Wir nutzen dies zum Beispiel, um beliebige Daten aus dem Dateisystem an das Einbettungssystem zu übertragen."

-- Community & Code
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T4158546761"] = "Gemeinschaft & Code"

-- We use the HtmlAgilityPack to extract content from the web. This is necessary, e.g., when you provide a URL as input for an assistant.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T4184485147"] = "Wir verwenden das HtmlAgilityPack, um Inhalte aus dem Internet zu extrahieren. Das ist zum Beispiel notwendig, wenn du eine URL als Eingabe für einen Assistenten angibst."

-- When transferring sensitive data between Rust runtime and .NET app, we encrypt the data. We use some libraries from the Rust Crypto project for this purpose: cipher, aes, cbc, pbkdf2, hmac, and sha2. We are thankful for the great work of the Rust Crypto project.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T4229014037"] = "Beim Übertragen sensibler Daten zwischen der Rust-Laufzeitumgebung und der .NET-Anwendung verschlüsseln wir die Daten. Dafür verwenden wir einige Bibliotheken aus dem Rust Crypto-Projekt: cipher, aes, cbc, pbkdf2, hmac und sha2. Wir sind dankbar für die großartige Arbeit des Rust Crypto-Projekts."

-- This is a library providing the foundations for asynchronous programming in Rust. It includes key trait definitions like Stream, as well as utilities like join!, select!, and various futures combinator methods which enable expressive asynchronous control flow.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T566998575"] = "Dies ist eine Bibliothek, die die Grundlagen für asynchrones Programmieren in Rust bereitstellt. Sie enthält zentrale Trait-Definitionen wie Stream sowie Hilfsfunktionen wie join!, select! und verschiedene Methoden zur Kombination von Futures, die einen ausdrucksstarken asynchronen Kontrollfluss ermöglichen."

-- Used .NET SDK
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T585329785"] = "Verwendetes .NET SDK"

-- Did you find a bug or are you experiencing issues? Report your concern here.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T639371534"] = "Haben Sie einen Fehler gefunden oder Probleme festgestellt? Melden Sie Ihr Anliegen hier."

-- This Rust library is used to output the app's messages to the terminal. This is helpful during development and troubleshooting. This feature is initially invisible; when the app is started via the terminal, the messages become visible.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T64689067"] = "Diese Rust-Bibliothek wird verwendet, um die Nachrichten der App im Terminal auszugeben. Das ist während der Entwicklung und Fehlersuche hilfreich. Diese Funktion ist zunächst unsichtbar; werden App über das Terminal gestartet, werden die Nachrichten sichtbar."

-- For some data transfers, we need to encode the data in base64. This Rust library is great for this purpose.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ABOUT::T870640199"] = "Für einige Datenübertragungen müssen wir die Daten in Base64 kodieren. Diese Rust-Bibliothek eignet sich dafür hervorragend."

-- Get coding and debugging support from an LLM.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1243850917"] = "Erhalte Unterstützung beim Programmieren und Debuggen durch ein KI-Modell."

-- Business
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T131837803"] = "Geschäft"

-- Legal Check
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1348190638"] = "Rechtliche Prüfung"

-- General
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1432485131"] = "Allgemein"

-- Grammar & Spelling
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1514925962"] = "Grammatik & Rechtschreibung"

-- Assistants
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1614176092"] = "Assistenten"

-- Coding
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1617786407"] = "Programmieren"

-- Analyze a text or an email for tasks you need to complete.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1728590051"] = "Analysiere einen Text oder eine E-Mail nach Aufgaben, die du erledigen musst."

-- Text Summarizer
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1907192403"] = "Textzusammenfasser"

-- Check grammar and spelling of a given text.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T1934717573"] = "Rechtschreibung und Grammatik eines gegebenen Textes überprüfen."

-- Translate text into another language.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T209791153"] = "Text in eine andere Sprache übersetzen."

-- Generate an e-mail for a given context.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2383649630"] = "Erstelle eine E-Mail für einen bestimmten Kontext."

-- Generate an agenda for a given meeting, seminar, etc.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2406168562"] = "Erstelle eine Tagesordnung für eine bestimmte Besprechung, ein Seminar usw."

-- Agenda Planner
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2435638853"] = "Terminplaner"

-- Synonyms
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2547582747"] = "Synonyme"

-- Find synonyms for a given word or phrase.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2712131461"] = "Finde Synonyme für ein angegebenes Wort oder eine Phrase."

-- AI Studio Development
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2830810750"] = "AI Studio Entwicklung"

-- Generate a job posting for a given job description.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T2831103254"] = "Erstellen Sie eine Stellenanzeige anhand einer vorgegebenen Stellenbeschreibung."

-- My Tasks
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3011450657"] = "Meine Aufgaben"

-- E-Mail
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3026443472"] = "E-Mail"

-- Translate AI Studio text content into other languages
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3181803840"] = "AI Studio Textinhalte in andere Sprachen übersetzen"

-- Software Engineering
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3260960011"] = "Software-Entwicklung"

-- Rewrite & Improve
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3309133329"] = "Umformulieren & Verbessern"

-- Icon Finder
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3693102312"] = "Icon Finder"

-- Generate an ERI server to integrate business systems.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3756213118"] = "Erstellen Sie einen ERI-Server zur Integration von Geschäftssystemen."

-- Use an LLM to find an icon for a given context.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3881504200"] = "Verwenden Sie ein LLM, um ein Symbol für einen bestimmten Kontext zu finden."

-- Job Posting
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3930052338"] = "Stellenanzeige"

-- Ask a question about a legal document.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T3970214537"] = "Stellen Sie eine Frage zu einem juristischen Dokument."

-- ERI Server
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T4204533420"] = "ERI-Server"

-- Use an LLM to summarize a given text.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T502222021"] = "Verwenden Sie ein LLM, um einen gegebenen Text zusammenzufassen."

-- Translation
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T613888204"] = "Übersetzung"

-- Rewrite and improve a given text for a chosen style.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T722167136"] = "Einen gegebenen Text für einen gewählten Stil umschreiben und verbessern."

-- Learning
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T755590027"] = "Lernen"

-- Bias of the Day
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T782102948"] = "Vorurteil des Tages"

-- Learn about one cognitive bias every day.
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T878695986"] = "Lerne jeden Tag einen kognitiven Bias kennen."

-- Localization
UI_TEXT_CONTENT["AISTUDIO::PAGES::ASSISTANTS::T897888480"] = "Lokalisierung"

-- Hide your workspaces
UI_TEXT_CONTENT["AISTUDIO::PAGES::CHAT::T2351468526"] = "Arbeitsbereiche ausblenden"

-- Disappearing Chat
UI_TEXT_CONTENT["AISTUDIO::PAGES::CHAT::T3046519404"] = "Verschwindender Chat"

-- Your workspaces
UI_TEXT_CONTENT["AISTUDIO::PAGES::CHAT::T3745240468"] = "Ihre Arbeitsbereiche"

-- Chat in Workspace
UI_TEXT_CONTENT["AISTUDIO::PAGES::CHAT::T582100343"] = "Chat im Arbeitsbereich"

-- Show your workspaces
UI_TEXT_CONTENT["AISTUDIO::PAGES::CHAT::T733672375"] = "Zeige deine Arbeitsbereiche"

-- Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1009708591"] = "Im Gegensatz zu Diensten wie ChatGPT, die nach intensiver Nutzung Einschränkungen verhängen, bietet MindWork AI Studio unbegrenzte Nutzung über die API des Anbieters."

-- Welcome to MindWork AI Studio!
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1024253064"] = "Willkommen bei MindWork AI Studio!"

-- Thank you for considering MindWork AI Studio for your AI needs. This app is designed to help you harness the power of Large Language Models (LLMs). Please note that this app doesn't come with an integrated LLM. Instead, you will need to bring an API key from a suitable provider.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1146553980"] = "Vielen Dank, dass Sie MindWork AI Studio für Ihre KI-Anwendungen in Betracht ziehen. Diese App wurde entwickelt, um Ihnen die Nutzung von leistungsstarken Sprachmodellen (LLMs) zu ermöglichen. Bitte beachten Sie, dass die App kein integriertes LLM enthält. Stattdessen benötigen Sie einen API-Schlüssel von einem passenden Anbieter."

-- The app requires minimal storage for installation and operates with low memory usage. Additionally, it has a minimal impact on system resources, which is beneficial for battery life.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T144565305"] = "Die App benötigt nur wenig Speicherplatz für die Installation und verwendet wenig Arbeitsspeicher. Außerdem hat sie einen minimalen Einfluss auf die Systemressourcen, was sich positiv auf die Akkulaufzeit auswirkt."

-- You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T149711988"] = "Sie zahlen nur für das, was Sie tatsächlich nutzen – das kann günstiger sein als monatliche Abos wie ChatGPT Plus, vor allem bei gelegentlicher Nutzung. Aber Vorsicht: Bei sehr intensiver Nutzung können die API-Kosten deutlich höher ausfallen. Leider bieten die Anbieter derzeit keine Möglichkeit, die aktuellen Kosten direkt in der App anzuzeigen. Prüfen Sie deshalb regelmäßig Ihr Konto beim jeweiligen Anbieter, um Ihre Ausgaben im Blick zu behalten. Nutzen Sie, wenn möglich, Prepaid-Optionen und legen Sie ein Ausgabenlimit fest."

-- Assistants
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1614176092"] = "Assistenten"

-- Unrestricted usage
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1686815996"] = "Unbeschränkte Nutzung"

-- Introduction
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1702902297"] = "Einführung"

-- Vision
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T1892426825"] = "Vision"

-- You are not tied to any single provider. Instead, you might choose the provider that best suits your needs. Right now, we support OpenAI (GPT4o, o1, etc.), Mistral, Anthropic (Claude), Google Gemini, xAI (Grok), DeepSeek, Alibaba Cloud (Qwen), Hugging Face, and self-hosted models using llama.cpp, ollama, LM Studio, Groq, or Fireworks. For scientists and employees of research institutions, we also support Helmholtz and GWDG AI services. These are available through federated logins like eduGAIN to all 18 Helmholtz Centers, the Max Planck Society, most German, and many international universities.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T2217921237"] = "Sie sind nicht an einen einzelnen Anbieter gebunden. Stattdessen können Sie den Anbieter wählen, der am besten zu Ihren Bedürfnissen passt. Aktuell unterstützen wir OpenAI (GPT4o, o1 usw.), Mistral, Anthropic (Claude), Google Gemini, xAI (Grok), DeepSeek, Alibaba Cloud (Qwen), Hugging Face sowie selbst gehostete Modelle mit llama.cpp, ollama, LM Studio, Groq oder Fireworks. Für Wissenschaftler und Beschäftigte von Forschungseinrichtungen unterstützen wir außerdem die KI-Dienste von Helmholtz und GWDG. Diese sind über föderierte Logins wie eduGAIN für alle 18 Helmholtz-Zentren, die Max-Planck-Gesellschaft, die meisten deutschen sowie viele internationale Universitäten verfügbar."

-- Let's get started
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T2331588413"] = "Lass uns anfangen"

-- Last Changelog
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T2348849647"] = "Letztes Änderungsprotokoll"

-- Choose the provider and model best suited for your current task.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T2588488920"] = "Wählen Sie den Anbieter und das Modell aus, die am besten zu Ihrer aktuellen Aufgabe passen."

-- Quick Start Guide
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T3002014720"] = "Schnellstart-Anleitung"

-- You just want to quickly translate a text? AI Studio has so-called assistants for such and other tasks. No prompting is necessary when working with these assistants.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T3228075421"] = "Sie möchten einfach schnell einen Text übersetzen? Für solche und andere Aufgaben gibt es in AI Studio sogenannte Assistenten. Beim Arbeiten mit diesen Assistenten sind keine Eingabeaufforderungen erforderlich."

-- We hope you enjoy using MindWork AI Studio to bring your AI projects to life!
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T3275341342"] = "Wir hoffen, dass Sie viel Freude daran haben, mit MindWork AI Studio Ihre KI-Projekte zum Leben zu erwecken!"

-- Cost-effective
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T3341379752"] = "Kosteneffizient"

-- Flexibility
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T3723223888"] = "Flexibilität"

-- Privacy
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T3959064551"] = "Datenschutz"

-- You can control which providers receive your data using the provider confidence settings. For example, you can set different protection levels for writing emails compared to general chats, etc. Additionally, most providers guarantee that they won't use your data to train new AI systems.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T457410099"] = "Sie können über die Einstellungen zur Anbietervertrauenswürdigkeit steuern, welche Anbieter Ihre Daten erhalten. Zum Beispiel können Sie für das Schreiben von E-Mails einen anderen Schutzlevel festlegen als für allgemeine Chats usw. Außerdem garantieren die meisten Anbieter, dass Ihre Daten nicht zum Trainieren neuer KI-Systeme verwendet werden."

-- Free of charge
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T617579208"] = "Kostenlos"

-- Independence
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T649448159"] = "Unabhängigkeit"

-- No bloatware
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T858047957"] = "Keinen unnötigen Software-Balast"

-- Here's what makes MindWork AI Studio stand out:
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T873851215"] = "Das zeichnet MindWork AI Studio aus:"

-- The app is free to use, both for personal and commercial purposes.
UI_TEXT_CONTENT["AISTUDIO::PAGES::HOME::T91074375"] = "Die App ist sowohl für private als auch für kommerzielle Zwecke kostenlos nutzbar."

-- Disable plugin
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T1430375822"] = "Plugin deaktivieren"

-- Internal Plugins
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T158493184"] = "Interne Plugins"

-- Disabled Plugins
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T1724138133"] = "Deaktivierte Plugins"

-- Enable plugin
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T2057806005"] = "Plugin aktivieren"

-- Plugins
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T2222816203"] = "Plugins"

-- Enabled Plugins
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T2738444034"] = "Aktivierte Plugins"

-- Actions
UI_TEXT_CONTENT["AISTUDIO::PAGES::PLUGINS::T3865031940"] = "Aktionen"

-- Settings
UI_TEXT_CONTENT["AISTUDIO::PAGES::SETTINGS::T1258653480"] = "Einstellungen"

-- Thank you for being the first to contribute a one-time donation.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T1470916504"] = "Vielen Dank, dass Sie als Erste:r eine einmalige Spende geleistet haben."

-- Thank you, Peer, for your courage in being the second person to support the project financially.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T1714878838"] = "Danke, Peer, für deinen Mut, als zweite Person das Projekt finanziell zu unterstützen."

-- Individual Contributors
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T1874835680"] = "Einzelne Mitwirkende"

-- Thanks, Nils, for taking the time to learn Rust and build the foundation for local retrieval.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T2355807535"] = "Danke, Nils, dass du dir die Zeit genommen hast, Rust zu lernen und die Grundlage für die lokale Suche zu schaffen."

-- The first 10 supporters who make a one-time contribution:
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T2410456125"] = "Die ersten 10 Unterstützer, die einen einmaligen Beitrag leisten:"

-- Supporters
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T2929332068"] = "Unterstützer"

-- Content Contributors
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3060804484"] = "Inhaltsbeitragende"

-- Financial Support
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3061261435"] = "Finanzielle Unterstützung"

-- The first 10 supporters who make a monthly contribution:
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3364384944"] = "Die ersten 10 Unterstützer, die einen monatlichen Beitrag leisten:"

-- Thank you, Richard, for being the first.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3660718138"] = "Danke, Richard, dass du der Erste warst."

-- Thanks Dominic for being the third supporter.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3664780201"] = "Danke, Dominic, dass du als dritter Unterstützer dabei bist."

-- Our Titans
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3805270964"] = "Unsere Titans"

-- Moderation, Design, Wiki, and Documentation
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T3821668394"] = "Moderation, Design, Wiki und Dokumentation"

-- Thank you, Peer, for familiarizing yourself with C#, providing excellent contributions like the Alibaba and Hugging Face providers, and revising the settings management.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T4106820759"] = "Vielen Dank, Peer, dass du dich mit C# vertraut gemacht, großartige Beiträge wie die Alibaba- und Hugging-Face-Anbieter geleistet und die Verwaltung der Einstellungen überarbeitet hast."

-- Code Contributions
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T4135925647"] = "Code-Beiträge"

-- Become our first Titan
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T414428338"] = "Werde unser erster Titan"

-- Become a contributor
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T414604046"] = "Werden Sie Mitwirkender"

-- In this section, we highlight the titan supporters of MindWork AI Studio. Titans are prestigious companies that provide significant support to our mission.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T4270177642"] = "In diesem Abschnitt stellen wir die Titanen-Unterstützer von MindWork AI Studio vor. Titanen sind renommierte Unternehmen, die unsere Mission maßgeblich unterstützen."

-- Thanks Luc for your build script contribution.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T432023389"] = "Danke, Luc, für deinen Beitrag zum Build-Skript."

-- For companies, sponsoring MindWork AI Studio is not only a way to support innovation but also a valuable opportunity for public relations and marketing. Your company's name and logo will be featured prominently, showcasing your commitment to using cutting-edge AI tools and enhancing your reputation as an innovative enterprise.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T68519158"] = "Für Unternehmen ist das Sponsoring von MindWork AI Studio nicht nur eine Möglichkeit, Innovationen zu unterstützen, sondern auch eine wertvolle Chance für Öffentlichkeitsarbeit und Marketing. Der Name und das Logo Ihres Unternehmens werden prominent präsentiert und zeigen Ihr Engagement für den Einsatz fortschrittlicher KI-Werkzeuge sowie Ihren Ruf als innovatives Unternehmen."

-- Thanks for your build script contribution.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T686206269"] = "Vielen Dank für deinen Beitrag zum Build-Skript."

-- Business Contributors
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T838479287"] = "Geschäftsmitwirkende"

-- Thank you very much, Kerstin, for taking care of creating the Wiki.
UI_TEXT_CONTENT["AISTUDIO::PAGES::SUPPORTERS::T991294232"] = "Vielen herzlichen Dank, Kerstin, dass du dich um die Erstellung des Wikis gekümmert hast."

-- Write your text
UI_TEXT_CONTENT["AISTUDIO::PAGES::WRITER::T2220943334"] = "Schreiben Sie Ihren Text"

-- Writer
UI_TEXT_CONTENT["AISTUDIO::PAGES::WRITER::T2979224202"] = "Autor"

-- Suggestion
UI_TEXT_CONTENT["AISTUDIO::PAGES::WRITER::T3948127789"] = "Vorschlag"

-- Your stage directions
UI_TEXT_CONTENT["AISTUDIO::PAGES::WRITER::T779923726"] = "Ihre Regieanweisungen"
