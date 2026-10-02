# Décisions

| Sujet | Décision | Raison |
|---|---|---|
| UI | WPF + WPF UI 4.3.0 (Fluent) | Natif Windows, look Windows 11, une seule dépendance |
| Runtime | .NET 10 LTS, `net10.0-windows`, win-x64 | Support long terme |
| Architecture | Code-behind + `Services/` statiques testables | Micro-app : MVVM/DI/CQRS n'apporteraient rien |
| Distribution | Portable self-contained, single-file compressé, sans installeur | Copier-lancer sur un poste pro sans .NET ni droits admin |
| Droits | `asInvoker`, jamais d'élévation | Postes d'entreprise sans droits admin |
| Données | Pas de DB, de compte, de télémétrie, d'auto-update ni de réseau | Hors périmètre ; confiance et simplicité |
| Logs | Diagnostic de session en mémoire, copié à la demande | Diagnostiquer sans écrire sur disque |
| Shell | Pas de menu contextuel ni de clé de registre ; arguments CLI + raccourci | « Envoyer vers » ou un glisser sur le raccourci suffisent, sans toucher au registre |
| Sélection | Un dossier OU plusieurs fichiers, jamais les deux | Garde l'UI et la logique simples |
| Trimming | Désactivé | Risque de casser WPF / WPF UI |

## PowerShell retiré du runtime (décision clé)
La première implémentation lançait `powershell.exe -EncodedCommand` pour exécuter `Unblock-File`. Avast et les EDR déclenchent une **détection heuristique** sur ce type de lancement, ce qui est inacceptable en entreprise. PowerShell a donc été **totalement retiré du runtime** : `Zone.Identifier` est détecté et supprimé directement en C#, comme le fait `Unblock-File` en interne. La commande PowerShell affichée dans « Informations avancées » est **uniquement informative** (copier-coller).

## Démarrage
Mesuré à chaud, environ 1 s, avec un écart négligeable entre single-file compressé, single-file non compressé et dossier. Le premier lancement d'un EXE nouvellement copié a pris 27 à 34 s dans tous les formats, vraisemblablement à cause de l'analyse antivirus. Le single-file compressé (61 Mo) est donc retenu.
