# The Binding of a Robot Named Fight

**An unofficial overhaul mod for [A Robot Named Fight](https://store.steampowered.com/app/603530/A_Robot_Named_Fight/), with a lot of inspiration from The Binding of Isaac.**

> [!WARNING]
> **Disclaimer: this mod kind of sucks right now.** It's an early work in progress, and it's honestly rough. Expect:
> - **Bugs.** Most of the new features compile and boot, but they've barely been played.
> - **Bad balance.** The characters, synergy costs and weapon scaling are first guesses.
> - **Missing or broken visual effects.** Several energy weapons don't show their effects properly yet (the flamethrower aura, for example).
> - **Placeholder art and a debug-looking character select screen.**
> - **Save risk.** Starting a run with the mod can change your save. Back up your save folder before you play.
>
> It's provided as is, with no warranty. Use it at your own risk. Bug reports and ideas are welcome in [Issues](../../issues).

The mod adds playable characters, each with a real strength and a real flaw, and makes items stack the way Isaac's do: every energy weapon now scales with your stats and picks up your shot upgrades.

> **This is a mod, not the official game.** A Robot Named Fight © 2017 Morningstar Game Studio Incorporated (Matt Bitner). This project is built on the publicly released Unity source and the community [OpenARNF](https://github.com/OpenARNF/OpenARNF) fork, and follows the [A Robot Named Fight license](License.md): non-commercial only, the copyright notice kept, and marked clearly as a mod. If you enjoy it, [buy the real game](https://store.steampowered.com/app/603530/A_Robot_Named_Fight/).
>
> *The Binding of Isaac* is by Edmund McMillen and Nicalis. This project is not affiliated with or endorsed by them, Morningstar Game Studio, or Nintendo.

## Features

### Character select
**New Game** opens a character select before the run is created. Every robot has a strength and a strong flaw, so none of them is simply better than Fight.

| Robot | Strength | Flaw |
|---|---|---|
| **Fight** | The original. No weaknesses. | No strengths. |
| **Volt** | Laser Beam is his default attack. +8 energy, very fast recharge, electric damage, synergy surcharge halved. | No normal bolt at all. The beam is short (it reaches further as shot size grows), and when energy runs dry he can't attack. |
| **Wraith** | ×1.35 damage, much faster movement, piercing bolts, Cloaking Device. | No health bar, only **3 hits per room**. Any hit costs one, and they refill on entering a new room. Healing does nothing. |
| **Swarmlord** | A Lilith-style summoner. His **Brood** familiar fires every shot (bolts, charge shots, every energy weapon) with all his upgrades, and all his orbs scale with his stats. | He can't shoot from his body. The Brood trails behind him, and its shots get blocked when it drifts into walls. |
| **Scrapper** | Scrap is power. His bolt grows with every scrap he carries, colored scrap powers his stats, kills often drop scrap, and he has a long pickup range. | Scrap is also his health. Every hit knocks scrap out of him, which also shrinks his bolt, and every shop purchase spends his life. |
| **Hunter** | Fast shots, faster movement, Rocket Launcher from the start. | Energy never recharges on its own. It only comes from pickups. |

### Energy weapon synergies
All 14 energy weapons now work with the rest of your build:

- **Stats:** damage, fire rate, shot speed and shot size affect every weapon. Held weapons (beams, flamethrower, lightning, boomerang) gain damage per second from fire rate, and their range and size grow with shot speed and shot size.
- **Shot upgrades:** status effects (burn, poison, slow), elements, pierce, bounce, phasing, homing, growth, Explosive and Fragment carry over to every weapon. Weapons that don't fire bullets turn Explosive and Fragment into on-hit blasts and shards.
- **Each weapon uses multishot its own way:** Rail Gun fires a fan of rails, Flamethrower adds extra flame streams, Lightning Gun forks to nearby enemies, Phaserang throws a fan of boomerangs, and so on.
- **Item hooks:** Big Bolt makes energy weapons huge without slowing their fire rate. Bolt Helm shots take on the equipped weapon's element. Health Booster and Scrap Booster bonuses stay live while you fire.
- **Synergy cost:** each synergy multiplies the weapon's energy cost by its strength: Multishot ×2, Explosive ×1.5, Fragment ×1.4, Pierce ×1.3, and so on, down to ×1.15. Stat upgrades are free.

### Testing tools
These are available in the Unity editor and in development builds:

| Key | Action |
|---|---|
| F7 | Warp into a large empty test arena (press again to go back) |
| F10 | Open the spawn menu: put any item on a pedestal or equip it directly, spawn DPS dummies, switch characters, clear your inventory |
| F12 | Spawn a DPS dummy that shows rolling DPS, peak DPS, total damage and biggest hit |
| F9 / F11 | Give mod items / refill energy |

## Building

1. Install Unity **2022.3.36f1**. Unity Hub works.
2. Clone this repository and open it as a Unity project.
3. To play in the editor, press Play. The game always boots from the first scene.
4. To build, use **File → Build Settings → Build**. Then start a **New Game** to reach the character select.

A controller or keyboard works for the menus.

### Optional: the Samus sprite
Hunter is designed to use a Samus sprite, but this repository **does not include Nintendo's artwork**. Without it, Hunter uses Fight's sprite. If you own a sheet, `Tools/samus-importer/` converts it into `Assets/Resources/Samus/`. See that folder's README.

## Project layout (mod code)
| Path | What it is |
|---|---|
| `Assets/Scripts/ModContent/` | Characters, the character select, Swarmlord's Brood and minion links, the DPS dummy, pedestals and the test arena |
| `Assets/Scripts/Player/WeaponSynergy.cs` | Shared synergy layer for energy weapons |
| `Assets/ModContentTestKeys.cs` | Test bench hotkeys and the spawn menu |
| `Tools/` | Asset and art generators and a save cleanup script |

Changes to the game's own scripts are tagged with `[MODCONTENT]` comments, so `grep -r MODCONTENT Assets` finds every hook.

## Credits
- **A Robot Named Fight** by Matt Bitner / Morningstar Game Studio. The original game, its art and its source.
- **[OpenARNF](https://github.com/OpenARNF/OpenARNF)** community fork: Unity 2022 upgrade, InputSystem controller support and fixes.
- **The Binding of Isaac** by Edmund McMillen / Nicalis. Inspiration for the characters and item synergies.
- Middleware included with the original source, used with the original author's permission: partial code from Rewired (Guavaman) and Super Tilemap Editor (Creative Spore).

**AI disclosure:** this mod's code and its small pixel-art additions (pedestal, DPS dummy, arena tiles, Brood orb) were made with Anthropic's Claude (Claude Code), directed and playtested by the mod's author.

## License
The game source and assets follow the [A Robot Named Fight license](License.md): non-commercial use only, keep the copyright notice, and mark modified versions as mods. The original README is preserved in [README-OpenARNF.md](README-OpenARNF.md).
