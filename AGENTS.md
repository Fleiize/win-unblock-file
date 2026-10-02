# AGENTS.md — Unblock File

Micro-utilitaire Windows qui retire le « Mark of the Web » (flux NTFS `Zone.Identifier`) des fichiers choisis par l'utilisateur.

**Avant toute modification : lire [PROJECT_STATE.md](PROJECT_STATE.md).**

## Stack
C# · WPF · .NET 10 (`net10.0-windows`) · WPF UI 4.3.0 · win-x64 · portable, sans installeur.

## Commandes
```powershell
dotnet build -c Release      # build
dotnet test -c Release       # tests (xUnit)
./publish.ps1                # dist\UnblockFile-win-x64\ + .zip + raccourci « Unblock File.lnk »
```

## Règles non négociables
- **Aucun PowerShell au runtime.** `Zone.Identifier` est détecté et supprimé directement en C# (`Services/`). La commande PowerShell affichée dans l'UI est uniquement informative.
- **Pas d'UAC** : `requestedExecutionLevel="asInvoker"`, jamais de relance en administrateur.
- Ne jamais ouvrir, exécuter ou modifier le contenu d'un fichier. Un chemin reçu en argument est seulement sélectionné et analysé : le déblocage exige un clic.
- Ne jamais affaiblir les protections Windows globales (SmartScreen, Defender, zones Internet/Intranet, registre, stratégies).
- Pas de télémétrie, réseau, logs sur disque, base de données, compte ou auto-update.
- Éviter toute dépendance supplémentaire. Architecture légère : code-behind pour l'UI, logique testable dans `Services/`.

## Après une évolution
- Mettre à jour `PROJECT_STATE.md` (état réel, nombre de tests, limites).
- Documenter toute décision structurante dans `docs/DECISIONS.md`.

Docs : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/DECISIONS.md](docs/DECISIONS.md) · [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
