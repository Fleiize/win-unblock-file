namespace Golabox.UnblockFile.Models;

public enum FileStatus
{
    /// <summary>Le marqueur Zone.Identifier a été retiré.</summary>
    Unblocked,
    /// <summary>Le fichier n'avait (plus) aucun marqueur.</summary>
    AlreadyUnblocked,
    AccessDenied,
    NotFound,
    Error,
}

/// <summary>Résultat du déblocage d'un fichier.</summary>
public sealed record FileResult(string Path, FileStatus Status, string? Message = null)
{
    public bool IsFailure => Status is FileStatus.AccessDenied or FileStatus.NotFound or FileStatus.Error;
}
