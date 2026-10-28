// THE VALLEY v4.15 - the small installer: TheValley_Setup.exe downloads the game itself (no parts for players to handle).
// (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd | AEDI | Policy 986 AED | License AED 900 | www.ionity.today
// Author: Johan Wilhelm van Antwerp
//
// The release record (latest.json, "full": { version, files: [ { name, size, md5, urls[] } ] }) lists the pieces of the full
// client. The setup downloads every piece (resuming a broken download where it stopped, trying every mirror, 6 attempts),
// checks each piece's size and MD5, then reads the zip straight across the pieces (PartsStream) - no 25 GB joined copy.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

public class ValleyFullFile
{
    public string Name = "", Md5 = ""; public long Size; public List<string> Urls = new List<string>();
}

public static class ValleyFull
{
    const string Agent = "TheValleySetup/4.15";

    /** The full client's pieces from the release record (GitHub first, the update server second). */
    public static List<ValleyFullFile> Files(out string version, out string error)
    {
        version = ""; error = "";
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        string[] sources = {
            ValleyUpdate.GitHubLatest + "?t=" + DateTime.UtcNow.Ticks,
            "https://firestore.googleapis.com/v1/projects/the-valley-mmo/databases/(default)/documents/release/client" };
        foreach (string url in sources)
        {
            try
            {
                string body = GetText(url);
                JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(body);
                if (d != null && d.ContainsKey("fields"))
                {   // the update server wraps the same JSON in a document field
                    Dictionary<string, object> f = d["fields"] as Dictionary<string, object>;
                    Dictionary<string, object> v = f != null && f.ContainsKey("latest") ? f["latest"] as Dictionary<string, object> : null;
                    if (v == null || !v.ContainsKey("stringValue")) throw new Exception("no release record");
                    d = js.Deserialize<Dictionary<string, object>>((string)v["stringValue"]);
                }
                Dictionary<string, object> full = d != null && d.ContainsKey("full") ? d["full"] as Dictionary<string, object> : null;
                if (full == null || !full.ContainsKey("files")) throw new Exception("the release record has no download list");
                version = Str(full, "version");
                List<ValleyFullFile> list = new List<ValleyFullFile>();
                foreach (object o in (ArrayList)full["files"])
                {
                    Dictionary<string, object> p = o as Dictionary<string, object>; if (p == null) continue;
                    ValleyFullFile ff = new ValleyFullFile { Name = Path.GetFileName(Str(p, "name")), Md5 = Str(p, "md5").ToLowerInvariant() };
                    long n; long.TryParse(Str(p, "size"), out n); ff.Size = n;
                    object us; if (p.TryGetValue("urls", out us) && us is ArrayList) foreach (object u in (ArrayList)us) if (u != null && u.ToString().StartsWith("https://")) ff.Urls.Add(u.ToString());
                    if (ff.Name.Length == 0 || ff.Size <= 0 || ff.Urls.Count == 0) throw new Exception("a broken entry in the download list");
                    list.Add(ff);
                }
                if (list.Count == 0) throw new Exception("the download list is empty");
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return list;
            }
            catch (Exception ex) { error += (error.Length > 0 ? "; " : "") + ex.Message; }
        }
        return null;
    }

    static string Str(Dictionary<string, object> d, string k) { object o; return d != null && d.TryGetValue(k, out o) && o != null ? Convert.ToString(o, CultureInfo.InvariantCulture) : ""; }

    static string GetText(string url)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url); r.Timeout = 15000; r.UserAgent = Agent;
        r.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        using (WebResponse resp = r.GetResponse()) using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd();
    }

    public static string Md5(string path, Action<long, long> progress)
    {
        using (MD5 h = MD5.Create()) using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        {
            byte[] buf = new byte[8 << 20]; int n; long done = 0, all = f.Length; DateTime next = DateTime.MinValue;
            while ((n = f.Read(buf, 0, buf.Length)) > 0)
            {
                h.TransformBlock(buf, 0, n, null, 0); done += n;
                if (progress != null && DateTime.UtcNow > next) { progress(done, all); next = DateTime.UtcNow.AddMilliseconds(300); }
            }
            h.TransformFinalBlock(buf, 0, 0);
            return BitConverter.ToString(h.Hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /** One piece into 'to': kept when it is already there and whole, resumed from a .part file, retried across the mirrors. */
    public static void Fetch(ValleyFullFile f, string to, Action<long, long> progress, Action<string> say)
    {
        if (File.Exists(to) && new FileInfo(to).Length == f.Size)
        {
            if (f.Md5.Length == 0 || Md5(to, null) == f.Md5) { progress(f.Size, f.Size); return; }
            File.Delete(to);
        }
        string part = to + ".part"; Exception last = null;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            string url = f.Urls[attempt % f.Urls.Count];
            try
            {
                long have = File.Exists(part) ? new FileInfo(part).Length : 0;
                if (have > f.Size) { File.Delete(part); have = 0; }
                if (have < f.Size)
                {
                    HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url); r.Timeout = 30000; r.ReadWriteTimeout = 120000; r.UserAgent = Agent; r.AllowAutoRedirect = true;
                    if (have > 0) r.AddRange(have);
                    using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
                    {
                        if ((resp.ContentType ?? "").StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                            throw new Exception("the download server sent a web page instead of the game (it may be busy - try again later)");
                        if (have > 0 && resp.StatusCode != HttpStatusCode.PartialContent) have = 0;   // no resume offered: start over
                        using (Stream s = resp.GetResponseStream())
                        using (FileStream o = new FileStream(part, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                        {
                            byte[] buf = new byte[1 << 20]; int n; long got = have; DateTime next = DateTime.MinValue;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                o.Write(buf, 0, n); got += n;
                                if (got > f.Size) throw new Exception("the piece is bigger than expected");
                                if (DateTime.UtcNow > next) { progress(got, f.Size); next = DateTime.UtcNow.AddMilliseconds(250); }
                            }
                        }
                    }
                }
                long len = new FileInfo(part).Length;
                if (len != f.Size) throw new Exception("connection dropped at " + (len / 1048576) + " MB");
                progress(f.Size, f.Size);
                if (f.Md5.Length > 0)
                {
                    say("Checking " + f.Name + " ...");
                    if (Md5(part, null) != f.Md5) { File.Delete(part); throw new Exception(f.Name + " arrived damaged - downloading it again"); }
                }
                if (File.Exists(to)) File.Delete(to);
                File.Move(part, to);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                say(f.Name + ": " + ex.Message + " - trying again (" + (attempt + 1) + " of 6)...");
                Thread.Sleep(Math.Min(30000, 3000 * (attempt + 1)));
            }
        }
        throw new Exception("could not download " + f.Name + ": " + (last != null ? last.Message : "unknown"));
    }
}

/** The pieces of a split zip read as one seekable stream (no joined copy on disk). */
public class PartsStream : Stream
{
    readonly string[] files; readonly long[] starts; readonly long total;
    long pos; int openIx = -1; FileStream open;

    public PartsStream(string[] parts)
    {
        files = parts; starts = new long[parts.Length]; long s = 0;
        for (int i = 0; i < parts.Length; i++) { starts[i] = s; s += new FileInfo(parts[i]).Length; }
        total = s;
    }
    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return true; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return total; } }
    public override long Position { get { return pos; } set { pos = Math.Max(0, Math.Min(total, value)); } }
    public override void Flush() { }
    public override void SetLength(long v) { throw new NotSupportedException(); }
    public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
    public override long Seek(long off, SeekOrigin o)
    {
        Position = o == SeekOrigin.Begin ? off : o == SeekOrigin.Current ? pos + off : total + off;
        return pos;
    }
    public override int Read(byte[] buf, int off, int count)
    {
        int done = 0;
        while (count > 0 && pos < total)
        {
            int ix = Array.BinarySearch(starts, pos); if (ix < 0) ix = ~ix - 1;
            while (ix + 1 < starts.Length && starts[ix + 1] <= pos) ix++;
            if (ix != openIx) { if (open != null) open.Dispose(); open = new FileStream(files[ix], FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20); openIx = ix; }
            long inPart = pos - starts[ix], left = open.Length - inPart;
            if (left <= 0) { pos = (ix + 1 < starts.Length) ? starts[ix + 1] : total; continue; }
            open.Position = inPart;
            int n = open.Read(buf, off, (int)Math.Min(count, left));
            if (n <= 0) break;
            pos += n; off += n; count -= n; done += n;
        }
        return done;
    }
    protected override void Dispose(bool disposing) { if (disposing && open != null) { open.Dispose(); open = null; } base.Dispose(disposing); }
}
