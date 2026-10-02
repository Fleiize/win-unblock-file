using Golabox.UnblockFile.Models;

namespace Golabox.UnblockFile.Services;

/// <summary>
/// Règles de la liste « Débloqués cette session ». Tout reste en mémoire : rien n'est écrit sur disque.
/// </summary>
public static class SessionHistory
{
    /// <summary>Au-delà, les fichiers d'une sélection explicite sont regroupés en une ligne de résumé.</summary>
    public const int MaxFileEntries = 25;

    /// <summary>Garde-fou : les lignes les plus anciennes sont retirées de la liste.</summary>
    public const int MaxEntries = 200;

    /// <summary>
    /// Dossier → une seule ligne de résumé (jamais une ligne par fichier).
    /// Sélection explicite de fichiers → une ligne par fichier réellement débloqué,
    /// plus une ligne de résumé pour l'excédent.
    /// </summary>
    public static IReadOnlyList<SessionEntry> BuildEntries(Selection selection, IReadOnlyList<FileResult> results)
    {
        var done = results.Where(r => r.Status == FileStatus.Unblocked).Select(r => r.Path).ToList();
        if (done.Count == 0) return [];

        if (selection.IsDirectory)
        {
            var folder = selection.Paths[0];
            return [new SessionEntry(FolderName(folder), UnblockedLabel(done.Count), folder, IsFolder: true)];
        }

        var entries = done.Take(MaxFileEntries)
            .Select(p => new SessionEntry(Path.GetFileName(p), null, p, IsFolder: false))
            .ToList();

        if (done.Count > MaxFileEntries)
        {
            var rest = done.Skip(MaxFileEntries).ToList();
            var folder = selection.CommonFolder() ?? Path.GetDirectoryName(rest[0]) ?? rest[0];
            entries.Add(new SessionEntry("Autres fichiers", UnblockedLabel(rest.Count), folder, IsFolder: true));
        }
        return entries;
    }

    /// <summary>
    /// Succès complet : au moins un fichier débloqué, aucun échec, et la ré-analyse confirme
    /// qu'il ne reste ni fichier bloqué ni erreur. Seul ce cas autorise le retour automatique à l'accueil.
    /// </summary>
    public static bool IsCompleteSuccess(IReadOnlyList<FileResult> results, ScanResult rescan) =>
        results.Any(r => r.Status == FileStatus.Unblocked)
        && !results.Any(r => r.IsFailure)
        && !rescan.Canceled
        && rescan.Errors.Count == 0
        && rescan.BlockedCount == 0;

    /// <summary>Ajoute les nouvelles lignes en tête de liste (les plus récentes d'abord).</summary>
    public static void Push(IList<SessionEntry> list, IEnumerable<SessionEntry> entries)
    {
        int index = 0;
        foreach (var entry in entries) list.Insert(index++, entry);
        while (list.Count > MaxEntries) list.RemoveAt(list.Count - 1);
    }

    private static string FolderName(string folder) =>
        Path.GetFileName(folder.TrimEnd('\\')) is { Length: > 0 } name ? name : folder;

    private static string UnblockedLabel(int n) => n <= 1 ? $"{n} fichier débloqué" : $"{n:N0} fichiers débloqués";
}
