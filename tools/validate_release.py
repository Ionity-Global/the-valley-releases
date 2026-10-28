#!/usr/bin/env python3
# THE VALLEY - release record validator (used by the GitHub Action on every push)
# (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd | AEDI | Policy 986 AED | Author: Johan Wilhelm van Antwerp
# Checks latest.json: the schema, version order, that every version from minVersion up has a patch chain to the latest,
# and (--probe) that every patch mirror answers with a file - not a web page.
import json, re, sys, urllib.request

def vkey(v):
    out = []
    for p in re.split(r"[.\-]", (v or "").strip().lstrip("vV"))[:4]:
        m = re.match(r"\d+", p); out.append(int(m.group()) if m else 0)
    return tuple(out + [0] * (4 - len(out)))

def fail(msg): print("::error::" + msg); sys.exit(1)

def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "latest.json"
    probe = "--probe" in sys.argv
    d = json.load(open(path, encoding="utf-8"))
    for k in ("schema", "version", "minVersion", "patches"):
        if k not in d: fail("latest.json has no '%s'" % k)
    if vkey(d["minVersion"]) > vkey(d["version"]): fail("minVersion is newer than version")
    pts = d["patches"]
    for p in pts:
        for k in ("from", "to", "file", "size", "sha256", "urls"):
            if k not in p: fail("patch %s has no '%s'" % (p.get("file", "?"), k))
        if vkey(p["from"]) >= vkey(p["to"]): fail("patch %s goes backwards" % p["file"])
        if not re.fullmatch(r"[0-9a-f]{64}", p["sha256"]): fail("patch %s: bad sha256" % p["file"])
        if not p["urls"]: fail("patch %s has no mirror" % p["file"])
    # every version a player may have (each 'from', at or above minVersion) must reach the latest
    for start in sorted({p["from"] for p in pts if vkey(p["from"]) >= vkey(d["minVersion"])} | {d["minVersion"]}, key=vkey):
        at, hops = start, 0
        while vkey(at) < vkey(d["version"]) and hops < 64:
            nxt = [p for p in pts if vkey(p["from"]) == vkey(at) and vkey(p["to"]) > vkey(at)]
            if not nxt: fail("no patch chain from %s to %s" % (start, d["version"]))
            at = max(nxt, key=lambda p: vkey(p["to"]))["to"]; hops += 1
        print("chain ok: %s -> %s in %d patch(es)" % (start, d["version"], hops))
    if probe:
        for p in pts:
            for u in p["urls"]:
                req = urllib.request.Request(u, headers={"Range": "bytes=0-1023", "User-Agent": "TheValleyRelease/1.0"})
                try:
                    with urllib.request.urlopen(req, timeout=30) as r:
                        ct = r.headers.get("Content-Type", "")
                        if ct.startswith("text/html"): fail("%s answers with a web page (not public?)" % u)
                        print("mirror ok: %s (%s)" % (u, ct))
                except Exception as e: fail("%s: %s" % (u, e))
    print("latest.json is valid: The Valley %s (min %s), %d patch(es)" % (d["version"], d["minVersion"], len(pts)))

if __name__ == "__main__":
    main()
