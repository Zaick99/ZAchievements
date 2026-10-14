# ZAchievements

**ZAchievements** is a Steam achievement manager in **one executable**: no installer, no runtime to
install, no loose DLLs. Open it, pick a game, unlock everything in a click.

A fork of [SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager) by gibbed,
rebuilt by [Zaick99](https://github.com/Zaick99) on .NET 10 / WPF with a new interface.

---

## Download

[**→ Get the latest release**](https://github.com/Zaick99/ZAchievements/releases/latest)

One file: `ZAchievements.exe`. Put it anywhere except the Steam folder and run it.

---

## What it does

- **Unlock all & save** — one click unlocks every achievement in a game and commits it to Steam.
- **Unlock every game** — walks the whole filtered library, one game at a time, with live progress
  and a stop button.
- **Attempts protected achievements** — the ones Steam validates server side are tried anyway;
  whatever Steam refuses is counted, not fatal.
- **Statistics editor** — integer and float stats, behind an explicit "I know this can break a game"
  switch.
- **The whole Steam catalogue** — the *Not owned* filter widens the library from the games you own to
  all ~88 000 known apps, titles and capsules included.
- **Free games filter** — *Free* keeps only what the store gives away; a click on one of those opens
  Steam so you can add it to your library first.
- **Search that gives up less** — by title, by initials (`aoe` finds *Age of Empires*), by app id,
  accent- and case-insensitive, several terms at once.

---

## Requirements

Steam must be running and signed in. You must own the game you want to change — Steam checks
ownership on its own servers and refuses writes for anything else.

---

## How to use

1. Run `ZAchievements.exe` with Steam open; your library loads by itself.
2. Click a game to open its achievement manager.
3. **Unlock all & save**, or flip individual switches and **Save changes**.
4. Or use **Unlock every game** in the library to sweep everything currently shown.

---

## Notes

- **Games you don't own.** You can list and open them, but Steam rejects the writes. For free games
  ZAchievements offers to open the Steam client so you can add the licence first — claiming licences
  in bulk needs a logged-in Steam session, which this tool deliberately does not handle.
- **Protected achievements.** Online and anti-cheat protected games validate achievements on Valve's
  servers. ZAchievements tries, reports how many were refused, and moves on.
- **Unlock dates.** Steam records them. Nothing here can undo that.
- This tool changes your Steam achievement data. Use at your own risk.

---

## Caches

Downloaded artwork and catalogues live in `%LOCALAPPDATA%\ZAchievements`. Deleting that folder costs
nothing but a fresh download.

---

## Building

```
dotnet publish src/ZAchievements/ZAchievements.csproj -c Release -o dist
```

Produces a single self-contained `dist/ZAchievements.exe` (x86 — Steam's `steamclient.dll` is 32-bit,
so the process must be too).

---

## Credits

- Original SteamAchievementManager and the Steam client interop by [gibbed](https://github.com/gibbed),
  zlib licensed — see [LICENSE.txt](LICENSE.txt).
- Fork, rewrite and interface by [Zaick99](https://github.com/Zaick99).
