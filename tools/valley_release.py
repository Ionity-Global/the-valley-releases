#!/usr/bin/env python3
# =====================================================================================================================
# THE VALLEY - RELEASE TOOL  (valley_release.py)
# Document: GME-2026-10-043 | Version 1.0 | 28 Oct 2026 (SAST)
# Author: Johan Wilhelm van Antwerp | Ionity (Pty) Ltd - formerly Antwerp Designs - AEDI
# Governance: Policy 986 AED | License: AED 900 | (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd - All rights reserved - TM2
# Web: https://www.ionity.today | Ref: https://www.ionity.co.za | Building Tomorrow, Today.
#
# Publishes a new client version so every runner ("The Valley.exe") updates itself:
#   1. writes version.txt into the client folder, hashes every file (SHA-256 + CRC-32, cached by size and time)
#   2. compares with the previous version's manifest (or, for the first patch, with the 4.13 client zip on Drive)
#   3. packs the changed files + __patch__.json (deleted files) into TheValley_patch_<from>_to_<to>.zip
#   4. uploads it to Google Drive (rclone, public link) and to GitHub Releases (gh, when under 2 GB)
#   5. writes latest.json (+ manifest + release notes + CHANGELOG) into the the-valley-releases repo, commits and pushes
#   6. writes the same record to Firestore  release/client  (gcloud token) - the runner reads Firebase first, GitHub second
# The GitHub Action in the repo validates every push and (with the FIREBASE_SA secret) re-syncs Firestore.
#
# Usage (Windows, from anywhere):
#   python valley_release.py --version 4.14.0 --title "Field of Honour" --notes notes.md [--min 4.13.0]
#          [--baseline-zip "E:\...\TheValley_v4.13_Client.zip.001"] [--dry] [--no-drive] [--no-github] [--no-firebase]
# =====================================================================================================================
import argparse, datetime, hashlib, io, json, os, re, subprocess, sys, zipfile, zlib

CLIENT = r"E:\TheValley_UE5\Release\TheValley_v4.13_Gathering"
STATE = r"E:\TheValley_UE5\Release\updates"
REPO = r"E:\TheValley_UE5\the-valley-releases"
REPO_SLUG = "Ionity-Global/the-valley-releases"
DRIVE = "ionity-digital:The Valley - Downloads/updates"
PROJECT = "the-valley-mmo"
BASE_VERSION = "4.13.0"
CHANGELOG = r"E:\TheValley_UE5\TheValley\Docs\CHANGELOG.md"
FULL = {"version": "4.13.0",
        "setup": "https://drive.google.com/open?id=1M3gTJeHkbuXCyHQpR2Re0N-qQjP8uqnu",
        "folder": "https://drive.google.com/open?id=1pWhlsx7gwKH53fpulbngL6OW34IVnQ1U"}
SKIP_DIRS = {"saved", "_update", "intermediate", "deriveddatacache", ".git"}
SKIP_EXT = (".log", ".old", ".part", ".tmp", ".dmp", ".pdb")   # debug symbols stay out of patches
AUTHOR = ("Johan Wilhelm van Antwerp", "johan@ionity.today")
TRAILER = "\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\nClaude-Session: https://claude.ai/code/session_01SxRSyKhQNJ4N1dbcMZbyHG"

def log(*a): print("[valley-release]", *a, flush=True)

def vkey(v):
    out = []
    for p in re.split(r"[.\-]", (v or "").strip().lstrip("vV"))[:4]:
        m = re.match(r"\d+", p); out.append(int(m.group()) if m else 0)
    return tuple(out + [0] * (4 - len(out)))

def run(cmd, cwd=None, check=True, capture=True):
    r = subprocess.run(cmd, cwd=cwd, capture_output=capture, text=True, shell=isinstance(cmd, str))
    if check and r.returncode != 0: raise SystemExit("FAILED: %s\n%s\n%s" % (cmd, r.stdout, r.stderr))
    return (r.stdout or "").strip()

def file_sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for b in iter(lambda: fh.read(8 << 20), b""): h.update(b)
    return h.hexdigest()

# ---------------------------------------------------------------------------------------------------------------- scan
def scan(root, cache_path):
    cache = json.load(open(cache_path, encoding="utf-8")) if os.path.exists(cache_path) else {}
    out, n, hashed = {}, 0, 0
    for d, dirs, files in os.walk(root):
        dirs[:] = [x for x in dirs if x.lower() not in SKIP_DIRS]
        for f in files:
            if f.lower().endswith(SKIP_EXT): continue
            full = os.path.join(d, f); rel = os.path.relpath(full, root).replace("\\", "/")
            st = os.stat(full); key = "%d:%d" % (st.st_size, st.st_mtime_ns)
            c = cache.get(rel)
            if c and c[0] == key: sha, crc = c[1], c[2]
            else:
                h = hashlib.sha256(); crc = 0
                with open(full, "rb") as fh:
                    while True:
                        b = fh.read(8 << 20)
                        if not b: break
                        h.update(b); crc = zlib.crc32(b, crc)
                sha = h.hexdigest(); cache[rel] = [key, sha, crc]; hashed += st.st_size
            out[rel] = {"size": st.st_size, "sha256": sha, "crc32": crc}
            n += 1
            if n % 500 == 0: log("scanned %d files (%.1f GB hashed)" % (n, hashed / 2**30))
    os.makedirs(os.path.dirname(cache_path), exist_ok=True)
    json.dump(cache, open(cache_path, "w", encoding="utf-8"))
    log("scanned %d files, %.1f GB newly hashed" % (n, hashed / 2**30))
    return out

class MultiPart(io.RawIOBase):
    """The Drive download's .001 .002 ... parts read as one zip (only the central directory is touched)."""
    def __init__(self, first):
        base = first[:-4]; self.parts = []; i = 1
        while os.path.exists("%s.%03d" % (base, i)): p = "%s.%03d" % (base, i); self.parts.append((p, os.path.getsize(p))); i += 1
        self.size = sum(s for _, s in self.parts); self.pos = 0
    def readable(self): return True
    def seekable(self): return True
    def tell(self): return self.pos
    def seek(self, off, whence=0):
        self.pos = off if whence == 0 else self.pos + off if whence == 1 else self.size + off; return self.pos
    def readinto(self, b):
        want = len(b); got = 0; at = 0
        for p, s in self.parts:
            if self.pos < at + s and want > 0:
                with open(p, "rb") as fh:
                    fh.seek(self.pos - at); chunk = fh.read(min(want, at + s - self.pos))
                b[got:got + len(chunk)] = chunk; got += len(chunk); want -= len(chunk); self.pos += len(chunk)
            at += s
        return got

def zip_manifest(first_part):
    z = zipfile.ZipFile(MultiPart(first_part))
    names = [i for i in z.infolist() if not i.is_dir()]
    exe = sorted([i.filename for i in names if i.filename.replace("\\", "/").endswith("The Valley.exe")], key=len)
    prefix = exe[0][: -len("The Valley.exe")] if exe else ""
    out = {}
    for i in names:
        n = i.filename.replace("\\", "/")
        if prefix and not n.startswith(prefix): continue
        out[n[len(prefix):]] = {"size": i.file_size, "crc32": i.CRC}
    log("baseline zip: %d files (prefix '%s')" % (len(out), prefix))
    return out

# ---------------------------------------------------------------------------------------------------------------- main
def main():
    a = argparse.ArgumentParser(description="Publish a The Valley client update")
    a.add_argument("--version", required=True); a.add_argument("--title", default=""); a.add_argument("--notes", default="")
    a.add_argument("--min", default=""); a.add_argument("--prev", default=""); a.add_argument("--baseline-zip", default="")
    a.add_argument("--client", default=CLIENT); a.add_argument("--dry", action="store_true")
    a.add_argument("--no-drive", action="store_true"); a.add_argument("--no-github", action="store_true"); a.add_argument("--no-firebase", action="store_true")
    o = a.parse_args()
    ver = o.version.strip().lstrip("vV")
    os.makedirs(os.path.join(STATE, "manifests"), exist_ok=True); os.makedirs(os.path.join(STATE, "patches"), exist_ok=True)

    # 1. the version stamp and the scan
    if not o.dry: open(os.path.join(o.client, "version.txt"), "w").write(ver)
    cur = scan(o.client, os.path.join(STATE, "hashcache.json"))
    json.dump({"version": ver, "files": cur}, open(os.path.join(STATE, "manifests", ver + ".json"), "w"), indent=0)

    # 2. what changed since the previous version
    prev_ver = o.prev
    if not prev_ver:
        older = sorted([f[:-5] for f in os.listdir(os.path.join(STATE, "manifests")) if f.endswith(".json") and vkey(f[:-5]) < vkey(ver)], key=vkey)
        prev_ver = older[-1] if older else BASE_VERSION
    mp = os.path.join(STATE, "manifests", prev_ver + ".json")
    if os.path.exists(mp): prev = json.load(open(mp))["files"]
    elif o.baseline_zip: prev = zip_manifest(o.baseline_zip)
    else: raise SystemExit("No manifest for %s - pass --baseline-zip <...zip.001> for the first patch" % prev_ver)
    changed = []
    for rel, f in cur.items():
        p = prev.get(rel)
        if p is None or p["size"] != f["size"] or ("sha256" in p and p["sha256"] != f["sha256"]) or ("sha256" not in p and p.get("crc32") != f["crc32"]):
            changed.append(rel)
    deleted = sorted(r for r in prev if r not in cur and not r.lower().endswith(SKIP_EXT) and r.split("/")[0].lower() not in SKIP_DIRS)
    log("%s -> %s: %d changed/new, %d deleted" % (prev_ver, ver, len(changed), len(deleted)))
    for r in sorted(changed)[:40]: log("   +", r)
    if not changed and not deleted: raise SystemExit("Nothing changed - no patch made.")

    # 3. the patch
    name = "TheValley_patch_%s_to_%s.zip" % (prev_ver, ver)
    zpath = os.path.join(STATE, "patches", name)
    if not o.dry:
        with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as z:
            z.writestr("__patch__.json", json.dumps({"from": prev_ver, "to": ver, "deleted": deleted, "made": datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None).isoformat() + "Z"}, indent=1))
            for rel in sorted(changed): z.write(os.path.join(o.client, rel.replace("/", os.sep)), rel)
    size = os.path.getsize(zpath) if os.path.exists(zpath) else 0
    sha = file_sha(zpath) if size else ""
    log("patch %s: %.1f MB, sha256 %s" % (name, size / 2**20, sha))

    # 4. the mirrors
    urls = []
    if not o.dry and not o.no_drive:
        run(["rclone", "copyto", zpath, DRIVE + "/" + name])
        link = run(["rclone", "link", DRIVE + "/" + name])
        m = re.search(r"(?:id=|/d/)([\w-]{20,})", link)
        if not m: raise SystemExit("rclone link gave no Drive id: " + link)
        urls.append("https://drive.usercontent.google.com/download?id=%s&export=download&confirm=t" % m.group(1))
        log("Drive:", urls[-1])
    gh_ok = not o.no_github and size < 1900 * 2**20
    if gh_ok: urls.append("https://github.com/%s/releases/download/v%s/%s" % (REPO_SLUG, ver, name))

    # 5. latest.json in the repo
    lp = os.path.join(REPO, "latest.json")
    latest = json.load(open(lp, encoding="utf-8")) if os.path.exists(lp) else {}
    notes = open(o.notes, encoding="utf-8").read() if o.notes and os.path.exists(o.notes) else o.title
    patches = [p for p in latest.get("patches", []) if not (p.get("from") == prev_ver and p.get("to") == ver)]
    patches.append({"from": prev_ver, "to": ver, "file": name, "size": size, "sha256": sha, "urls": urls})
    latest.update({"schema": 1, "product": "The Valley", "publisher": "Ionity (Pty) Ltd", "version": ver,
                   "minVersion": o.min or latest.get("minVersion") or BASE_VERSION, "title": o.title or latest.get("title", ""),
                   "notes": notes[:4000], "released": datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None).replace(microsecond=0).isoformat() + "Z",
                   "patches": sorted(patches, key=lambda p: vkey(p["to"])), "full": latest.get("full", FULL)})
    if o.dry: log("dry run - latest.json would be:\n" + json.dumps(latest, indent=2)); return
    os.makedirs(os.path.join(REPO, "manifests"), exist_ok=True); os.makedirs(os.path.join(REPO, "release-notes"), exist_ok=True)
    json.dump(latest, open(lp, "w", encoding="utf-8"), indent=2)
    json.dump({"version": ver, "files": {k: {"size": v["size"], "sha256": v["sha256"]} for k, v in cur.items()}}, open(os.path.join(REPO, "manifests", ver + ".json"), "w"), indent=0)
    open(os.path.join(REPO, "release-notes", ver + ".md"), "w", encoding="utf-8").write("# The Valley %s%s\n\n%s\n" % (ver, (" - " + o.title) if o.title else "", notes))
    if os.path.exists(CHANGELOG):
        import shutil; shutil.copyfile(CHANGELOG, os.path.join(REPO, "CHANGELOG.md"))
    env = dict(os.environ, GIT_AUTHOR_NAME=AUTHOR[0], GIT_AUTHOR_EMAIL=AUTHOR[1], GIT_COMMITTER_NAME=AUTHOR[0], GIT_COMMITTER_EMAIL=AUTHOR[1])
    subprocess.run(["git", "add", "-A"], cwd=REPO, check=True)
    subprocess.run(["git", "commit", "-m", "The Valley %s%s%s" % (ver, (" - " + o.title) if o.title else "", TRAILER)], cwd=REPO, env=env)
    subprocess.run(["git", "push", "origin", "HEAD"], cwd=REPO, check=True)
    if gh_ok:
        notes_file = os.path.join(REPO, "release-notes", ver + ".md")
        r = subprocess.run(["gh", "release", "create", "v" + ver, zpath, "--repo", REPO_SLUG, "--title", "The Valley %s%s" % (ver, (" - " + o.title) if o.title else ""), "--notes-file", notes_file], capture_output=True, text=True)
        if r.returncode != 0:
            subprocess.run(["gh", "release", "upload", "v" + ver, zpath, "--clobber", "--repo", REPO_SLUG], check=True)
        log("GitHub release v%s published" % ver)

    # 6. Firestore release/client
    if not o.no_firebase:
        token = run("gcloud auth print-access-token")
        body = json.dumps({"fields": {"version": {"stringValue": ver}, "minVersion": {"stringValue": latest["minVersion"]},
                                      "latest": {"stringValue": json.dumps(latest)},
                                      "updated": {"timestampValue": latest["released"]}}})
        url = ("https://firestore.googleapis.com/v1/projects/%s/databases/(default)/documents/release/client"
               "?updateMask.fieldPaths=version&updateMask.fieldPaths=minVersion&updateMask.fieldPaths=latest&updateMask.fieldPaths=updated") % PROJECT
        import urllib.request
        req = urllib.request.Request(url, data=body.encode(), method="PATCH", headers={"Authorization": "Bearer " + token, "Content-Type": "application/json", "x-goog-user-project": PROJECT})
        with urllib.request.urlopen(req, timeout=30) as resp: log("Firestore release/client updated (%d)" % resp.status)
    log("DONE - The Valley %s is live. Runners update from %s." % (ver, prev_ver))

if __name__ == "__main__":
    main()
