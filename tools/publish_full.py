#!/usr/bin/env python3
# =====================================================================================================================
# THE VALLEY - FULL CLIENT PUBLISHER  (publish_full.py)
# Document: GME-2026-10-044 | Version 1.0 | 28 Oct 2026 (SAST)
# Author: Johan Wilhelm van Antwerp | Ionity (Pty) Ltd - AEDI | Policy 986 AED | (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd
#
# After 7-Zip has split the client into Release\Drive_Upload\v<X.Y>\TheValley_v<X.Y>_Client.zip.001 ...:
#   1. adds TheValley_Setup.exe and a JOIN.bat for these parts
#   2. uploads the folder to Google Drive  "The Valley - Downloads/v<X.Y>"  (rclone) and makes every file public by link
#   3. writes links_v<X.Y>.json, points the ionity.fun /valley/ page at the new parts, commits and pushes IO-Man
#   4. sets latest.json "full" (what a brand-new player downloads) and writes it to Firestore release/client
# Usage:  python publish_full.py --version 4.14.0 --name "FIELD OF HONOUR"
# =====================================================================================================================
import argparse, datetime, glob, io, json, os, re, shutil, subprocess, urllib.request

UPLOAD = r"E:\TheValley_UE5\Release\Drive_Upload"
DRIVE = "ionity-digital:The Valley - Downloads"
SETUP = r"E:\TheValley_UE5\Release\Drive_Upload\v4.13\TheValley_Setup.exe"
SITE = r"E:\TheValley_UE5\web\IO-Man"
REPO = r"E:\TheValley_UE5\the-valley-releases"
PROJECT = "the-valley-mmo"
TRAILER = "\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\nClaude-Session: https://claude.ai/code/session_01SxRSyKhQNJ4N1dbcMZbyHG"
ENV = dict(os.environ, GIT_AUTHOR_NAME="Johan Wilhelm van Antwerp", GIT_AUTHOR_EMAIL="johan@ionity.today",
           GIT_COMMITTER_NAME="Johan Wilhelm van Antwerp", GIT_COMMITTER_EMAIL="johan@ionity.today")

def log(*a): print("[valley-publish]", *a, flush=True)
def run(cmd, cwd=None):
    r = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    if r.returncode != 0: raise SystemExit("FAILED: %s\n%s\n%s" % (cmd, r.stdout, r.stderr))
    return r.stdout.strip()
def drive_id(link):
    m = re.search(r"(?:id=|/d/|folders/)([\w-]{20,})", link)
    if not m: raise SystemExit("no Drive id in: " + link)
    return m.group(1)

def main():
    a = argparse.ArgumentParser(); a.add_argument("--version", required=True); a.add_argument("--name", default=""); a.add_argument("--skip-upload", action="store_true")
    o = a.parse_args()
    ver = o.version.strip().lstrip("vV"); short = ".".join(ver.split(".")[:2])
    folder = os.path.join(UPLOAD, "v" + short); base = "TheValley_v%s_Client.zip" % short
    parts = sorted(glob.glob(os.path.join(folder, base + ".[0-9][0-9][0-9]")))
    if not parts: raise SystemExit("no parts in " + folder)
    for i, p in enumerate(parts):
        if not p.endswith(".%03d" % (i + 1)): raise SystemExit("part %03d missing" % (i + 1))
    total = sum(os.path.getsize(p) for p in parts)
    log("%d parts, %.1f GB" % (len(parts), total / 2**30))

    # 1. setup + JOIN.bat
    shutil.copyfile(SETUP, os.path.join(folder, "TheValley_Setup.exe"))
    io.open(os.path.join(folder, "JOIN.bat"), "w", newline="\r\n").write(
        "@echo off\nrem THE VALLEY v%s - joins the downloaded parts into %s (keep all parts in this folder)\n"
        "rem Easier: run TheValley_Setup.exe instead - it joins, unpacks, makes a desktop link and starts the game.\n"
        "cd /d \"%%~dp0\"\nif not exist \"%s.001\" (echo Missing %s.001 - download every part first. & pause & exit /b 1)\n"
        "echo Joining %d parts - this takes a few minutes...\ncopy /b %s %s >nul\n"
        "if errorlevel 1 (echo Join failed. & pause & exit /b 1)\n"
        "echo Done: %s - right-click it, Extract All, then run The Valley.exe inside.\npause\n"
        % (short, base, base, base, len(parts), "+".join(os.path.basename(p) for p in parts), base, base))

    # 2. upload + public links
    remote = "%s/v%s" % (DRIVE, short)
    if not o.skip_upload:
        log("uploading to Drive (%s) ..." % remote)
        subprocess.run(["rclone", "copy", folder, remote, "--transfers", "3", "--drive-chunk-size", "256M", "--stats", "60s", "--stats-one-line", "-v"], check=True)
    links = {"folder": "https://drive.google.com/open?id=" + drive_id(run(["rclone", "link", remote]))}
    for f in ["TheValley_Setup.exe"] + [os.path.basename(p) for p in parts] + ["JOIN.bat"]:
        links[f] = "https://drive.google.com/open?id=" + drive_id(run(["rclone", "link", remote + "/" + f]))
    json.dump(links, io.open(os.path.join(UPLOAD, "links_v%s.json" % short), "w", encoding="utf-8"), indent=4)
    log("links:", json.dumps(links, indent=1))

    # 3. the download page
    p = os.path.join(SITE, "valley", "index.html"); s = io.open(p, encoding="utf-8").read()
    s = re.sub(r'(<a class="dl" id="dl" href=")[^"]+(")', r"\g<1>%s\g<2>" % links["folder"], s)
    s = re.sub(r'(<div class="meta" id="dlmeta">)[^<]*(</div>)', r'\g<1>v%s%s · client only · Google Drive · %d parts of 4 GB (%.1f GB) · free · updates itself\g<2>'
               % (short, (' "%s"' % o.name) if o.name else "", len(parts), total / 2**30), s)
    s = re.sub(r"TheValley_v\d+\.\d+_Client\.zip", base, s)
    s = re.sub(r"\b\d+ parts of 4 GB", "%d parts of 4 GB" % len(parts), s)
    items = ['<li><a href="%s" target="_blank" rel="noopener"><b>TheValley_Setup.exe</b></a> (installer, 0.2 MB)</li>' % links["TheValley_Setup.exe"]]
    items += ['<li><a href="%s" target="_blank" rel="noopener">%s</a></li>' % (links[os.path.basename(q)], os.path.basename(q)) for q in parts]
    items += ['<li><a href="%s" target="_blank" rel="noopener">JOIN.bat</a></li>' % links["JOIN.bat"]]
    s = re.sub(r'(<ol class="parts">\s*)(.*?)(\s*</ol>)', lambda m: m.group(1) + "\n".join(items) + m.group(3), s, count=1, flags=re.S)
    s = re.sub(r'(TheValley_Setup\.exe</a> into the same folder)', r"\1", s)
    s = re.sub(r'href="https://drive\.google\.com/open\?id=[\w-]+"( target="_blank" rel="noopener">TheValley_Setup\.exe</a> into)', 'href="%s"\\1' % links["TheValley_Setup.exe"], s)
    s = re.sub(r'href="https://drive\.google\.com/open\?id=[\w-]+"( target="_blank" rel="noopener">Open the whole folder)', 'href="%s"\\1' % links["folder"], s)
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    subprocess.run(["git", "add", "valley/index.html"], cwd=SITE, check=True)
    subprocess.run(["git", "commit", "-q", "-m", "The Valley v%s client on Google Drive (%d parts, setup, JOIN.bat)%s" % (short, len(parts), TRAILER)], cwd=SITE, env=ENV)
    subprocess.run(["git", "push", "-q"], cwd=SITE, check=True)
    log("ionity.fun /valley/ points at v" + short)

    # 4. latest.json "full" + Firestore
    lp = os.path.join(REPO, "latest.json"); latest = json.load(io.open(lp, encoding="utf-8"))
    latest["full"] = {"version": ver, "setup": links["TheValley_Setup.exe"], "folder": links["folder"], "parts": len(parts), "size": total}
    json.dump(latest, io.open(lp, "w", encoding="utf-8"), indent=2)
    subprocess.run(["git", "add", "latest.json"], cwd=REPO, check=True)
    subprocess.run(["git", "commit", "-q", "-m", "Full client %s on Drive%s" % (ver, TRAILER)], cwd=REPO, env=ENV)
    subprocess.run(["git", "push", "-q"], cwd=REPO, check=True)
    token = subprocess.run("gcloud auth print-access-token", shell=True, capture_output=True, text=True).stdout.strip()
    body = json.dumps({"fields": {"version": {"stringValue": latest["version"]}, "minVersion": {"stringValue": latest["minVersion"]},
                                  "latest": {"stringValue": json.dumps(latest)}, "updated": {"timestampValue": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")}}})
    url = ("https://firestore.googleapis.com/v1/projects/%s/databases/(default)/documents/release/client"
           "?updateMask.fieldPaths=version&updateMask.fieldPaths=minVersion&updateMask.fieldPaths=latest&updateMask.fieldPaths=updated") % PROJECT
    req = urllib.request.Request(url, data=body.encode(), method="PATCH", headers={"Authorization": "Bearer " + token, "Content-Type": "application/json", "x-goog-user-project": PROJECT})
    with urllib.request.urlopen(req, timeout=30) as r: log("Firestore release/client full = %s (%d)" % (ver, r.status))
    log("DONE - new players download v%s: %s" % (short, links["folder"]))

if __name__ == "__main__":
    main()
