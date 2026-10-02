namespace Golabox.UnblockFile.Models;

/// <summary>Résultat d'une analyse (lecture seule) d'un fichier ou d'un dossier.</summary>
public sealed class ScanResult
{
    public required string Path { get; init; }
    public bool IsDirectory { get; init; }
    public bool IncludeSubfolders { get; init; }

    /// <summary>Nombre de fichiers examinés.</summary>
    public int ExaminedCount { get; set; }

    /// <summary>Fichiers possédant un flux Zone.Identifier.</summary>
    public List<string> BlockedFiles { get; } = [];

    /// <summary>Parmi les fichiers bloqués : extensions exécutables ou pouvant contenir du contenu actif.</summary>
    public int RiskyBlockedCount { get; set; }

    /// <summary>Erreurs rencontrées pendant l'analyse (dossier inaccessible, etc.).</summary>
    public List<string> Errors { get; } = [];

    public bool IsNetworkPath { get; init; }
    public bool Canceled { get; set; }
    public TimeSpan Duration { get; set; }

    public int BlockedCount => BlockedFiles.Count;
}
