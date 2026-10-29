# THE VALLEY - make a full-client release live: latest.json -> <version>, its GitHub pieces (MD5 from the local cut), notes,
# then the update server. Refuses unless every piece is on GitHub with the right size.
# Usage: python flip_release.py --src <pieces dir> --tag v4.15.0 --version 4.15.0 --min 4.15.0 --title "Wild Karoo" --notes release-notes\4.15.0.md
# (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd | AEDI | Policy 986 AED | Author: Johan Wilhelm van Antwerp
import argparse, hashlib, io, json, os, subprocess, datetime, urllib.request, re
a = argparse.ArgumentParser()
for k in ("src", "tag", "version", "min", "title", "notes"): a.add_argument("--" + k, required=True)
a.add_argument("--early", action="store_true")   # go live while the last pieces are still uploading (the setup waits for them)
o = a.parse_args()
REPO = r"E:\TheValley_UE5\the-valley-releases"; GH = "Ionity-Global/the-valley-releases"; PROJECT = "the-valley-mmo"
TRAILER = "\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\nClaude-Session: https://claude.ai/code/session_01SxRSyKhQNJ4N1dbcMZbyHG"
ENV = dict(os.environ, GIT_AUTHOR_NAME="Johan Wilhelm van Antwerp", GIT_AUTHOR_EMAIL="johan@ionity.today", GIT_COMMITTER_NAME="Johan Wilhelm van Antwerp", GIT_COMMITTER_EMAIL="johan@ionity.today")
names = sorted(n for n in os.listdir(o.src) if re.search(r"_Client\.zip\.\d{3}$", n))
rel = json.loads(subprocess.run(["gh", "api", "repos/%s/releases/tags/%s" % (GH, o.tag)], capture_output=True, text=True, cwd=REPO).stdout)
assets = {x["name"]: x for x in rel.get("assets", []) if x.get("state") == "uploaded"}
missing = [n for n in names if n not in assets or assets[n]["size"] != os.path.getsize(os.path.join(o.src, n))]
if missing and not o.early: raise SystemExit("NOT LIVE: %d piece(s) not on GitHub yet: %s" % (len(missing), ", ".join(missing[:5])))
cache_p = os.path.join(o.src, "pieces_md5.json"); cache = json.load(io.open(cache_p)) if os.path.exists(cache_p) else {}
files = []
for n in names:
    fp = os.path.join(o.src, n); sz = os.path.getsize(fp); key = "%s:%d" % (n, sz)
    if key not in cache:
        h = hashlib.md5()
        with open(fp, "rb") as fh:
            for blk in iter(lambda: fh.read(16 << 20), b""): h.update(blk)
        cache[key] = h.hexdigest(); json.dump(cache, io.open(cache_p, "w"), indent=1); print("md5", n, flush=True)
    files.append({"name": n, "size": sz, "md5": cache[key], "urls": ["https://github.com/%s/releases/download/%s/%s" % (GH, o.tag, n)]})
total = sum(f["size"] for f in files)
lp = os.path.join(REPO, "latest.json"); latest = json.load(io.open(lp, encoding="utf-8-sig"))
latest["version"] = o.version; latest["minVersion"] = o.min; latest["title"] = o.title
latest["notes"] = io.open(os.path.join(REPO, o.notes), encoding="utf-8-sig").read()
latest["released"] = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
latest["full"] = dict(latest.get("full", {}), version=o.version, name=o.title.upper(), parts=len(files), size=total, files=files,
                      setup="https://github.com/%s/releases/latest/download/TheValley_Setup.exe" % GH)
io.open(lp, "w", encoding="utf-8", newline="\n").write(json.dumps(latest, indent=2) + "\n")
subprocess.run(["git", "add", "latest.json", o.notes.replace("\\", "/")], cwd=REPO, check=True)
subprocess.run(["git", "commit", "-q", "-m", "The Valley %s %s is live: %d pieces on GitHub (%.1f GB)%s" % (o.version, o.title, len(files), total / 2**30, TRAILER)], cwd=REPO, env=ENV)
subprocess.run(["git", "push", "-q"], cwd=REPO, check=True)
token = subprocess.run("gcloud auth print-access-token", shell=True, capture_output=True, text=True).stdout.strip()
body = json.dumps({"fields": {"version": {"stringValue": latest["version"]}, "minVersion": {"stringValue": latest["minVersion"]}, "latest": {"stringValue": json.dumps(latest)},
                              "updated": {"timestampValue": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")}}})
url = ("https://firestore.googleapis.com/v1/projects/%s/databases/(default)/documents/release/client?updateMask.fieldPaths=version&updateMask.fieldPaths=minVersion&updateMask.fieldPaths=latest&updateMask.fieldPaths=updated") % PROJECT
req = urllib.request.Request(url, data=body.encode(), method="PATCH", headers={"Authorization": "Bearer " + token, "Content-Type": "application/json", "x-goog-user-project": PROJECT})
with urllib.request.urlopen(req, timeout=30) as r: print(("EARLY " if o.early and missing else "") + "LIVE (%d): The Valley %s, %d pieces, %.1f GB" % (r.status, o.version, len(files), total / 2**30))
