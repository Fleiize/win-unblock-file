# Unblock File

Petit utilitaire Windows qui retire le « Mark of the Web » (flux NTFS `Zone.Identifier`) des fichiers que vous choisissez, pour réactiver leur aperçu dans l'Explorateur Windows. Équivalent graphique de `Unblock-File`.

## Pourquoi ?

Depuis les mises à jour Windows d'octobre 2025, l'Explorateur désactive volontairement le volet d'aperçu des fichiers marqués comme provenant d'Internet (téléchargements, pièces jointes…). Retirer le marqueur réactive l'aperçu.

## Utilisation

1. Glissez un dossier, ou un ou plusieurs fichiers, dans la fenêtre. Vous pouvez aussi cliquer sur **Parcourir** ou les glisser directement sur `UnblockFile.exe` ou son raccourci.
2. L'application indique combien de fichiers sont bloqués. L'analyse est en lecture seule ; pour un dossier, « Inclure les sous-dossiers » est désactivé par défaut.
3. Cliquez sur **Débloquer**. Rien n'est jamais débloqué sans ce clic.

Les petites icônes de la carte permettent d'afficher l'élément dans l'Explorateur (pour vérifier l'aperçu), d'analyser à nouveau ou d'effacer la sélection. « Informations avancées » affiche le détail, la commande PowerShell équivalente (informative), le rapport, le diagnostic de session et la version.

## Avertissement

Retirer le Mark of the Web **n'est pas une analyse antivirus** : cela supprime une protection de Windows sans vérifier ni désinfecter le fichier. Ne débloquez que des fichiers dont vous connaissez et acceptez la provenance. Une confirmation est demandée si le lot contient des fichiers exécutables ou à contenu actif (`.exe`, `.msi`, `.ps1`, `.docm`…).

L'application ne modifie que le flux `Zone.Identifier` des fichiers choisis. Elle n'ouvre ni n'exécute aucun fichier, et ne touche ni au registre, ni à SmartScreen, ni aux zones de sécurité, ni à Defender.

## Droits et fonctionnement

- **Aucun droit administrateur** (`asInvoker`). Un fichier refusé par les permissions NTFS est signalé ; les autres sont traités.
- **Aucun processus PowerShell** : `Zone.Identifier` est traité directement en C#, ce qui évite les détections heuristiques antivirus/EDR.
- Aucune connexion réseau, télémétrie ou installation. Le diagnostic reste en mémoire et n'est copié qu'à votre demande.
- Partage réseau classé en zone Internet : sans marqueur local, l'aperçu peut rester bloqué par la zone du partage. L'application ne modifie jamais les zones de sécurité.

## Télécharger

Prenez `UnblockFile-win-x64.zip` dans les [Releases](https://github.com/Fleiize/win-unblock-file/releases), décompressez-le et lancez `UnblockFile.exe`. .NET n'a pas besoin d'être installé. Le premier lancement peut être plus lent pendant l'analyse antivirus du nouvel EXE.

## Compiler et publier

Prérequis : SDK .NET 10.

```powershell
dotnet build -c Release
dotnet test -c Release
./publish.ps1   # dist\UnblockFile-win-x64\, dist\UnblockFile-win-x64.zip, raccourci « Unblock File.lnk »
```

Détails : [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) · [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/DECISIONS.md](docs/DECISIONS.md)

## Licence

[MIT](LICENSE) — Golabox
