using System.Text;
using Golabox.UnblockFile.Models;

namespace Golabox.UnblockFile.Services;

/// <summary>
/// Retire le flux NTFS alternatif Zone.Identifier (« Mark of the Web »), exactement ce que fait Unblock-File.
/// Tout se passe dans le processus, avec les droits de l'utilisateur courant : aucun PowerShell n'est lancé.
/// </summary>
public static class UnblockService
{
    public static Task<IReadOnlyList<FileResult>> UnblockAsync(IReadOnlyList<string> files) =>
        Task.Run<IReadOnlyList<FileResult>>(() => files.Select(Unblock).ToList());

    /// <summary>Débloque un seul fichier ; ne lève jamais d'exception.</summary>
    internal static FileResult Unblock(string path)
    {
        try
        {
            if (!File.Exists(path)) return new FileResult(path, FileStatus.NotFound, "Le fichier n'existe plus.");
            if (!FileScanService.HasZoneIdentifier(path)) return new FileResult(path, FileStatus.AlreadyUnblocked);

            File.Delete(path + FileScanService.ZoneStream);

            return FileScanService.HasZoneIdentifier(path)
                ? new FileResult(path, FileStatus.Error, "Le marqueur est toujours présent après la suppression.")
                : new FileResult(path, FileStatus.Unblocked);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new FileResult(path, FileStatus.AccessDenied, ex.Message);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new FileResult(path, FileStatus.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            // fichier verrouillé, partage déconnecté, chemin invalide…
            return new FileResult(path, FileStatus.Error, ex.Message);
        }
    }

    public static string StatusLabel(FileStatus status) => status switch
    {
        FileStatus.Unblocked => "Débloqué",
        FileStatus.AlreadyUnblocked => "Déjà débloqué",
        FileStatus.AccessDenied => "Accès refusé",
        FileStatus.NotFound => "Fichier introuvable",
        _ => "Erreur",
    };

    /// <summary>Rapport texte, une ligne par fichier.</summary>
    public static string BuildReport(IReadOnlyList<FileResult> results)
    {
        var sb = new StringBuilder();
        foreach (var r in results)
        {
            sb.Append('[').Append(StatusLabel(r.Status)).Append("] ").Append(r.Path);
            if (r.IsFailure && !string.IsNullOrWhiteSpace(r.Message))
                sb.Append(" — ").Append(r.Message.Trim());
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
