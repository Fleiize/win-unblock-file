using Golabox.UnblockFile.Models;

namespace Golabox.UnblockFile.Services;

/// <summary>Analyse en lecture seule : repère les fichiers portant un flux Zone.Identifier.</summary>
public static class FileScanService
{
    internal const string ZoneStream = ":Zone.Identifier";

    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msix", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".js", ".jse",
        ".wsf", ".scr", ".com", ".lnk", ".chm", ".reg", ".docm", ".xlsm", ".pptm",
    };

    // Comme Get-ChildItem sans -Force : les fichiers masqués et système sont ignorés.
    private static readonly EnumerationOptions EnumOptions = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = false,
    };

    public static bool IsSensitiveExtension(string path) =>
        SensitiveExtensions.Contains(Path.GetExtension(path));

    public static bool HasZoneIdentifier(string filePath) => File.Exists(filePath + ZoneStream);

    /// <summary>Chemin UNC ou lecteur réseau mappé.</summary>
    public static bool IsNetworkPath(string path)
    {
        try
        {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
                path = path[4..];
            else if (path.StartsWith(@"\\", StringComparison.Ordinal))
                return true;

            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    public static Task<ScanResult> ScanAsync(Selection selection, bool includeSubfolders,
        IProgress<int>? progress = null, CancellationToken ct = default) =>
        Task.Run(() => Scan(selection, includeSubfolders, progress, ct), CancellationToken.None);

    internal static ScanResult Scan(Selection selection, bool includeSubfolders, IProgress<int>? progress, CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = selection.Paths.Count == 1
            ? Scan(selection.Paths[0], includeSubfolders, progress, ct)
            : ScanFiles(selection.Paths, progress, ct);
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>Lot de fichiers choisis individuellement.</summary>
    internal static ScanResult ScanFiles(IReadOnlyList<string> files, IProgress<int>? progress, CancellationToken ct)
    {
        var result = new ScanResult
        {
            Path = files[0],
            IsNetworkPath = files.Any(IsNetworkPath),
        };

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) { result.Canceled = true; break; }
            if (File.Exists(file)) Examine(file, result);
            else result.Errors.Add($"Introuvable : {file}");
            if (result.ExaminedCount % 100 == 0) progress?.Report(result.ExaminedCount);
        }

        progress?.Report(result.ExaminedCount);
        return result;
    }

    internal static ScanResult Scan(string path, bool includeSubfolders, IProgress<int>? progress, CancellationToken ct)
    {
        bool isDirectory = Directory.Exists(path);
        var result = new ScanResult
        {
            Path = path,
            IsDirectory = isDirectory,
            IncludeSubfolders = isDirectory && includeSubfolders,
            IsNetworkPath = IsNetworkPath(path),
        };

        if (!isDirectory)
        {
            if (!File.Exists(path))
            {
                result.Errors.Add($"Introuvable : {path}");
                return result;
            }
            Examine(path, result);
            progress?.Report(result.ExaminedCount);
            return result;
        }

        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            if (ct.IsCancellationRequested) { result.Canceled = true; break; }
            var dir = pending.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", EnumOptions))
                {
                    if (ct.IsCancellationRequested) { result.Canceled = true; break; }
                    Examine(file, result);
                    if (result.ExaminedCount % 100 == 0) progress?.Report(result.ExaminedCount);
                }

                if (result.IncludeSubfolders && !result.Canceled)
                {
                    foreach (var sub in new DirectoryInfo(dir).EnumerateDirectories("*", EnumOptions))
                    {
                        // On ne suit pas les liens symboliques / jonctions (boucles possibles).
                        if (sub.LinkTarget is null) pending.Push(sub.FullName);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                result.Errors.Add($"{dir} : {ex.Message}");
            }
            if (result.Canceled) break;
        }

        progress?.Report(result.ExaminedCount);
        return result;
    }

    private static void Examine(string file, ScanResult result)
    {
        result.ExaminedCount++;
        if (!HasZoneIdentifier(file)) return;
        result.BlockedFiles.Add(file);
        if (IsSensitiveExtension(file)) result.RiskyBlockedCount++;
    }
}
