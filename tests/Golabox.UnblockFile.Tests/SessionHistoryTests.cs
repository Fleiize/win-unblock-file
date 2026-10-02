using Golabox.UnblockFile.Models;
using Golabox.UnblockFile.Services;

namespace Golabox.UnblockFile.Tests;

public class SessionHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UnblockFileTests-" + Guid.NewGuid().ToString("N"));

    public SessionHistoryTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Create(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "contenu");
        return path;
    }

    private static List<FileResult> Unblocked(IEnumerable<string> paths) =>
        paths.Select(p => new FileResult(p, FileStatus.Unblocked)).ToList();

    [Fact]
    public void ExplicitFiles_GiveOneEntryPerUnblockedFile_WithoutAlreadyUnblocked()
    {
        var a = Create("a.pdf");
        var b = Create("b.pdf");
        var selection = Selection.FromPaths([a, b], out _)!;
        var results = new List<FileResult> { new(a, FileStatus.Unblocked), new(b, FileStatus.AlreadyUnblocked) };

        var entries = SessionHistory.BuildEntries(selection, results);

        var entry = Assert.Single(entries);
        Assert.Equal("a.pdf", entry.Title);
        Assert.Equal(a, entry.Path);
        Assert.False(entry.IsFolder);
    }

    [Fact]
    public void Folder_GivesOneSummaryEntry_NotOnePerFile()
    {
        var selection = Selection.FromPaths([_root], out _)!;
        var results = Unblocked(Enumerable.Range(0, 427).Select(i => Path.Combine(_root, $"f{i}.txt")));

        var entry = Assert.Single(SessionHistory.BuildEntries(selection, results));

        Assert.Equal(Path.GetFileName(_root), entry.Title);
        Assert.Equal("427 fichiers débloqués", entry.Subtitle);
        Assert.Equal(_root, entry.Path);
        Assert.True(entry.IsFolder);
    }

    [Fact]
    public void LargeExplicitSelection_IsCappedWithASummaryForTheRest()
    {
        var paths = Enumerable.Range(0, SessionHistory.MaxFileEntries + 10).Select(i => Create($"f{i}.txt")).ToList();
        var selection = Selection.FromPaths(paths, out _)!;

        var entries = SessionHistory.BuildEntries(selection, Unblocked(paths));

        Assert.Equal(SessionHistory.MaxFileEntries + 1, entries.Count);
        var summary = entries[^1];
        Assert.True(summary.IsFolder);
        Assert.Equal(_root, summary.Path);
        Assert.StartsWith("10 fichiers", summary.Subtitle);
    }

    [Fact]
    public void NothingUnblocked_GivesNoEntry()
    {
        var a = Create("a.pdf");
        var selection = Selection.FromPaths([a], out _)!;

        Assert.Empty(SessionHistory.BuildEntries(selection, [new FileResult(a, FileStatus.AlreadyUnblocked), new FileResult(a, FileStatus.Error, "x")]));
    }

    [Fact]
    public void CompleteSuccess_RequiresNoFailureAndACleanRescan()
    {
        var clean = new ScanResult { Path = _root };
        var withError = new ScanResult { Path = _root };
        withError.Errors.Add("accès refusé");
        var stillBlocked = new ScanResult { Path = _root };
        stillBlocked.BlockedFiles.Add("x");
        var canceled = new ScanResult { Path = _root, Canceled = true };

        var ok = new List<FileResult> { new("a", FileStatus.Unblocked) };
        var partial = new List<FileResult> { new("a", FileStatus.Unblocked), new("b", FileStatus.AccessDenied) };

        Assert.True(SessionHistory.IsCompleteSuccess(ok, clean));
        Assert.False(SessionHistory.IsCompleteSuccess(partial, clean));
        Assert.False(SessionHistory.IsCompleteSuccess(ok, withError));
        Assert.False(SessionHistory.IsCompleteSuccess(ok, stillBlocked));
        Assert.False(SessionHistory.IsCompleteSuccess(ok, canceled));
        Assert.False(SessionHistory.IsCompleteSuccess([new FileResult("a", FileStatus.AlreadyUnblocked)], clean));
    }

    [Fact]
    public void Push_PutsNewestFirst_AndTrimsOldest()
    {
        var list = new List<SessionEntry>();
        SessionHistory.Push(list, [new("1", null, "p1", false), new("2", null, "p2", false)]);
        SessionHistory.Push(list, [new("3", null, "p3", false)]);

        Assert.Equal(["3", "1", "2"], list.Select(e => e.Title));

        SessionHistory.Push(list, Enumerable.Range(0, SessionHistory.MaxEntries).Select(i => new SessionEntry($"n{i}", null, "p", false)));
        Assert.Equal(SessionHistory.MaxEntries, list.Count);
        Assert.Equal("n0", list[0].Title);
    }
}
