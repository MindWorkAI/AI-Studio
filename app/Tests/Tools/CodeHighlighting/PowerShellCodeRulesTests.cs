using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class PowerShellCodeRulesTests
{
    [TestCase("function Get-Greeting {", "{keyword:function} {function:Get-Greeting} {")]
    [TestCase("param([string]$Name = 'World')", "{keyword:param}([{type:string}]{variable:$Name} = {string:'World'})")]
    [TestCase("if ($Name -eq $null) { Write-Host \"Hi $Name\" -ForegroundColor Green }", "{keyword:if} ({variable:$Name} {keyword:-eq} {variable:$null}) { {function:Write-Host} {string:\"Hi $Name\"} -ForegroundColor Green }")]
    [TestCase("IF ($a) { RETURN }", "{keyword:IF} ({variable:$a}) { {keyword:RETURN} }", Description = "PowerShell ignores the case of its keywords.")]
    [TestCase("<# block #> $env:PATH # note", "{comment:<# block #>} {variable:$env:PATH} {comment:# note}")]
    [TestCase("@\"\nmulti\n\"@", "{string:@\"\nmulti\n\"@}")]
    [TestCase("'it''s' \"a`\"b\"", "{string:'it''s'} {string:\"a`\"b\"}")]
    [TestCase("$list[0]", "{variable:$list}[{number:0}]", Description = "Brackets after a name index into it.")]
    [TestCase("[int[]]$numbers", "[{type:int[]}]{variable:$numbers}")]
    [TestCase("Get-ChildItem -Path . | ForEach-Object { $_.Name }", "{function:Get-ChildItem} -Path . | {function:ForEach-Object} { {variable:$_}.Name }")]
    public void PowerShellGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, PowerShellCodeRules.RULES), Is.EqualTo(expected));
    }
}