# PROJECT_STATE — Unblock File

**Version : 0.1.0**. Fonctionnel ; reste la validation visuelle manuelle.

## Terminé
- Sélection d'un fichier, de **plusieurs fichiers** (un lot) ou d'un dossier, par glisser-déposer, dialogues (multi-sélection) ou **arguments de l'EXE / du raccourci**. Un mélange dossier + fichiers ou plusieurs dossiers est refusé avec un message.
- Dossier : option « Inclure les sous-dossiers », désactivée par défaut ; la changer relance l'analyse.
- Analyse asynchrone en lecture seule, avec progression, annulation et durée mesurée ; détection réelle de `Zone.Identifier`.
- Déblocage **direct en C#** par suppression du flux `Zone.Identifier`. **Aucun processus PowerShell** au runtime.
- Statut par fichier ; une erreur n'arrête jamais le lot ; recomptage réel après traitement.
- Avertissement et confirmation pour les extensions actives/exécutables ; information pour les partages réseau (UNC / lecteur mappé).
- Actions de carte : Afficher dans l'Explorateur (`/select`, n'ouvre jamais le fichier), Analyser à nouveau, Effacer.
- Informations avancées (fermées par défaut) : type, fichiers examinés/marqués, erreurs, durée, version, commande PowerShell équivalente (informative), « Copier la commande », « Copier le rapport », « Copier le diagnostic », lien GitHub.
- Diagnostic de session **en mémoire** ; filet anti-crash avec message simple et copie des détails.
- Thème Windows clair/sombre ; `asInvoker` (aucun droit admin) ; Entrée déclenche « Débloquer » ; les boutons icônes ont un nom accessible.
- Icône embarquée dans l'EXE (vérifiée) ; version 0.1.0 / Golabox dans les propriétés de l'EXE.

## Qualité
- Build Release : 0 erreur, 0 avertissement.
- Tests : **42/42** (xUnit, sur de vrais fichiers NTFS temporaires).

## Livraison
- `./publish.ps1` génère `dist\UnblockFile-win-x64\UnblockFile.exe` (single-file compressé, self-contained, environ 61 Mo) et `dist\UnblockFile-win-x64.zip`.
- `Unblock File.lnk` (racine, non versionné), (re)créé par `publish.ps1`, pointe vers `dist\UnblockFile-win-x64\UnblockFile.exe`.
- Démarrage : environ 1 s à chaud. Le premier lancement d'un nouvel EXE a pris environ 30 s (vraisemblablement l'antivirus), quel que soit le format.
- GitHub : https://github.com/Golabox/win-unblock-file, Release `v0.1.0` avec le ZIP (voir docs/DEPLOYMENT.md).

## Limites connues
- Pas de mélange dossier + fichiers, ni de plusieurs dossiers dans une même sélection.
- « Afficher dans l'Explorateur » ne sélectionne que le premier fichier d'un lot.
- Fichier verrouillé par un autre programme : signalé en erreur, sans réessai.
- Glisser depuis un Explorateur lancé en administrateur vers l'app non élevée : bloqué par Windows (UIPI).
- Chemins très longs : gérés via le manifeste `longPathAware`, mais non testés.

## Prochaine action
Validation manuelle finale de l'interface par l'utilisateur.
