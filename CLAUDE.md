@AGENTS.md

## Claude Code
- Lire AGENTS.md, puis PROJECT_STATE.md avant de modifier quoi que ce soit.
- Ne pas refaire d'audit complet ; pas de recherche Web sauf blocage réel.
- Ne pas complexifier : modifier uniquement le sous-système demandé et respecter `docs/DECISIONS.md`.
- Pendant le développement : tests ciblés (`dotnet test --filter`). Validation complète (build Release, tests, `./publish.ps1`) uniquement avant livraison.
