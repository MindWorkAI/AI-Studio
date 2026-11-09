using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class JavaCodeRulesTests
{
    [TestCase("@Override public String toString() {", "{meta:@Override} {keyword:public} {type:String} {function:toString}() {")]
    [TestCase("public record Point(int x, int y) {}", "{keyword:public} {keyword:record} {type:Point}({type:int} x, {type:int} y) {}")]
    [TestCase("List<String> names = new ArrayList<>();", "{type:List}<{type:String}> names = {keyword:new} {type:ArrayList}<>();")]
    [TestCase("char c = 'a'; long n = 10L; double d = 1.5e3;", "{type:char} c = {string:'a'}; {type:long} n = {number:10L}; {type:double} d = {number:1.5e3};")]
    [TestCase("String s = \"\"\"\n  text\n  \"\"\";", "{type:String} s = {string:\"\"\"\n  text\n  \"\"\"};")]
    [TestCase("if (x == null) return;", "{keyword:if} (x == {literal:null}) {keyword:return};")]
    [TestCase("System.out.println(MAX_SIZE);", "{type:System}.out.{function:println}(MAX_SIZE);", Description = "A constant in capitals is no type.")]
    public void JavaGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, JavaCodeRules.RULES), Is.EqualTo(expected));
    }
}