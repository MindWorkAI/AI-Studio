using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class CssCodeRulesTests
{
    [TestCase(":root { --main-color: #06c; }", ":root { {attribute:--main-color}: {number:#06c}; }")]
    [TestCase(".card:hover > a { color: var(--main-color) !important; }", "{selector:.card}:hover > a { {attribute:color}: {function:var}(--main-color) {keyword:!important}; }")]
    [TestCase("#main { margin: 0 auto; }", "{selector:#main} { {attribute:margin}: {number:0} auto; }", Description = "Before a block, # names an ID, not a color.")]
    [TestCase("@media (max-width: 600px) {", "{keyword:@media} (max-width: {number:600px}) {")]
    [TestCase("p { width: calc(100% - 2rem); }", "p { {attribute:width}: {function:calc}({number:100%} - {number:2rem}); }")]
    [TestCase("@media print {\n  a:hover { color: red; }\n}", "{keyword:@media} print {\n  a:hover { {attribute:color}: red; }\n}", Description = "Within an at-rule, a:hover is a selector, not a property.")]
    [TestCase("p { font-family: \"Inter\", sans-serif; margin: -2px .5em; }", "p { {attribute:font-family}: {string:\"Inter\"}, sans-serif; {attribute:margin}: -{number:2px} {number:.5em}; }")]
    [TestCase("/* note */ a {}", "{comment:/* note */} a {}")]
    public void StyleSheetsGetTheirColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, CssCodeRules.RULES), Is.EqualTo(expected));
    }
}