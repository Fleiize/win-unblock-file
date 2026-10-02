using System.Text;

namespace Golabox.UnblockFile.Services;

/// <summary>
/// Génère la commande PowerShell équivalente, affichée dans « Informations avancées » à titre pédagogique
/// (copier-coller). L'application elle-même ne lance jamais PowerShell.
/// </summary>
public static class PowerShellCommandBuilder
{
    /// <summary>
    /// Littéral PowerShell entre apostrophes. PowerShell considère aussi les apostrophes typographiques
    /// (U+2018 à U+201B) comme des apostrophes : tous ces caractères sont doublés.
    /// </summary>
    public static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2).Append('\'');
        foreach (var c in value)
        {
            sb.Append(c);
            if (c is '\'' or '‘' or '’' or '‚' or '‛') sb.Append(c);
        }
        return sb.Append('\'').ToString();
    }

    /// <summary>Plusieurs fichiers : -LiteralPath accepte une liste.</summary>
    public static string BuildDisplayCommand(IReadOnlyList<string> paths, bool isDirectory, bool recursive) =>
        paths.Count == 1
            ? BuildDisplayCommand(paths[0], isDirectory, recursive)
            : $"Unblock-File -LiteralPath {string.Join(", ", paths.Select(Quote))}";

    /// <summary>Équivalent lisible de ce que fait l'application, avec -LiteralPath.</summary>
    public static string BuildDisplayCommand(string path, bool isDirectory, bool recursive)
    {
        var quoted = Quote(path);
        if (!isDirectory) return $"Unblock-File -LiteralPath {quoted}";
        return recursive
            ? $"Get-ChildItem -LiteralPath {quoted} -File -Recurse | Unblock-File"
            : $"Get-ChildItem -LiteralPath {quoted} -File | Unblock-File";
    }
}
