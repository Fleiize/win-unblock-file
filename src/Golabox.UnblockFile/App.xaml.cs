using System.Windows;
using System.Windows.Threading;
using Golabox.UnblockFile.Services;

namespace Golabox.UnblockFile;

public partial class App : Application
{
    private bool _showingError;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Une erreur inattendue ne doit jamais fermer l'application silencieusement.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            SessionDiagnostics.LogException("tâche", args.Exception);
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) SessionDiagnostics.LogException("fatale", ex);
        };

        base.OnStartup(e);
        SessionDiagnostics.Log($"Démarrage, {e.Args.Length} argument(s)");

        // Les chemins passés en argument (glisser sur l'EXE ou le raccourci) sont seulement sélectionnés et analysés.
        new MainWindow(e.Args).Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        SessionDiagnostics.LogException("interface", e.Exception);
        if (_showingError) return;

        _showingError = true;
        try
        {
            var answer = MessageBox.Show(
                $"Une erreur inattendue s’est produite. L’application reste ouverte.\n\n{e.Exception.Message}\n\n" +
                "Copier les détails techniques dans le presse-papiers ?",
                "Unblock File", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer == MessageBoxResult.Yes)
            {
                try { Clipboard.SetDataObject(SessionDiagnostics.BuildReport(), copy: true); }
                catch (Exception) { /* presse-papiers occupé : tant pis */ }
            }
        }
        finally
        {
            _showingError = false;
        }
    }
}
