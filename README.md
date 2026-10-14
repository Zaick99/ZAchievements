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
- **Free games filter** — *Free* keeps only what the store gives away.
- **Claim free games** — adds Steam's free games to your account in bulk, so their achievements
  become yours to unlock. See below for how it works and what it costs.
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

## Claiming free games

Steam exposes no client API for adding a licence, so **Claim free games** does it the only honest
way: it opens Steam's own store in an embedded browser, you sign in there, and the requests then run
*inside that page* — the same call the store's own **Play Game** button makes. ZAchievements never
sees your password, your Steam Guard code or your session token; the browser profile lives in
`%LOCALAPPDATA%\ZAchievements\webview` and you can wipe it by deleting that folder.

It takes two steps per game, because that endpoint wants a **package** id and not an app id, and the
free package is only named on the app page a signed-in visitor sees — as the argument of the store's
own `AddFreeLicense(…)` button. The cart's package is deliberately ignored: that one is the paid
edition, and asking for it free is what earns a 403.

What to expect:

- **It is slow on purpose.** One request every 1.2 s, and Steam caps free activations anyway. When
  the cap hits, the run pauses 90 s and carries on; if Steam keeps refusing, it stops and tells you
  to come back later.
- **It resumes.** Every app already offered is remembered in `claimed.txt`, so pressing *Claim them*
  again picks up where it stopped. *Start over* forgets that list.
- **Games you already own are skipped** before the first request goes out, and games whose page
  offers nothing free are counted and passed over.
- **Steam's client lags.** A licence lands on the account immediately, but the running Steam client
  can take a minute to notice. The library count catches up on the next rescan.
- **Your library will grow a lot.** All of Steam's free games is tens of thousands of entries, and
  removing a licence afterwards is a one-at-a-time job on Steam's support site. Use the *Free* filter
  first if you only want a slice of it.

## Notes

- **Games you don't own.** You can list and open them, but Steam rejects every write: ownership is
  checked on Valve's servers. Claim the licence first.
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
