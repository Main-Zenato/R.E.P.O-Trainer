# R.E.P.O Trainer

An in-game trainer for **R.E.P.O**, built as a BepInEx 5 plugin. Open it with **Insert** while
playing: god mode, noclip, see enemies and valuables through walls, bring loot to you, money,
upgrades, enemy and team controls, in an English or French menu styled like the truck's terminal.

Made by **Zenato**.

> [Version française plus bas.](#version-française)

## Contents

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
- [Host and guest](#host-and-guest)
- [Troubleshooting](#troubleshooting)
- [Uninstall](#uninstall)
- [Building from source](#building-from-source)
- [Credits and license](#credits-and-license)

## Features

**Player** (works everywhere)
- God mode, infinite stamina, never knocked over, heal to full.
- Noclip: fly through walls. Jump to rise, crouch to sink, sprint to go faster.
- Walk and sprint speed, jump height and gravity multipliers, infinite jump.
- Longer grab beam; stronger carry and throw *(host: carried objects are simulated on the host)*.
- No overcharge: holding an enemy no longer fills the gauge that ends in an explosion.
- Custom field of view.

**Vision** (works everywhere, only you see it)
- Enemies through walls, with a frame, their name, distance and remaining health.
- Valuables with their current price, filtered by a minimum value.
- Teammates, dead players' heads, extraction points, items and weapons.
- Reveal the whole map and put every valuable on it (only your map changes).
- Fullbright and no fog.

**Enemies** *(host)*
- List of active enemies: go to them, freeze them, kill them.
- Kill, send away or freeze every enemy at once.
- Enemy speed from ×0.25 to ×2.
- Invisible to enemies.

**Loot**
- The most valuable objects in the level, with a button to go to each one or bring it to you.
- Bring every valuable in front of you, or drop them all into the extraction point and switch it
  on (both also work as a guest).
- Infinite battery, unbreakable valuables and value ×2 / ×5 *(host)*.

**Spawn** *(host)*
- Any shop item or weapon (59), any valuable from every level (157), any enemy (50), searchable.
- Up to 10 copies per click, laid out in front of you; enemies appear on a nearby waypoint.

**Run** *(host)*
- Add money, or set an exact amount.
- Give yourself any upgrade, one level at a time or all at once.
- Complete the active extraction with whatever is already inside it.
- Break every door, skip to the next level.

**Team**
- Every player with their health: go to them and heal them (works as a guest).
- Team regeneration: everyone recovers a little health every two seconds (works as a guest).
- Bring a player to you, or send everyone to you, the truck or the extraction point *(host)*.
  Dead players travel with their head, ready to be revived at the truck.
- Revive, upgrade, kick, and make the whole team invisible to enemies *(host)*.
- Give the arena winner's crown to anyone *(host)*.

**Cosmetics** (works anywhere)
- Add cosmetic tokens of any rarity, to spend at the lobby's cosmetics machine.
- Unlock every cosmetic at once. R.E.P.O cosmetics are earned in play, none are paid. This is
  written to your cosmetics save and can't be undone from the trainer, so it asks for a second
  click.

**Teleport**
- To where you aim, to the truck, to the extraction point.
- 5 saved spots for the current level.

**Interface**
- A search box that finds any option across every tab.
- English and French, picked automatically from your Windows language or set in Settings.
- Rebindable keys, adjustable menu size, terminal-style confirmations, an optional strip
  listing what's active.

## Installation

### Option A: ready-made package (easiest)

1. Download `RepoTrainer-vX-full.zip` from the [latest release](../../releases/latest).
2. In Steam, right-click **R.E.P.O.** → **Manage** → **Browse local files**.
3. Extract the zip into that folder. You should end up with `winhttp.dll` next to `REPO.exe`,
   and a `BepInEx` folder.
4. Start the game.

This package includes BepInEx 5.4.23.5. If you already use BepInEx 5 for other mods (for example
through a mod manager), take `RepoTrainer-vX-plugin.zip` instead and extract it into the same
game folder, or drop `RepoTrainer.dll` into your profile's `BepInEx\plugins` folder.

### Option B: install script (from source)

Needs the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer. Close the game, then:

```powershell
git clone https://github.com/Main-Zenato/R.E.P.O-Trainer.git
cd R.E.P.O-Trainer
powershell -ExecutionPolicy Bypass -File install.ps1
```

The script downloads BepInEx if the game doesn't have it yet, builds the plugin, and copies it to
`BepInEx\plugins\RepoTrainer\`. If the game isn't in the default Steam library:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -GameDir "D:\SteamLibrary\steamapps\common\REPO"
```

### Checking that it works

After starting the game once, open `BepInEx\LogOutput.log` in the game folder. It should contain:

```
[Info   :R.E.P.O Trainer] R.E.P.O Trainer 1.0.0 by Zenato loaded. Menu key: Insert
```

## Usage

Press **Insert** anywhere, even on the main menu. Most options need a level to be loaded.

| Key | Action |
|---|---|
| Insert | Open or close the menu (Esc also closes it) |
| F6 | Toggle noclip |
| F7 | Teleport to where you aim |
| F8 | Show enemies and valuables through walls |

All four can be changed in **Settings**, or in `BepInEx\config\zen.repo.trainer.cfg`.
On laptops without an Insert key, try **Fn + Insert** or rebind the menu key in that file
(for example `Menu = F9`).

While the menu is open your robot stands still and the mouse is free. Drag the window by its
title to move it, scroll to see a whole tab, and type in the search box to find an option.
The number next to a tab tells you how many of its options are switched on.

## Host and guest

R.E.P.O runs enemies, physics and the run's money on the host's machine. Options that change
those only work when **you host the game** or play solo. When you join someone else's game
they are greyed out with a *host* badge.

Your own robot (god mode, stamina, noclip, speed, jumps, grab reach, field of view), the vision
options, teleports, healing teammates and bringing valuables to you work in any game. As a guest,
"Never knocked over" stands you back up immediately instead of preventing the fall, and valuables
are brought over a few per second so host-side anti-spam mods don't mistake it for flooding.

The game marks itself `[MODDED]` next to its version number whenever any BepInEx plugin is
loaded. That label is cosmetic: it doesn't block lobbies or matchmaking.

Please be considerate when you join other people's games.

## Troubleshooting

**Nothing happens when I press Insert.**
Check `BepInEx\LogOutput.log` (see above). If the file doesn't exist, BepInEx isn't loading:
make sure `winhttp.dll` sits right next to `REPO.exe`. Some antivirus tools quarantine
`winhttp.dll`; restore it and add an exception for the game folder.

**The menu opened once, then stopped working after loading a level.**
Open `BepInEx\config\BepInEx.cfg` and make sure it says `HideManagerGameObject = true`.
The release package and the install script set this for you.

**An option is greyed out.**
It's a host option and you joined someone else's game. See [Host and guest](#host-and-guest).

**Turn everything off at once.**
Settings → **Turn everything off**. Money and upgrades you already gave stay.

## Uninstall

- Trainer only: delete `BepInEx\plugins\RepoTrainer\` from the game folder.
- Everything: also delete `BepInEx\`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version`.

## Building from source

```powershell
dotnet build -c Release
```

The DLL goes to `build\RepoTrainer.dll`. The project references the game's assemblies straight
from its install folder; if your game is elsewhere, pass `-p:GameDir="D:\...\REPO"`.
BepInEx's own assemblies are expected in `.tools\bepinex\pkg\BepInEx\core`, which `install.ps1`
downloads. The game assembly is publicized at build time
([BepInEx.AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer)) so the
trainer reads the game's fields directly instead of through reflection.

`dotnet build -c Release -p:SelfTest=true` adds an automated check: it starts a throwaway solo
run, exercises every feature, writes screenshots and a report to `BepInEx\selftest\`, deletes
the save and quits. Never ship that build.

`package.ps1` builds both release zips into `build\release\`.

Project layout:

```
src/
  Plugin.cs          BepInEx entry point, hotkeys, update loop
  Menu.cs            window, header vitals, search, sidebar, scrolling
  Settings.cs        keys, language, interface options
  L.cs               English/French strings
  Game.cs            accessors for the game's singletons, teleporting
  Features/          one file per tab, with its Harmony patches; Scan.cs caches the level's entities
  UI/                theme, generated textures, widgets, toasts, active-options strip
  SelfTest.cs        automated in-game test (SelfTest builds only)
```

The interface is drawn with Unity IMGUI. Every texture (rounded corners, scanlines, the header
glow) is generated at runtime, so the plugin is a single DLL.

## Credits and license

- Trainer by **Zenato**, released under the [MIT license](LICENSE).
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) loads the plugin and ships with the full release package.
- [HarmonyX](https://github.com/BepInEx/HarmonyX) patches the game's methods.

This project isn't affiliated with or endorsed by semiwork, the developers of R.E.P.O. It
doesn't contain any game files or code.

---

## Version française

Trainer en jeu pour **R.E.P.O**, sous forme de plugin BepInEx 5. Ouvre-le avec **Inser** à tout
moment. Par **Zenato**.

### Installation

1. Télécharge `RepoTrainer-vX-full.zip` depuis la [dernière release](../../releases/latest).
2. Dans Steam : clic droit sur **R.E.P.O.** → **Gérer** → **Parcourir les fichiers locaux**.
3. Extrais le zip dans ce dossier : `winhttp.dll` doit se retrouver à côté de `REPO.exe`.
4. Lance le jeu, entre dans une partie et appuie sur **Inser**.

Si tu utilises déjà BepInEx 5 pour d'autres mods, prends plutôt `RepoTrainer-vX-plugin.zip`.
Depuis les sources : `powershell -ExecutionPolicy Bypass -File install.ps1` (jeu fermé, SDK .NET 8 requis).

### Touches

| Touche | Action |
|---|---|
| Inser | Ouvrir ou fermer le menu (Échap le ferme aussi) |
| F6 | Noclip |
| F7 | Téléportation au viseur |
| F8 | Ennemis et objets de valeur à travers les murs |

Elles se changent dans **Réglages**. Le menu s'affiche en français si Windows est en français ;
la langue se choisit aussi dans Réglages. La barre de recherche en haut trouve n'importe quelle
option dans tous les onglets.

### Fonctions

- **Joueur** : invincible, endurance infinie, jamais renversé, noclip, vitesse, saut, gravité, saut infini, portée et force de saisie, champ de vision.
- **Vision** : ennemis, objets de valeur, joueurs, têtes des morts, extractions et objets à travers les murs, pleine lumière, sans brouillard.
- **Ennemis** : liste, figer, tuer, renvoyer, invisible pour les ennemis.
- **Objets** : objets les plus chers, tout amener devant soi, batterie infinie, objets incassables, valeur ×2 ou ×5.
- **Partie** : argent, améliorations, niveau suivant.
- **Apparition** : n'importe quel objet ou arme (59), objet de valeur (157) ou ennemi (50), jusqu'à 10 à la fois.
- **Équipe** : rejoindre, soigner, amener vers soi, ranimer, améliorer et expulser les joueurs ; tout le monde vers moi, au camion ou à l'extraction ; équipe invisible.
- **Téléportation** : viseur, camion, extraction, 5 emplacements par niveau.
- **Cosmétiques** : jetons de toutes raretés, déblocage de tous les cosmétiques (définitif, double clic).
- **En plus** : pas de surcharge, carte révélée avec les objets de valeur, couronne, portes cassées,
  tout envoyer à l'extraction, valider l'extraction en cours, régénération de l'équipe, vitesse des ennemis.

Les options qui touchent aux ennemis, à la physique (force de portée, lancer, objets incassables,
batterie) et à l'argent sont réservées à l'**hôte** (ou au solo) : elles sont grisées quand tu
rejoins la partie de quelqu'un d'autre. Le jeu affiche « [MODDED] » à côté de sa version dès qu'un
mod BepInEx est chargé ; c'est purement indicatif, ça ne bloque aucun lobby.

### Désinstallation

Supprime `BepInEx\plugins\RepoTrainer\` dans le dossier du jeu. Pour tout retirer, supprime
aussi `BepInEx\`, `winhttp.dll`, `doorstop_config.ini` et `.doorstop_version`.
