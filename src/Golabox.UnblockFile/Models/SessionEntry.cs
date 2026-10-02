namespace Golabox.UnblockFile.Models;

/// <summary>
/// Une ligne de « Débloqués cette session » (liste volatile, jamais persistée).
/// <paramref name="Path"/> est le chemin affiché et la cible de « Afficher dans l'Explorateur » :
/// un fichier (sélectionné dans l'Explorateur) ou, si <paramref name="IsFolder"/>, un dossier (ouvert).
/// </summary>
public sealed record SessionEntry(string Title, string? Subtitle, string Path, bool IsFolder);
