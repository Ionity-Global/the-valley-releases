// THE VALLEY - portable setup for the Google Drive download (no .bat): joins the 4 GB parts, unpacks the game, starts it.
// v4.14: writes version.txt so the runner's updater knows what was installed; later versions arrive as small patches.
// (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd | AEDI | Policy 986 AED | License AED 900 | www.ionity.today
// Author: Johan Wilhelm van Antwerp
// Put this exe in the same folder as TheValley_v<ver>_Client.zip.001, .002 ... and run it. It checks that every part is there
// and complete, joins them into the zip (with progress), unpacks the zip into the folder you pick, and starts The Valley.exe.
// The joined zip can be deleted afterwards to free space. Built with the .NET Framework that ships with Windows 10 and 11.
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;

public class SetupForm : Form
{
    readonly Label head, info;
    readonly ProgressBar bar;
    readonly TextBox dest;
    readonly Button go, browse;
    readonly CheckBox keepZip, launch;
    string folder, baseName; string[] parts = new string[0];
    string updatedTo = "", updateNote = "";   // v4.14
    static readonly Color Ink = Color.FromArgb(12, 14, 20), Gold = Color.FromArgb(232, 196, 110), Txt = Color.FromArgb(225, 225, 220);

    public SetupForm()
    {
        Text = "The Valley - Setup"; ClientSize = new Size(640, 360); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; BackColor = Ink; ForeColor = Txt; Font = new Font("Segoe UI", 10f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
        head = new Label { Text = "THE VALLEY", Location = new Point(24, 16), Size = new Size(590, 40), ForeColor = Gold, Font = new Font("Georgia", 20f, FontStyle.Bold) };
        info = new Label { Location = new Point(26, 62), Size = new Size(590, 66) };
        Controls.Add(head); Controls.Add(info);
        Controls.Add(new Label { Text = "Install the game into:", Location = new Point(26, 134), Size = new Size(300, 22), ForeColor = Gold });
        dest = new TextBox { Location = new Point(26, 158), Size = new Size(486, 26), BackColor = Color.FromArgb(28, 34, 48), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        browse = new Button { Text = "...", Location = new Point(520, 156), Size = new Size(94, 30), FlatStyle = FlatStyle.Flat, ForeColor = Gold };
        browse.Click += delegate { using (FolderBrowserDialog d = new FolderBrowserDialog { SelectedPath = dest.Text }) if (d.ShowDialog(this) == DialogResult.OK) dest.Text = Path.Combine(d.SelectedPath, "The Valley"); };
        keepZip = new CheckBox { Text = "Keep the joined zip afterwards", Location = new Point(26, 196), Size = new Size(300, 24) };
        launch = new CheckBox { Text = "Start The Valley when done", Location = new Point(330, 196), Size = new Size(280, 24), Checked = true };
        bar = new ProgressBar { Location = new Point(26, 232), Size = new Size(588, 22) };
        go = new Button { Text = "INSTALL", Location = new Point(26, 270), Size = new Size(588, 50), FlatStyle = FlatStyle.Flat, ForeColor = Ink, BackColor = Gold, Font = new Font("Georgia", 13f, FontStyle.Bold) };
        go.Click += delegate
        {   // v4.15 play button: once installed, the button starts the game and closes the setup
            if (installedExe != null) { try { Process.Start(new ProcessStartInfo(installedExe) { WorkingDirectory = Path.GetDirectoryName(installedExe), UseShellExecute = true }); } catch (Exception) { } Close(); return; }
            Start();
        };
        Controls.Add(dest); Controls.Add(browse); Controls.Add(keepZip); Controls.Add(launch); Controls.Add(bar); Controls.Add(go);
        Find();
        // v4.14 unattended: TheValley_Setup.exe -install "<folder>" [-quiet]  (tests, admins) - installs, updates, closes
        string[] av = Environment.GetCommandLineArgs();
        int ix = Array.FindIndex(av, x => x.Equals("-install", StringComparison.OrdinalIgnoreCase));
        if (ix > 0 && ix + 1 < av.Length && go.Enabled)
        {
            dest.Text = av[ix + 1]; launch.Checked = false; quiet = av.Any(x => x.Equals("-quiet", StringComparison.OrdinalIgnoreCase));
            Shown += delegate { Start(); };
        }
    }
    bool quiet; string installedExe;   // v4.15 play button
    bool bNet;   // v4.15 installer: no pieces beside the setup - it downloads them

    /** v4.15 upgrade in place: an existing install (the desktop link, or the usual folders) is upgraded where it is; a fresh
     *  install goes to <drive with most space>\Games\The Valley - visible, not hidden under AppData. */
    static string existingInstall;
    string DefaultTarget()
    {
        List<string> tries = new List<string>();
        try { string rec = InstallRecord(); if (File.Exists(rec)) tries.Add(File.ReadAllText(rec).Trim()); } catch (Exception) { }   // v4.15.1b where it was installed last time
        try
        {   // the desktop link made by an earlier setup
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "The Valley.url");
            if (File.Exists(lnk)) foreach (string l in File.ReadAllLines(lnk)) if (l.StartsWith("URL=file:///")) tries.Add(Path.GetDirectoryName(Uri.UnescapeDataString(l.Substring(12).Replace("/", "\\"))));
        }
        catch (Exception) { }
        tries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "The Valley"));
        foreach (DriveInfo d in DriveInfo.GetDrives()) if (d.IsReady && d.DriveType == DriveType.Fixed) tries.Add(Path.Combine(d.RootDirectory.FullName, "Games", "The Valley"));
        foreach (string t in tries)
        {
            try { if (t != null && File.Exists(Path.Combine(t, "The Valley.exe")) && File.Exists(Path.Combine(t, "version.txt"))) { existingInstall = t; return t; } } catch (Exception) { }
        }
        DriveInfo best = null;
        foreach (DriveInfo d in DriveInfo.GetDrives()) { try { if (d.IsReady && d.DriveType == DriveType.Fixed && (best == null || d.AvailableFreeSpace > best.AvailableFreeSpace)) best = d; } catch (Exception) { } }
        return Path.Combine(best != null ? best.RootDirectory.FullName : "C:\\", "Games", "The Valley");
    }

    static string InstallRecord() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TheValley", "install.txt"); }
    static void RememberInstall(string dir) { try { Directory.CreateDirectory(Path.GetDirectoryName(InstallRecord())); File.WriteAllText(InstallRecord(), dir); } catch (Exception) { } }

    // v4.15.1b: an installed Valley is checked against the release record - up to date (0), a patch path exists (1), only a full
    // download will do (2), or the server could not be asked (-1).
    ValleyRelease relInfo; List<ValleyPatch> relChain; string haveVer = "";
    int CheckExisting()
    {
        if (existingInstall == null) return 2;
        haveVer = ValleyUpdate.LocalVersion(existingInstall);
        string err; relInfo = ValleyUpdate.Fetch("", "the-valley-mmo", out err);
        if (relInfo == null) return -1;
        if (ValleyUpdate.Compare(relInfo.Version, haveVer) <= 0) return 0;
        relChain = ValleyUpdate.Chain(relInfo, haveVer);
        return relChain != null ? 1 : 2;
    }

    void Say(string s) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "TheValley_Setup.log"), DateTime.Now.ToString("HH:mm:ss ") + s + Environment.NewLine); } catch (Exception) { }   /* v4.15.1c setup log */ if (InvokeRequired) BeginInvoke((Action)(() => info.Text = s)); else info.Text = s; }
    void Prog(int v) { if (InvokeRequired) BeginInvoke((Action)(() => bar.Value = Math.Max(0, Math.Min(100, v)))); else bar.Value = Math.Max(0, Math.Min(100, v)); }

    void Find()
    {
        folder = AppDomain.CurrentDomain.BaseDirectory;
        var all = Directory.GetFiles(folder, "*.zip.0??").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var groups = all.GroupBy(f => Regex.Replace(Path.GetFileName(f), @"\.\d{3}$", "")).OrderByDescending(g => g.Key).ToArray();
        dest.Text = DefaultTarget();   // v4.15 upgrade in place / visible Games folder
        if (groups.Length == 0)
        {   // v4.15 installer: nothing beside the setup - it downloads the game itself
            bNet = true; keepZip.Text = "Keep the downloaded pieces afterwards"; keepZip.Checked = false;
            if (existingInstall != null)
            {   // v4.15.1b installed already: ask the server what (if anything) is needed - never the whole game by default
                Say("The Valley is installed in " + existingInstall + " - checking for updates ...");
                new Thread(() =>
                {
                    int st = CheckExisting();
                    BeginInvoke((Action)(() =>
                    {
                        if (st == 0) { installedExe = Path.Combine(existingInstall, "The Valley.exe"); Say("The Valley " + haveVer + " is installed in " + existingInstall + " and up to date. Nothing to download."); go.Text = "PLAY"; keepZip.Visible = false; }
                        else if (st == 1) { long mb = relChain.Sum(p => p.Size) / 1048576; Say("The Valley " + haveVer + " is installed in " + existingInstall + ". UPDATE brings it to " + relInfo.Version + " - only " + mb + " MB to download."); go.Text = "UPDATE"; keepZip.Visible = false; }
                        else if (st == 2) Say("The Valley " + haveVer + " is installed in " + existingInstall + ". Version " + (relInfo != null ? relInfo.Version : "?") + " is a full download (about 26 GB) - INSTALL fetches it and upgrades in place; your account and settings stay.");
                        else Say("The Valley " + haveVer + " is installed in " + existingInstall + ". The update server did not answer - PLAY, or try again in a minute.");
                        if (st == -1) { installedExe = Path.Combine(existingInstall, "The Valley.exe"); go.Text = "PLAY"; }
                    }));
                }) { IsBackground = true }.Start();
                return;
            }
            Say("INSTALL downloads The Valley (about 26 GB) and installs it - there is nothing else to download. You need about 55 GB free on the drive you install to. If the connection breaks, run the setup again: it carries on where it stopped.");
            return;
        }
        keepZip.Visible = false;   // v4.15: the parts beside the setup are read in place and always kept
        baseName = groups[0].Key; parts = groups[0].ToArray();
        // every number from 001 up must be there
        for (int i = 0; i < parts.Length; i++)
            if (!parts[i].EndsWith("." + (i + 1).ToString("000"))) { Say("Part " + (i + 1).ToString("000") + " of " + baseName + " is missing - download every part into this folder."); go.Enabled = false; return; }
        long total = parts.Sum(p => new FileInfo(p).Length);
        // v4.15 setup: the parts are cut at one fixed size, so the LAST part is always smaller. Equal sizes = the last part(s) are missing.
        long first = new FileInfo(parts[0]).Length, last = new FileInfo(parts[parts.Length - 1]).Length;
        if (first >= 1000000000L && last == first)
        { Say(string.Format("Only {0} part(s) found ({1:0.0} GB) - the game has more parts. Download EVERY part (.001, .002, .003 ...) from the Drive folder into this folder, then run the setup again.", parts.Length, total / 1073741824.0)); go.Enabled = false; return; }
        Say(string.Format("Found {0} in {1} parts ({2:0.0} GB). INSTALL unpacks the game and starts it. You need about {3:0} GB free.", baseName, parts.Length, total / 1073741824.0, total * 1.1 / 1073741824.0));
        // a part that is smaller than the others (except the last) is a broken download
        for (int i = 0; i + 1 < parts.Length; i++)
            if (new FileInfo(parts[i]).Length != new FileInfo(parts[0]).Length) { Say("Part " + Path.GetExtension(parts[i]) + " looks incomplete (wrong size) - download it again."); go.Enabled = false; return; }
    }

    void Start()
    {
        string target = dest.Text.Trim(); if (target.Length == 0) return;
        go.Enabled = false; browse.Enabled = false; browse.Visible = false; dest.Enabled = false;
        bool keep = keepZip.Checked, run = launch.Checked;
        new Thread(() =>
        {
            try
            {
                string dl = null;
                if (bNet && existingInstall != null && string.Equals(Path.GetFullPath(target).TrimEnd('\\'), Path.GetFullPath(existingInstall).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {   // v4.15.1b
                    int st = CheckExisting();
                    if (st == 0 || st == 1)
                    {
                        string nv = haveVer;
                        if (st == 1) nv = ValleyUpdate.Apply(existingInstall, relChain, delegate (string t, int pct) { Say(t); Prog(pct); });
                        installedExe = Path.Combine(existingInstall, "The Valley.exe"); RememberInstall(existingInstall);
                        Prog(100); Say(st == 0 ? "The Valley " + nv + " is up to date - nothing was downloaded." : "Updated to " + nv + ".");
                        if (run && File.Exists(installedExe)) Process.Start(new ProcessStartInfo(installedExe) { WorkingDirectory = existingInstall, UseShellExecute = true });
                        BeginInvoke((Action)(() => { go.Text = "PLAY"; go.Enabled = true; if (quiet) Close(); }));
                        return;
                    }
                }
                if (bNet)
                {   // v4.15 installer: the list of pieces from the release record, then every piece (resumed, checked)
                    Say("Asking the server for the newest game ...");
                    string fv, ferr; List<ValleyFullFile> files = ValleyFull.Files(out fv, out ferr);
                    if (files == null) throw new Exception("could not reach the download server (" + ferr + ") - check the internet connection and try again");
                    long need = files.Sum(x => x.Size);
                    Directory.CreateDirectory(target);
                    string root = Path.GetPathRoot(Path.GetFullPath(target)); long free = -1;
                    try { free = new DriveInfo(root).AvailableFreeSpace; } catch (Exception) { }
                    dl = Path.Combine(target, "_download"); Directory.CreateDirectory(dl);
                    long have0 = Directory.GetFiles(dl).Sum(x => new FileInfo(x).Length);
                    if (free >= 0 && free + have0 < (long)(need * 2.05))
                        throw new Exception(string.Format("not enough free space on {0} - The Valley needs about {1:0} GB there, {2:0} GB is free. Pick another drive with the ... button.", root, need * 2.05 / 1073741824.0, (free + have0) / 1073741824.0));
                    long before = 0; List<string> got2 = new List<string>(); DateTime t0 = DateTime.UtcNow; long fresh = 0;
                    for (int k = 0; k < files.Count; k++)
                    {
                        ValleyFullFile ff = files[k]; string to2 = Path.Combine(dl, ff.Name); long b0 = before; int kk = k; long seen = 0;
                        ValleyFull.Fetch(ff, to2, (g, a) =>
                        {
                            if (g > seen) { fresh += g - seen; seen = g; }
                            long d = b0 + g; double secs = Math.Max(1.0, (DateTime.UtcNow - t0).TotalSeconds);
                            Prog((int)(d * 50 / need));
                            Say(string.Format("Downloading The Valley {0} - piece {1} of {2} - {3:0.0} of {4:0.0} GB  ({5:0.0} MB/s)", fv, kk + 1, files.Count, d / 1073741824.0, need / 1073741824.0, fresh / 1048576.0 / secs));
                        }, Say);
                        before += ff.Size; got2.Add(to2);
                        Say(string.Format("Verified piece {0} of {1} - checksum OK", kk + 1, files.Count));   // v4.15.1c verify
                    }
                    parts = got2.ToArray(); baseName = Regex.Replace(files[0].Name, @"\.\d{3}$", "");
                }
                Directory.CreateDirectory(target);
                ZipArchive za;
                try { za = new ZipArchive(new PartsStream(parts), ZipArchiveMode.Read); }   // v4.15: read across the pieces, no joined copy
                catch (Exception) { throw new Exception("the game pieces do not fit together - one is missing or broken. " + (bNet ? "Run the setup again: it downloads only what is missing." : "Download every part again and run the setup again.")); }
                using (ZipArchive a = za)
                {
                    long all = a.Entries.Sum(e => e.Length), got = 0; string full = Path.GetFullPath(target); int verified = 0;
                    foreach (ZipArchiveEntry e in a.Entries)
                    {
                        string to = Path.GetFullPath(Path.Combine(target, e.FullName));
                        if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;   // never outside the install folder
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(to); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        // v4.15 setup: copy in blocks so the bar moves while a 20 GB file unpacks
                        byte[] ub = new byte[8 << 20]; long eDone = 0; uint crc = 0xFFFFFFFFu;   // CRC-32 while writing
                        using (Stream src = e.Open()) using (FileStream dst = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                        {
                            int n; long lastSay = 0;
                            while ((n = src.Read(ub, 0, ub.Length)) > 0)
                            {
                                dst.Write(ub, 0, n); eDone += n; crc = Crc32.Update(crc, ub, n);
                                if (eDone - lastSay >= (256L << 20)) { lastSay = eDone; Prog(50 + (int)(all > 0 ? (got + eDone) * 45 / all : 45)); Say(string.Format("Unpacking and verifying {0} ... {1:0.0} of {2:0.0} GB", e.Name, (got + eDone) / 1073741824.0, all / 1073741824.0)); }
                            }
                        }
                        uint want; crc ^= 0xFFFFFFFFu;
                        if (eDone != e.Length) throw new Exception(e.FullName + " came out " + eDone + " bytes, expected " + e.Length + " - " + (bNet ? "run the setup again" : "download the parts again"));
                        if (Crc32.Of(e, out want) && want != crc && bNet && dl != null) try { foreach (string ok in Directory.GetFiles(dl, "*.ok")) File.Delete(ok); } catch (Exception) { }   // next run re-checks every piece
                        if (Crc32.Of(e, out want) && want != crc) throw new Exception(e.FullName + " failed verification (CRC " + crc.ToString("X8") + ", expected " + want.ToString("X8") + ") - " + (bNet ? "run the setup again: it re-checks the pieces" : "download the parts again"));
                        verified++;
                        try { File.SetLastWriteTime(to, e.LastWriteTime.DateTime); } catch (Exception) { }
                        got += e.Length;
                        Prog(50 + (int)(all > 0 ? got * 45 / all : 45)); Say("Unpacked and verified " + e.Name + " (" + verified + " items)");
                    }
                    // v4.15.1c verify: a last pass over every item - present, right size
                    var items = a.Entries.Where(x => !(x.FullName.EndsWith("/") || x.FullName.EndsWith("\\"))).ToList(); int vi = 0, bad = 0; string firstBad = null;
                    foreach (ZipArchiveEntry e in items)
                    {
                        vi++; string to = Path.GetFullPath(Path.Combine(target, e.FullName));
                        if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;
                        FileInfo fi = new FileInfo(to);
                        if (!fi.Exists || fi.Length != e.Length) { bad++; if (firstBad == null) firstBad = e.FullName; }
                        if (vi % 25 == 0 || vi == items.Count) { Prog(95 + vi * 5 / Math.Max(1, items.Count)); Say(string.Format("Verifying items {0} of {1} ...", vi, items.Count)); }
                    }
                    if (bad > 0) throw new Exception(bad + " item(s) failed verification (first: " + firstBad + ") - run the setup again");
                    Say(string.Format("Verified all {0} items - the install is complete and intact.", items.Count));
                }
                if (bNet && !keep && dl != null) try { Directory.Delete(dl, true); } catch (Exception) { }   // v4.15 installer: the pieces are no longer needed
                string exe = Directory.GetFiles(target, "The Valley.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (exe != null) ValleyUpdate.Lock(Path.GetDirectoryName(exe), true);   // v4.15g a launcher opened now waits instead of failing

                if (exe != null)
                {   // v4.14 the updater reads version.txt beside the runner: a zip without one is the version in its name
                    string vt = Path.Combine(Path.GetDirectoryName(exe), "version.txt");
                    Match vm = Regex.Match(baseName, @"v(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
                    if (!File.Exists(vt) && vm.Success) { string v = vm.Groups[1].Value; while (v.Split('.').Length < 3) v += ".0"; try { File.WriteAllText(vt, v); } catch (Exception) { } }
                    MakeShortcut(exe);
                    // v4.15 prereqs: the Visual C++ runtime and DirectX parts the game needs - installed once (Windows asks for permission)
                    try
                    {
                        bool have = false;
                        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
                            if (k != null) { object inst = k.GetValue("Installed"), minor = k.GetValue("Minor"); have = inst != null && Convert.ToInt32(inst) == 1 && minor != null && Convert.ToInt32(minor) >= 30; }
                        string pre = Directory.GetFiles(target, "UEPrereqSetup_x64.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (!have && pre != null)
                        {
                            Say("Installing what the game needs (Microsoft Visual C++ and DirectX) - Windows asks for permission once ...");
                            using (Process pp = Process.Start(new ProcessStartInfo(pre, "/quiet /norestart") { UseShellExecute = true, Verb = "runas" })) pp.WaitForExit(600000);
                        }
                    }
                    catch (Exception) { updateNote += " (if the game does not start: run UEPrereqSetup_x64.exe in the game folder once)"; }
                    // v4.14 bring the fresh install up to the newest version straight away (the same updater the runner uses)
                    string dir = Path.GetDirectoryName(exe);
                    try
                    {
                        Say("Checking for the newest version of The Valley...");
                        string key = "", proj = "the-valley-mmo";
                        try { foreach (string l in File.ReadAllLines(Path.Combine(dir, "ValleyRunner.ini"))) { int i = l.IndexOf('='); if (i <= 0 || l.TrimStart().StartsWith(";")) continue; string k = l.Substring(0, i).Trim(), v = l.Substring(i + 1).Trim(); if (k == "apiKey") key = v; else if (k == "project" && v.Length > 0) proj = v; } } catch (Exception) { }
                        string err, have = ValleyUpdate.LocalVersion(dir);
                        ValleyRelease rel = ValleyUpdate.Fetch(key, proj, out err);
                        if (rel != null && ValleyUpdate.Compare(rel.Version, have) > 0)
                        {
                            List<ValleyPatch> chain = ValleyUpdate.Chain(rel, have);
                            if (chain != null) { string nv = ValleyUpdate.Apply(dir, chain, delegate (string t, int pct) { Say(t); Prog(pct); }); updatedTo = nv; }
                        }
                    }
                    catch (Exception ex) { updateNote = " (the runner will offer the latest update: " + ex.Message + ")"; }
                }
                if (exe != null) ValleyUpdate.Lock(Path.GetDirectoryName(exe), false);   // v4.15g
                if (exe != null) RememberInstall(Path.GetDirectoryName(exe));   // v4.15.1b
                Prog(100); Say("The Valley " + (updatedTo.Length > 0 ? updatedTo + " " : "") + "is installed in " + target + (exe != null ? "." : " (The Valley.exe was not found in the zip).") + updateNote);
                if (run && exe != null) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
                installedExe = exe;
                try { File.WriteAllText(Path.Combine(folder, "setup.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + info.Text + "\r\n"); } catch (Exception) { }
                BeginInvoke((Action)(() => { go.Text = (run && exe != null) ? "PLAY AGAIN  /  CLOSE" : (exe != null ? "PLAY" : "CLOSE"); go.Enabled = true; if (quiet) Close(); }));
            }
            catch (Exception ex) { Say("Setup stopped: " + ex.Message); BeginInvoke((Action)(() => { go.Enabled = true; browse.Enabled = true; dest.Enabled = true; })); }
        }) { IsBackground = true }.Start();
    }

    static void MakeShortcut(string exe)
    {   // a desktop link (a .url file needs no COM): double-click starts the runner
        try
        {
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "The Valley.url");
            File.WriteAllText(lnk, "[InternetShortcut]\r\nURL=file:///" + exe.Replace("\\", "/") + "\r\nIconFile=" + exe + "\r\nIconIndex=0\r\n");
        }
        catch (Exception) { }
    }
}

public static class ValleySetup
{
    [STAThread]
    public static void Main() { Application.EnableVisualStyles(); Application.Run(new SetupForm()); }
}

/** v4.15.1c verify: CRC-32 (zip polynomial) and the CRC the zip recorded for an entry (the archive's own record). */
static class Crc32
{
    static readonly uint[] T = Make();
    static uint[] Make() { uint[] t = new uint[256]; for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[i] = c; } return t; }
    public static uint Update(uint crc, byte[] b, int n) { for (int i = 0; i < n; i++) crc = T[(crc ^ b[i]) & 0xFF] ^ (crc >> 8); return crc; }
    static readonly System.Reflection.FieldInfo F = typeof(ZipArchiveEntry).GetField("_crc32", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    public static bool Of(ZipArchiveEntry e, out uint crc) { crc = 0; if (F == null) return false; try { crc = (uint)F.GetValue(e); return true; } catch (Exception) { return false; } }
}
