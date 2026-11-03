# CLASSIFIED

Your loading screen becomes your faction's secure terminal. BEAR or USEC, read from your profile, with a different unit every launch. A sister mod of [ACCESS GRANTED](https://github.com/JoelHauser/ACCESSGRANTED), built from the same code.

For SPT 4.1.x. Client only.

## What it does

- **The real load.** The fifteen steps EFT runs, from verifying game files to opening the main menu, each timed, with a log of the ones done. The bar is timed against your own previous launches.
- **Your faction's own look.** Nothing borrowed from TerraGroup's terminal:
  - BEAR is Soviet hardware and speaks Russian: a staff map, aerial film or typed orders behind it; a PSO-1 reticle, a panel gauge or a riveted plate round the insignia; panel lamps or punched tape for the bar; a riveted border with a secrecy stamp; it opens with the stamp coming down.
  - USEC is a modern tactical display: an MGRS map, satellite imagery or an operation order behind it; a holographic HUD, a mil-dot scope or a target lock; a ruler or clean segments; a chamfered frame under classification banners; it opens with a secure boot.
- **Six units each**, every one with its own colours, wording and choice of the parts above:
  - BEAR: Command Post, Reconnaissance, Signals, Counterintelligence, Supply, Medical Service.
  - USEC: Tactical Operations Center, S2 Intelligence, S6 Communications, S4 Logistics, Medical, Corporate Command.
- **The debrief.** Beside the terminal, the unit's kit turns on a table as a hologram, one piece after another: one of its weapons, often with an optic, light, foregrip or suppressor fitted, then three pieces of its gear (helmets, armour, night vision, headsets, packs, medical kit, rations), with their names and figures; a weapon's parts are named as it comes side on. Next to it, one of several panels: the operations map (an assault, a drone over the target, direction finding, a convoy, a casualty evacuation or a surveillance track, in each side's own map conventions), a wanted dossier, a radio intercept, reconnaissance imagery being enhanced, the unit's readiness, or your own tasks.
- **Your tasks.** The terminal reads the tasks your profile has under way and now and then passes on intel about one of them.
- **The factions' own insignia.** The BEAR and USEC logos, flying together piece by piece as the terminal comes on.
- **Traffic.** Orders signed by officers of your side, priority intel on the region, the other side's radio intercepts, and a ticker of headlines and the day's readings. Nothing repeats until you have seen the rest.
- **A first launch** shows either side and switches to yours when your profile loads. After that it remembers your faction.
- **Raid loads too, if you like.** Switch on F12 > Raid Loading > **Show While Loading Raids**: the game's real deploy steps, the countdown, the traffic coming in as it loads, Esc to cancel while the game allows it. It stands aside for Deploy Screen Overhaul by default, and takes turns with ACCESS GRANTED's raid terminal.
- **The way back, too.** Once the after-action report is done, the terminal you signed in to covers the raid unloading and the menu loading (F12 > Raid Loading > **Show After Raids**, on by default).

## With ACCESS GRANTED

Both mods can be installed together. By default they take turns, one launch each: F12 > CLASSIFIED > **With ACCESS GRANTED** can instead always show one of the two. Neither refuses to load.

## Settings (F12)

- **Faction:** Profile, BEAR or USEC.
- **BEAR Language:** Russian, or English to read everything BEAR says in English.
- **Unit**, **Glitches**.

With **Advanced settings** ticked:

- **Layout**, **Background**, **Insignia Frame**, **Intro**, **Progress Bar**, **Ending** pin any part instead of letting the unit choose.
- **Preview Key** (Ctrl+F6 in the main menu) plays the terminal over a pretend load, with every key listed along the bottom: change unit, layout or any part, hold or slow the load, step through it, jump to the ending, call up a notice or a glitch, and hide parts of the screen down to the logo alone. **Preview Length** sets how long its load takes.
- **Play Showreel** runs every unit in turn (or one faction's, with **Showreel Units**), each for **Showreel Shot Length** seconds. Nothing but the terminal shows, for recording; Left/Right skip units, Space holds one, H shows what is on screen.

## Build

```
scripts\pack.ps1 -SPTPath H:\SPT4.1.X
```

The pictures the DLL carries are made by scripts (numpy, scipy, pillow; the weapons also UnityPy), and only need running again after changing what they make:

- `python scripts/make_insignia.py`: the insignia, from the logos in `art/`.
- `python scripts/make_weapons.py H:\SPT4.1.X`: the debrief's weapons and equipment, assembled from the game's own models by SPT's default presets, with their names and figures from SPT's database.
- `python scripts/make_backdrops.py`: the six faction backdrops.

Run the pack script from PowerShell. It builds against the install, checks that the versions agree, and packs `dist\CLASSIFIED-<version>.zip` (the DLL only). Add `-Install` to copy the DLL into the install.
