<div align="center">

# ⚡ OptiWin

**Optimiseur Windows tout-en-un, gratuit et open source.**
Nettoyage, optimisations, démarrage, réparation : tout en un seul `.exe`, sans installation.

### [⬇️ Télécharger OptiWin.exe](https://github.com/Hazeyy5/OptiWin/releases/latest/download/OptiWin.exe)

Windows 10 / 11 · 64 bits · aucun prérequis

</div>

---

## Installation

1. Cliquez sur **[Télécharger OptiWin.exe](https://github.com/Hazeyy5/OptiWin/releases/latest/download/OptiWin.exe)**.
2. Double-cliquez sur le fichier téléchargé et acceptez la demande d'administrateur.
3. Si Windows affiche « Windows a protégé votre ordinateur » : cliquez sur **Informations complémentaires** puis **Exécuter quand même**.
   *(Ce message apparaît pour toute application récente non signée numériquement.)*

L'application vous prévient automatiquement quand une nouvelle version est disponible.

## Fonctionnalités

| Page | Ce qu'elle fait |
|---|---|
| **Tableau de bord** | Infos système, CPU et RAM en temps réel, état des disques, **optimisation en 1 clic** |
| **Nettoyage** | Fichiers temporaires, cache Windows Update, rapports d'erreurs, miniatures, caches navigateurs, shaders DirectX, corbeille |
| **Optimisations** | 19 réglages (performances, jeux, interface, confidentialité, stockage), chacun activable / désactivable |
| **Démarrage** | Active ou désactive les programmes lancés au démarrage (comme le Gestionnaire des tâches) |
| **Outils** | Cache DNS, réinitialisation réseau, SFC, DISM, optimisation des disques, plan « Performances ultimes », points de restauration |

### Sécurité avant tout

- **Point de restauration** créé automatiquement avant l'optimisation en 1 clic.
- **Tout est réversible** : chaque optimisation revient au réglage Windows d'origine quand on la désactive.
- Les programmes au démarrage ne sont jamais supprimés, seulement désactivés.
- Aucun fichier personnel n'est touché ; Windows Defender, le pare-feu et les mises à jour ne sont jamais désactivés.

## Compiler soi-même

Prérequis : [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish src/OptiWin/OptiWin.csproj -c Release -o publish
```

L'exécutable autonome est généré dans `publish/OptiWin.exe`.

## Publier une nouvelle version

Les versions sont compilées et publiées automatiquement par GitHub Actions :

```bash
git tag v1.1.0
git push origin v1.1.0
```

Quelques minutes plus tard, `OptiWin.exe` est disponible dans l'onglet **Releases**, et le lien de téléchargement ci-dessus pointe automatiquement vers cette nouvelle version.

## Licence

MIT
