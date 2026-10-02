using Golabox.UnblockFile.Services;

namespace Golabox.UnblockFile.Tests;

public class FileScanServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UnblockFileTests-" + Guid.NewGuid().ToString("N"));

    public FileScanServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Create(string relative, bool blocked)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "contenu");
        if (blocked) File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        return path;
    }

    [Theory]
    [InlineData("setup.exe", true)]
    [InlineData("SETUP.MSI", true)]
    [InlineData("script.PS1", true)]
    [InlineData("macro.docm", true)]
    [InlineData("raccourci.lnk", true)]
    [InlineData("facture.pdf", false)]
    [InlineData("photo.jpg", false)]
    [InlineData("notes.docx", false)]
    [InlineData("sans-extension", false)]
    public void IsSensitiveExtension(string name, bool expected) =>
        Assert.Equal(expected, FileScanService.IsSensitiveExtension(@"C:\x\" + name));

    [Theory]
    [InlineData(@"\\serveur\partage\fichier.pdf", true)]
    [InlineData(@"\\?\UNC\serveur\partage\fichier.pdf", true)]
    [InlineData(@"C:\Users\fichier.pdf", false)]
    public void IsNetworkPath(string path, bool expected) =>
        Assert.Equal(expected, FileScanService.IsNetworkPath(path));

    [Fact]
    public void Scan_SingleBlockedFile()
    {
        var file = Create("facture.pdf", blocked: true);
        var scan = FileScanService.Scan(file, false, null, default);
        Assert.False(scan.IsDirectory);
        Assert.Equal(1, scan.ExaminedCount);
        Assert.Equal([file], scan.BlockedFiles);
        Assert.Equal(0, scan.RiskyBlockedCount);
    }

    [Fact]
    public void Scan_SingleCleanFile()
    {
        var scan = FileScanService.Scan(Create("propre.pdf", blocked: false), false, null, default);
        Assert.Equal(1, scan.ExaminedCount);
        Assert.Empty(scan.BlockedFiles);
    }

    [Fact]
    public void Scan_FolderIsNotRecursiveByDefault_AndCountsOnlyMarkedFiles()
    {
        Create("a.pdf", blocked: true);
        Create("b.txt", blocked: false);
        Create("sous/c.pdf", blocked: true);

        var scan = FileScanService.Scan(_root, includeSubfolders: false, null, default);
        Assert.True(scan.IsDirectory);
        Assert.Equal(2, scan.ExaminedCount);
        Assert.Single(scan.BlockedFiles);
    }

    [Fact]
    public void Scan_FolderRecursive_FindsNestedFilesAndSensitiveOnes()
    {
        Create("a.pdf", blocked: true);
        Create("b.txt", blocked: false);
        Create("sous/c.pdf", blocked: true);
        Create("sous/profond/outil.exe", blocked: true);

        var scan = FileScanService.Scan(_root, includeSubfolders: true, null, default);
        Assert.Equal(4, scan.ExaminedCount);
        Assert.Equal(3, scan.BlockedCount);
        Assert.Equal(1, scan.RiskyBlockedCount);
    }

    [Fact]
    public void Scan_DoesNotModifyAnything()
    {
        var file = Create("x.pdf", blocked: true);
        FileScanService.Scan(_root, true, null, default);
        Assert.True(FileScanService.HasZoneIdentifier(file));
    }

    [Fact]
    public void Scan_Canceled()
    {
        Create("a.pdf", blocked: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.True(FileScanService.Scan(_root, true, null, cts.Token).Canceled);
    }

    [Fact]
    public void Scan_MissingPath_ReportsErrorWithoutThrowing()
    {
        var scan = FileScanService.Scan(Path.Combine(_root, "absent.pdf"), false, null, default);
        Assert.Single(scan.Errors);
    }
}
