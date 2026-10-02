using Golabox.UnblockFile.Models;
using Golabox.UnblockFile.Services;

namespace Golabox.UnblockFile.Tests;

public class SelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UnblockFileTests-" + Guid.NewGuid().ToString("N"));

    public SelectionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Create(string relative, bool blocked = true)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "contenu");
        if (blocked) File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        return path;
    }

    [Fact]
    public void SeveralFiles_FormOneBatch()
    {
        var a = Create("a.pdf");
        var b = Create(@"sous\b.pdf");
        var selection = Selection.FromPaths([a, b], out var error);

        Assert.Null(error);
        Assert.NotNull(selection);
        Assert.False(selection.IsDirectory);
        Assert.True(selection.IsMultipleFiles);
        Assert.Equal([a, b], selection.Paths);
        Assert.Null(selection.CommonFolder());
    }

    [Fact]
    public void SingleFolder_IsAFolderSelection()
    {
        var selection = Selection.FromPaths([_root], out _);
        Assert.NotNull(selection);
        Assert.True(selection.IsDirectory);
    }

    [Fact]
    public void FolderPlusFiles_IsRefusedWithAMessage()
    {
        Assert.Null(Selection.FromPaths([_root, Create("a.pdf")], out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void SeveralFolders_AreRefused()
    {
        var other = Directory.CreateDirectory(Path.Combine(_root, "autre")).FullName;
        Assert.Null(Selection.FromPaths([_root, other], out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void MissingItemsAreIgnored_DuplicatesRemoved_QuotesTrimmed()
    {
        var a = Create("a.pdf");
        var selection = Selection.FromPaths([a, a.ToUpperInvariant(), $"\"{a}\"", Path.Combine(_root, "absent.pdf"), ""], out var error);

        Assert.Null(error);
        Assert.Equal([a], selection!.Paths);
        Assert.NotNull(selection.Note);
        Assert.Equal(_root, selection.CommonFolder());
    }

    [Fact]
    public void NothingUsable_ReturnsAnError()
    {
        Assert.Null(Selection.FromPaths([Path.Combine(_root, "absent.pdf")], out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void CommandLineArguments_SelectAndScanButNeverUnblock()
    {
        // Même chemin que App.OnStartup → MainWindow : sélection puis analyse, aucune écriture.
        var a = Create("facture.pdf");
        var b = Create("photo.jpg");
        var selection = Selection.FromPaths([a, b], out _)!;
        var scan = FileScanService.Scan(selection, includeSubfolders: false, null, default);

        Assert.Equal(2, scan.BlockedCount);
        Assert.True(FileScanService.HasZoneIdentifier(a));
        Assert.True(FileScanService.HasZoneIdentifier(b));
    }

    [Fact]
    public void ScanOfSeveralFiles_CountsMarkedOnesAndSensitiveOnes()
    {
        var files = new[] { Create("a.pdf"), Create("b.txt", blocked: false), Create("setup.exe") };
        var scan = FileScanService.Scan(Selection.FromPaths(files, out _)!, false, null, default);

        Assert.Equal(3, scan.ExaminedCount);
        Assert.Equal(2, scan.BlockedCount);
        Assert.Equal(1, scan.RiskyBlockedCount);
    }

    [Fact]
    public void DisplayCommand_SeveralFiles_UsesALiteralPathList() =>
        Assert.Equal(@"Unblock-File -LiteralPath 'C:\a.pdf', 'C:\o''b.pdf'",
            PowerShellCommandBuilder.BuildDisplayCommand([@"C:\a.pdf", @"C:\o'b.pdf"], false, false));

    [Fact]
    public void Diagnostic_ContainsEnvironmentAndSessionEntries()
    {
        SessionDiagnostics.Log("entrée de test");
        SessionDiagnostics.LogException("test", new InvalidOperationException("boum"));
        var report = SessionDiagnostics.BuildReport();

        Assert.Contains("Version", report);
        Assert.Contains("Windows", report);
        Assert.Contains("Architecture", report);
        Assert.Contains("entrée de test", report);
        Assert.Contains("InvalidOperationException: boum", report);
    }
}
