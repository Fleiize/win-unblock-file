using Golabox.UnblockFile.Models;
using Golabox.UnblockFile.Services;

namespace Golabox.UnblockFile.Tests;

public class UnblockServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UnblockFileTests-" + Guid.NewGuid().ToString("N"));

    public UnblockServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Create(string name, bool blocked)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "contenu");
        if (blocked) File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        return path;
    }

    [Fact]
    public async Task Unblock_HandlesSpecialCharactersAndMixedResults()
    {
        var files = new List<string>
        {
            Create("simple.pdf", true),
            Create("avec espaces.pdf", true),
            Create("o'brien (1) [copie].pdf", true),
            Create("l\u2019apostrophe \u00e9t\u00e9 \u65e5\u672c\u8a9e $env;calc&x.pdf", true),
            Create("deja-debloque.pdf", false),
            Path.Combine(_root, "disparu.pdf"),
        };

        var results = await UnblockService.UnblockAsync(files);

        Assert.Equal(files, results.Select(r => r.Path));
        Assert.All(results.Take(4), r => Assert.Equal(FileStatus.Unblocked, r.Status));
        Assert.Equal(FileStatus.AlreadyUnblocked, results[4].Status);
        Assert.Equal(FileStatus.NotFound, results[5].Status);

        // le marqueur a réellement disparu, le contenu est intact
        Assert.All(files.Take(5), f => Assert.False(FileScanService.HasZoneIdentifier(f)));
        Assert.Equal("contenu", File.ReadAllText(files[2]));
    }

    [Fact]
    public async Task Unblock_ReadOnlyFile()
    {
        var file = Create("lecture-seule.pdf", true);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            var result = (await UnblockService.UnblockAsync([file]))[0];
            // Windows autorise ou refuse selon la configuration : dans les deux cas, résultat explicite, pas d'exception.
            Assert.True(result.Status is FileStatus.Unblocked or FileStatus.AccessDenied or FileStatus.Error);
            Assert.Equal(result.Status == FileStatus.Unblocked, !FileScanService.HasZoneIdentifier(file));
        }
        finally { File.SetAttributes(file, FileAttributes.Normal); }
    }

    [Fact]
    public async Task Unblock_AFailingFileDoesNotStopTheOthers()
    {
        var locked = Create("verrouille.pdf", true);
        var ok1 = Create("ok1.pdf", true);
        var ok2 = Create("ok2.pdf", true);

        // Le flux Zone.Identifier est ouvert sans partage : le supprimer échoue pour ce fichier seulement.
        using var hold = new FileStream(locked + ":Zone.Identifier", FileMode.Open, FileAccess.Read, FileShare.None);
        var results = await UnblockService.UnblockAsync([ok1, locked, ok2]);

        Assert.Equal(FileStatus.Unblocked, results[0].Status);
        Assert.True(results[1].IsFailure, $"statut inattendu : {results[1].Status}");
        Assert.Equal(FileStatus.Unblocked, results[2].Status);
        Assert.True(FileScanService.HasZoneIdentifier(locked));
    }

    [Fact]
    public void BuildReport_ListsEveryFileWithItsStatus()
    {
        var report = UnblockService.BuildReport(
        [
            new FileResult(@"C:\a.pdf", FileStatus.Unblocked),
            new FileResult(@"C:\b.pdf", FileStatus.AccessDenied, "Refusé"),
        ]);
        Assert.Contains(@"[Débloqué] C:\a.pdf", report);
        Assert.Contains(@"[Accès refusé] C:\b.pdf — Refusé", report);
    }
}
