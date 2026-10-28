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
        go.Click += delegate { Start(); };
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
    bool quiet;

    void Say(string s) { if (InvokeRequired) BeginInvoke((Action)(() => info.Text = s)); else info.Text = s; }
    void Prog(int v) { if (InvokeRequired) BeginInvoke((Action)(() => bar.Value = Math.Max(0, Math.Min(100, v)))); else bar.Value = Math.Max(0, Math.Min(100, v)); }

    void Find()
    {
        folder = AppDomain.CurrentDomain.BaseDirectory;
        var all = Directory.GetFiles(folder, "*.zip.0??").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var groups = all.GroupBy(f => Regex.Replace(Path.GetFileName(f), @"\.\d{3}$", "")).OrderByDescending(g => g.Key).ToArray();
        dest.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "The Valley");
        if (groups.Length == 0) { Say("No game parts found here. Put this setup in the same folder as the downloaded parts (TheValley_..._Client.zip.001, .002 ...) and run it again."); go.Enabled = false; return; }
        baseName = groups[0].Key; parts = groups[0].ToArray();
        // every number from 001 up must be there
        for (int i = 0; i < parts.Length; i++)
            if (!parts[i].EndsWith("." + (i + 1).ToString("000"))) { Say("Part " + (i + 1).ToString("000") + " of " + baseName + " is missing - download every part into this folder."); go.Enabled = false; return; }
        long total = parts.Sum(p => new FileInfo(p).Length);
        Say(string.Format("Found {0} in {1} parts ({2:0.0} GB). INSTALL joins them, unpacks the game and starts it. You need about {3:0} GB free.", baseName, parts.Length, total / 1073741824.0, total * 2.1 / 1073741824.0));
        // a part that is smaller than the others (except the last) is a broken download
        for (int i = 0; i + 1 < parts.Length; i++)
            if (new FileInfo(parts[i]).Length != new FileInfo(parts[0]).Length) { Say("Part " + Path.GetExtension(parts[i]) + " looks incomplete (wrong size) - download it again."); go.Enabled = false; return; }
    }

    void Start()
    {
        string target = dest.Text.Trim(); if (target.Length == 0) return;
        go.Enabled = false; browse.Enabled = false; dest.Enabled = false;
        bool keep = keepZip.Checked, run = launch.Checked;
        new Thread(() =>
        {
            try
            {
                string zip = Path.Combine(folder, baseName);
                long total = parts.Sum(p => new FileInfo(p).Length), done = 0;
                if (!File.Exists(zip) || new FileInfo(zip).Length != total)
                {
                    byte[] buf = new byte[8 << 20];
                    using (FileStream o = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                        foreach (string p in parts)
                            using (FileStream i = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                            {
                                int n; while ((n = i.Read(buf, 0, buf.Length)) > 0) { o.Write(buf, 0, n); done += n; Prog((int)(done * 50 / total)); Say(string.Format("Joining the parts... {0:0.0} of {1:0.0} GB", done / 1073741824.0, total / 1073741824.0)); }
                            }
                }
                Directory.CreateDirectory(target);
                using (ZipArchive a = ZipFile.OpenRead(zip))
                {
                    long all = a.Entries.Sum(e => e.Length), got = 0; string full = Path.GetFullPath(target);
                    foreach (ZipArchiveEntry e in a.Entries)
                    {
                        string to = Path.GetFullPath(Path.Combine(target, e.FullName));
                        if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;   // never outside the install folder
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(to); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        e.ExtractToFile(to, true); got += e.Length;
                        Prog(50 + (int)(all > 0 ? got * 50 / all : 50)); Say("Unpacking " + e.Name + " ...");
                    }
                }
                if (!keep) try { File.Delete(zip); } catch (Exception) { }
                string exe = Directory.GetFiles(target, "The Valley.exe", SearchOption.AllDirectories).FirstOrDefault();

                if (exe != null)
                {   // v4.14 the updater reads version.txt beside the runner: a zip without one is the version in its name
                    string vt = Path.Combine(Path.GetDirectoryName(exe), "version.txt");
                    Match vm = Regex.Match(baseName, @"v(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
                    if (!File.Exists(vt) && vm.Success) { string v = vm.Groups[1].Value; while (v.Split('.').Length < 3) v += ".0"; try { File.WriteAllText(vt, v); } catch (Exception) { } }
                    MakeShortcut(exe);
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
                Prog(100); Say("The Valley " + (updatedTo.Length > 0 ? updatedTo + " " : "") + "is installed in " + target + (exe != null ? "." : " (The Valley.exe was not found in the zip).") + updateNote);
                if (run && exe != null) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
                try { File.WriteAllText(Path.Combine(folder, "setup.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + info.Text + "\r\n"); } catch (Exception) { }
                BeginInvoke((Action)(() => { go.Text = "DONE"; if (quiet) Close(); }));
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
