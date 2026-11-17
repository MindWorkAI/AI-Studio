using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class PythonCodeRulesTests
{
    [TestCase("@dataclass", "{meta:@dataclass}")]
    [TestCase("class Point(Base):", "{keyword:class} {type:Point}(Base):")]
    [TestCase("    \"\"\"A point.\"\"\"", "    {string:\"\"\"A point.\"\"\"}")]
    [TestCase("x: int = 0", "x: {type:int} = {number:0}")]
    [TestCase("def move(self, dx: float) -> None:", "{keyword:def} {function:move}(self, dx: {type:float}) -> {literal:None}:")]
    [TestCase("# shift it", "{comment:# shift it}")]
    [TestCase("print(f\"moved {dx}\")", "{function:print}({string:f\"moved {dx}\"})")]
    [TestCase("rb'\\x00' if x else B\"\"", "{string:rb'\\x00'} {keyword:if} x {keyword:else} {string:B\"\"}")]
    [TestCase("async def main(): await run()", "{keyword:async} {keyword:def} {function:main}(): {keyword:await} {function:run}()")]
    [TestCase("z = 1_000.5e3j", "z = {number:1_000.5e3j}")]
    [TestCase("re.match(p, s)", "re.{function:match}(p, s)", Description = "The soft keyword match is a name everywhere else.")]
    public void PythonGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, PythonCodeRules.RULES), Is.EqualTo(expected));
    }
}