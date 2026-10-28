#!/usr/bin/env python3
# =====================================================================================================================
# THE VALLEY - publish the small installer  (publish_installer.py)
# Author: Johan Wilhelm van Antwerp | Ionity (Pty) Ltd - AEDI | Policy 986 AED | (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd
#
#   1. reads the full client's pieces on Google Drive (name, size, MD5, file id) and writes them into latest.json "full.files"
#   2. puts TheValley_Setup.exe + HOW_TO_INSTALL.txt in the Drive folder "The Valley - Downloads/Client" (public by link)
#   3. latest.json "full.setup" / "full.folder" -> the Client folder; commits + pushes the releases repo, writes the update server
#   4. the ionity.fun /valley/ page: one DOWNLOAD button = the installer exe (no parts for players)
# Usage: python publish_installer.py --short 4.14 --version 4.14.0
# =====================================================================================================================
import argparse, datetime, io, json, os, re, subprocess, urllib.request

DRIVE = "ionity-digital:The Valley - Downloads"
UPLOAD = r"E:\TheValley_UE5\Release\Drive_Upload"
SETUP = r"E:\TheValley_UE5\Scripts\v414\TheValley_Setup.415.exe"
SITE = r"E:\TheValley_UE5\web\IO-Man"
REPO = r"E:\TheValley_UE5\the-valley-releases"
PROJECT = "the-valley-mmo"
TRAILER = "\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\nClaude-Session: https://claude.ai/code/session_01SxRSyKhQNJ4N1dbcMZbyHG"
ENV = dict(os.environ, GIT_AUTHOR_NAME="Johan Wilhelm van Antwerp", GIT_AUTHOR_EMAIL="johan@ionity.today",
           GIT_COMMITTER_NAME="Johan Wilhelm van Antwerp", GIT_COMMITTER_EMAIL="johan@ionity.today")

def log(*a): print("[valley-installer]", *a, flush=True)
def run(cmd, cwd=None):
    r = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0: raise SystemExit("FAILED: %s\n%s\n%s" % (cmd, r.stdout, r.stderr))
    return r.stdout.strip()
def drive_id(link):
    m = re.search(r"(?:id=|/d/|folders/)([\w-]{20,})", link)
    if not m: raise SystemExit("no Drive id in: " + link)
    return m.group(1)
def dl(fid): return "https://drive.usercontent.google.com/download?id=%s&export=download&confirm=t" % fid

HOWTO = """THE VALLEY - HOW TO INSTALL  (Windows 10 / 11, 64-bit)
=======================================================
1. Download TheValley_Setup.exe (this folder) - it is small (under 1 MB).
2. Run it and click INSTALL. It downloads the whole game by itself (about 26 GB), checks every piece,
   installs what Windows needs to run it (Microsoft Visual C++ / DirectX - Windows asks permission once),
   puts "The Valley" on your desktop and updates the game to the newest version.
3. You need about 55 GB free on the drive you install to. If the internet breaks, run the setup again -
   it carries on where it stopped.
4. Start "The Valley" from the desktop. Sign in, pick a server, play. Updates arrive in the launcher (UPDATE button).

"Windows protected your PC": click "More info" > "Run anyway" (the setup is new, so Windows does not know it yet).
Publisher: Ionity (Pty) Ltd - www.ionity.today - info@ionity.today

AFRIKAANS: Laai net TheValley_Setup.exe af, begin dit en klik INSTALL. Dit laai die hele speletjie self af
(omtrent 26 GB), installeer wat Windows nodig het, sit "The Valley" op die lessenaar en werk self op.
Jy het omtrent 55 GB vry spasie nodig. As die internet breek, begin die setup weer - dit gaan aan waar dit gestop het.
"""

def main():
    a = argparse.ArgumentParser(); a.add_argument("--short", default="4.14"); a.add_argument("--version", default="4.14.0"); a.add_argument("--name", default="FIELD OF HONOUR"); a.add_argument("--no-site", action="store_true")
    o = a.parse_args()
    # 1. the pieces
    items = json.loads(run(["rclone", "lsjson", "--hash", "--files-only", "%s/v%s" % (DRIVE, o.short)]))
    parts = sorted([i for i in items if re.search(r"_Client\.zip\.\d{3}$", i["Name"])], key=lambda i: i["Name"])
    if not parts: raise SystemExit("no pieces on Drive")
    for i, p in enumerate(parts):
        if not p["Name"].endswith(".%03d" % (i + 1)): raise SystemExit("piece %03d missing on Drive" % (i + 1))
        if not p.get("Hashes", {}).get("md5"): raise SystemExit("no MD5 for " + p["Name"])
    files = [{"name": p["Name"], "size": p["Size"], "md5": p["Hashes"]["md5"], "urls": [dl(p["ID"])]} for p in parts]
    total = sum(p["Size"] for p in parts)
    log("%d pieces, %.1f GB" % (len(parts), total / 2**30))
    # 2. the Client folder
    client = os.path.join(UPLOAD, "Client"); os.makedirs(client, exist_ok=True)
    import shutil; shutil.copyfile(SETUP, os.path.join(client, "TheValley_Setup.exe"))
    io.open(os.path.join(client, "HOW_TO_INSTALL.txt"), "w", encoding="utf-8", newline="\r\n").write(HOWTO)
    remote = DRIVE + "/Client"
    run(["rclone", "copy", client, remote])
    folder = "https://drive.google.com/open?id=" + drive_id(run(["rclone", "link", remote]))
    setup = "https://drive.google.com/open?id=" + drive_id(run(["rclone", "link", remote + "/TheValley_Setup.exe"]))
    run(["rclone", "link", remote + "/HOW_TO_INSTALL.txt"])
    log("Client folder", folder, "setup", setup)
    # 3. latest.json + update server
    lp = os.path.join(REPO, "latest.json"); latest = json.load(io.open(lp, encoding="utf-8-sig"))
    latest["full"] = {"version": o.version, "setup": setup, "folder": folder, "parts": len(parts), "size": total, "files": files}
    latest["notes"] = latest.get("notes", "").replace("\ufeff", "").replace("(a Firestore rule from the updater blocked the champion list)", "(an account rule added with the updater blocked the champion list)")
    io.open(lp, "w", encoding="utf-8", newline="\n").write(json.dumps(latest, indent=2) + "\n")
    subprocess.run(["git", "add", "latest.json"], cwd=REPO, check=True)
    subprocess.run(["git", "commit", "-q", "-m", "Small installer: TheValley_Setup.exe downloads the %s client itself (%d pieces, MD5-checked)%s" % (o.version, len(parts), TRAILER)], cwd=REPO, env=ENV)
    subprocess.run(["git", "push", "-q"], cwd=REPO, check=True)
    token = subprocess.run("gcloud auth print-access-token", shell=True, capture_output=True, text=True).stdout.strip()
    body = json.dumps({"fields": {"version": {"stringValue": latest["version"]}, "minVersion": {"stringValue": latest["minVersion"]},
                                  "latest": {"stringValue": json.dumps(latest)}, "updated": {"timestampValue": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")}}})
    url = ("https://firestore.googleapis.com/v1/projects/%s/databases/(default)/documents/release/client"
           "?updateMask.fieldPaths=version&updateMask.fieldPaths=minVersion&updateMask.fieldPaths=latest&updateMask.fieldPaths=updated") % PROJECT
    req = urllib.request.Request(url, data=body.encode(), method="PATCH", headers={"Authorization": "Bearer " + token, "Content-Type": "application/json", "x-goog-user-project": PROJECT})
    with urllib.request.urlopen(req, timeout=30) as r: log("update server release/client (%d)" % r.status)
    # 4. the website
    if o.no_site: log("website not changed (--no-site)"); return
    p = os.path.join(SITE, "valley", "index.html"); s = io.open(p, encoding="utf-8").read()
    s = re.sub(r'(<a class="dl" id="dl" href=")[^"]+(")', r"\g<1>%s\g<2>" % setup, s)
    s = re.sub(r'(<div class="meta" id="dlmeta">)[^<]*(</div>)', r'\g<1>v%s "%s" · TheValley_Setup.exe (under 1 MB) · it downloads the game itself (%.1f GB) · free · updates itself\g<2>' % (o.short, o.name, total / 2**30), s)
    new = ('<h2>Download</h2>\n'
           '  <p><b>One small file:</b> download <a href="%s" target="_blank" rel="noopener"><b>TheValley_Setup.exe</b></a> and run it. Click <b>INSTALL</b> - it downloads the whole game by itself (%.1f GB), checks every piece, installs what Windows needs, puts <i>The Valley</i> on your desktop and brings the game up to the newest version. If the internet breaks, run it again: it carries on where it stopped.</p>\n'
           '  <p>You need about 55 GB free on the drive you install to (an SSD is best). If Windows says <i>Windows protected your PC</i>, click <i>More info</i> &rarr; <i>Run anyway</i> - the setup is new, so Windows does not know it yet. Publisher: Ionity (Pty) Ltd.</p>\n'
           '  <p><b>Updates:</b> open <i>The Valley</i> (the launcher). It checks for new versions itself, and the <b>UPDATE</b> button installs only the changed files (release channel: <a href="https://github.com/Ionity-Global/the-valley-releases" target="_blank" rel="noopener">github.com/Ionity-Global/the-valley-releases</a>).</p>\n'
           '  <p><a href="%s" target="_blank" rel="noopener">Install guide (HOW_TO_INSTALL.txt)</a></p>\n') % (setup, total / 2**30, folder)
    s2, n = re.subn(r'<h2>Download \(Google Drive\)</h2>.*?(?=\s*<h2>Install</h2>)', lambda m: new, s, count=1, flags=re.S)
    if n != 1: raise SystemExit("website download section not found")
    s2 = re.sub(r'<li>Download every part and JOIN\.bat \(above\), run JOIN\.bat, and unzip the <b>whole</b> folder to a drive with about 55 GB free \(an SSD is best\)\.</li>',
                '<li>Download <b>TheValley_Setup.exe</b> (above) and run it - it downloads and installs everything. About 55 GB free is needed (an SSD is best).</li>', s2)
    s2 = re.sub(r'\s*<li>First time only: run <code>Windows\\Engine\\Extras\\Redist\\en-us\\UEPrereqSetup_x64\.exe</code> and click Install\.</li>', '', s2)
    io.open(p, "w", encoding="utf-8", newline="").write(s2)
    subprocess.run(["git", "add", "valley/index.html"], cwd=SITE, check=True)
    subprocess.run(["git", "commit", "-q", "-m", "The Valley: one-file download - TheValley_Setup.exe fetches the game itself%s" % TRAILER], cwd=SITE, env=ENV)
    subprocess.run(["git", "push", "-q"], cwd=SITE, check=True)
    log("DONE - players download only:", setup)

if __name__ == "__main__":
    main()
