using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Golabox.UnblockFile.Models;
using Golabox.UnblockFile.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Golabox.UnblockFile;

/// <summary>
/// Fenêtre unique. Le code-behind ne fait que coordonner l'interface ;
/// l'analyse et le déblocage vivent dans Services/.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private Selection? _selection;
    private ScanResult? _scan;
    private IReadOnlyList<FileResult>? _results;
    private string? _note;

    private bool _scanning;
    private bool _unblocking;
    private int _examined;

    private int _scanId;
    private CancellationTokenSource? _scanCts;
    private bool _suppressToggle;

    // Le succès pur disparaît seul ; avertissements et erreurs restent affichés.
    private readonly DispatcherTimer _successTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private bool _successDismissed;

    private bool IsDirectory => _selection?.IsDirectory == true;

    public MainWindow(IReadOnlyList<string>? startupPaths = null)
    {
        ApplicationThemeManager.ApplySystemTheme();
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        DependencyPropertyDescriptor.FromProperty(InfoBar.IsOpenProperty, typeof(InfoBar))
            .AddValueChanged(ResultBar, ResultBar_IsOpenChanged);
        _successTimer.Tick += (_, _) =>
        {
            _successTimer.Stop();
            _successDismissed = true;
            ResultBar.IsOpen = false;
        };
        AdvVersion.Text = SessionDiagnostics.Version;
        AboutRun.Text = $"Unblock File {SessionDiagnostics.Version} · Golabox · Licence MIT · ";
        Refresh();

        // Arguments : sélection + analyse uniquement. Le déblocage exige toujours un clic.
        if (startupPaths is { Count: > 0 }) Loaded += (_, _) => Select(startupPaths);
    }

    // ───────────── Sélection ─────────────

    private void Select(IEnumerable<string> paths)
    {
        if (_unblocking) return;

        var selection = Selection.FromPaths(paths, out var error);
        if (selection is null)
        {
            _note = error;
            SessionDiagnostics.Log($"Sélection refusée : {error}");
            Refresh();
            return;
        }

        _selection = selection;
        _scan = null;
        _results = null;
        _note = selection.Note;
        SessionDiagnostics.Log($"Sélection : {DescribeKind()} — {string.Join(" | ", selection.Paths.Take(5))}{(selection.Paths.Count > 5 ? " | …" : "")}");

        _suppressToggle = true;
        SubfoldersToggle.IsChecked = false; // par sécurité : désactivé par défaut
        _suppressToggle = false;

        _ = ScanAsync();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_unblocking) return;
        _scanCts?.Cancel();
        _scanId++;
        _selection = null;
        _scan = null;
        _results = null;
        _note = null;
        _scanning = false;
        Refresh();
        BrowseFileButton.Focus();
    }

    private void Rescan_Click(object sender, RoutedEventArgs e) => _ = ScanAsync();

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choisir un ou plusieurs fichiers",
            CheckFileExists = true,
            DereferenceLinks = false,
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == true) Select(dialog.FileNames);
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choisir un dossier" };
        if (dialog.ShowDialog(this) == true) Select([dialog.FolderName]);
    }

    /// <summary>Ouvre l'Explorateur sur l'élément (sélectionné, jamais ouvert ni exécuté).</summary>
    private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_selection is null) return;
        var target = _selection.Paths[0];
        // Les noms de fichiers Windows ne peuvent pas contenir de guillemets : la mise entre guillemets est sûre.
        var arguments = _selection.IsDirectory ? $"\"{target}\"" : $"/select,\"{target}\"";
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), arguments)
            {
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            SessionDiagnostics.LogException("Explorateur", ex);
            _note = "Impossible d’ouvrir l’Explorateur.";
            Refresh();
        }
    }

    // ───────────── Glisser-déposer ─────────────

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        e.Effects = _unblocking ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        SetDropHighlight(!_unblocking);
    }

    private void Window_PreviewDragLeave(object sender, DragEventArgs e) => SetDropHighlight(false);

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        SetDropHighlight(false);
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } items) return;

        e.Handled = true;
        Select(items);
    }

    private void SetDropHighlight(bool on)
    {
        DropOutline.Stroke = (Brush)FindResource(on ? "AccentFillColorDefaultBrush" : "ControlStrongStrokeColorDefaultBrush");
        DropOutline.StrokeThickness = on ? 2.5 : 1.5;
    }

    // ───────────── Analyse ─────────────

    private async Task ScanAsync(bool keepResult = false)
    {
        if (_selection is null) return;
        var selection = _selection;

        _scanCts?.Cancel();
        var cts = _scanCts = new CancellationTokenSource();
        int id = ++_scanId;

        _scanning = true;
        _scan = null;
        _examined = 0;
        if (!keepResult) _results = null;
        Refresh();

        var progress = new Progress<int>(n =>
        {
            if (id != _scanId || !_scanning) return;
            _examined = n;
            Refresh();
        });

        ScanResult scan;
        try
        {
            scan = await FileScanService.ScanAsync(selection, SubfoldersToggle.IsChecked == true, progress, cts.Token);
        }
        catch (Exception ex)
        {
            SessionDiagnostics.LogException("analyse", ex);
            scan = new ScanResult { Path = selection.Paths[0], IsDirectory = selection.IsDirectory };
            scan.Errors.Add(ex.Message);
        }

        if (id != _scanId) return; // une analyse plus récente a pris le relais
        _scan = scan;
        _scanning = false;

        SessionDiagnostics.Log($"Analyse : {scan.ExaminedCount} examiné(s), {scan.BlockedCount} avec Zone.Identifier, " +
                               $"{scan.RiskyBlockedCount} sensible(s), {scan.Errors.Count} erreur(s), {scan.Duration.TotalMilliseconds:N0} ms" +
                               (scan.Canceled ? ", ANNULÉE" : "") + (scan.IsNetworkPath ? ", réseau" : ""));
        foreach (var error in scan.Errors.Take(50)) SessionDiagnostics.Log($"  erreur d’analyse : {error}");

        Refresh();
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

    private void SubfoldersToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle || _selection is null) return;
        _results = null;
        _ = ScanAsync();
    }

    // ───────────── Déblocage ─────────────

    private async void Unblock_Click(object sender, RoutedEventArgs e)
    {
        if (_scan is not { BlockedCount: > 0 } scan || _scanning || _unblocking) return;

        try
        {
            if (scan.RiskyBlockedCount > 0 && !await ConfirmRiskyAsync(scan.RiskyBlockedCount)) return;

            _unblocking = true;
            Refresh();
            var files = scan.BlockedFiles.ToList();
            var stopwatch = Stopwatch.StartNew();
            _results = await UnblockService.UnblockAsync(files);
            _successDismissed = false;
            _unblocking = false;
            LogResults(_results, stopwatch.Elapsed);

            await ScanAsync(keepResult: true); // recompte réel après déblocage
        }
        catch (Exception ex)
        {
            SessionDiagnostics.LogException("déblocage", ex);
            _unblocking = false;
            _results = (_scan?.BlockedFiles ?? []).Select(f => new FileResult(f, FileStatus.Error, ex.Message)).ToList();
            Refresh();
        }
    }

    private static void LogResults(IReadOnlyList<FileResult> results, TimeSpan duration)
    {
        SessionDiagnostics.Log($"Déblocage : {results.Count(r => r.Status == FileStatus.Unblocked)} réussi(s), " +
                               $"{results.Count(r => r.IsFailure)} échec(s), " +
                               $"{results.Count(r => r.Status == FileStatus.AlreadyUnblocked)} déjà débloqué(s), " +
                               $"{duration.TotalMilliseconds:N0} ms");
        foreach (var failure in results.Where(r => r.IsFailure).Take(50))
            SessionDiagnostics.Log($"  {UnblockService.StatusLabel(failure.Status)} : {failure.Path} — {failure.Message}");
    }

    private async Task<bool> ConfirmRiskyAsync(int count)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Confirmer le déblocage",
            Content = $"{(count == 1 ? "1 fichier à débloquer est" : $"{count} fichiers à débloquer sont")} exécutable(s) ou susceptible(s) de contenir du contenu actif " +
                      "(.exe, .msi, .ps1, .docm…). Leur déblocage peut réduire certaines protections Windows.\n\n" +
                      "Débloquez uniquement des fichiers dont vous connaissez et acceptez la provenance.",
            PrimaryButtonText = "Débloquer quand même",
            CloseButtonText = "Annuler",
            Owner = this,
        };
        var confirmed = await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
        SessionDiagnostics.Log($"Confirmation fichiers sensibles ({count}) : {(confirmed ? "acceptée" : "annulée")}");
        return confirmed;
    }

    // ───────────── Affichage ─────────────

    private void Refresh()
    {
        bool has = _selection is not null;
        bool busy = _scanning || _unblocking;

        EmptyPanel.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        SelectionPanel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        DropOutline.StrokeDashArray = has ? null : [4, 3];
        DropZone.MinHeight = has ? 0 : 230; // grande zone d'accueil si vide, carte compacte sinon

        BrowseFileButton.IsEnabled = BrowseFolderButton.IsEnabled = !_unblocking;
        ClearButton.IsEnabled = RescanButton.IsEnabled = !_unblocking;
        SubfoldersToggle.IsEnabled = !_unblocking;
        SubfoldersToggle.Visibility = has && IsDirectory ? Visibility.Visible : Visibility.Collapsed;
        ScanRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CancelScanButton.Visibility = _scanning && !_unblocking ? Visibility.Visible : Visibility.Collapsed;

        if (_selection is { } selection)
        {
            if (selection.IsMultipleFiles)
            {
                SelectionIcon.Symbol = SymbolRegular.DocumentMultiple24;
                NameText.Text = $"{selection.Paths.Count:N0} fichiers sélectionnés";
                PathText.Text = selection.CommonFolder() is { } folder ? $"Dans {folder}" : "Dans plusieurs dossiers";
            }
            else
            {
                var path = selection.Paths[0];
                SelectionIcon.Symbol = selection.IsDirectory ? SymbolRegular.Folder24 : SymbolRegular.Document24;
                NameText.Text = Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } name ? name : path;
                PathText.Text = path;
            }
            PathText.ToolTip = PathText.Text;
        }

        RefreshStatus();
        RefreshBars();
        RefreshAdvanced();
    }

    private void RefreshStatus()
    {
        string status = "", detail = "", button = "Débloquer";
        bool canUnblock = false;
        bool multi = _selection?.IsMultipleFiles == true;
        bool justUnblocked = _results?.Any(r => r.Status == FileStatus.Unblocked) == true;

        if (_unblocking)
        {
            status = "Déblocage en cours…";
            button = "Déblocage en cours…";
        }
        else if (_scanning)
        {
            status = "Analyse en cours…";
            detail = $"{Files(_examined)} examiné{(_examined > 1 ? "s" : "")}";
            button = "Analyse en cours…";
        }
        else if (_scan is { } s)
        {
            int blocked = s.BlockedCount;
            bool unreadable = s.ExaminedCount == 0 && s.Errors.Count > 0;

            status = unreadable ? (IsDirectory ? "Impossible de lire ce dossier" : "Impossible de lire ce fichier")
                : s.Canceled ? "Analyse annulée"
                : !IsDirectory && !multi ? (blocked > 0 ? "Ce fichier est bloqué par Windows" : justUnblocked ? "Ce fichier n’est plus bloqué" : "Ce fichier n’est pas bloqué")
                : blocked == 1 ? "1 fichier est actuellement bloqué par Windows"
                : blocked > 1 ? $"{blocked:N0} fichiers sont actuellement bloqués par Windows"
                : s.ExaminedCount == 0 ? "Ce dossier ne contient aucun fichier"
                : IsDirectory ? (justUnblocked ? "Plus aucun fichier bloqué dans ce dossier" : "Aucun fichier n’est bloqué dans ce dossier")
                : justUnblocked ? "Ces fichiers ne sont plus bloqués" : "Aucun de ces fichiers n’est bloqué";

            if ((IsDirectory || multi) && s.ExaminedCount > 0)
                detail = $"{Files(s.ExaminedCount)} {(IsDirectory ? "trouvé" : "examiné")}{(s.ExaminedCount > 1 ? "s" : "")} · {blocked:N0} marqué{(blocked > 1 ? "s" : "")} comme provenant d’Internet";
            else if (blocked > 0)
                detail = "Marqué comme provenant d’Internet (Zone.Identifier)";
            if (s.Canceled && blocked > 0) detail += " · résultat partiel";
            if (s.Errors.Count > 0)
                detail += (detail.Length > 0 ? " · " : "") + $"{s.Errors.Count} erreur{(s.Errors.Count > 1 ? "s" : "")} d’analyse (voir Informations avancées)";

            if (blocked > 0)
            {
                canUnblock = true;
                button = IsDirectory || multi ? $"Débloquer {Files(blocked)}" : "Débloquer";
            }
            else
            {
                button = "Aucun fichier bloqué";
            }
        }

        StatusText.Text = status;
        DetailText.Text = detail;
        DetailText.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        UnblockButton.Content = button;
        UnblockButton.IsEnabled = canUnblock;
    }

    private void RefreshBars()
    {
        bool ready = !_scanning && !_unblocking && _scan is not null;

        // Fichiers exécutables / à contenu actif
        if (ready && _scan!.RiskyBlockedCount > 0)
        {
            RiskBar.Message = IsDirectory
                ? "Ce dossier contient aussi des fichiers exécutables ou pouvant contenir du contenu actif. Leur déblocage peut réduire certaines protections Windows."
                : _selection!.IsMultipleFiles
                    ? "Cette sélection contient des fichiers exécutables ou pouvant contenir du contenu actif. Leur déblocage peut réduire certaines protections Windows."
                    : "Ce fichier est exécutable ou peut contenir du contenu actif. Son déblocage peut réduire certaines protections Windows.";
            RiskBar.IsOpen = true;
        }
        else RiskBar.IsOpen = false;

        // Partage réseau sans marqueur local, ou note de sélection
        string? note = ready && _scan!.IsNetworkPath && _scan.BlockedCount == 0 && !_scan.Canceled && _scan.Errors.Count == 0
            ? "Aucun marqueur local n’a été trouvé. Si l’aperçu reste bloqué, Windows peut considérer ce partage réseau comme provenant de la zone Internet."
            : _note;
        NoteBar.Message = note ?? "";
        NoteBar.IsOpen = note is not null;

        // Résultat du dernier déblocage
        if (_results is { Count: > 0 } && !_unblocking)
        {
            int done = _results.Count(r => r.Status == FileStatus.Unblocked);
            int already = _results.Count(r => r.Status == FileStatus.AlreadyUnblocked);
            int failed = _results.Count(r => r.IsFailure);

            if (failed > 0)
            {
                ResultBar.Severity = InfoBarSeverity.Warning;
                ResultBar.Title = $"{FilesUnblocked(done)} — {(failed == 1 ? "1 fichier n’a pas pu être modifié" : $"{failed} fichiers n’ont pas pu être modifiés")}";
            }
            else if (done > 0)
            {
                ResultBar.Severity = InfoBarSeverity.Success;
                ResultBar.Title = done == 1 ? "Fichier débloqué avec succès" : $"{FilesUnblocked(done)} avec succès";
            }
            else
            {
                ResultBar.Severity = InfoBarSeverity.Informational;
                ResultBar.Title = already == 1 ? "Ce fichier n’avait plus de marqueur" : "Ces fichiers n’avaient plus de marqueur";
            }
            ResultBar.Message = "";

            // Seul un succès pur est fermable et temporaire.
            bool pureSuccess = failed == 0 && done > 0;
            ResultBar.IsClosable = pureSuccess;
            ResultBar.IsOpen = !(pureSuccess && _successDismissed);
            if (pureSuccess && ResultBar.IsOpen)
            {
                if (!_successTimer.IsEnabled) _successTimer.Start();
            }
            else _successTimer.Stop();
            ShowErrorsButton.Visibility = failed > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _successTimer.Stop();
            ResultBar.IsOpen = false;
            ShowErrorsButton.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Fermeture (bouton X ou délai) : le succès ne doit pas se rouvrir au prochain Refresh().</summary>
    private void ResultBar_IsOpenChanged(object? sender, EventArgs e)
    {
        if (ResultBar.IsOpen) return;
        _successTimer.Stop();
        _successDismissed = true;
    }

    private string DescribeKind() =>
        _selection is null ? "—"
        : _selection.IsMultipleFiles ? $"{_selection.Paths.Count} fichiers"
        : !_selection.IsDirectory ? "Fichier"
        : SubfoldersToggle.IsChecked == true ? "Dossier (avec les sous-dossiers)" : "Dossier (sans les sous-dossiers)";

    private void RefreshAdvanced()
    {
        bool recursive = SubfoldersToggle.IsChecked == true;

        AdvKind.Text = DescribeKind();
        AdvTotal.Text = _scan is { } s ? s.ExaminedCount.ToString("N0") : "—";
        AdvBlocked.Text = _scan is { } s2 ? s2.BlockedCount.ToString("N0") : "—";
        AdvErrors.Text = _scan is { } s3 ? s3.Errors.Count.ToString("N0") : "—";
        AdvDuration.Text = _scan is { } s4 ? $"{s4.Duration.TotalSeconds:0.00} s" : "—";

        CommandBox.Text = _selection is { } selection
            ? PowerShellCommandBuilder.BuildDisplayCommand(selection.Paths, selection.IsDirectory, recursive)
            : "";
        CopyCommandButton.IsEnabled = _selection is not null;

        LogBox.Text = BuildLog();
        CopyReportButton.IsEnabled = LogBox.Text.Length > 0;
    }

    private string BuildLog()
    {
        var sb = new StringBuilder();
        if (_scan is { Errors.Count: > 0 } scan)
        {
            sb.AppendLine("Erreurs d’analyse :");
            foreach (var error in scan.Errors) sb.Append("  ").AppendLine(error);
        }
        if (_results is { Count: > 0 })
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine("Déblocage :");
            sb.Append(UnblockService.BuildReport(_results));
        }
        return sb.ToString().TrimEnd();
    }

    private void ShowErrors_Click(object sender, RoutedEventArgs e)
    {
        AdvancedExpander.IsExpanded = true;
        AdvancedExpander.BringIntoView();
        LogBox.ScrollToHome();
    }

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { SessionDiagnostics.LogException("lien", ex); }
        e.Handled = true;
    }

    // ───────────── Presse-papiers ─────────────

    private async void CopyCommand_Click(object sender, RoutedEventArgs e) =>
        await CopyAsync(CopyCommandButton, CommandBox.Text);

    private async void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder()
            .AppendLine($"Unblock File {SessionDiagnostics.Version} — rapport")
            .AppendLine($"Date : {DateTime.Now:g}")
            .AppendLine($"Sélection : {string.Join(" | ", _selection?.Paths ?? [])}")
            .AppendLine($"Type : {AdvKind.Text}")
            .AppendLine($"Fichiers examinés : {AdvTotal.Text} · avec Zone.Identifier : {AdvBlocked.Text}")
            .AppendLine($"Commande équivalente : {CommandBox.Text}");
        if (_results is { Count: > 0 }) report.AppendLine($"Résultat : {ResultBar.Title}");
        report.AppendLine().Append(LogBox.Text);

        await CopyAsync(CopyReportButton, report.ToString());
    }

    private async void CopyDiagnostic_Click(object sender, RoutedEventArgs e) =>
        await CopyAsync(CopyDiagnosticButton, SessionDiagnostics.BuildReport());

    private static async Task CopyAsync(Wpf.Ui.Controls.Button button, string text)
    {
        var original = button.Content;
        try
        {
            Clipboard.SetDataObject(text, copy: true);
            button.Content = "Copié";
        }
        catch (Exception ex)
        {
            SessionDiagnostics.LogException("presse-papiers", ex);
            button.Content = "Copie impossible";
        }
        await Task.Delay(1500);
        button.Content = original;
    }

    // ───────────── Textes ─────────────

    private static string Files(int n) => n <= 1 ? $"{n} fichier" : $"{n:N0} fichiers";

    private static string FilesUnblocked(int n) => n <= 1 ? $"{n} fichier débloqué" : $"{n:N0} fichiers débloqués";
}
