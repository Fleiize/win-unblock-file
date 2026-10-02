using Golabox.UnblockFile.Services;

namespace Golabox.UnblockFile.Tests;

public class PowerShellCommandBuilderTests
{
    [Theory]
    [InlineData(@"C:\Users\User\Downloads\facture.pdf", @"'C:\Users\User\Downloads\facture.pdf'")]
    [InlineData(@"C:\Mon dossier\o'brien.txt", @"'C:\Mon dossier\o''brien.txt'")]
    [InlineData("C:\\it\u2019s.txt", "'C:\\it\u2019\u2019s.txt'")]                       // apostrophe typographique
    [InlineData(@"C:\$env:USERNAME\`n$(calc).txt", @"'C:\$env:USERNAME\`n$(calc).txt'")]  // aucune expansion dans '...'
    [InlineData(@"C:\été\日本語\[x]\a;b&c.txt", @"'C:\été\日本語\[x]\a;b&c.txt'")]
    public void Quote_EscapesSingleQuotesAndLeavesOtherCharactersLiteral(string input, string expected) =>
        Assert.Equal(expected, PowerShellCommandBuilder.Quote(input));

    [Fact]
    public void Quote_InjectionAttemptStaysInsideTheLiteral()
    {
        var quoted = PowerShellCommandBuilder.Quote("x'; Remove-Item C:\\ -Recurse; '");
        Assert.Equal("'x''; Remove-Item C:\\ -Recurse; '''", quoted);
    }

    [Fact]
    public void DisplayCommand_File() =>
        Assert.Equal(@"Unblock-File -LiteralPath 'C:\Users\User\Downloads\facture.pdf'",
            PowerShellCommandBuilder.BuildDisplayCommand(@"C:\Users\User\Downloads\facture.pdf", false, false));

    [Fact]
    public void DisplayCommand_Folder() =>
        Assert.Equal(@"Get-ChildItem -LiteralPath 'C:\Mon dossier' -File | Unblock-File",
            PowerShellCommandBuilder.BuildDisplayCommand(@"C:\Mon dossier", true, false));

    [Fact]
    public void DisplayCommand_FolderRecursive() =>
        Assert.Equal(@"Get-ChildItem -LiteralPath 'C:\Mon dossier' -File -Recurse | Unblock-File",
            PowerShellCommandBuilder.BuildDisplayCommand(@"C:\Mon dossier", true, true));
}
