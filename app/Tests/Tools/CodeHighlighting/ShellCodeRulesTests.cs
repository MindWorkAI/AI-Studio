using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class ShellCodeRulesTests
{
    [TestCase("#!/bin/bash", "{comment:#!/bin/bash}")]
    [TestCase("if [ -f \"$FILE\" ]; then", "{keyword:if} [ -f {string:\"$FILE\"} ]; {keyword:then}")]
    [TestCase("for f in *.txt; do echo $f; done", "{keyword:for} f {keyword:in} *.txt; {keyword:do} echo {variable:$f}; {keyword:done}")]
    [TestCase("greet() { local name=$1; }", "{function:greet}() { {keyword:local} name={variable:$1}; }")]
    [TestCase("echo ${#items[@]} $# # count", "echo {variable:${#items[@]}} {variable:$#} {comment:# count}")]
    [TestCase("x=$(date +%s)", "x=$(date +%s)")]
    [TestCase("echo 'a\\'", "echo {string:'a\\'}", Description = "Within single quotation marks, nothing escapes.")]
    public void ShellScriptsGetTheirColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, ShellCodeRules.RULES), Is.EqualTo(expected));
    }
}