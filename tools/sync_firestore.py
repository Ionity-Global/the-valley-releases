#!/usr/bin/env python3
# THE VALLEY - writes latest.json to Firestore release/client (CI; service account in env FIREBASE_SA)
# (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd | AEDI | Policy 986 AED | Author: Johan Wilhelm van Antwerp
import json, os, sys
import google.auth.transport.requests
from google.oauth2 import service_account
import requests

PROJECT = "the-valley-mmo"

def main():
    latest = json.load(open(sys.argv[1] if len(sys.argv) > 1 else "latest.json", encoding="utf-8"))
    info = json.loads(os.environ["FIREBASE_SA"])
    cred = service_account.Credentials.from_service_account_info(info, scopes=["https://www.googleapis.com/auth/datastore"])
    cred.refresh(google.auth.transport.requests.Request())
    url = ("https://firestore.googleapis.com/v1/projects/%s/databases/(default)/documents/release/client"
           "?updateMask.fieldPaths=version&updateMask.fieldPaths=minVersion&updateMask.fieldPaths=latest&updateMask.fieldPaths=updated") % PROJECT
    body = {"fields": {"version": {"stringValue": latest["version"]}, "minVersion": {"stringValue": latest["minVersion"]},
                       "latest": {"stringValue": json.dumps(latest)}, "updated": {"timestampValue": latest["released"]}}}
    r = requests.patch(url, json=body, headers={"Authorization": "Bearer " + cred.token}, timeout=30)
    r.raise_for_status()
    print("Firestore release/client = The Valley %s (min %s)" % (latest["version"], latest["minVersion"]))

if __name__ == "__main__":
    main()
