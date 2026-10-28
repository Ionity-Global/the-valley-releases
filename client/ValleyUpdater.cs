// =====================================================================================================================
// THE VALLEY v4.14 - THE UPDATER (built into the runner "The Valley.exe")
// Document: GME-2026-10-042 | Version 1.0 | 28 Oct 2026 (SAST)
// Author: Johan Wilhelm van Antwerp | Ionity (Pty) Ltd - formerly Antwerp Designs - AEDI
// Governance: Policy 986 AED | License: AED 900 | (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd - All rights reserved - TM2
// Web: https://www.ionity.today | Ref: https://www.ionity.co.za | Building Tomorrow, Today.
//
// How a client stays current:
//   1. version.txt beside the runner says what this install is (no file = 4.13.0, the first Drive release).
//   2. The release record is read from Firestore  release/client  (field "latest", public read) and, when that fails, from
//      GitHub  https://raw.githubusercontent.com/Ionity-Global/the-valley-releases/main/latest.json  - the same JSON.
//   3. A newer version (or a World Server that runs a newer one) offers UPDATE NOW. Below minVersion the runner will not start
//      the game until it is updated.
//   4. Patches are chained (4.13.0 -> 4.14.0 -> ...). Each patch zip is fetched from its first working mirror (Google Drive,
//      then GitHub Releases), its SHA-256 checked, unpacked to _update\, then moved over the install; files named in
//      __patch__.json "deleted" are removed. The running runner renames itself to "The Valley.exe.old" so it can be replaced,
//      then starts the new one. Nothing is touched until a patch is fully downloaded and verified.
// Build: compiled together with ValleyRunner.cs (see that file's header).
// =====================================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

public class ValleyPatch
{
    public string From = "", To = "", File = "", Sha256 = ""; public long Size; public List<string> Urls = new List<string>();
}

public class ValleyRelease
{
    public string Version = "", MinVersion = "", Title = "", Notes = "", Released = "", Source = "", FullSetup = "", FullFolder = "";
    public List<ValleyPatch> Patches = new List<ValleyPatch>();
}

public static class ValleyUpdate
{
    public const string GitHubLatest = "https://raw.githubusercontent.com/Ionity-Global/the-valley-releases/main/latest.json";
    public const string DefaultVersion = "4.13.0";   // the first Drive release shipped without version.txt

    // ------------------------------------------------------------------------------------------------- versions
    public static string LocalVersion(string root)
    {
        try { string p = Path.Combine(root, "version.txt"); if (File.Exists(p)) { string v = File.ReadAllText(p).Trim(); if (v.Length > 0) return v; } } catch (Exception) { }
        return DefaultVersion;
    }

    /** -1 a older, 0 same, 1 a newer. "4.13" == "4.13.0"; anything after the numbers ("4.14.0-w") is ignored. */
    public static int Compare(string a, string b)
    {
        int[] x = Parts(a), y = Parts(b);
        for (int i = 0; i < 4; i++) { if (x[i] != y[i]) return x[i] < y[i] ? -1 : 1; }
        return 0;
    }
    static int[] Parts(string v)
    {
        int[] r = new int[4]; string[] f = (v ?? "").Trim().TrimStart('v', 'V').Split('.', '-', ' ');
        for (int i = 0; i < 4 && i < f.Length; i++) { int n; if (int.TryParse(new string(f[i].TakeWhile(char.IsDigit).ToArray()), out n)) r[i] = n; }
        return r;
    }

    // ------------------------------------------------------------------------------------------------- the release record
    public static ValleyRelease Fetch(string apiKey, string project, out string error)
    {
        error = "";
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        string json = null, src = "";
        try
        {   // Firestore first (public read of release/client)
            string url = "https://firestore.googleapis.com/v1/projects/" + project + "/databases/(default)/documents/release/client" + (apiKey.Length > 0 ? "?key=" + apiKey : "");
            string body = Get(url, 10000);
            Dictionary<string, object> d = Js().Deserialize<Dictionary<string, object>>(body);
            if (d != null && d.ContainsKey("fields"))
            {
                Dictionary<string, object> f = d["fields"] as Dictionary<string, object>;
                if (f != null && f.ContainsKey("latest")) { Dictionary<string, object> v = f["latest"] as Dictionary<string, object>; if (v != null && v.ContainsKey("stringValue")) { json = (string)v["stringValue"]; src = "Firebase"; } }
            }
        }
        catch (Exception ex) { error = "Firebase: " + ex.Message; }
        if (string.IsNullOrEmpty(json))
        {
            try { json = Get(GitHubLatest + "?t=" + DateTime.UtcNow.Ticks, 10000); src = "GitHub"; }
            catch (Exception ex) { error += (error.Length > 0 ? "; " : "") + "GitHub: " + ex.Message; return null; }
        }
        try { ValleyRelease r = Parse(json); r.Source = src; return r; }
        catch (Exception ex) { error = "release record unreadable: " + ex.Message; return null; }
    }

    static JavaScriptSerializer Js() { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }; }
    static string S(Dictionary<string, object> d, string k) { object o; return d != null && d.TryGetValue(k, out o) && o != null ? Convert.ToString(o, CultureInfo.InvariantCulture) : ""; }

    public static ValleyRelease Parse(string json)
    {
        Dictionary<string, object> d = Js().Deserialize<Dictionary<string, object>>(json);
        ValleyRelease r = new ValleyRelease { Version = S(d, "version"), MinVersion = S(d, "minVersion"), Title = S(d, "title"), Notes = S(d, "notes"), Released = S(d, "released") };
        object full; if (d.TryGetValue("full", out full) && full is Dictionary<string, object>) { r.FullSetup = S((Dictionary<string, object>)full, "setup"); r.FullFolder = S((Dictionary<string, object>)full, "folder"); }
        object ps; if (d.TryGetValue("patches", out ps) && ps is ArrayList)
            foreach (object o in (ArrayList)ps)
            {
                Dictionary<string, object> p = o as Dictionary<string, object>; if (p == null) continue;
                ValleyPatch vp = new ValleyPatch { From = S(p, "from"), To = S(p, "to"), File = S(p, "file"), Sha256 = S(p, "sha256").ToLowerInvariant() };
                long n; if (long.TryParse(S(p, "size"), out n)) vp.Size = n;
                object us; if (p.TryGetValue("urls", out us) && us is ArrayList) foreach (object u in (ArrayList)us) if (u != null && u.ToString().Length > 0) vp.Urls.Add(u.ToString());
                r.Patches.Add(vp);
            }
        if (r.Version.Length == 0) throw new Exception("no version");
        return r;
    }

    /** The patches that carry this install from 'have' to the release's version, in order; null when no chain exists. */
    public static List<ValleyPatch> Chain(ValleyRelease r, string have)
    {
        List<ValleyPatch> chain = new List<ValleyPatch>(); string at = have;
        for (int guard = 0; guard < 64 && Compare(at, r.Version) < 0; guard++)
        {
            ValleyPatch next = r.Patches.Where(p => Compare(p.From, at) == 0 && Compare(p.To, at) > 0).OrderByDescending(p => p.To, Comparer<string>.Create(Compare)).FirstOrDefault();
            if (next == null) return null;
            chain.Add(next); at = next.To;
        }
        return Compare(at, r.Version) >= 0 ? chain : null;
    }

    // ------------------------------------------------------------------------------------------------- applying
    public static bool GameRunning()
    {
        foreach (string n in new[] { "TheValley", "TheValley-Win64-Shipping", "TheValley-Win64-Development" }) if (Process.GetProcessesByName(n).Length > 0) return true;
        return false;
    }

    /** Downloads, verifies and applies every patch of the chain. say(text, percent) reports progress. Returns the new version. */
    public static string Apply(string root, List<ValleyPatch> chain, Action<string, int> say)
    {
        string work = Path.Combine(root, "_update"); Directory.CreateDirectory(work);
        string version = LocalVersion(root);
        for (int i = 0; i < chain.Count; i++)
        {
            ValleyPatch p = chain[i];
            string tag = "(" + (i + 1) + "/" + chain.Count + ") " + p.From + " -> " + p.To;
            string zip = Path.Combine(work, string.IsNullOrEmpty(p.File) ? "patch_" + p.To + ".zip" : Path.GetFileName(p.File));
            if (!(File.Exists(zip) && (p.Size <= 0 || new FileInfo(zip).Length == p.Size) && Sha(zip) == p.Sha256))
            {
                Exception last = null; bool ok = false;
                foreach (string url in p.Urls)
                {
                    try { Download(url, zip, p.Size, delegate (long got, long all) { say("Downloading " + tag + "  " + Mb(got) + " / " + Mb(all), all > 0 ? (int)(got * 80 / all) : 0); }); }
                    catch (Exception ex) { last = ex; continue; }
                    say("Checking " + tag + "...", 82);
                    if (p.Sha256.Length == 0 || Sha(zip) == p.Sha256) { ok = true; break; }
                    last = new Exception("the download did not match its SHA-256");
                }
                if (!ok) throw new Exception("Patch " + tag + " could not be downloaded" + (last != null ? ": " + last.Message : ""));
            }
            say("Installing " + tag + "...", 88);
            string stage = Path.Combine(work, "stage_" + p.To); if (Directory.Exists(stage)) Directory.Delete(stage, true);
            ZipFile.ExtractToDirectory(zip, stage);
            List<string> deleted = new List<string>();
            string meta = Path.Combine(stage, "__patch__.json");
            if (File.Exists(meta))
            {
                Dictionary<string, object> m = Js().Deserialize<Dictionary<string, object>>(File.ReadAllText(meta));
                object del; if (m != null && m.TryGetValue("deleted", out del) && del is ArrayList) foreach (object o in (ArrayList)del) deleted.Add(o.ToString());
                File.Delete(meta);
            }
            string self = Path.GetFullPath(System.Windows.Forms.Application.ExecutablePath), fullRoot = Path.GetFullPath(root);
            foreach (string src in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
            {
                string rel = src.Substring(stage.Length).TrimStart('\\', '/');
                string dst = Path.GetFullPath(Path.Combine(root, rel));
                if (!dst.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) continue;   // never outside the install
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                if (File.Exists(dst))
                {
                    if (string.Equals(dst, self, StringComparison.OrdinalIgnoreCase))
                    {   // the running runner cannot be overwritten, but it can be renamed
                        string old = dst + ".old"; if (File.Exists(old)) File.Delete(old); File.Move(dst, old);
                    }
                    else { File.SetAttributes(dst, FileAttributes.Normal); File.Delete(dst); }
                }
                File.Move(src, dst);
            }
            foreach (string rel in deleted)
            {
                try { string dst = Path.GetFullPath(Path.Combine(root, rel)); if (dst.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(dst) && !string.Equals(dst, self, StringComparison.OrdinalIgnoreCase)) File.Delete(dst); } catch (Exception) { }
            }
            version = p.To;
            File.WriteAllText(Path.Combine(root, "version.txt"), version);
            try { Directory.Delete(stage, true); File.Delete(zip); } catch (Exception) { }
        }
        try { Directory.Delete(work, true); } catch (Exception) { }
        say("Updated to " + version + ".", 100);
        return version;
    }

    /** Removes the renamed runner of the last update (call at start). */
    public static void CleanOld(string root)
    {
        try { foreach (string f in Directory.GetFiles(root, "*.old")) File.Delete(f); } catch (Exception) { }
    }

    // ------------------------------------------------------------------------------------------------- the wire
    static string Mb(long b) { return (b / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"; }

    static string Get(string url, int timeout)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url); r.Timeout = timeout; r.UserAgent = "TheValleyRunner/4.14";
        r.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        using (WebResponse resp = r.GetResponse()) using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd();
    }

    static void Download(string url, string to, long expect, Action<long, long> progress)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url); r.Timeout = 30000; r.ReadWriteTimeout = 60000; r.UserAgent = "TheValleyRunner/4.14"; r.AllowAutoRedirect = true;
        using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
        {
            if ((resp.ContentType ?? "").StartsWith("text/html", StringComparison.OrdinalIgnoreCase)) throw new Exception("the mirror answered with a web page, not the file");
            long all = resp.ContentLength > 0 ? resp.ContentLength : expect, got = 0;
            string part = to + ".part";
            using (Stream s = resp.GetResponseStream()) using (FileStream o = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            {
                byte[] buf = new byte[1 << 20]; int n; DateTime next = DateTime.MinValue;
                while ((n = s.Read(buf, 0, buf.Length)) > 0) { o.Write(buf, 0, n); got += n; if (DateTime.UtcNow > next) { progress(got, all); next = DateTime.UtcNow.AddMilliseconds(250); } }
            }
            progress(got, all);
            if (expect > 0 && got != expect) { File.Delete(part); throw new Exception("incomplete download (" + got + " of " + expect + " bytes)"); }
            if (File.Exists(to)) File.Delete(to); File.Move(part, to);
        }
    }

    public static string Sha(string path)
    {
        using (SHA256 h = SHA256.Create()) using (FileStream f = File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
    }
}
