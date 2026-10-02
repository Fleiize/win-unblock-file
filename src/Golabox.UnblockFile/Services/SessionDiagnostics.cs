using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Golabox.UnblockFile.Services;

/// <summary>
/// Journal de diagnostic de la session, en mémoire uniquement : rien n'est écrit sur disque ni envoyé.
/// L'utilisateur le copie explicitement (« Copier le diagnostic ») s'il veut le transmettre.
/// </summary>
public static class SessionDiagnostics
{
    private const int MaxEntries = 500;
    private static readonly List<string> Entries = [];
    private static readonly Lock Gate = new();

    public static string Version { get; } =
        (Assembly.GetEntryAssembly() ?? typeof(SessionDiagnostics).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(SessionDiagnostics).Assembly.GetName().Version?.ToString(3) ?? "?";

    public static void Log(string message)
    {
        lock (Gate)
        {
            if (Entries.Count >= MaxEntries) Entries.RemoveAt(0);
            Entries.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        }
    }

    public static void LogException(string context, Exception ex) => Log($"EXCEPTION ({context}) : {ex}");

    /// <summary>Texte complet du diagnostic : environnement puis journal de la session.</summary>
    public static string BuildReport()
    {
        var sb = new StringBuilder()
            .AppendLine("Unblock File — diagnostic de session")
            .AppendLine($"Version          : {Version}")
            .AppendLine($"Date             : {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Windows          : {RuntimeInformation.OSDescription} ({Environment.OSVersion.Version})")
            .AppendLine($"Architecture     : processus {RuntimeInformation.ProcessArchitecture}, système {RuntimeInformation.OSArchitecture}")
            .AppendLine($".NET             : {RuntimeInformation.FrameworkDescription}")
            .AppendLine($"Administrateur   : {(IsElevated() ? "oui" : "non")}")
            .AppendLine()
            .AppendLine("Journal :");

        lock (Gate)
        {
            if (Entries.Count == 0) sb.AppendLine("  (vide)");
            foreach (var entry in Entries) sb.Append("  ").AppendLine(entry);
        }
        return sb.ToString();
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static void Clear()
    {
        lock (Gate) Entries.Clear();
    }
}
