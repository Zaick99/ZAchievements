# ZAchievements

**ZAchievements** is a Steam achievement manager built for speed: unlock every achievement you want, in every game you own, in seconds — no fuss.

A personal fork of the original [SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager) by gibbed, redesigned and extended by [Zaick99](https://github.com/Zaick99).

---

## Features

- **Unlock All & Save** — one click to unlock every achievement in a game and commit it to Steam immediately
- **Bulk unlock across all games** — the picker lets you process your entire library in a single pass
- **Attempts protected achievements** — ZAchievements tries to unlock even "protected" achievements; the ones that truly can't be touched are skipped silently
- **Dark themed UI** — clean dark background with green accents, easy on the eyes
- **Standalone EXEs** — no installer, no loose DLLs. Two files, drop and run

---

## Download

[**→ Get the latest release**](https://github.com/Zaick99/ZAchievements/releases/latest)

| File | Description |
|------|-------------|
| `ZAchievements.exe` | Game picker — start here |
| `ZAchievements.Game.exe` | Achievement manager — launched automatically per game |

---

## Requirements

- Steam must be running and you must be logged in
- You must own the game you want to modify

---

## How to use

1. Run `ZAchievements.exe` with Steam open — your library loads automatically
2. Double-click any game to open its achievement manager
3. Click **Unlock All & Save** to unlock every achievement in one shot
4. Or click **Unlock All Games** in the picker to process your entire library at once

---

## Notes

- Some achievements use server-side validation (common in online/VAC-protected games). ZAchievements will attempt them anyway — those that Steam rejects are silently skipped.
- This tool modifies your Steam achievement data. Use at your own risk.

---

## Credits

- Original SteamAchievementManager by [gibbed](https://github.com/gibbed)
- Icons from the [Fugue Icons](https://p.yusukekamiyamane.com/) set
- Fork and modifications by [Zaick99](https://github.com/Zaick99)
