# CLASSIFIED

Your loading screen becomes your faction's secure terminal. BEAR or USEC, read from your profile, with a different unit every launch. A sister mod of [ACCESS GRANTED](https://github.com/JoelHauser/ACCESSGRANTED), built from the same code.

For SPT 4.1.x. Client only.

## What it does

- **The real load.** The fifteen steps EFT runs, from verifying game files to opening the main menu, each timed, with a log of the ones done. The bar is timed against your own previous launches.
- **Your faction.** BEAR and USEC each have six units, every one with its own colours, insignia frame, background, intro, progress bar, ending and wording:
  - BEAR: Command Post, Reconnaissance, Signals, Counterintelligence, Supply, Medical Service.
  - USEC: Tactical Operations Center, S2 Intelligence, S6 Communications, S4 Logistics, Medical, Corporate Command.
- **Insignia made for the mod.** A BEAR shield cut by claw marks over a mountain ridge; a USEC spearhead with swept wings over a chevron.
- **Traffic.** Orders signed by officers of your side, priority intel on the region, the other side's radio intercepts, and a ticker of headlines and the day's readings. Nothing repeats until you have seen the rest.
- **A first launch** signs in unidentified and switches to your faction when your profile loads. After that it remembers your faction.

## With ACCESS GRANTED

Both mods can be installed together. By default they take turns, one launch each: F12 > CLASSIFIED > **With ACCESS GRANTED** can instead always show one of the two. Neither refuses to load.

## Settings (F12)

- **Faction:** Profile, BEAR or USEC.
- **Unit**, **Glitches**, **Layout**, **Background**, **Insignia Frame**, **Intro**, **Progress Bar**, **Ending**.
- **Preview Key** (Ctrl+F6 in the main menu) plays the terminal over a pretend load. **Play Showreel** runs a short reel of both factions, for recording.

## Build

```
scripts\pack.ps1 -SPTPath H:\SPT4.1.X
```

Run it from PowerShell. It builds against the install, checks that the versions agree, and packs `dist\CLASSIFIED-<version>.zip` (the DLL only). Add `-Install` to copy the DLL into the install.
