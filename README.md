# The Valley — Releases & Updates

> DOC-2026-10-057 · V1.0 · PUBLIC · Johan Wilhelm van Antwerp · Ionity (Pty) Ltd — AEDI · Policy 986 AED
> Web: [ionity.co.za](https://www.ionity.co.za) · Ecosystem: [ionity.today](https://www.ionity.today) · (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd — All rights reserved — TM2

This repository is the **release channel** of *The Valley*, the Unreal Engine 5 MMORPG by Ionity. Every player's runner (`The Valley.exe`) reads the release record here (through Firebase first) and updates the game in place. Players never re-download the 28 GB client for a new version. They get a small patch with only the files that changed.

## For players

1. Download the client once from Google Drive. You need every `TheValley_v4.13_Client.zip.00N` part and `TheValley_Setup.exe` in one folder.
2. Run **TheValley_Setup.exe**. It joins the parts, installs the game, writes `version.txt` and starts the runner.
3. From then on, open **The Valley** (the runner). When a new version is out, it shows **UPDATE TO x.y.z**. Press it and the runner does the rest: it downloads, verifies, installs and restarts.

- If a World Server runs a newer version than yours, the runner asks you to update before you join.
- Below the minimum version (`minVersion`), the runner will not start the game until it is updated.
- Nothing is replaced until a patch is fully downloaded and its SHA-256 matches. A broken download is simply fetched again.

## How it works

```
 build (UE 5.8, Johan's PC)                     players
 ─────────────────────────                      ───────
 Release\TheValley_v4.13_Gathering   ──►  tools/valley_release.py
   hash every file (SHA-256)                │
   diff with the last manifest              ├─► patch zip ──► Google Drive (public link)          ─┐
   pack changed files + __patch__.json      ├─► patch zip ──► GitHub Releases (v<version>)        ├─► runner downloads
                                            ├─► latest.json + manifest ──► this repo (git push)  ─┤   (first mirror that works)
                                            └─► Firestore  release/client  (field "latest")      ─┘
 GitHub Action: validates every push (schema, patch chains, mirrors answer) and, with the
 FIREBASE_SA secret, re-syncs Firestore from latest.json → GitHub is the source of truth.
```

The runner reads Firestore `release/client` first, because it is fast and already used for the server list. It falls back to `https://raw.githubusercontent.com/Ionity-Global/the-valley-releases/main/latest.json`.

### latest.json

```json
{
  "schema": 1, "product": "The Valley", "version": "4.14.0", "minVersion": "4.13.0",
  "title": "Field of Honour", "notes": "...", "released": "2026-10-28T15:00:00Z",
  "patches": [
    { "from": "4.13.0", "to": "4.14.0", "file": "TheValley_patch_4.13.0_to_4.14.0.zip",
      "size": 123456, "sha256": "…64 hex…",
      "urls": ["https://drive.usercontent.google.com/download?id=…&export=download&confirm=t",
               "https://github.com/Ionity-Global/the-valley-releases/releases/download/v4.14.0/TheValley_patch_4.13.0_to_4.14.0.zip"] }
  ],
  "full": { "version": "4.13.0", "setup": "https://drive.google.com/open?id=…", "folder": "https://drive.google.com/open?id=…" }
}
```

Patches chain: a 4.13.0 client goes 4.13.0 → 4.14.0 → 4.15.0 … one patch after another. If a version has no chain, the runner opens the full download instead.

A patch zip holds the changed files at their paths relative to the install folder (the folder with `The Valley.exe`), plus `__patch__.json`:

```json
{ "from": "4.13.0", "to": "4.14.0", "deleted": ["old/file/to/remove.ext"] }
```

## Publishing a version (Johan)

```powershell
cd E:\TheValley_UE5\the-valley-releases\tools
# first patch only: compare against the 4.13 client zip on Drive
python valley_release.py --version 4.14.0 --title "Field of Honour" --notes ..\release-notes\draft.md `
       --baseline-zip "E:\TheValley_UE5\Release\Drive_Upload\v4.13\TheValley_v4.13_Client.zip.001"
# every later version: the tool diffs against the previous manifest by itself
python valley_release.py --version 4.15.0 --title "..." --notes notes.md [--min 4.14.0]
```

The tool needs `rclone` (remote `ionity-digital:`), `gh` (logged in, `repo` scope), `gcloud` (an account with Firestore write on `the-valley-mmo`) and Python 3.10+. Use `--dry` to preview, and `--no-drive`, `--no-github` or `--no-firebase` to skip a mirror.

### CI secret (optional)

`FIREBASE_SA`: a service-account JSON of `the-valley-mmo` with the *Cloud Datastore User* role. With it, every push of `latest.json` is mirrored to Firestore by the Action. Without it, the local tool writes Firestore itself.

## Files

```
the-valley-releases/
├── README.md                    this file
├── CHANGELOG.md                 the game's changelog (copied by the release tool)
├── latest.json                  the release record the runners read
├── manifests/<version>.json     every file of that version: size + SHA-256
├── release-notes/<version>.md   notes shown on the GitHub release
├── client/                      runner + updater + setup sources (C#, .NET Framework 4)
│   ├── ValleyRunner.cs
│   ├── ValleyUpdater.cs
│   └── ValleySetup.cs
├── tools/
│   ├── valley_release.py        build + publish a patch
│   ├── validate_release.py      CI check of latest.json (and --probe of every mirror)
│   └── sync_firestore.py        CI mirror of latest.json to Firestore
└── .github/workflows/release.yml
```

## Firestore rule

```
match /release/{doc} { allow read: if true; allow write: if false; }   // admins write with OAuth / service accounts
```

---
Ionity (Pty) Ltd · IONITY GLOBAL (Pty) Ltd 2026/276325/07 · Centurion, South Africa · info@ionity.today
**BUILDING TOMORROW, TODAY**
