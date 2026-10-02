# Déploiement

Prérequis : Windows, SDK .NET 10. Les sources NuGet sont fixées par `nuget.config` (nuget.org).

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
./publish.ps1
```

`publish.ps1` :
- `dotnet publish` en win-x64, self-contained, `PublishSingleFile`, `EnableCompressionInSingleFile`, `IncludeNativeLibrariesForSelfExtract`, sans trimming ni symboles ;
- produit `dist\UnblockFile-win-x64\` (`UnblockFile.exe`, `README.md`, `LICENSE`) et `dist\UnblockFile-win-x64.zip` ;
- crée ou met à jour `Unblock File.lnk` à la racine. Il cible `dist\UnblockFile-win-x64\UnblockFile.exe`, avec ce même dossier comme répertoire de travail et l'icône de l'EXE. Glisser des fichiers sur ce raccourci les sélectionne.

Le raccourci garde une cible stable ; c'est `publish.ps1` qui remplace l'EXE à cette cible. **À relancer après toute modification à tester** (build + tests + `publish.ps1`), puis vérifier sur `dist\UnblockFile-win-x64\UnblockFile.exe` ou via le raccourci, jamais sur `bin\`.

`dist/` et `*.lnk` ne sont pas versionnés.

## GitHub Release
```powershell
gh release create vX.Y.Z dist\UnblockFile-win-x64.zip --repo Fleiize/win-unblock-file --title "Unblock File vX.Y.Z" --notes "..."
```
Mettre d'abord à jour `<Version>` dans `src/Golabox.UnblockFile/Golabox.UnblockFile.csproj`. Ne jamais réécrire un tag existant.
