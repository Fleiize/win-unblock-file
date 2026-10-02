# Architecture

```
src/Golabox.UnblockFile/
  App.xaml(.cs)            démarrage, arguments CLI, filet anti-crash
  MainWindow.xaml(.cs)     fenêtre unique ; code-behind = coordination UI uniquement
  Models/   Selection, ScanResult, FileResult
  Services/ FileScanService, UnblockService, PowerShellCommandBuilder, SessionDiagnostics
tests/Golabox.UnblockFile.Tests/   xUnit, sur de vrais fichiers NTFS temporaires
```

## Flux
1. **Sélection** (`Models/Selection`) : glisser-déposer, dialogues ou arguments de l'EXE. Elle accepte *soit un dossier, soit un ou plusieurs fichiers*. Un mélange de dossier et de fichiers, ou plusieurs dossiers, est refusé avec un message. Les éléments introuvables sont ignorés et signalés.
2. **Analyse** (`FileScanService`), en lecture seule, dans `Task.Run`, avec progression et annulation (`CancellationToken`). Un fichier est « bloqué » si `File.Exists(path + ":Zone.Identifier")`. Dossier : récursion optionnelle, désactivée par défaut. Les fichiers masqués/système sont ignorés, comme `Get-ChildItem`, et les liens symboliques et jonctions ne sont pas suivis. Une erreur de dossier est comptée sans interrompre le reste.
3. **Déblocage** (`UnblockService`) : uniquement les fichiers marqués par l'analyse. `File.Delete(path + ":Zone.Identifier")` est exactement ce que fait `Unblock-File`. Chaque fichier est vérifié ensuite et obtient un statut : `Unblocked`, `AlreadyUnblocked`, `AccessDenied`, `NotFound` ou `Error`. Une erreur n'arrête jamais le lot. L'analyse est ensuite relancée pour afficher le recomptage réel.
4. **PowerShellCommandBuilder** : génère seulement la commande équivalente affichée (`-LiteralPath`, apostrophes doublées, y compris les typographiques), sans jamais l'exécuter.

## Erreurs et diagnostic
- `SessionDiagnostics` est un journal **en mémoire** (500 entrées max) : sélection, analyse, déblocage, durées, erreurs, exceptions. « Copier le diagnostic » y ajoute la version, Windows, l'architecture, .NET et le statut admin. Rien n'est écrit sur disque ni envoyé.
- `App` intercepte les exceptions UI (`DispatcherUnhandledException`) et affiche un message simple avec la proposition de copier les détails. Les exceptions de tâches et les exceptions fatales sont journalisées.

## Droits
Manifeste `asInvoker` : tout s'exécute avec les droits de l'utilisateur courant. Un refus NTFS donne `AccessDenied` pour ce fichier seulement.
