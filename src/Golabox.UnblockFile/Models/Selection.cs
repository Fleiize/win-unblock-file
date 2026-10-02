namespace Golabox.UnblockFile.Models;

/// <summary>
/// Ce que l'utilisateur a choisi : soit UN dossier, soit un ou plusieurs fichiers.
/// Le mélange dossier + fichiers et les dossiers multiples sont refusés (message simple).
/// Construire une sélection ne modifie jamais aucun fichier.
/// </summary>
public sealed class Selection
{
    public IReadOnlyList<string> Paths { get; }
    public bool IsDirectory { get; }

    /// <summary>Remarque non bloquante (éléments introuvables ignorés…).</summary>
    public string? Note { get; }

    private Selection(IReadOnlyList<string> paths, bool isDirectory, string? note)
    {
        Paths = paths;
        IsDirectory = isDirectory;
        Note = note;
    }

    public bool IsMultipleFiles => !IsDirectory && Paths.Count > 1;

    /// <summary>
    /// Construit une sélection depuis un glisser-déposer, un dialogue ou la ligne de commande.
    /// Retourne null avec un message d'erreur si la combinaison n'est pas prise en charge.
    /// </summary>
    public static Selection? FromPaths(IEnumerable<string> rawPaths, out string? error)
    {
        error = null;
        var files = new List<string>();
        var folders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int missing = 0;

        foreach (var raw in rawPaths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string path;
            try { path = System.IO.Path.GetFullPath(raw.Trim().Trim('"')); }
            catch (Exception) { missing++; continue; }

            if (!seen.Add(path)) continue;
            if (Directory.Exists(path)) folders.Add(path);
            else if (File.Exists(path)) files.Add(path);
            else missing++;
        }

        string? note = missing switch
        {
            0 => null,
            1 => "1 élément introuvable a été ignoré.",
            _ => $"{missing} éléments introuvables ont été ignorés.",
        };

        if (folders.Count > 0 && files.Count > 0)
            error = "Choisissez soit un dossier, soit un ou plusieurs fichiers, mais pas les deux à la fois.";
        else if (folders.Count > 1)
            error = "Un seul dossier à la fois, s’il vous plaît.";
        else if (folders.Count == 1)
            return new Selection(folders, isDirectory: true, note);
        else if (files.Count > 0)
            return new Selection(files, isDirectory: false, note);
        else
            error = "Impossible d’accéder à cet élément.";

        return null;
    }

    /// <summary>Dossier parent commun des fichiers, ou null s'ils sont répartis dans plusieurs dossiers.</summary>
    public string? CommonFolder()
    {
        var parents = Paths.Select(p => System.IO.Path.GetDirectoryName(p) ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return parents.Count == 1 && parents[0].Length > 0 ? parents[0] : null;
    }
}
